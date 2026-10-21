using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    internal static class Program
    {
        private const string MutexName = "Local\\OpenCodeUsageWin_SingleInstance";
        private const string ShowEventName = "Local\\OpenCodeUsageWin_Show";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [STAThread]
        private static int Main(string[] args)
        {
            RuntimeOptions opt = new RuntimeOptions();
            string argError = "";
            bool help = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--once" || a == "-o")
                {
                    opt.Once = true;
                }
                else if (a == "--json" || a == "-j")
                {
                    opt.Once = true;
                    opt.RawOutput = true;
                }
                else if (a == "--demo")
                {
                    opt.Demo = true;
                }
                else if (a == "--base-url" || a == "--url")
                {
                    if (i + 1 < args.Length) opt.BaseUrl = args[++i];
                    else argError = "Nilai untuk " + a + " kosong.";
                }
                else if (a == "--timeout" || a == "-t")
                {
                    if (i + 1 < args.Length)
                    {
                        int ms;
                        if (int.TryParse(args[i + 1], out ms) && ms > 0) opt.TimeoutMs = ms;
                        else argError = "Nilai timeout tidak valid: " + args[i + 1];
                        i++;
                    }
                    else
                    {
                        argError = "Nilai untuk " + a + " kosong.";
                    }
                }
                else if (a == "--help" || a == "-h" || a == "-?" || a == "/?" || a == "/help")
                {
                    help = true;
                }
                else if (a.StartsWith("--"))
                {
                    argError = "Argumen tidak dikenal: " + args[i];
                }
            }

            AppConfig cfg = AppConfig.Load();

            if (help)
            {
                TextWriter writer = OpenConsole();
                PrintHelp(writer);
                writer.Flush();
                return 0;
            }

            if (opt.Once)
            {
                TextWriter writer = OpenConsole();
                int code = RunOnce(cfg, opt, writer);
                writer.Flush();
                return code;
            }

            if (!string.IsNullOrEmpty(argError))
            {
                MessageBox.Show(argError + "\n\nGunakan --help untuk melihat daftar argumen.", "OpenCode Go Usage",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }

            string urlError = UsageClient.ValidateBaseUrl(opt.BaseUrl);
            if (!string.IsNullOrEmpty(urlError))
            {
                MessageBox.Show(urlError, "OpenCode Go Usage", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew = false;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Instans pertama mungkin sedang tersembunyi di tray: minta dia muncul.
                    if (!SignalExistingInstance(ShowEventName))
                    {
                        MessageBox.Show("OpenCode Go Usage sudah berjalan. Klik ikonnya di system tray untuk membuka.",
                            "OpenCode Go Usage", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return 0;
                }

                EventWaitHandle showEvent = null;
                try
                {
                    showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
                }
                catch
                {
                    showEvent = null;
                }

                Application.Run(new MainForm(cfg, opt, showEvent));
                GC.KeepAlive(mutex);
                if (showEvent != null) showEvent.Close();
            }
            return 0;
        }

        private static bool SignalExistingInstance(string eventName)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    using (EventWaitHandle handle = EventWaitHandle.OpenExisting(eventName))
                    {
                        handle.Set();
                        return true;
                    }
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    System.Threading.Thread.Sleep(100);
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private static TextWriter OpenConsole()
        {
            try
            {
                if (GetConsoleWindow() == IntPtr.Zero) AttachConsole(-1);
                Stream stdout = Console.OpenStandardOutput();
                StreamWriter writer = new StreamWriter(stdout, new UTF8Encoding(false));
                writer.AutoFlush = true;
                return writer;
            }
            catch
            {
                return Console.Out;
            }
        }

        private static void PrintHelp(TextWriter w)
        {
            w.WriteLine("OpenCode Go Usage 1.0 - pemantau kuota OpenCode Go (5 jam, mingguan, bulanan)");
            w.WriteLine("");
            w.WriteLine("Pemakaian:");
            w.WriteLine("  OpenCodeUsage.exe                  buka jendela aplikasi");
            w.WriteLine("  OpenCodeUsage.exe --once           cetak ringkasan kuota ke konsol, lalu keluar");
            w.WriteLine("  OpenCodeUsage.exe --once --json    cetak respons mentah JSON");
            w.WriteLine("  OpenCodeUsage.exe --demo           buka UI dengan data contoh (tanpa API)");
            w.WriteLine("");
            w.WriteLine("Opsi:");
            w.WriteLine("  --base-url <url>   base URL API (default https://opencode.ai/zen/go)");
            w.WriteLine("  --timeout <ms>     timeout HTTP dalam milidetik (default 15000)");
            w.WriteLine("  -h, --help         tampilkan bantuan ini");
            w.WriteLine("");
            w.WriteLine("API key dibaca dari (urutan): key tersimpan di aplikasi, OPENCODE_GO_API_KEY,");
            w.WriteLine("OPENCODE_API_KEY, lalu auth.json milik opencode.");
            w.WriteLine("");
            w.WriteLine("Kode keluar: 0 sukses, 2 tanpa key, 3 key ditolak, 4 jaringan, 5 HTTP lain, 6 format.");
        }

        private static int RunOnce(AppConfig cfg, RuntimeOptions opt, TextWriter w)
        {
            string urlError = UsageClient.ValidateBaseUrl(opt.BaseUrl);
            if (!string.IsNullOrEmpty(urlError))
            {
                w.WriteLine("Base URL ditolak: " + urlError);
                return 1;
            }

            UsageClient client = new UsageClient(opt.BaseUrl, opt.TimeoutMs);
            UsageResult result;
            string keySource;

            if (opt.Demo)
            {
                keySource = "data contoh (--demo)";
                result = new UsageResult();
                result.Success = true;
                result.HttpOk = true;
                result.StatusCode = 200;
                result.Raw = DemoData.Build();
                result.ElapsedMs = 0;
                result.Endpoint = "demo";
                try
                {
                    result.Snapshot = UsageSnapshot.Parse(result.Raw);
                }
                catch (Exception ex)
                {
                    w.WriteLine("Gagal membaca data demo: " + ex.Message);
                    return 6;
                }
            }
            else
            {
                KeyInfo info = KeyFinder.Resolve(cfg);
                if (!info.Found)
                {
                    w.WriteLine("Tidak ada API key.");
                    w.WriteLine("Simpan key lewat jendela aplikasi, atau set variabel lingkungan OPENCODE_GO_API_KEY.");
                    return 2;
                }
                keySource = info.Source + " (" + Mask(info.Key) + ")";
                result = client.Fetch(info.Key);
            }

            if (opt.RawOutput)
            {
                if (result != null && !string.IsNullOrEmpty(result.Raw)) w.WriteLine(result.Raw);
                else w.WriteLine(result == null ? "tidak ada respons" : (result.Error ?? "tidak ada body"));
                if (result != null && result.Success) return 0;
                return ExitCodeFor(result);
            }

            w.WriteLine("OpenCode Go usage");
            w.WriteLine("  endpoint : " + result.Endpoint);
            w.WriteLine("  key      : " + keySource);

            if (result == null || !result.Success)
            {
                w.WriteLine("  status   : GAGAL");
                w.WriteLine("  error    : " + (result == null ? "tidak ada respons" : result.Error));
                if (result != null && result.StatusCode > 0) w.WriteLine("  http     : " + result.StatusCode + " (" + result.ElapsedMs + " ms)");
                if (result != null && !string.IsNullOrEmpty(result.Raw))
                {
                    string body = result.Raw.Replace("\r", " ").Replace("\n", " ");
                    if (body.Length > 300) body = body.Substring(0, 297) + "...";
                    w.WriteLine("  body     : " + body);
                }
                return ExitCodeFor(result);
            }

            w.WriteLine("  status   : OK (" + result.SummaryLine() + ")");
            w.WriteLine("");
            for (int i = 0; i < result.Snapshot.Windows.Count; i++)
            {
                UsageWindow win = result.Snapshot.Windows[i];
                StringBuilder line = new StringBuilder();
                line.Append("  ").Append(Pad(win.Label, 10));
                line.Append(Pad(Fmt.Percent(win.Percent), 8));
                string countdown = Fmt.CountdownSentence(win.ResetsAt);
                if (!string.IsNullOrEmpty(countdown)) line.Append(countdown);
                if (!string.IsNullOrEmpty(win.Detail)) line.Append("  [").Append(win.Detail).Append(']');
                w.WriteLine(line.ToString().TrimEnd());
            }

            string extras = result.Snapshot.ExtrasText();
            if (!string.IsNullOrEmpty(extras)) w.WriteLine("  " + extras);
            return 0;
        }

        private static int ExitCodeFor(UsageResult result)
        {
            if (result == null) return 4;
            if (result.Success) return 0;
            if (result.StatusCode == 401 || result.StatusCode == 403) return 3;
            if (result.StatusCode == 0) return 4;
            if (result.StatusCode >= 500) return 5;
            if (result.StatusCode >= 200 && result.StatusCode < 300) return 6;
            return 5;
        }

        private static string Pad(string text, int width)
        {
            if (text == null) text = "";
            if (text.Length >= width) return text + " ";
            return text + new string(' ', width - text.Length);
        }

        private static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 8) return "****";
            return key.Substring(0, 4) + "..." + key.Substring(key.Length - 4);
        }
    }
}
