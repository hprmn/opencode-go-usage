using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppConfig _cfg;
        private readonly UsageClient _client;
        private readonly ToolTip _tip = new ToolTip();

        public bool Saved;

        private TextBox _keyBox;
        private LinkLabel _showKeyLink;
        private Label _hint;
        private Segmented _interval;
        private ToggleSwitch _topMost;
        private ToggleSwitch _autoStart;
        private ToggleSwitch _bubble;
        private Button _testButton;
        private Button _saveButton;
        private Button _cancelButton;
        private Label _testResult;
        private LinkLabel _clearLink;
        private bool _showKey;
        private bool _testing;

        public SettingsForm(AppConfig cfg, UsageClient client)
        {
            _cfg = cfg;
            _client = client;
            BuildUi();
            LoadSettings();
            UpdateHint();
        }

        private void BuildUi()
        {
            SuspendLayout();

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;

            Text = "Pengaturan \u2014 OpenCode Go";
            BackColor = Ui.Bg;
            ForeColor = Ui.Text;
            Font = Ui.Font(9f, FontStyle.Regular);
            ClientSize = new Size(456, 376);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;

            Label keyLabel = MakeLabel("API key OpenCode Go", 16, Ui.TextDim, 8.25f);
            Controls.Add(keyLabel);

            CardPanel keyPanel = new CardPanel();
            keyPanel.Radius = 8;
            keyPanel.FillColor = Color.FromArgb(10, 14, 20);
            keyPanel.BorderColor = Ui.Border;
            keyPanel.SetBounds(18, 34, 300, 34);
            Controls.Add(keyPanel);

            _keyBox = new TextBox();
            _keyBox.BorderStyle = BorderStyle.None;
            _keyBox.BackColor = Color.FromArgb(10, 14, 20);
            _keyBox.ForeColor = Ui.Text;
            _keyBox.Font = Ui.Mono(9.75f);
            _keyBox.UseSystemPasswordChar = true;
            _keyBox.SetBounds(10, 8, 280, 20);
            _keyBox.TextChanged += delegate { UpdateHint(); };
            keyPanel.Controls.Add(_keyBox);

            _showKeyLink = new LinkLabel();
            _showKeyLink.Text = "tampilkan";
            _showKeyLink.AutoSize = true;
            _showKeyLink.Location = new Point(326, 42);
            _showKeyLink.LinkColor = Ui.Blue;
            _showKeyLink.ActiveLinkColor = Ui.Amber;
            _showKeyLink.VisitedLinkColor = Ui.Blue;
            _showKeyLink.LinkBehavior = LinkBehavior.HoverUnderline;
            _showKeyLink.BackColor = Ui.Bg;
            _showKeyLink.Font = Ui.Font(8.25f, FontStyle.Regular);
            _showKeyLink.LinkClicked += delegate
            {
                _showKey = !_showKey;
                _keyBox.UseSystemPasswordChar = !_showKey;
                _showKeyLink.Text = _showKey ? "sembunyikan" : "tampilkan";
            };
            Controls.Add(_showKeyLink);

            _hint = new Label();
            _hint.AutoSize = false;
            _hint.SetBounds(18, 72, 420, 42);
            _hint.ForeColor = Ui.TextMuted;
            _hint.BackColor = Ui.Bg;
            _hint.Font = Ui.Font(8f, FontStyle.Regular);
            Controls.Add(_hint);

            Label intervalLabel = MakeLabel("Perbarui otomatis", 120, Ui.TextDim, 8.5f);
            Controls.Add(intervalLabel);

            _interval = new Segmented();
            _interval.SetBounds(18, 140, 300, 28);
            _interval.SetItems(
                new string[] { "30 dtk", "60 dtk", "2 mnt", "5 mnt" },
                new int[] { 30, 60, 120, 300 },
                60);
            Controls.Add(_interval);

            Label topLabel = MakeLabel("Selalu di atas", 180, Ui.Text, 9f);
            Controls.Add(topLabel);

            _topMost = new ToggleSwitch();
            _topMost.SetBounds(400, 177, 38, 20);
            Controls.Add(_topMost);

            Label autoLabel = MakeLabel("Jalankan saat login Windows", 212, Ui.Text, 9f);
            Controls.Add(autoLabel);

            _autoStart = new ToggleSwitch();
            _autoStart.SetBounds(400, 209, 38, 20);
            Controls.Add(_autoStart);

            Label bubbleLabel = MakeLabel("Bubble kecil di desktop", 244, Ui.Text, 9f);
            Controls.Add(bubbleLabel);

            _bubble = new ToggleSwitch();
            _bubble.SetBounds(400, 241, 38, 20);
            Controls.Add(_bubble);

            Panel separator = new Panel();
            separator.BackColor = Ui.Border;
            separator.SetBounds(18, 278, 420, 1);
            Controls.Add(separator);

            _testButton = Ui.Button("Tes koneksi", 110);
            _testButton.SetBounds(18, 290, 110, 30);
            _testButton.Margin = new Padding(0);
            _testButton.Click += delegate { RunTest(); };
            Controls.Add(_testButton);

            _testResult = new Label();
            _testResult.AutoSize = false;
            _testResult.AutoEllipsis = true;
            _testResult.SetBounds(136, 290, 302, 30);
            _testResult.TextAlign = ContentAlignment.MiddleLeft;
            _testResult.ForeColor = Ui.TextMuted;
            _testResult.BackColor = Ui.Bg;
            _testResult.Font = Ui.Font(8f, FontStyle.Regular);
            Controls.Add(_testResult);

            _clearLink = new LinkLabel();
            _clearLink.Text = "Hapus key tersimpan";
            _clearLink.AutoSize = true;
            _clearLink.Location = new Point(18, 338);
            _clearLink.LinkColor = Ui.TextDim;
            _clearLink.ActiveLinkColor = Ui.Red;
            _clearLink.VisitedLinkColor = Ui.TextDim;
            _clearLink.LinkBehavior = LinkBehavior.HoverUnderline;
            _clearLink.BackColor = Ui.Bg;
            _clearLink.Font = Ui.Font(8.25f, FontStyle.Regular);
            _clearLink.LinkClicked += delegate { ClearKey(); };
            Controls.Add(_clearLink);

            _cancelButton = Ui.Button("Batal", 80);
            _cancelButton.SetBounds(272, 330, 80, 30);
            _cancelButton.Margin = new Padding(0);
            _cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_cancelButton);

            _saveButton = Ui.Button("Simpan", 80);
            _saveButton.SetBounds(358, 330, 80, 30);
            _saveButton.Margin = new Padding(0);
            _saveButton.BackColor = Ui.Blue;
            _saveButton.FlatAppearance.BorderColor = Ui.Blue;
            _saveButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(112, 166, 250);
            _saveButton.Click += delegate { SaveSettings(); };
            Controls.Add(_saveButton);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter) { SaveSettings(); }
            };

            Load += delegate { NativeUi.DarkTitleBar(this); };
            Shown += delegate { ActiveControl = null; };

            ResumeLayout(false);
            PerformLayout();
        }

        private static Label MakeLabel(string text, int top, Color color, float size)
        {
            Label l = new Label();
            l.AutoSize = true;
            l.Text = text;
            l.ForeColor = color;
            l.BackColor = Ui.Bg;
            l.Font = Ui.Font(size, FontStyle.Regular);
            l.Location = new Point(18, top);
            return l;
        }

        private void LoadSettings()
        {
            _keyBox.Text = _cfg.ApiKey;
            _keyBox.SelectionStart = 0;
            _keyBox.SelectionLength = 0;
            _interval.SelectValue(_cfg.RefreshSeconds);
            _topMost.Checked = _cfg.AlwaysOnTop;
            _autoStart.Checked = AutoStart.IsEnabled();
            _bubble.Checked = _cfg.BubbleEnabled;
            _testResult.Text = "";
        }

        private void UpdateHint()
        {
            string typed = _keyBox.Text.Trim();
            if (typed.Length > 0)
            {
                _hint.Text = "Key disimpan terenkripsi (Windows DPAPI) di " + AppConfig.FilePath + ".";
                return;
            }

            KeyInfo auto = KeyFinder.Resolve(new AppConfig());
            if (auto.Found)
            {
                _hint.Text = "Kosongkan untuk memakai key otomatis dari " + auto.Source + " (" + Mask(auto.Key) + ").";
            }
            else
            {
                _hint.Text = "Belum ada key otomatis (OPENCODE_GO_API_KEY / auth.json). Ambil key di opencode.ai/console lalu tempel di atas.";
            }
        }

        private static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 8) return "****";
            return key.Substring(0, 4) + "..." + key.Substring(key.Length - 4);
        }

        private void SaveSettings()
        {
            _cfg.ApiKey = _keyBox.Text.Trim();
            _cfg.RefreshSeconds = _interval.SelectedValue;
            _cfg.AlwaysOnTop = _topMost.Checked;
            _cfg.BubbleEnabled = _bubble.Checked;
            _cfg.Save();

            if (AutoStart.IsEnabled() != _autoStart.Checked) AutoStart.Set(_autoStart.Checked);

            Saved = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ClearKey()
        {
            _cfg.ApiKey = "";
            _cfg.Save();
            _keyBox.Text = "";
            Saved = true;
            _testResult.Text = "Key tersimpan dihapus.";
            _testResult.ForeColor = Ui.TextDim;
            UpdateHint();
        }

        private async void RunTest()
        {
            if (_testing) return;

            string typed = _keyBox.Text.Trim();
            string key = typed;
            string source = "input manual";
            if (key.Length == 0)
            {
                KeyInfo auto = KeyFinder.Resolve(new AppConfig());
                if (!auto.Found)
                {
                    _testResult.Text = "Tidak ada key untuk diuji.";
                    _testResult.ForeColor = Ui.Amber;
                    return;
                }
                key = auto.Key;
                source = auto.Source;
            }

            _testing = true;
            _testButton.Enabled = false;
            _testButton.Text = "Menguji...";
            _testResult.Text = "Menghubungi opencode.ai...";
            _testResult.ForeColor = Ui.TextDim;

            UsageClient client = new UsageClient(_client.BaseUrl, Math.Min(10000, _client.TimeoutMs));
            UsageResult result = null;
            try
            {
                result = await System.Threading.Tasks.Task.Run(delegate { return client.Fetch(key); });
            }
            catch (Exception ex)
            {
                result = new UsageResult();
                result.Error = ex.Message;
            }
            finally
            {
                _testing = false;
                _testButton.Enabled = true;
                _testButton.Text = "Tes koneksi";
            }

            if (result != null && result.Success && result.Snapshot != null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("OK \u00B7 HTTP ").Append(result.StatusCode).Append(" \u00B7 ").Append(result.ElapsedMs).Append(" ms");
                for (int i = 0; i < result.Snapshot.Windows.Count && i < 3; i++)
                {
                    sb.Append(" \u00B7 ").Append(result.Snapshot.Windows[i].Label).Append(' ')
                      .Append(Fmt.Percent(result.Snapshot.Windows[i].Percent));
                }
                _testResult.Text = sb.ToString();
                _testResult.ForeColor = Ui.Green;
                _tip.SetToolTip(_testResult, "Key dari " + source + "\n" + result.Endpoint);
            }
            else
            {
                _testResult.Text = result == null ? "Gagal." : result.Error;
                _testResult.ForeColor = Ui.Red;
                _tip.SetToolTip(_testResult, result == null ? "" : (result.Endpoint + "\n" + (result.Raw ?? "")));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tip.Dispose();
            base.Dispose(disposing);
        }
    }
}
