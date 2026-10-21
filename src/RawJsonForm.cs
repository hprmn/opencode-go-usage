using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCodeUsage
{
    internal sealed class RawJsonForm : Form
    {
        private readonly string _copyText;

        public RawJsonForm(string title, string display, string copyText)
        {
            _copyText = copyText ?? display ?? "";
            BuildUi(title, display ?? "");
        }

        private void BuildUi(string title, string display)
        {
            SuspendLayout();

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;

            Text = title;
            BackColor = Ui.Bg;
            ForeColor = Ui.Text;
            ClientSize = new Size(660, 470);
            MinimumSize = new Size(480, 320);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;

            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Padding = new Padding(16, 16, 16, 8);
            host.BackColor = Ui.Bg;

            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.WordWrap = false;
            box.ScrollBars = ScrollBars.Both;
            box.Dock = DockStyle.Fill;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Color.FromArgb(10, 14, 20);
            box.ForeColor = Ui.Text;
            box.Font = Ui.Mono(9.25f);
            box.Text = display.Replace("\n", "\r\n");
            box.SelectionStart = 0;
            box.SelectionLength = 0;
            host.Controls.Add(box);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 50;
            bottom.Padding = new Padding(16, 6, 16, 8);
            bottom.BackColor = Ui.Bg;

            Button copy = Ui.Button("Salin JSON", 100);
            copy.Dock = DockStyle.Left;
            copy.Margin = new Padding(0);
            copy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(_copyText);
                    copy.Text = "Tersalin";
                }
                catch
                {
                    copy.Text = "Gagal";
                }
            };

            Button close = Ui.Button("Tutup", 80);
            close.Dock = DockStyle.Right;
            close.Margin = new Padding(0);
            close.Click += delegate { Close(); };

            bottom.Controls.Add(close);
            bottom.Controls.Add(copy);

            Controls.Add(host);
            Controls.Add(bottom);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
            Load += delegate { NativeUi.DarkTitleBar(this); };
            Shown += delegate { ActiveControl = null; };

            ResumeLayout(false);
        }
    }
}
