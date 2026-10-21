using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    internal sealed class MainForm : Form
    {
        private readonly AppConfig _cfg;
        private readonly RuntimeOptions _opt;
        private readonly UsageClient _client;
        private readonly EventWaitHandle _activateEvent;

        private readonly System.Windows.Forms.Timer _refreshTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _tickTimer = new System.Windows.Forms.Timer();
        private readonly ToolTip _tip = new ToolTip();
        private readonly Dictionary<string, UsageCard> _cardsByKey =
            new Dictionary<string, UsageCard>(StringComparer.OrdinalIgnoreCase);

        private NotifyIcon _tray;
        private ToolStripMenuItem _trayTopMostItem;
        private ToolStripMenuItem _trayBubbleItem;
        private BubbleForm _bubble;
        private bool _exiting;
        private bool _trayHintShown;
        private bool _busy;
        private UsageResult _last;
        private UsageSnapshot _lastGood;
        private string _cardSignature = "";
        private string _subtitleMode = "loading";
        private DateTime _lastUpdated = DateTime.MinValue;

        private Label _titleLabel;
        private Label _subtitleLabel;
        private Pill _pill;
        private LineLabel _statusLabel;
        private Button _refreshButton;
        private Button _keyButton;
        private Button _jsonButton;
        private Panel _cardsHost;
        private TableLayoutPanel _cards;
        private Label _emptyLabel;

        public MainForm(AppConfig cfg, RuntimeOptions opt, EventWaitHandle activateEvent)
        {
            _cfg = cfg;
            _opt = opt;
            _activateEvent = activateEvent;
            _client = new UsageClient(opt.BaseUrl, opt.TimeoutMs);

            BuildUi();
            BuildTray();
            ApplyConfig();

            _refreshTimer.Tick += OnRefreshTick;
            _tickTimer.Interval = 1000;
            _tickTimer.Tick += OnSecondTick;
            _tickTimer.Start();

            Load += delegate { NativeUi.DarkTitleBar(this); };
            Shown += OnShown;
        }

        // ---------------------------------------------------------------- layout

        private void BuildUi()
        {
            SuspendLayout();

            Text = "OpenCode Go \u00B7 Usage";
            BackColor = Ui.Bg;
            ForeColor = Ui.Text;
            Font = Ui.Font(9f, FontStyle.Regular);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(468, 386);
            MinimumSize = new Size(420, 350);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            DoubleBuffered = true;
            try { Icon = Ui.AppIcon(); }
            catch { }

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Ui.Bg;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.Padding = new Padding(18, 16, 18, 12);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

            // header ---------------------------------------------------------
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Ui.Bg;
            header.Margin = new Padding(0);
            header.ColumnCount = 2;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel headerLeft = new Panel();
            headerLeft.Dock = DockStyle.Fill;
            headerLeft.BackColor = Ui.Bg;
            headerLeft.Margin = new Padding(0);

            _titleLabel = new Label();
            _titleLabel.AutoSize = true;
            _titleLabel.Text = "OpenCode Go";
            _titleLabel.Font = Ui.Font("Segoe UI Semibold", 14f, FontStyle.Regular);
            _titleLabel.ForeColor = Ui.Text;
            _titleLabel.BackColor = Ui.Bg;
            _titleLabel.Location = new Point(0, 2);

            _subtitleLabel = new Label();
            _subtitleLabel.AutoSize = true;
            _subtitleLabel.Text = "Menyambung ke opencode.ai...";
            _subtitleLabel.Font = Ui.Font(8.25f, FontStyle.Regular);
            _subtitleLabel.ForeColor = Ui.TextMuted;
            _subtitleLabel.BackColor = Ui.Bg;
            _subtitleLabel.Location = new Point(1, 30);

            headerLeft.Controls.Add(_titleLabel);
            headerLeft.Controls.Add(_subtitleLabel);

            _pill = new Pill();
            _pill.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _pill.Margin = new Padding(0, 6, 0, 0);
            _pill.Set("MEMUAT", Ui.Blue, Ui.CardAlt);

            header.Controls.Add(headerLeft, 0, 0);
            header.Controls.Add(_pill, 1, 0);

            // cards ----------------------------------------------------------
            _cardsHost = new Panel();
            _cardsHost.Dock = DockStyle.Fill;
            _cardsHost.BackColor = Ui.Bg;
            _cardsHost.Margin = new Padding(0);

            _cards = new TableLayoutPanel();
            _cards.Dock = DockStyle.Fill;
            _cards.BackColor = Ui.Bg;
            _cards.Margin = new Padding(0);
            _cards.ColumnCount = 1;
            _cards.RowCount = 0;
            _cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _emptyLabel = new Label();
            _emptyLabel.Dock = DockStyle.Fill;
            _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _emptyLabel.ForeColor = Ui.TextMuted;
            _emptyLabel.BackColor = Ui.Bg;
            _emptyLabel.Font = Ui.Font(9f, FontStyle.Regular);
            _emptyLabel.Text = "Memuat data kuota...";
            _emptyLabel.Visible = true;

            _cardsHost.Controls.Add(_cards);
            _cardsHost.Controls.Add(_emptyLabel);

            // footer ---------------------------------------------------------
            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.BackColor = Ui.Bg;
            footer.Margin = new Padding(0);
            footer.ColumnCount = 2;
            footer.RowCount = 1;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _statusLabel = new LineLabel();
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.ForeColor = Ui.TextMuted;
            _statusLabel.BackColor = Ui.Bg;
            _statusLabel.Font = Ui.Font(8.25f, FontStyle.Regular);
            _statusLabel.Text = "Menunggu data";
            _statusLabel.Margin = new Padding(0, 0, 8, 0);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.BackColor = Ui.Bg;
            buttons.Margin = new Padding(0);

            _refreshButton = Ui.Button("Perbarui", 80);
            _keyButton = Ui.Button("Key", 58);
            _jsonButton = Ui.Button("JSON", 58);
            _refreshButton.Click += delegate { BeginRefresh(); };
            _keyButton.Click += delegate { OpenSettings(); };
            _jsonButton.Click += delegate { ShowRawJson(); };

            buttons.Controls.Add(_refreshButton);
            buttons.Controls.Add(_keyButton);
            buttons.Controls.Add(_jsonButton);

            footer.Controls.Add(_statusLabel, 0, 0);
            footer.Controls.Add(buttons, 1, 0);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_cardsHost, 0, 1);
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);

            KeyDown += OnKeyDown;

            ResumeLayout(false);
            PerformLayout();
        }

        private void BuildTray()
        {
            _tray = new NotifyIcon();
            try { _tray.Icon = Ui.AppIcon(); }
            catch { _tray.Icon = SystemIcons.Application; }
            _tray.Text = "OpenCode Go \u00B7 memuat";

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Ui.Card;
            menu.ForeColor = Ui.Text;
            menu.Items.Add("Buka dashboard", null, delegate { RestoreFromTray(); });
            menu.Items.Add("Perbarui sekarang", null, delegate { BeginRefresh(); });
            menu.Items.Add("Salin JSON", null, delegate { CopyJson(); });
            menu.Items.Add("Pengaturan...", null, delegate { OpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());

            _trayTopMostItem = new ToolStripMenuItem("Selalu di atas");
            _trayTopMostItem.CheckOnClick = true;
            _trayTopMostItem.CheckedChanged += delegate
            {
                _cfg.AlwaysOnTop = _trayTopMostItem.Checked;
                TopMost = _cfg.AlwaysOnTop;
                _cfg.Save();
            };
            menu.Items.Add(_trayTopMostItem);

            _trayBubbleItem = new ToolStripMenuItem("Bubble di desktop");
            _trayBubbleItem.CheckOnClick = false;
            _trayBubbleItem.Click += delegate { ToggleBubble(); };
            menu.Items.Add(_trayBubbleItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Keluar", null, delegate { ExitApp(); });

            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { RestoreFromTray(); };
        }

        private void ApplyConfig()
        {
            TopMost = _cfg.AlwaysOnTop;
            if (_trayTopMostItem != null) _trayTopMostItem.Checked = _cfg.AlwaysOnTop;
            if (_trayBubbleItem != null) _trayBubbleItem.Checked = _cfg.BubbleEnabled;
            _refreshTimer.Interval = Math.Max(15, _cfg.RefreshSeconds) * 1000;
        }

        private void ShowBubble()
        {
            if (_bubble == null)
            {
                _bubble = new BubbleForm();
                _bubble.OpenRequested += delegate { RestoreFromTray(); };
                _bubble.RefreshRequested += delegate { BeginRefresh(); };
                _bubble.HideRequested += delegate { HideBubble(); };
                _bubble.ExitRequested += delegate { ExitApp(); };
                _bubble.Moved += delegate
                {
                    _cfg.BubbleX = _bubble.Location.X;
                    _cfg.BubbleY = _bubble.Location.Y;
                    _cfg.Save();
                };
                _bubble.RestorePosition(_cfg.BubbleX, _cfg.BubbleY);
                if (_lastGood != null) _bubble.SetSnapshot(_lastGood);
            }

            _bubble.Show();
            _bubble.BringToFront();
            _cfg.BubbleEnabled = true;
            if (_trayBubbleItem != null) _trayBubbleItem.Checked = true;
        }

        private void HideBubble()
        {
            if (_bubble != null) _bubble.Hide();
            _cfg.BubbleEnabled = false;
            if (_trayBubbleItem != null) _trayBubbleItem.Checked = false;
            _cfg.Save();
        }

        private void ToggleBubble()
        {
            if (_bubble != null && _bubble.Visible) HideBubble();
            else ShowBubble();
        }

        // ---------------------------------------------------------------- flow

        private void OnShown(object sender, EventArgs e)
        {
            // NotifyIcon lebih andal kalau diaktifkan setelah jendela benar-benar tampil.
            if (_tray != null)
            {
                try { _tray.Visible = true; }
                catch { }
            }
            StartActivationListener();

            if (_cfg.BubbleEnabled) ShowBubble();

            if (_opt.Demo)
            {
                _subtitleMode = "demo";
                UsageSnapshot snap = UsageSnapshot.Parse(DemoData.Build());
                PopulateCards(snap);
                SetPill("DEMO", Ui.Amber, Ui.CardAlt);
                SetStatus("Mode demo: data contoh, bukan kuota asli.", Ui.Amber);
                return;
            }
            BeginRefresh();
        }

        private void OnRefreshTick(object sender, EventArgs e)
        {
            BeginRefresh();
        }

        private void OnSecondTick(object sender, EventArgs e)
        {
            foreach (UsageCard card in _cardsByKey.Values) card.RefreshCountdown();
            UpdateSubtitle();
        }

        private async void BeginRefresh()
        {
            if (_busy) return;

            if (_opt.Demo)
            {
                UsageSnapshot demo = UsageSnapshot.Parse(DemoData.Build());
                PopulateCards(demo);
                SetPill("DEMO", Ui.Amber, Ui.CardAlt);
                SetStatus("Mode demo: data contoh, bukan kuota asli.", Ui.Amber);
                return;
            }

            KeyInfo info = KeyFinder.Resolve(_cfg);
            if (!info.Found)
            {
                _subtitleMode = "nokey";
                UpdateSubtitle();
                SetPill("TANPA KEY", Ui.TextMuted, Ui.CardAlt);
                SetStatus("API key belum diatur. Klik tombol Key untuk menyimpan key OpenCode Go.", Ui.Amber);
                ShowEmpty("Belum ada data.\nSimpan API key dulu lewat tombol Key.");
                ScheduleRefresh();
                return;
            }

            _busy = true;
            _refreshButton.Enabled = false;
            _subtitleMode = "loading";
            UpdateSubtitle();
            SetPill("MEMUAT", Ui.Blue, Ui.CardAlt);
            SetStatus("Menghubungi opencode.ai...", Ui.TextMuted);

            string key = info.Key;
            UsageClient client = _client;
            UsageResult result = null;
            try
            {
                result = await Task.Run(delegate { return client.Fetch(key); });
            }
            catch (Exception ex)
            {
                result = new UsageResult();
                result.Error = "Kesalahan tak terduga: " + ex.Message;
            }
            finally
            {
                _busy = false;
                _refreshButton.Enabled = true;
            }

            ApplyResult(result);
            ScheduleRefresh();
        }

        private void ApplyResult(UsageResult result)
        {
            _last = result;
            if (result == null)
            {
                _subtitleMode = "error";
                UpdateSubtitle();
                SetPill("ERROR", Ui.Red, Ui.CardAlt);
                SetStatus("Tidak ada respons.", Ui.Red);
                return;
            }

            if (result.Success && result.Snapshot != null)
            {
                _subtitleMode = "ok";
                _lastUpdated = DateTime.Now;
                PopulateCards(result.Snapshot);

                StringBuilder status = new StringBuilder();
                status.Append(result.SummaryLine());
                string extras = result.Snapshot.ExtrasText();
                if (!string.IsNullOrEmpty(extras)) status.Append(" \u00B7 ").Append(extras);
                SetStatus(status.ToString(), Ui.TextMuted);
                SetPill("LIVE", Ui.Green, Color.FromArgb(19, 44, 31));
                _tip.SetToolTip(_statusLabel, result.Endpoint);
                return;
            }

            _subtitleMode = "error";
            UpdateSubtitle();

            bool auth = result.StatusCode == 401 || result.StatusCode == 403;
            string pill = "ERROR";
            if (auth) pill = "AUTH";
            else if (result.StatusCode == 0) pill = "OFFLINE";
            else if (result.StatusCode > 0) pill = "HTTP " + result.StatusCode;
            SetPill(pill, auth ? Ui.Amber : Ui.Red, Ui.CardAlt);

            string message = string.IsNullOrEmpty(result.Error) ? "Gagal memuat data." : result.Error;
            SetStatus(message, auth ? Ui.Amber : Ui.Red);
            _tip.SetToolTip(_statusLabel, message + "\n" + result.Endpoint);

            if (_lastGood == null)
            {
                string hint = auth ? "Periksa API key lewat tombol Key." : "Cek koneksi internet lalu klik Perbarui.";
                ShowEmpty("Gagal memuat kuota.\n" + hint);
            }
            UpdateTrayText(_lastGood, true);
        }

        private void ScheduleRefresh()
        {
            _refreshTimer.Stop();
            int seconds = Math.Max(15, _cfg.RefreshSeconds);
            if (_last == null || !_last.Success) seconds = Math.Min(seconds, 30);
            _refreshTimer.Interval = seconds * 1000;
            _refreshTimer.Start();
        }

        // ---------------------------------------------------------------- view

        private void PopulateCards(UsageSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Windows.Count == 0)
            {
                ShowEmpty("Respons tidak memuat data kuota.\nCoba lagi beberapa saat.");
                return;
            }

            _emptyLabel.Visible = false;
            _cards.Visible = true;
            EnsureCards(snapshot);
            _lastGood = snapshot;
            UpdateTrayText(snapshot, false);
            if (_bubble != null && _bubble.Visible) _bubble.SetSnapshot(snapshot);
        }

        private void EnsureCards(UsageSnapshot snapshot)
        {
            StringBuilder signature = new StringBuilder();
            for (int i = 0; i < snapshot.Windows.Count; i++)
            {
                signature.Append(snapshot.Windows[i].Key.ToLowerInvariant()).Append('|');
            }

            if (string.Equals(signature.ToString(), _cardSignature, StringComparison.Ordinal) &&
                _cardsByKey.Count == snapshot.Windows.Count)
            {
                for (int i = 0; i < snapshot.Windows.Count; i++)
                {
                    UsageCard existing;
                    if (_cardsByKey.TryGetValue(snapshot.Windows[i].Key, out existing)) existing.Update(snapshot.Windows[i]);
                }
                return;
            }

            _cardSignature = signature.ToString();
            _cardsByKey.Clear();

            Control[] old = new Control[_cards.Controls.Count];
            _cards.Controls.CopyTo(old, 0);
            for (int i = 0; i < old.Length; i++) old[i].Dispose();
            _cards.Controls.Clear();

            _cards.SuspendLayout();
            _cards.RowCount = snapshot.Windows.Count;
            _cards.RowStyles.Clear();
            float rowPercent = 100f / Math.Max(1, snapshot.Windows.Count);

            for (int i = 0; i < snapshot.Windows.Count; i++)
            {
                _cards.RowStyles.Add(new RowStyle(SizeType.Percent, rowPercent));
                UsageCard card = new UsageCard(snapshot.Windows[i], _tip);
                card.Name = "card_" + i;
                _cards.Controls.Add(card, 0, i);
                _cardsByKey[snapshot.Windows[i].Key] = card;
            }
            _cards.ResumeLayout(true);
        }

        private void ShowEmpty(string text)
        {
            _emptyLabel.Text = text;
            _emptyLabel.Visible = true;
            _emptyLabel.BringToFront();
            _cards.Visible = false;
            _cardsByKey.Clear();
            _cardSignature = "";
        }

        private void SetPill(string text, Color foreground, Color background)
        {
            _pill.Set(text, foreground, background);
        }

        private void SetStatus(string text, Color color)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = color;
            if (color == Ui.TextMuted || color == Ui.Green || color == Ui.Amber)
                _tip.SetToolTip(_statusLabel, text);
        }

        private void UpdateSubtitle()
        {
            switch (_subtitleMode)
            {
                case "loading":
                    _subtitleLabel.Text = "Menyambung ke opencode.ai...";
                    break;
                case "ok":
                    _subtitleLabel.Text = "Diperbarui " + Fmt.Clock(_lastUpdated) + " \u00B7 " + Fmt.Ago(_lastUpdated) +
                                          " \u00B7 tiap " + _cfg.RefreshSeconds + " dtk";
                    break;
                case "error":
                    _subtitleLabel.Text = "Gagal " + Fmt.Clock(DateTime.Now) + " \u00B7 mencoba lagi otomatis";
                    break;
                case "nokey":
                    _subtitleLabel.Text = "API key belum diatur";
                    break;
                case "demo":
                    _subtitleLabel.Text = "Mode demo \u00B7 data contoh";
                    break;
                default:
                    _subtitleLabel.Text = "";
                    break;
            }
        }

        private void UpdateTrayText(UsageSnapshot snapshot, bool stale)
        {
            if (_tray == null) return;
            if (snapshot == null || snapshot.Windows.Count == 0)
            {
                _tray.Text = "OpenCode Go \u00B7 belum ada data";
                return;
            }

            StringBuilder sb = new StringBuilder();
            if (stale) sb.Append("! ");
            for (int i = 0; i < snapshot.Windows.Count && i < 3; i++)
            {
                if (i > 0) sb.Append(" \u00B7 ");
                sb.Append(snapshot.Windows[i].ShortLabel).Append(' ').Append(Fmt.Percent(snapshot.Windows[i].Percent));
            }
            string text = sb.ToString();
            if (text.Length > 63) text = text.Substring(0, 63);
            _tray.Text = text;
        }

        // ---------------------------------------------------------------- actions

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5 || (e.Control && e.KeyCode == Keys.R))
            {
                BeginRefresh();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                HideToTray();
                e.Handled = true;
            }
        }

        private void OpenSettings()
        {
            using (SettingsForm form = new SettingsForm(_cfg, _client))
            {
                form.ShowDialog(this);
                if (!form.Saved) return;
                _cfg.Save();
                ApplyConfig();
                if (_cfg.BubbleEnabled) ShowBubble(); else HideBubble();
                BeginRefresh();
            }
        }

        private void ShowRawJson()
        {
            string raw = _last != null ? _last.Raw : "";
            if (string.IsNullOrEmpty(raw) && _lastGood != null) raw = _lastGood.Raw;
            if (string.IsNullOrEmpty(raw))
            {
                SetStatus("Belum ada JSON untuk ditampilkan.", Ui.TextMuted);
                return;
            }

            StringBuilder head = new StringBuilder();
            if (_last != null)
            {
                head.Append("# GET ").Append(_last.Endpoint).Append('\n');
                head.Append("# ").Append(_last.SummaryLine());
                if (!_last.Success && !string.IsNullOrEmpty(_last.Error)) head.Append("  ").Append(_last.Error);
                head.Append('\n');
            }
            head.Append('\n');
            head.Append(Json.PrettyOrRaw(raw));

            using (RawJsonForm form = new RawJsonForm("Respons JSON", head.ToString(), raw))
            {
                form.ShowDialog(this);
            }
        }

        private void CopyJson()
        {
            string raw = _last != null ? _last.Raw : "";
            if (string.IsNullOrEmpty(raw) && _lastGood != null) raw = _lastGood.Raw;
            if (string.IsNullOrEmpty(raw))
            {
                SetStatus("Belum ada JSON untuk disalin.", Ui.TextMuted);
                return;
            }
            try
            {
                Clipboard.SetText(raw);
                SetStatus("JSON disalin ke clipboard.", Ui.Green);
            }
            catch (Exception ex)
            {
                SetStatus("Gagal menyalin: " + ex.Message, Ui.Red);
            }
        }

        private void StartActivationListener()
        {
            if (_activateEvent == null) return;

            Thread thread = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        if (!_activateEvent.WaitOne()) continue;
                        if (IsDisposed) return;
                        BeginInvoke((MethodInvoker)delegate { RestoreFromTray(); });
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (InvalidOperationException)
                    {
                        return;
                    }
                }
            });
            thread.IsBackground = true;
            thread.Name = "OpenCodeUsage-activate";
            thread.Start();
        }

        private void HideToTray()
        {
            Hide();
            if (_trayHintShown || _tray == null) return;
            _trayHintShown = true;
            try
            {
                _tray.ShowBalloonTip(2500, "OpenCode Go", "Aplikasi tetap berjalan di system tray. Klik dua kali ikonnya untuk membuka.",
                    ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void ExitApp()
        {
            _exiting = true;
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            _refreshTimer.Stop();
            _tickTimer.Stop();
            if (_bubble != null)
            {
                _bubble.Dispose();
                _bubble = null;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }
                _tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
