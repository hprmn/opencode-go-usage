using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace OpenCodeUsage
{
    internal sealed class AppConfig
    {
        public string ApiKey = "";
        public int RefreshSeconds = 60;
        public bool AlwaysOnTop = false;

        /// <summary>Test hook: when set, config lives here instead of %APPDATA%.</summary>
        internal static string OverrideDir = null;

        public static string Dir
        {
            get
            {
                if (!string.IsNullOrEmpty(OverrideDir)) return OverrideDir;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCodeUsage");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Dir, "config.json"); }
        }

        public static AppConfig Load()
        {
            AppConfig cfg = new AppConfig();
            try
            {
                if (!File.Exists(FilePath)) return cfg;
                JsonNode root = JsonReader.Parse(File.ReadAllText(FilePath, Encoding.UTF8));
                if (root == null || !root.IsObject) return cfg;

                string encrypted = Jx.Scalar(Jx.Prop(root, "apiKeyProtected"));
                if (!string.IsNullOrEmpty(encrypted))
                {
                    try
                    {
                        byte[] blob = Convert.FromBase64String(encrypted);
                        byte[] plain = ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser);
                        cfg.ApiKey = Encoding.UTF8.GetString(plain);
                    }
                    catch
                    {
                        cfg.ApiKey = "";
                    }
                }

                double? refresh = Jx.Number(Jx.Prop(root, "refreshSeconds"));
                if (refresh.HasValue && refresh.Value >= 15) cfg.RefreshSeconds = (int)refresh.Value;

                JsonNode top = Jx.Prop(root, "alwaysOnTop");
                if (top != null && top.Kind == JsonKind.Bool) cfg.AlwaysOnTop = top.BoolValue;
            }
            catch
            {
            }
            if (cfg.RefreshSeconds < 15) cfg.RefreshSeconds = 60;
            return cfg;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string encrypted = "";
                if (!string.IsNullOrEmpty(ApiKey))
                {
                    byte[] plain = Encoding.UTF8.GetBytes(ApiKey);
                    byte[] blob = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                    encrypted = Convert.ToBase64String(blob);
                }

                StringBuilder sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"refreshSeconds\": ").Append(RefreshSeconds).Append(",\n");
                sb.Append("  \"alwaysOnTop\": ").Append(AlwaysOnTop ? "true" : "false").Append(",\n");
                sb.Append("  \"apiKeyProtected\": \"").Append(encrypted).Append("\"\n");
                sb.Append("}\n");
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }

    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "OpenCodeUsage";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool Set(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return false;
                    if (enabled)
                    {
                        string exe = System.Windows.Forms.Application.ExecutablePath;
                        key.SetValue(ValueName, "\"" + exe + "\"", RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class RuntimeOptions
    {
        public string BaseUrl = UsageClient.DefaultBaseUrl;
        public int TimeoutMs = UsageClient.DefaultTimeoutMs;
        public bool Demo = false;
        public bool Once = false;
        public bool RawOutput = false;
    }
}
