using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenCodeUsage
{
    internal sealed class UsageWindow
    {
        public string Key;
        public string Label;
        public string ShortLabel;
        public double Percent = -1;
        public string Status;
        public DateTimeOffset? ResetsAt;
        public string Detail;

        public bool HasPercent { get { return Percent >= 0; } }

        public string TooltipText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Label);
            sb.Append(": ");
            sb.Append(HasPercent ? Fmt.Percent(Percent) : "tanpa data");
            if (!string.IsNullOrEmpty(Status)) sb.Append("  (status ").Append(Status).Append(')');
            if (ResetsAt.HasValue) sb.Append('\n').Append("Reset ").Append(Fmt.Stamp(ResetsAt.Value));
            if (!string.IsNullOrEmpty(Detail)) sb.Append('\n').Append(Detail);
            return sb.ToString();
        }
    }

    internal sealed class UsageSnapshot
    {
        public List<UsageWindow> Windows = new List<UsageWindow>();
        public List<KeyValuePair<string, string>> Extras = new List<KeyValuePair<string, string>>();
        public string Raw = "";

        public UsageWindow Find(string key)
        {
            for (int i = 0; i < Windows.Count; i++)
            {
                if (string.Equals(Windows[i].Key, key, StringComparison.OrdinalIgnoreCase)) return Windows[i];
            }
            return null;
        }

        public double Highest()
        {
            double max = -1;
            for (int i = 0; i < Windows.Count; i++)
            {
                if (Windows[i].HasPercent && Windows[i].Percent > max) max = Windows[i].Percent;
            }
            return max;
        }

        public string ExtrasText()
        {
            if (Extras.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Extras.Count && i < 4; i++)
            {
                if (sb.Length > 0) sb.Append(" \u00B7 ");
                sb.Append(Extras[i].Key).Append(": ").Append(Extras[i].Value);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses the documented shape ({ usage: { rolling, weekly, monthly } }) and
        /// degrades gracefully for anything else the endpoint might return.
        /// </summary>
        public static UsageSnapshot Parse(string raw)
        {
            UsageSnapshot snap = new UsageSnapshot();
            snap.Raw = raw ?? "";
            if (string.IsNullOrEmpty(snap.Raw)) return snap;

            JsonNode root = JsonReader.Parse(snap.Raw);
            if (root == null) return snap;

            if (root.IsObject)
            {
                foreach (string k in root.Keys)
                {
                    if (IsContainerName(k)) continue;
                    JsonNode v = root.Props[k];
                    if (v != null && v.IsScalar)
                    {
                        string text = Jx.Scalar(v);
                        if (!string.IsNullOrEmpty(text)) snap.Extras.Add(new KeyValuePair<string, string>(k, text));
                    }
                }
            }

            JsonNode usage = FindUsageNode(root);
            if (usage != null)
            {
                string[] known = new string[] { "rolling", "weekly", "monthly" };
                for (int i = 0; i < known.Length; i++) AddWindow(snap, usage, known[i]);
                foreach (string k in usage.Keys)
                {
                    bool isKnown = false;
                    for (int i = 0; i < known.Length; i++)
                    {
                        if (string.Equals(known[i], k, StringComparison.OrdinalIgnoreCase)) isKnown = true;
                    }
                    if (!isKnown) AddWindow(snap, usage, k);
                }
            }

            if (snap.Windows.Count == 0) AddGenericWindows(snap, root);
            return snap;
        }

        private static bool IsContainerName(string key)
        {
            return string.Equals(key, "usage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "data", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "quota", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "limits", StringComparison.OrdinalIgnoreCase);
        }

        private static JsonNode FindUsageNode(JsonNode root)
        {
            if (root == null) return null;

            JsonNode direct = Jx.Prop(root, "usage");
            if (direct != null && direct.IsObject) return direct;

            JsonNode data = Jx.Prop(root, "data");
            if (data != null && data.IsObject)
            {
                JsonNode nested = Jx.Prop(data, "usage");
                if (nested != null && nested.IsObject) return nested;
                if (HasWindowKeys(data)) return data;
            }

            JsonNode quota = Jx.Prop(root, "quota");
            if (quota != null && quota.IsObject && HasWindowKeys(quota)) return quota;

            if (HasWindowKeys(root)) return root;

            foreach (JsonNode n in Jx.Walk(root))
            {
                if (n.IsObject && n != root && HasWindowKeys(n)) return n;
            }
            return null;
        }

        private static bool HasWindowKeys(JsonNode n)
        {
            if (n == null || !n.IsObject) return false;
            return Jx.Prop(n, "rolling") != null || Jx.Prop(n, "weekly") != null || Jx.Prop(n, "monthly") != null;
        }

        private static void AddWindow(UsageSnapshot snap, JsonNode usage, string key)
        {
            if (snap.Find(key) != null) return;
            JsonNode w = Jx.Prop(usage, key);
            if (w == null || !w.IsObject) return;

            UsageWindow uw = new UsageWindow();
            uw.Key = key;
            uw.Label = LabelFor(key);
            uw.ShortLabel = ShortLabelFor(key);

            double? percent = Jx.Number(Jx.PropAny(w, "percent", "usedPercent", "used_percent", "usagePercent",
                                                    "usage_percent", "percentUsed", "pct"));
            double? used = Jx.Number(Jx.PropAny(w, "used", "usedUsd", "used_usd", "spent", "spentUsd", "consumed"));
            double? limit = Jx.Number(Jx.PropAny(w, "limit", "limitUsd", "limit_usd", "max", "quota", "cap", "allowance"));

            if (!percent.HasValue && used.HasValue && limit.HasValue && limit.Value > 0)
                percent = used.Value / limit.Value * 100.0;

            uw.Percent = percent.HasValue ? percent.Value : -1;
            uw.Status = Jx.Scalar(Jx.PropAny(w, "status", "state"));
            uw.ResetsAt = Jx.Date(Jx.PropAny(w, "resetsAt", "resets_at", "resetAt", "reset_at", "resetTime", "reset_time"));

            if (used.HasValue && limit.HasValue)
                uw.Detail = string.Format(CultureInfo.InvariantCulture, "${0:0.00} dari ${1:0.00}", used.Value, limit.Value);
            else
            {
                double? remaining = Jx.Number(Jx.PropAny(w, "remaining", "left", "remainingUsd"));
                if (remaining.HasValue)
                    uw.Detail = string.Format(CultureInfo.InvariantCulture, "sisa ${0:0.00}", remaining.Value);
            }

            snap.Windows.Add(uw);
        }

        private static void AddGenericWindows(UsageSnapshot snap, JsonNode root)
        {
            int added = 0;
            foreach (JsonNode n in Jx.Walk(root))
            {
                if (added >= 3) break;
                if (!n.IsObject) continue;
                double? percent = Jx.Number(Jx.PropAny(n, "percent", "usedPercent", "used_percent", "usagePercent", "pct"));
                DateTimeOffset? reset = Jx.Date(Jx.PropAny(n, "resetsAt", "resets_at", "resetAt", "reset_at"));
                if (!percent.HasValue) continue;

                UsageWindow uw = new UsageWindow();
                uw.Key = "window" + added;
                uw.Label = "Window " + (added + 1);
                uw.ShortLabel = "w" + (added + 1);
                uw.Percent = percent.Value;
                uw.Status = Jx.Scalar(Jx.PropAny(n, "status", "state"));
                uw.ResetsAt = reset;
                snap.Windows.Add(uw);
                added++;
            }
        }

        private static string LabelFor(string key)
        {
            if (string.Equals(key, "rolling", StringComparison.OrdinalIgnoreCase)) return "5 jam";
            if (string.Equals(key, "weekly", StringComparison.OrdinalIgnoreCase)) return "Mingguan";
            if (string.Equals(key, "monthly", StringComparison.OrdinalIgnoreCase)) return "Bulanan";
            return TitleCase(key);
        }

        private static string ShortLabelFor(string key)
        {
            if (string.Equals(key, "rolling", StringComparison.OrdinalIgnoreCase)) return "5j";
            if (string.Equals(key, "weekly", StringComparison.OrdinalIgnoreCase)) return "mgg";
            if (string.Equals(key, "monthly", StringComparison.OrdinalIgnoreCase)) return "bln";
            return key;
        }

        private static string TitleCase(string key)
        {
            if (string.IsNullOrEmpty(key)) return "?";
            string s = key.Replace('_', ' ').Replace('-', ' ');
            if (s.Length == 1) return s.ToUpperInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }

    internal sealed class UsageResult
    {
        public bool Success;
        public bool HttpOk;
        public int StatusCode;
        public string Raw = "";
        public UsageSnapshot Snapshot;
        public string Error;
        public long ElapsedMs;
        public string Endpoint = "";

        public string SummaryLine()
        {
            if (Success) return string.Format(CultureInfo.InvariantCulture, "HTTP {0} \u00B7 {1} ms", StatusCode, ElapsedMs);
            if (StatusCode > 0) return string.Format(CultureInfo.InvariantCulture, "HTTP {0} \u00B7 {1} ms", StatusCode, ElapsedMs);
            return "gagal terhubung";
        }
    }

    internal sealed class UsageClient
    {
        public const string DefaultBaseUrl = "https://opencode.ai/zen/go";
        public const int DefaultTimeoutMs = 15000;

        public string BaseUrl;
        public int TimeoutMs;
        public string UserAgent = "OpenCodeUsageWin/1.0 (+windows desktop)";

        public UsageClient(string baseUrl, int timeoutMs)
        {
            BaseUrl = string.IsNullOrEmpty(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
            TimeoutMs = timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs;
        }

        public string EndpointUrl()
        {
            string b = (BaseUrl ?? "").Trim();
            if (b.Length == 0) b = DefaultBaseUrl;
            if (b.EndsWith("/usage", StringComparison.OrdinalIgnoreCase)) return b;
            b = b.TrimEnd('/');
            if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return b + "/usage";
            return b + "/v1/usage";
        }

        public static string ValidateBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            string u = url.Trim();
            Uri uri;
            if (!Uri.TryCreate(u, UriKind.Absolute, out uri)) return "URL tidak valid.";
            if (uri.Scheme == Uri.UriSchemeHttps) return null;
            if (uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host == "localhost")) return null;
            return "URL harus https (http hanya untuk localhost).";
        }

        public UsageResult Fetch(string apiKey)
        {
            UsageResult res = new UsageResult();
            res.Endpoint = EndpointUrl();
            Stopwatch sw = Stopwatch.StartNew();

            if (string.IsNullOrEmpty(apiKey))
            {
                res.Error = "API key belum diatur.";
                res.ElapsedMs = 0;
                return res;
            }

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                ServicePointManager.Expect100Continue = false;

                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    handler.AllowAutoRedirect = true;
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);
                        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
                        using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, res.Endpoint))
                        {
                            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
                            req.Headers.TryAddWithoutValidation("Accept", "application/json");
                            using (HttpResponseMessage r = client.SendAsync(req, HttpCompletionOption.ResponseContentRead)
                                                               .GetAwaiter().GetResult())
                            {
                                res.StatusCode = (int)r.StatusCode;
                                res.HttpOk = r.IsSuccessStatusCode;
                                res.Raw = r.Content == null ? "" : r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                sw.Stop();
                res.ElapsedMs = sw.ElapsedMilliseconds;
                res.Error = DescribeCancellation(ex, TimeoutMs);
                return res;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                res.ElapsedMs = sw.ElapsedMilliseconds;
                res.Error = "Permintaan dibatalkan atau timeout.";
                return res;
            }
            catch (Exception ex)
            {
                sw.Stop();
                res.ElapsedMs = sw.ElapsedMilliseconds;
                Exception root = ex;
                while (root.InnerException != null) root = root.InnerException;
                res.Error = "Tidak bisa terhubung: " + root.Message;
                return res;
            }

            sw.Stop();
            res.ElapsedMs = sw.ElapsedMilliseconds;

            if (!res.HttpOk)
            {
                res.Error = FriendlyHttpError(res.StatusCode, res.Raw);
                return res;
            }

            try
            {
                res.Snapshot = UsageSnapshot.Parse(res.Raw);
                if (res.Snapshot.Windows.Count == 0)
                {
                    res.Error = "Respons diterima, tapi tidak ada data kuota yang dikenali. Lihat tab JSON.";
                    return res;
                }
            }
            catch (Exception ex)
            {
                res.Error = "Gagal membaca JSON: " + ex.Message;
                return res;
            }

            res.Success = true;
            return res;
        }

        private static string DescribeCancellation(Exception ex, int timeoutMs)
        {
            Exception root = ex;
            while (root.InnerException != null) root = root.InnerException;

            WebException web = root as WebException;
            if (web != null)
            {
                switch (web.Status)
                {
                    case WebExceptionStatus.ConnectFailure:
                        return "Tidak bisa terhubung ke server. Cek koneksi internet atau proxy.";
                    case WebExceptionStatus.NameResolutionFailure:
                        return "Nama host tidak bisa ditemukan. Cek DNS/koneksi.";
                    case WebExceptionStatus.TrustFailure:
                        return "Sertifikat SSL server tidak bisa diverifikasi.";
                    case WebExceptionStatus.ProxyNameResolutionFailure:
                        return "Proxy tidak bisa ditemukan.";
                }
            }

            string seconds = (timeoutMs / 1000.0).ToString("0.#", CultureInfo.InvariantCulture);
            return "Timeout setelah " + seconds + " detik. Cek koneksi internet.";
        }

        private static string FriendlyHttpError(int code, string raw)
        {
            string detail = ExtractMessage(raw);
            string suffix = string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")";
            switch (code)
            {
                case 401:
                    return "API key ditolak (401). Periksa key OpenCode Go kamu." + suffix;
                case 402:
                    return "Pembayaran diperlukan (402)." + suffix;
                case 403:
                    return "Key valid, tapi tanpa langganan OpenCode Go (403)." + suffix;
                case 404:
                    return "Endpoint tidak ditemukan (404)." + suffix;
                case 429:
                    return "Terlalu banyak permintaan (429). Tunggu sebentar." + suffix;
            }
            if (code >= 500) return "Server opencode.ai sedang bermasalah (" + code + ")." + suffix;
            return "Respons HTTP " + code + "." + suffix;
        }

        private static string ExtractMessage(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.TrimStart().Length == 0) return "";
            string trimmed = raw.TrimStart();
            if (trimmed[0] != '{' && trimmed[0] != '[') return "";
            try
            {
                JsonNode root = JsonReader.Parse(raw);
                JsonNode msg = Jx.PropAny(root, "message", "error", "detail", "code", "reason");
                if (msg == null) return "";
                string text = msg.IsScalar ? Jx.Scalar(msg) : Json.Pretty(msg);
                if (text == null) return "";
                text = text.Replace("\r", " ").Replace("\n", " ");
                if (text.Length > 120) text = text.Substring(0, 117) + "...";
                return text;
            }
            catch
            {
                return "";
            }
        }
    }

    internal sealed class KeyInfo
    {
        public string Key = "";
        public string Source = "tidak ada";
        public bool Found { get { return !string.IsNullOrEmpty(Key); } }
    }

    internal static class KeyFinder
    {
        public static KeyInfo Resolve(AppConfig cfg)
        {
            KeyInfo info = new KeyInfo();

            if (cfg != null && !string.IsNullOrEmpty(cfg.ApiKey))
            {
                info.Key = cfg.ApiKey;
                info.Source = "disimpan di aplikasi";
                return info;
            }

            string[] envNames = new string[] { "OPENCODE_GO_API_KEY", "OPENCODE_API_KEY" };
            for (int i = 0; i < envNames.Length; i++)
            {
                string v = Environment.GetEnvironmentVariable(envNames[i]);
                if (!string.IsNullOrEmpty(v))
                {
                    info.Key = v.Trim();
                    info.Source = "variabel lingkungan " + envNames[i];
                    return info;
                }
            }

            string authPath = FindAuthFile();
            if (authPath != null)
            {
                string key = ReadAuthFile(authPath);
                if (!string.IsNullOrEmpty(key))
                {
                    info.Key = key;
                    info.Source = "auth.json opencode";
                    return info;
                }
            }

            return info;
        }

        public static string FindAuthFile()
        {
            List<string> candidates = new List<string>();
            string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdg)) candidates.Add(Path.Combine(xdg, "opencode", "auth.json"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "opencode", "auth.json"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "opencode", "auth.json"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "opencode", "auth.json"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "opencode", "auth.json"));

            for (int i = 0; i < candidates.Count; i++)
            {
                try
                {
                    if (File.Exists(candidates[i])) return candidates[i];
                }
                catch
                {
                }
            }
            return null;
        }

        public static string ReadAuthFile(string path)
        {
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                JsonNode root = JsonReader.Parse(text);
                if (root == null || !root.IsObject) return null;

                string[] preferred = new string[] { "opencode-go", "opencode" };
                for (int i = 0; i < preferred.Length; i++)
                {
                    JsonNode entry = Jx.Prop(root, preferred[i]);
                    string key = ExtractKey(entry);
                    if (!string.IsNullOrEmpty(key)) return key;
                }
                foreach (string name in root.Keys)
                {
                    string key = ExtractKey(root.Props[name]);
                    if (!string.IsNullOrEmpty(key)) return key;
                }
            }
            catch
            {
            }
            return null;
        }

        private static string ExtractKey(JsonNode entry)
        {
            if (entry == null) return null;
            if (entry.IsString) return entry.StringValue;
            if (!entry.IsObject) return null;
            JsonNode key = Jx.PropAny(entry, "key", "apiKey", "api_key", "access", "token");
            return key == null ? null : Jx.Scalar(key);
        }
    }

    internal static class Fmt
    {
        private static readonly string[] Months = new string[]
        {
            "Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des"
        };

        public static string Percent(double p)
        {
            if (p < 0) return "\u2014";
            if (Math.Abs(p - Math.Round(p)) < 0.05) return ((long)Math.Round(p)).ToString(CultureInfo.InvariantCulture) + "%";
            return p.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        public static string Stamp(DateTimeOffset t)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2:00}:{3:00}",
                t.Day, Months[t.Month - 1], t.Hour, t.Minute);
        }

        public static string Countdown(DateTimeOffset? reset)
        {
            if (!reset.HasValue) return null;
            TimeSpan span = reset.Value - DateTimeOffset.Now;
            if (span.TotalSeconds <= 0) return "segera";
            if (span.TotalMinutes < 1) return "kurang dari 1m";
            if (span.TotalMinutes < 60)
                return ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m";
            if (span.TotalHours < 24)
                return string.Format(CultureInfo.InvariantCulture, "{0}j {1:00}m", (int)span.TotalHours, span.Minutes);
            return string.Format(CultureInfo.InvariantCulture, "{0}d {1}j", (int)span.TotalDays, span.Hours);
        }

        public static string CountdownSentence(DateTimeOffset? reset)
        {
            string c = Countdown(reset);
            if (c == null) return "";
            if (c == "segera") return "kuota akan diperbarui";
            return "reset dalam " + c;
        }

        public static string Clock(DateTime t)
        {
            return t.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public static string Ago(DateTime t)
        {
            TimeSpan span = DateTime.Now - t;
            if (span.TotalSeconds < 10) return "baru saja";
            if (span.TotalMinutes < 1) return ((int)span.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " detik lalu";
            if (span.TotalMinutes < 60) return ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " menit lalu";
            if (span.TotalHours < 24) return ((int)span.TotalHours).ToString(CultureInfo.InvariantCulture) + " jam lalu";
            return ((int)span.TotalDays).ToString(CultureInfo.InvariantCulture) + " hari lalu";
        }
    }
}
