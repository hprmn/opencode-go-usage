using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    internal static class Ui
    {
        public static readonly Color Bg = Color.FromArgb(14, 17, 22);
        public static readonly Color Card = Color.FromArgb(22, 27, 34);
        public static readonly Color CardAlt = Color.FromArgb(27, 33, 43);
        public static readonly Color Border = Color.FromArgb(38, 46, 58);
        public static readonly Color Track = Color.FromArgb(44, 52, 64);
        public static readonly Color Text = Color.FromArgb(230, 237, 243);
        public static readonly Color TextDim = Color.FromArgb(139, 152, 170);
        public static readonly Color TextMuted = Color.FromArgb(104, 116, 133);
        public static readonly Color Green = Color.FromArgb(46, 194, 106);
        public static readonly Color Amber = Color.FromArgb(245, 165, 36);
        public static readonly Color Red = Color.FromArgb(240, 82, 82);
        public static readonly Color Blue = Color.FromArgb(90, 150, 245);

        public static Font Font(float size, FontStyle style)
        {
            return Font("Segoe UI", size, style);
        }

        public static Font Font(string family, float size, FontStyle style)
        {
            try
            {
                return new Font(family, size, style, GraphicsUnit.Point);
            }
            catch
            {
                return new Font("Segoe UI", size, style, GraphicsUnit.Point);
            }
        }

        public static Font Mono(float size)
        {
            string[] families = new string[] { "Cascadia Mono", "Consolas", "Courier New" };
            for (int i = 0; i < families.Length; i++)
            {
                try
                {
                    Font f = new Font(families[i], size, FontStyle.Regular, GraphicsUnit.Point);
                    if (string.Equals(f.Name, families[i], StringComparison.OrdinalIgnoreCase)) return f;
                    f.Dispose();
                }
                catch
                {
                }
            }
            return new Font(FontFamily.GenericMonospace, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Color Severity(double percent)
        {
            if (percent < 0) return TextMuted;
            if (percent >= 90) return Red;
            if (percent >= 70) return Amber;
            return Green;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 1)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 2)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPath(b, p);
            }
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color color)
        {
            if (r.Width <= 1 || r.Height <= 1) return;
            using (GraphicsPath p = Round(r, radius))
            using (Pen pen = new Pen(color))
            {
                g.DrawPath(pen, p);
            }
        }

        public static Button Button(string text, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 30;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 48, 63);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(46, 58, 76);
            b.BackColor = CardAlt;
            b.ForeColor = Text;
            b.Font = Font(9f, FontStyle.Regular);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.Margin = new Padding(6, 0, 0, 0);
            b.TabStop = false;
            return b;
        }

        public static Icon AppIcon()
        {
            try
            {
                Icon i = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (i != null) return (Icon)i.Clone();
            }
            catch
            {
            }
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    /// <summary>Panel that paints a rounded, bordered card surface.</summary>
    internal class CardPanel : Panel
    {
        public int Radius = 10;
        public Color FillColor = Ui.Card;
        public Color BorderColor = Ui.Border;

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Bg;
            DoubleBuffered = true;
            BorderStyle = BorderStyle.None;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, r, Radius, FillColor);
            Ui.StrokeRound(g, r, Radius, BorderColor);
        }
    }

    /// <summary>Slim rounded progress track.</summary>
    internal sealed class UsageBar : Control
    {
        private double _percent;
        private Color _fill = Ui.Green;

        public UsageBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Card;
            DoubleBuffered = true;
            TabStop = false;
        }

        public void SetValue(double percent, Color color)
        {
            _percent = percent;
            _fill = color;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Math.Max(1, Height / 2);
            Ui.FillRound(g, track, radius, Ui.Track);

            if (_percent <= 0) return;
            double pct = Math.Min(100.0, _percent);
            int w = (int)Math.Round((Width - 1) * pct / 100.0);
            if (w < Height) w = Math.Min(Height, Width - 1);
            if (w <= 0) return;
            Rectangle fill = new Rectangle(0, 0, w, Height - 1);
            Ui.FillRound(g, fill, radius, _fill);
        }
    }

    /// <summary>Small status capsule.</summary>
    internal sealed class Pill : Control
    {
        private Color _bg = Ui.CardAlt;
        private Color _fg = Ui.TextDim;

        public Pill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(78, 22);
            Font = Ui.Font(8f, FontStyle.Bold);
            BackColor = Ui.Bg;
            TabStop = false;
        }

        public void Set(string text, Color fg, Color bg)
        {
            Text = text;
            _fg = fg;
            _bg = bg;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, r, Height / 2, _bg);
            TextRenderer.DrawText(g, Text, Font, r, _fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Flat on/off switch used in the settings dialog.</summary>
    internal sealed class ToggleSwitch : Control
    {
        private bool _checked;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(38, 20);
            BackColor = Ui.Bg;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, track, Height / 2, _checked ? Ui.Green : Ui.Track);
            int knob = Height - 6;
            int x = _checked ? Width - knob - 3 : 3;
            using (SolidBrush b = new SolidBrush(Color.White))
            {
                g.FillEllipse(b, x, 3, knob, knob);
            }
        }
    }

    internal static class NativeUi
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void DarkTitleBar(Form f)
        {
            if (f == null || !f.IsHandleCreated) return;
            try
            {
                int on = 1;
                int hr = DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int));
                if (hr != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, sizeof(int));
            }
            catch
            {
            }
        }
    }
}

namespace OpenCodeUsage
{
    /// <summary>
    /// Single-line text control. Plain WinForms labels wrap when the text does not
    /// fit, which silently hides the tail of values such as "12.5%".
    /// </summary>
    internal sealed class LineLabel : Control
    {
        private ContentAlignment _align = ContentAlignment.MiddleLeft;

        public LineLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            BackColor = Ui.Bg;
            ForeColor = Ui.Text;
            Font = Ui.Font(9f, FontStyle.Regular);
        }

        public ContentAlignment TextAlign
        {
            get { return _align; }
            set { _align = value; Invalidate(); }
        }

        public override string Text
        {
            get { return base.Text; }
            set { base.Text = value; Invalidate(); }
        }

        public override Color ForeColor
        {
            get { return base.ForeColor; }
            set { base.ForeColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (string.IsNullOrEmpty(Text)) return;

            TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding |
                                    TextFormatFlags.NoPrefix;
            switch (_align)
            {
                case ContentAlignment.TopRight:
                case ContentAlignment.MiddleRight:
                case ContentAlignment.BottomRight:
                    flags |= TextFormatFlags.Right;
                    break;
                case ContentAlignment.TopCenter:
                case ContentAlignment.MiddleCenter:
                case ContentAlignment.BottomCenter:
                    flags |= TextFormatFlags.HorizontalCenter;
                    break;
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
        }
    }
}
