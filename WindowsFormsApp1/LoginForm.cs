using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    static class Ui
    {
        public static readonly Color Page = Color.FromArgb(233, 238, 246);
        public static readonly Color Blue = Color.FromArgb(30, 111, 232);
        public static readonly Color Red = Color.FromArgb(232, 48, 64);
        public static readonly Color Ink = Color.FromArgb(31, 41, 55);
        public static readonly Color Muted = Color.FromArgb(100, 110, 125);
        public static readonly Color Border = Color.FromArgb(222, 228, 237);
        public const string Font = "Segoe UI";
        public const string Icons = "Segoe MDL2 Assets";

        public static Font F(float size, FontStyle style = FontStyle.Regular) => Gfx.Typeface(Font, size, style);
    }

    /// <summary>Rounded text input with a placeholder; border turns blue on focus.</summary>
    class InputBox : Control
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        const int EM_SETCUEBANNER = 0x1501;

        public readonly TextBox Box = new TextBox { BorderStyle = BorderStyle.None, Font = Ui.F(11f), ForeColor = Ui.Ink };
        readonly string placeholder, glyph;

        public InputBox(string placeholder, string glyph = null)
        {
            this.placeholder = placeholder;
            this.glyph = glyph;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
            Height = 46;
            Controls.Add(Box);
            Box.GotFocus += (s, e) => Invalidate();
            Box.LostFocus += (s, e) => Invalidate();
            Box.HandleCreated += (s, e) => SetCue();
        }

        public override string Text { get => Box.Text; set => Box.Text = value; }

        void SetCue()
        {
            try { SendMessage(Box.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder); }
            catch (Exception) { } // ponytail: non-Windows runtimes have no cue banner
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int left = glyph == null ? 14 : 42;
            Box.SetBounds(left, (Height - Box.Height) / 2, Width - left - 14, Box.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(r, 10))
            using (var pen = new Pen(Box.Focused ? Ui.Blue : Ui.Border, Box.Focused ? 1.6f : 1f))
            {
                g.FillPath(Brushes.White, path);
                g.DrawPath(pen, path);
            }
            if (glyph != null)
                TextRenderer.DrawText(g, glyph, Gfx.Typeface(Ui.Icons, 11f), new Rectangle(0, 0, 42, Height), Ui.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnClick(EventArgs e) { Box.Focus(); base.OnClick(e); }
    }

    /// <summary>Solid rounded button, blue by default.</summary>
    class FlatButton : Button
    {
        public Color Fill = Ui.Blue;
        public string Glyph;
        bool hover;

        public FlatButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Ui.F(11f, FontStyle.Bold);
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            Height = 46;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Gfx.Round(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (var b = new SolidBrush(hover ? ControlPaint.Dark(Fill, 0.1f) : Fill))
                g.FillPath(b, path);

            string text = Glyph == null ? Text : Glyph + "   " + Text;
            var size = TextRenderer.MeasureText(text, Font);
            int x = (Width - size.Width) / 2;
            if (Glyph != null)
            {
                TextRenderer.DrawText(g, Glyph, Gfx.Typeface(Ui.Icons, 10f), new Point(x, (Height - 14) / 2), ForeColor, TextFormatFlags.NoPadding);
                x += 26;
                text = Text;
            }
            TextRenderer.DrawText(g, text, Font, new Point(x, (Height - size.Height) / 2), ForeColor);
        }
    }

    public class LoginForm : Form
    {
        public string UserName => user.Text.Trim();

        readonly InputBox user = new InputBox("Username", "");
        readonly InputBox pass = new InputBox("Password", "");
        readonly Label error = new Label { ForeColor = Ui.Red, Font = Ui.F(9.5f), AutoSize = false, Height = 22 };

        public LoginForm()
        {
            Text = "Tie the knot — Sign in";
            ClientSize = new Size(940, 580);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Page;
            DoubleBuffered = true;

            var brand = new BrandPanel { Dock = DockStyle.Left, Width = 400 };

            var card = new RoundedPanel { BackColor = Color.White, Size = new Size(420, 470), Location = new Point(460, 55) };
            int x = 40, w = card.Width - 80;

            card.Controls.Add(new Label { Text = "Welcome back", Font = Ui.F(20f, FontStyle.Bold), ForeColor = Ui.Ink, AutoSize = true, Location = new Point(x - 3, 38) });
            card.Controls.Add(new Label { Text = "Sign in to keep recording your guests and gifts.", Font = Ui.F(10f), ForeColor = Ui.Muted, AutoSize = true, Location = new Point(x, 82) });

            card.Controls.Add(new Label { Text = "Username", Font = Ui.F(9.5f, FontStyle.Bold), ForeColor = Ui.Ink, AutoSize = true, Location = new Point(x, 128) });
            user.SetBounds(x, 152, w, 46);
            card.Controls.Add(user);

            card.Controls.Add(new Label { Text = "Password", Font = Ui.F(9.5f, FontStyle.Bold), ForeColor = Ui.Ink, AutoSize = true, Location = new Point(x, 212) });
            pass.SetBounds(x, 236, w, 46);
            pass.Box.UseSystemPasswordChar = true;
            card.Controls.Add(pass);

            var show = new CheckBox { Text = "Show password", Font = Ui.F(9.5f), ForeColor = Ui.Muted, AutoSize = true, Location = new Point(x, 294) };
            show.CheckedChanged += (s, e) => pass.Box.UseSystemPasswordChar = !show.Checked;
            card.Controls.Add(show);

            var forgot = new LinkLabel { Text = "Forgot password?", Font = Ui.F(9.5f), LinkColor = Ui.Blue, LinkBehavior = LinkBehavior.HoverUnderline, AutoSize = true };
            forgot.LinkClicked += (s, e) => MessageBox.Show(this, "Please contact your administrator to reset your password.", Text);
            card.Controls.Add(forgot);
            forgot.Location = new Point(x + w - forgot.PreferredWidth, 296);

            error.SetBounds(x, 326, w, 22);
            card.Controls.Add(error);

            var signIn = new FlatButton { Text = "Sign in", Location = new Point(x, 352), Width = w };
            signIn.Click += (s, e) => TrySignIn();
            card.Controls.Add(signIn);

            card.Controls.Add(new Label { Text = "Demo account: admin / 1234", Font = Ui.F(9f), ForeColor = Ui.Muted, AutoSize = true, Location = new Point(x, 414) });

            Controls.Add(card);
            Controls.Add(brand);
            AcceptButton = signIn;
            Shown += (s, e) => user.Box.Focus();
        }

        void TrySignIn()
        {
            if (UserName.Length == 0) { Fail("Please enter your username.", user); return; }
            if (pass.Text.Length == 0) { Fail("Please enter your password.", pass); return; }

            // ponytail: hard-coded demo credentials; swap for a real user store (DB + hashed passwords) before shipping
            if (UserName != "admin" || pass.Text != "1234") { Fail("Incorrect username or password.", pass); return; }

            DialogResult = DialogResult.OK;
        }

        void Fail(string message, InputBox focus)
        {
            error.Text = message;
            focus.Box.Focus();
            focus.Box.SelectAll();
        }
    }

    class BrandPanel : Control
    {
        public BrandPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = ClientRectangle;
            using (var b = new LinearGradientBrush(r, Color.FromArgb(244, 80, 92), Color.FromArgb(196, 28, 52), LinearGradientMode.ForwardDiagonal))
                g.FillRectangle(b, r);

            // Soft decorative circles
            using (var b = new SolidBrush(Color.FromArgb(28, Color.White)))
            {
                g.FillEllipse(b, Width - 180, -80, 300, 300);
                g.FillEllipse(b, -120, Height - 200, 320, 320);
            }

            var logo = new Rectangle(48, 64, 64, 64);
            using (var path = Gfx.Round(logo, 16))
                g.FillPath(Brushes.White, path);
            TextRenderer.DrawText(g, "\U0001F381", Gfx.Typeface("Segoe UI Emoji", 24f), logo, Ui.Red,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            Gfx.Text(g, "Tie the knot", Ui.F(28f, FontStyle.Bold), Color.White, 44, 160);
            Gfx.Text(g, "Record every gift.\nRemember every guest.", Ui.F(14f), Color.FromArgb(235, Color.White), 48, 222);

            int y = Height - 150;
            foreach (var line in new[] { "Track guests and bond money", "Riel and Dollar totals at a glance", "Daily records and reports" })
            {
                TextRenderer.DrawText(g, "", Gfx.Typeface(Ui.Icons, 10f), new Point(50, y + 3), Color.White, TextFormatFlags.NoPadding);
                Gfx.Text(g, line, Ui.F(10.5f), Color.White, 76, y);
                y += 32;
            }
        }
    }
}
