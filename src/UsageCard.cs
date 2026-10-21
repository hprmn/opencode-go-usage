using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    /// <summary>One quota window (5h / weekly / monthly) rendered as a card.</summary>
    internal sealed class UsageCard : CardPanel
    {
        private readonly LineLabel _name = new LineLabel();
        private readonly LineLabel _value = new LineLabel();
        private readonly LineLabel _sub = new LineLabel();
        private readonly UsageBar _bar = new UsageBar();
        private readonly ToolTip _tip;
        private UsageWindow _window;

        public UsageCard(UsageWindow window, ToolTip tip)
        {
            _window = window;
            _tip = tip;

            Height = 78;
            Margin = new Padding(0, 0, 0, 10);
            Dock = DockStyle.Fill;

            _name.Font = Ui.Font("Segoe UI Semibold", 9.5f, FontStyle.Regular);
            _name.ForeColor = Ui.TextDim;
            _name.BackColor = Ui.Card;
            _name.TextAlign = ContentAlignment.MiddleLeft;

            _value.Font = Ui.Font(12.5f, FontStyle.Bold);
            _value.ForeColor = Ui.Green;
            _value.BackColor = Ui.Card;
            _value.TextAlign = ContentAlignment.MiddleRight;

            _sub.Font = Ui.Font(8.25f, FontStyle.Regular);
            _sub.ForeColor = Ui.TextMuted;
            _sub.BackColor = Ui.Card;
            _sub.TextAlign = ContentAlignment.MiddleLeft;

            _bar.BackColor = Ui.Card;

            Controls.Add(_name);
            Controls.Add(_value);
            Controls.Add(_bar);
            Controls.Add(_sub);

            Update(window);
        }

        public string Key
        {
            get { return _window == null ? "" : _window.Key; }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = 14;
            int w = Width;
            int h = Height;
            if (w <= 0 || h <= 0) return;

            int valueWidth = 86;
            _name.SetBounds(pad, 8, Math.Max(40, w - pad * 2 - valueWidth - 4), 22);
            _value.SetBounds(w - pad - valueWidth, 6, valueWidth, 26);
            _bar.SetBounds(pad, h - 36, Math.Max(20, w - pad * 2), 6);
            _sub.SetBounds(pad, h - 27, Math.Max(20, w - pad * 2), 16);
        }

        public void Update(UsageWindow window)
        {
            _window = window;
            _name.Text = window.Label;
            _value.Text = window.HasPercent ? Fmt.Percent(window.Percent) : "\u2014";

            Color color = Ui.Severity(window.Percent);
            _value.ForeColor = color;
            _bar.SetValue(window.HasPercent ? window.Percent : 0, color);
            RefreshCountdown();
        }

        /// <summary>Cheap per-second update: only the countdown text changes.</summary>
        public void RefreshCountdown()
        {
            if (_window == null) return;
            _sub.Text = BuildSubText(_window);
            string tip = _window.TooltipText();
            if (_tip != null)
            {
                _tip.SetToolTip(this, tip);
                _tip.SetToolTip(_name, tip);
                _tip.SetToolTip(_value, tip);
                _tip.SetToolTip(_sub, tip);
            }
        }

        private static string BuildSubText(UsageWindow w)
        {
            string status = "";
            if (!string.IsNullOrEmpty(w.Status) &&
                !string.Equals(w.Status, "ok", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(w.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                status = "status " + w.Status;
            }

            string main;
            if (w.HasPercent && w.Percent >= 100) main = "kuota tercapai";
            else main = Fmt.CountdownSentence(w.ResetsAt);

            if (string.IsNullOrEmpty(main) && !string.IsNullOrEmpty(w.Detail)) main = w.Detail;
            if (string.IsNullOrEmpty(main)) main = "\u2014";

            if (!string.IsNullOrEmpty(status)) return status + " \u00B7 " + main;
            return main;
        }
    }
}
