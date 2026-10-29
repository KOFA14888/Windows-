using System;
using System.Drawing;
using System.Windows.Forms;

namespace Win10Sim.WinFormsTemplate
{
    internal sealed class ShellForm : Form
    {
        private readonly Timer _spinnerTimer;
        private readonly Timer _clockTimer;
        private float _angle;

        public ShellForm()
        {
            Text = "Windows 10 Simulator (WinForms)";
            BackColor = Color.Black;
            DoubleBuffered = true;
            Cursor = Cursors.Default;

            _spinnerTimer = new Timer { Interval = 16 };
            _spinnerTimer.Tick += (_, __) => { _angle = (_angle + 3.75f) % 360f; Invalidate(); };

            _clockTimer = new Timer { Interval = 1000 };
            _clockTimer.Tick += (_, __) => Invalidate();

            Load += (_, __) => { _spinnerTimer.Start(); _clockTimer.Start(); };
            FormClosed += (_, __) => { _spinnerTimer.Stop(); _clockTimer.Stop(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int cx = ClientSize.Width / 2;
            int cy = ClientSize.Height / 2;

            // Логотип: 4 квадрата
            const int tile = 46, gap = 8;
            int left = cx - tile - gap / 2;
            int top = cy - 130 - tile - gap / 2;
            using (var white = new SolidBrush(Color.White))
            {
                g.FillRectangle(white, left, top, tile, tile);
                g.FillRectangle(white, left + tile + gap, top, tile, tile);
                g.FillRectangle(white, left, top + tile + gap, tile, tile);
                g.FillRectangle(white, left + tile + gap, top + tile + gap, tile, tile);
            }

            // Вращающиеся точки вокруг центра
            int ring = 26;
            for (int i = 0; i < 5; i++)
            {
                double a = (_angle + i * 72.0) * Math.PI / 180.0;
                int px = cx + (int)(Math.Cos(a) * ring) - 4;
                int py = cy - 80 + (int)(Math.Sin(a) * ring) - 4;
                using (var b = new SolidBrush(Color.FromArgb(255 - i * 46, 255, 255, 255)))
                    g.FillEllipse(b, px, py, 8, 8);
            }

            using (var f = new Font("Segoe UI", 20f, FontStyle.Regular, GraphicsUnit.Point))
            using (var br = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString("Windows 10 Simulator", f, br, new RectangleF(0, cy + 10, ClientSize.Width, 40), sf);
            }

            using (var f = new Font("Segoe UI", 11f))
            using (var br = new SolidBrush(Color.FromArgb(120, 120, 120)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString("Insert — выход на рабочий стол", f, br,
                    new RectangleF(0, ClientSize.Height - 48, ClientSize.Width, 24), sf);
                g.DrawString(DateTime.Now.ToString("HH:mm:ss"), f, br,
                    new RectangleF(ClientSize.Width - 160, 24, 140, 24), sf);
            }
        }
    }
}
