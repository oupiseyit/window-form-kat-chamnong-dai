using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Falling snow drawn over an owner form's client area. WinForms can't draw on top of child controls,
    /// so this is a borderless, click-through window that follows the owner. Everything painted in the
    /// key colour is see-through (WinForms TransparencyKey), so only the flakes show.
    /// </summary>
    // ponytail: colour-key transparency = hard-edged flakes; per-pixel alpha (UpdateLayeredWindow) looks softer
    // but silently showed nothing on the target machine, so the reliable option wins.
    class SnowOverlay : Form
    {
        const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        static readonly Color Key = Color.FromArgb(255, 0, 255); // never used by a flake

        struct Flake { public float X, Y, Radius, Speed, Phase; }

        readonly Form owner;
        readonly Timer timer = new Timer { Interval = 33 }; // ~30 fps is plenty for drifting snow
        readonly Random rng = new Random();
        readonly SolidBrush snow = new SolidBrush(Color.White);
        readonly SolidBrush rim = new SolidBrush(Color.FromArgb(176, 192, 216)); // keeps flakes visible over white cards
        Flake[] flakes = new Flake[0];
        Size seededFor;
        bool snowing = true;

        public SnowOverlay(Form owner)
        {
            this.owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Key;
            TransparencyKey = Key;
            DoubleBuffered = true;

            timer.Tick += (s, e) => Step();
            owner.Move += (s, e) => Sync();
            owner.Resize += (s, e) => Sync();
            owner.VisibleChanged += (s, e) => Sync();
            owner.FormClosed += (s, e) => Close();
            Sync();
        }

        /// <summary>Turn the snow on or off; off hides the window and stops the timer.</summary>
        public bool Snowing
        {
            get => snowing;
            set { snowing = value; Sync(); }
        }

        /// <summary>Makes a ❄ button turn this snow on/off and keeps the button's look in sync.</summary>
        public void AttachToggle(FlatButton button, Color accent, Color onFill)
        {
            var tip = new ToolTip();
            void ShowState()
            {
                button.Fill = snowing ? onFill : Color.White;
                button.ForeColor = snowing ? accent : Color.FromArgb(160, 170, 185);
                tip.SetToolTip(button, snowing ? "Snow is on — click to turn it off" : "Snow is off — click to turn it on");
                button.Invalidate();
            }
            button.Click += (s, e) => { Snowing = !Snowing; ShowState(); };
            Disposed += (s, e) => tip.Dispose();
            ShowState();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE; // click-through, no taskbar, no focus
                return cp;
            }
        }

        /// <summary>Cover the owner's client area; pause while snow is off or the owner is hidden/minimized.</summary>
        void Sync()
        {
            if (IsDisposed) return;
            if (!snowing || !owner.Visible || owner.WindowState == FormWindowState.Minimized)
            {
                timer.Stop();
                Hide();
                return;
            }

            var area = owner.RectangleToScreen(owner.ClientRectangle);
            if (area.Width <= 0 || area.Height <= 0) return;
            Bounds = area;
            if (seededFor != area.Size) Seed(area.Size);
            if (!Visible) Show(owner);
            timer.Start();
        }

        void Seed(Size size)
        {
            seededFor = size;
            int count = Math.Max(25, size.Width * size.Height / 16000); // density, not a fixed count (~75 on a 1440px window)
            flakes = new Flake[count];
            for (int i = 0; i < count; i++)
            {
                flakes[i] = NewFlake(size.Width);
                flakes[i].Y = (float)rng.NextDouble() * size.Height; // start spread out, not all at the top
            }
        }

        Flake NewFlake(int width)
        {
            float r = 1.5f + (float)rng.NextDouble() * 2.5f;
            return new Flake
            {
                X = (float)rng.NextDouble() * width,
                Y = -r * 2,
                Radius = r,
                Speed = 0.6f + r * 0.45f, // bigger flakes are "closer", so they fall faster
                Phase = (float)(rng.NextDouble() * Math.PI * 2),
            };
        }

        void Step()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            for (int i = 0; i < flakes.Length; i++)
            {
                ref Flake f = ref flakes[i];
                f.Phase += 0.03f;
                f.Y += f.Speed;
                f.X += (float)Math.Sin(f.Phase) * 0.6f + 0.15f; // sway plus a light breeze
                if (f.Y - f.Radius > h) f = NewFlake(w);
                if (f.X > w + f.Radius) f.X -= w;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // No anti-aliasing on purpose: blended edge pixels would mix with the key colour and show pink fringes.
            var g = e.Graphics;
            foreach (var f in flakes)
            {
                float d = f.Radius * 2;
                g.FillEllipse(rim, f.X - f.Radius - 1, f.Y - f.Radius - 1, d + 2, d + 2);
                g.FillEllipse(snow, f.X - f.Radius, f.Y - f.Radius, d, d);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                snow.Dispose();
                rim.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
