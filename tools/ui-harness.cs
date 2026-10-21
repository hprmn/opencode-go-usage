// Dev-only harness: renders each window on its own and captures a screenshot.
// Compile with tools\build-harness.ps1.
// Run: UiHarness.exe <outDir> <settings|json|main|main-live|error|nokey|config>
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    internal static class UiHarness
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : ".";
            string which = args.Length > 1 ? args[1] : "settings";

            if (which == "config")
            {
                RunConfigTest(outDir);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppConfig cfg = new AppConfig();
            cfg.ApiKey = "opencode-abcdef1234567890abcdef";
            cfg.RefreshSeconds = 60;
            UsageClient client = new UsageClient("http://127.0.0.1:18771/zen/go", 15000);

            Form form;
            string file;
            int delay = 900;

            if (which == "json")
            {
                string raw = DemoData.Build();
                form = new RawJsonForm("Respons JSON",
                    "# GET https://opencode.ai/zen/go/v1/usage\n# HTTP 200 \u00B7 214 ms \u00B7 ok\n\n" + Json.PrettyOrRaw(raw),
                    raw);
                file = "harness-json.png";
            }
            else if (which == "main")
            {
                RuntimeOptions opt = new RuntimeOptions();
                opt.Demo = true;
                form = new MainForm(cfg, opt, null);
                file = "harness-main.png";
            }
            else if (which == "bubble")
            {
                BubbleForm bubble = new BubbleForm();
                bubble.SetSnapshot(UsageSnapshot.Parse(DemoData.Build()));
                bubble.Location = new Point(300, 300);
                form = bubble;
                file = "harness-bubble.png";
            }
            else if (which == "main-live")
            {
                RuntimeOptions opt = new RuntimeOptions();
                opt.BaseUrl = "http://127.0.0.1:18777/zen/go";
                form = new MainForm(cfg, opt, null);
                file = "harness-main-live.png";
                delay = 3000;
            }
            else if (which == "error")
            {
                RuntimeOptions opt = new RuntimeOptions();
                opt.BaseUrl = "http://127.0.0.1:18772/zen/go";
                form = new MainForm(cfg, opt, null);
                file = "harness-error.png";
                delay = 3000;
            }
            else if (which == "nokey")
            {
                cfg.ApiKey = "";
                RuntimeOptions opt = new RuntimeOptions();
                form = new MainForm(cfg, opt, null);
                file = "harness-nokey.png";
                delay = 1800;
            }
            else
            {
                form = new SettingsForm(cfg, client);
                file = "harness-settings.png";
            }

            using (form)
            {
                Timer timer = new Timer();
                timer.Interval = delay;
                timer.Tick += delegate
                {
                    timer.Stop();
                    TryCapture(form, Path.Combine(outDir, file));
                    Application.Exit();
                };
                form.Shown += delegate
                {
                    form.TopMost = true;
                    form.Activate();
                    form.BringToFront();
                    timer.Start();
                };
                Application.Run(form);
            }
        }

        private static void TryCapture(Form form, string path)
        {
            try
            {
                Rectangle bounds = form.Bounds;
                Bitmap bmp = new Bitmap(bounds.Width, bounds.Height);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();
            }
            catch (Exception ex)
            {
                File.WriteAllText(path + ".error.txt", ex.ToString());
            }
        }

        private static void RunConfigTest(string outDir)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                AppConfig.OverrideDir = Path.Combine(outDir, "cfgtest");
                AppConfig saved = new AppConfig();
                saved.ApiKey = "opencode-selftest-1234567890";
                saved.RefreshSeconds = 120;
                saved.AlwaysOnTop = true;
                saved.Save();

                bool fileExists = File.Exists(AppConfig.FilePath);
                sb.AppendLine("config file : " + AppConfig.FilePath);
                sb.AppendLine("exists      : " + fileExists);

                AppConfig loaded = AppConfig.Load();
                sb.AppendLine("key ok      : " + (loaded.ApiKey == saved.ApiKey));
                sb.AppendLine("refresh ok  : " + (loaded.RefreshSeconds == saved.RefreshSeconds));
                sb.AppendLine("topmost ok  : " + (loaded.AlwaysOnTop == saved.AlwaysOnTop));
                sb.AppendLine("key         : [" + loaded.ApiKey + "]");
                if (fileExists)
                {
                    string text = File.ReadAllText(AppConfig.FilePath);
                    sb.AppendLine("no plaintext: " + (!text.Contains("opencode-selftest")));
                    sb.AppendLine("file        : " + text.Replace("\r", " ").Replace("\n", " "));
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("ERROR: " + ex);
            }
            File.WriteAllText(Path.Combine(outDir, "config-selftest.txt"), sb.ToString(), Encoding.UTF8);
        }
    }
}
