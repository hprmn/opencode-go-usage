using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    /// <summary>Small segmented selector used for the refresh interval.</summary>
    internal sealed class Segmented : Control
    {
        private string[] _labels = new string[0];
        private int[] _values = new int[0];
        private int _selected;

        public event EventHandler SelectionChanged;

        public Segmented()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Bg;
            Font = Ui.Font(8.25f, FontStyle.Regular);
            Cursor = Cursors.Hand;
            TabStop = false;
            Height = 28;
        }

        public int SelectedValue
        {
            get { return _values.Length == 0 ? 0 : _values[_selected]; }
        }

        public void SetItems(string[] labels, int[] values, int selectedValue)
        {
            _labels = labels;
            _values = values;
            _selected = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == selectedValue) _selected = i;
            }
            Invalidate();
        }

        public void SelectValue(int value)
        {
            for (int i = 0; i < _values.Length; i++)
            {
                if (_values[i] != value) continue;
                if (_selected == i) return;
                _selected = i;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                return;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_labels.Length == 0) return;
            int segment = Math.Max(1, Width / _labels.Length);
            int index = Math.Min(_labels.Length - 1, Math.Max(0, e.X / segment));
            SelectValue(_values[index]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, outer, 8, Ui.CardAlt);
            Ui.StrokeRound(g, outer, 8, Ui.Border);

            if (_labels.Length == 0) return;

            int segmentWidth = (Width - 6) / _labels.Length;
            for (int i = 0; i < _labels.Length; i++)
            {
                Rectangle cell = new Rectangle(3 + i * segmentWidth, 3, segmentWidth - 2, Height - 7);
                bool active = i == _selected;
                if (active)
                {
                    Ui.FillRound(g, cell, 6, Ui.Blue);
                }
                TextRenderer.DrawText(g, _labels[i], Font, cell,
                    active ? Color.White : Ui.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }
}
