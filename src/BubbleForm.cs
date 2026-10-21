using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    /// <summary>
    /// Small always-on-top "bubble" on the desktop that shows the three quota
    /// windows. It does not depend on the Windows notification area, so it still
    /// works when the tray icon cannot be registered.
    /// </summary>
    internal sealed class BubbleForm : Form
    {
        private readonly ToolTip _tip = new ToolTip();
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();
        private readonly List<UsageWindow> _windows = new List<UsageWindow>();

        private bool _dragging;
        private Point _dragOffset;
        private bool _moved;

        public event EventHandler OpenRequested;
        public event EventHandler RefreshRequested;
        public event EventHandler HideRequested;
        public event EventHandler ExitRequested;
        public event EventHandler Moved;

        public BubbleForm()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Ui.Bg;
            Font = Ui.Font(8.25f, FontStyle.Regular);
            ClientSize = new Size(214, 78);
            DoubleBuffered = true;
            Cursor = Cursors.SizeAll;

            BuildMenu();
            ApplyRegion();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: jangan muncul di Alt+Tab
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        private void BuildMenu()
        {
            _menu.BackColor = Ui.Card;
            _menu.ForeColor = Ui.Text;
            _menu.Items.Add("Buka dashboard", null, delegate { Raise(OpenRequested); });
            _menu.Items.Add("Perbarui sekarang", null, delegate { Raise(RefreshRequested); });
            _menu.Items.Add("Sembunyikan bubble", null, delegate { Raise(HideRequested); });
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Keluar", null, delegate { Raise(ExitRequested); });
        }

        private static void Raise(EventHandler handler)
        {
            if (handler != null) handler(null, EventArgs.Empty);
        }

        public void SetSnapshot(UsageSnapshot snapshot)
        {
            _windows.Clear();
            if (snapshot != null && snapshot.Windows != null)
            {
                for (int i = 0; i < snapshot.Windows.Count && i < 3; i++) _windows.Add(snapshot.Windows[i]);
            }

            string tip = "OpenCode Go";
            if (_windows.Count > 0)
            {
                tip = "";
                for (int i = 0; i < _windows.Count; i++)
                {
                    if (i > 0) tip += "\n";
                    tip += _windows[i].TooltipText();
                }
            }
            _tip.SetToolTip(this, tip);
            Invalidate();
        }

        public void PlaceBottomRight()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);
        }

        public void RestorePosition(int x, int y)
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            if (x < 0 || y < 0)
            {
                PlaceBottomRight();
                return;
            }

            // pastikan masih terlihat di layar
            Point candidate = new Point(x, y);
            Rectangle bounds = new Rectangle(candidate, Size);
            if (!area.IntersectsWith(bounds)) PlaceBottomRight();
            else Location = candidate;
        }

        private void ApplyRegion()
        {
            using (GraphicsPath path = Ui.Round(new Rectangle(0, 0, Width, Height), 12))
            {
                Region = new Region(path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.Bg);

            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(24, 29, 37)))
            {
                using (GraphicsPath path = Ui.Round(outer, 12))
                {
                    g.FillPath(fill, path);
                }
            }
            using (Pen border = new Pen(Color.FromArgb(46, 56, 70)))
            {
                using (GraphicsPath path = Ui.Round(outer, 12))
                {
                    g.DrawPath(border, path);
                }
            }

            int pad = 11;
            int labelWidth = 34;
            int percentWidth = 44;
            int rowHeight = 18;
            int y = pad - 2;

            if (_windows.Count == 0)
            {
                TextRenderer.DrawText(g, "OpenCode Go \u00B7 belum ada data",
                    Ui.Font(8.25f, FontStyle.Regular),
                    new Rectangle(pad, 0, Width - pad * 2, Height),
                    Ui.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            for (int i = 0; i < _windows.Count; i++)
            {
                UsageWindow win = _windows[i];
                Color color = Ui.Severity(win.Percent);

                TextRenderer.DrawText(g, win.ShortLabel, Ui.Font(8.25f, FontStyle.Regular),
                    new Rectangle(pad, y, labelWidth, rowHeight), Ui.TextDim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                int barLeft = pad + labelWidth + 2;
                int barWidth = Width - barLeft - percentWidth - pad;
                int barY = y + rowHeight / 2 - 2;
                Rectangle track = new Rectangle(barLeft, barY, barWidth, 4);
                Ui.FillRound(g, track, 2, Ui.Track);
                if (win.HasPercent && win.Percent > 0)
                {
                    double pct = Math.Min(100.0, win.Percent);
                    int fillWidth = (int)Math.Round(barWidth * pct / 100.0);
                    if (fillWidth < 3) fillWidth = 3;
                    if (fillWidth > barWidth) fillWidth = barWidth;
                    Ui.FillRound(g, new Rectangle(barLeft, barY, fillWidth, 4), 2, color);
                }

                TextRenderer.DrawText(g, Fmt.Percent(win.Percent), Ui.Font(8.5f, FontStyle.Bold),
                    new Rectangle(Width - pad - percentWidth, y, percentWidth, rowHeight), color,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                y += rowHeight;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _moved = false;
                _dragOffset = new Point(e.X, e.Y);
            }
            else if (e.Button == MouseButtons.Right)
            {
                _menu.Show(this, e.Location);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            Point screen = PointToScreen(new Point(e.X, e.Y));
            Point target = new Point(screen.X - _dragOffset.X, screen.Y - _dragOffset.Y);
            if (target != Location)
            {
                Location = target;
                _moved = true;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                if (_moved && Moved != null) Moved(this, EventArgs.Empty);
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (OpenRequested != null) OpenRequested(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tip.Dispose();
                _menu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
