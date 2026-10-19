using System;
using System.Drawing;
using System.Globalization;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        // Palette
        internal static readonly Color Cream = Color.FromArgb(250, 246, 240);
        internal static readonly Color Maroon = Color.FromArgb(122, 30, 44);
        internal static readonly Color Gold = Color.FromArgb(184, 137, 62);
        internal static readonly Color Ink = Color.FromArgb(43, 38, 36);
        internal static readonly Color Muted = Color.FromArgb(110, 102, 96);
        internal static readonly Color Line = Color.FromArgb(238, 231, 222);

        internal const string Serif = "Georgia";
        internal const string Khmer = "Khmer UI";
        internal const string Icons = "Segoe MDL2 Assets";

        readonly Panel content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Cream };
        readonly HeroPanel hero = new HeroPanel();
        readonly StatCard[] stats =
        {
            new StatCard("Total Gifts", "$12,480", "(50,720,000 KHR)", "\U0001F381", "Segoe UI Emoji", "↗", Color.FromArgb(252, 238, 238), Maroon),
            new StatCard("Guests", "186", null, "", Icons, "", Color.FromArgb(253, 244, 228), Color.FromArgb(150, 104, 30)),
            new StatCard("Upcoming Events", "2", null, "", Icons, "", Color.FromArgb(236, 245, 239), Color.FromArgb(46, 110, 80)),
            new StatCard("Recent Events", "3", null, "", Icons, "", Color.FromArgb(252, 238, 240), Maroon),
        };
        readonly RoundedPanel eventsCard = new RoundedPanel { BackColor = Color.White };
        readonly DataGridView grid = new DataGridView();
        readonly FooterPanel footer = new FooterPanel();
        readonly Panel calendarPage = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Cream, Visible = false };
        readonly CircleButton fab = new CircleButton { Size = new Size(64, 64) };
        Label eventsTitle, calendarTitle;
        LinkLabel viewAll;
        Action refreshCalendarText;

        public Form1()
        {
            InitializeComponent();
            Text = "Kat Chamnong Dai";
            BackColor = Cream;
            ClientSize = new Size(1440, 900);
            MinimumSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;

            content.Controls.Add(hero);
            foreach (var s in stats) content.Controls.Add(s);
            BuildEventsCard();
            content.Controls.Add(eventsCard);
            content.Controls.Add(footer);
            content.Resize += (s, e) => LayoutContent();

            fab.Click += (s, e) => SignInAndOpenApp();
            hero.CreateClicked += (s, e) => SignInAndOpenApp();

            BuildCalendarPage();
            var nav = new NavBar();
            nav.Navigate += (s, item) =>
            {
                bool showCalendar = item == "Events";
                calendarPage.Visible = showCalendar;
                content.Visible = !showCalendar;
            };

            Controls.Add(fab);
            Controls.Add(content);
            Controls.Add(calendarPage);
            Controls.Add(nav);
            fab.BringToFront();
            Resize += (s, e) => PlaceFab();
            Lang.Changed += ApplyLanguage;

            LayoutContent();
            PlaceFab();
        }

        /// <summary>Re-texts the real controls; painted controls pick the language up on repaint.</summary>
        void ApplyLanguage()
        {
            eventsTitle.Font = Lang.F(15f);
            eventsTitle.Text = Lang.T("Recent Events");
            viewAll.Font = Lang.F(10f);
            viewAll.Text = Lang.T("View all events  →");
            grid.ColumnHeadersDefaultCellStyle.Font = Lang.F(9.5f);
            foreach (DataGridViewColumn c in grid.Columns) c.HeaderText = Lang.T((string)c.Tag);
            calendarTitle.Font = Lang.F(24f, FontStyle.Bold);
            calendarTitle.Text = Lang.T("Events Calendar");
            refreshCalendarText();
            hero.ApplyLanguage();
            Invalidate(true);
        }

        void BuildCalendarPage()
        {
            // Events come from the Recent Events table, so both views always agree.
            var events = grid.Rows.Cast<DataGridViewRow>().Select(r => new
            {
                Date = DateTime.Parse((string)r.Cells["Date"].Value, CultureInfo.InvariantCulture),
                Name = (string)r.Cells["Name"].Value,
                Venue = (string)r.Cells["Venue"].Value,
                Status = (string)r.Cells["Status"].Value,
            }).ToList();

            var cal = new MonthCalendar
            {
                CalendarDimensions = new Size(3, 1),
                MaxSelectionCount = 1,
                Font = new Font(Serif, 11f),
                BoldedDates = events.Select(x => x.Date).ToArray(),
                TitleBackColor = Maroon,
                TitleForeColor = Color.White,
                TrailingForeColor = Color.FromArgb(200, 190, 180),
            };
            var details = new Label { Font = new Font(Serif, 12f), ForeColor = Ink, AutoSize = true, Margin = new Padding(0, 20, 0, 0) };
            var shownDay = DateTime.Today;

            void ShowDay(DateTime day)
            {
                shownDay = day;
                var hits = events.Where(x => x.Date == day.Date).ToList();
                details.Font = Lang.F(12f);
                details.Text = hits.Count == 0
                    ? $"{day:MMM d, yyyy}  —  {Lang.T("No events on this day.")}"
                    : string.Join("\n", hits.Select(x => $"{x.Date:MMM d, yyyy}  —  {x.Name}  ·  {x.Venue}  ({Lang.T(x.Status)})"));
            }
            cal.DateChanged += (s, e) => ShowDay(e.Start);
            refreshCalendarText = () => ShowDay(shownDay);

            var first = events.Min(x => x.Date);
            cal.SetDate(first);
            ShowDay(first);

            var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Color.White };
            stack.Controls.Add(cal);
            stack.Controls.Add(details);

            var card = new RoundedPanel { BackColor = Color.White, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(24), Location = new Point(68, 100) };
            card.Controls.Add(stack);

            calendarPage.Controls.Add(calendarTitle = new Label
            {
                Text = "Events Calendar",
                Font = new Font(Serif, 24f, FontStyle.Bold),
                ForeColor = Maroon,
                AutoSize = true,
                Location = new Point(64, 36),
            });
            calendarPage.Controls.Add(card);
        }

        void SignInAndOpenApp()
        {
            string user;
            using (var login = new LoginForm())
            {
                if (login.ShowDialog(this) != DialogResult.OK) return;
                user = login.UserName;
            }

            Hide();
            bool loggedOut;
            using (var app = new MainForm(user))
            {
                app.ShowDialog();
                loggedOut = app.LoggedOut;
            }
            if (loggedOut) Show(); // back to the landing page
            else Close();          // closed with X: quit the app
        }

        void PlaceFab() => fab.Location = new Point(ClientSize.Width - fab.Width - 40, ClientSize.Height - fab.Height - 60);

        void LayoutContent()
        {
            const int margin = 68, gap = 20;
            int w = Math.Max(content.ClientSize.Width, MinimumSize.Width - 20);
            int inner = w - margin * 2;
            int y = -content.VerticalScroll.Value;

            hero.SetBounds(0, y, w, 400);
            y += 400;

            int cardW = (inner - gap * 3) / 4;
            for (int i = 0; i < stats.Length; i++)
                stats[i].SetBounds(margin + i * (cardW + gap), y, cardW, 110);
            y += 110 + gap;

            int gridH = grid.ColumnHeadersHeight + grid.Rows.Count * grid.RowTemplate.Height + 2;
            eventsCard.SetBounds(margin, y, inner, 58 + gridH);
            grid.SetBounds(0, 58, inner, gridH);
            y += eventsCard.Height;

            footer.SetBounds(0, y, w, 80);
            y += 80;

            content.AutoScrollMinSize = new Size(0, y + content.VerticalScroll.Value);
        }

        void BuildEventsCard()
        {
            var title = eventsTitle = new Label
            {
                Text = "Recent Events",
                Font = new Font(Serif, 15f),
                ForeColor = Ink,
                AutoSize = true,
                Location = new Point(70, 16),
                BackColor = Color.Transparent,
            };
            var icon = new Label
            {
                Text = "",
                Font = new Font(Icons, 16f),
                ForeColor = Maroon,
                AutoSize = true,
                Location = new Point(24, 18),
                BackColor = Color.Transparent,
            };
            viewAll = new LinkLabel
            {
                Text = "View all events  →",
                Font = new Font(Serif, 10f),
                LinkColor = Maroon,
                ActiveLinkColor = Gold,
                LinkBehavior = LinkBehavior.HoverUnderline,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent,
            };
            eventsCard.Controls.Add(icon);
            eventsCard.Controls.Add(title);
            eventsCard.Controls.Add(viewAll);
            void PlaceViewAll() => viewAll.Location = new Point(eventsCard.Width - viewAll.Width - 28, 22);
            eventsCard.Resize += (s, e) => PlaceViewAll();
            viewAll.SizeChanged += (s, e) => PlaceViewAll();

            StyleGrid();
            eventsCard.Controls.Add(grid);
        }

        void StyleGrid()
        {
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Color.White;
            grid.GridColor = Line;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.ScrollBars = ScrollBars.None;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 40;
            grid.RowTemplate.Height = 39;

            var header = grid.ColumnHeadersDefaultCellStyle;
            header.BackColor = Color.FromArgb(250, 247, 243);
            header.SelectionBackColor = header.BackColor;
            header.ForeColor = Ink;
            header.Font = new Font(Serif, 9.5f);
            header.Padding = new Padding(28, 0, 0, 0);

            var cell = grid.DefaultCellStyle;
            cell.Font = new Font(Serif, 10f);
            cell.ForeColor = Ink;
            cell.SelectionBackColor = Color.FromArgb(252, 245, 240);
            cell.SelectionForeColor = Ink;
            cell.Padding = new Padding(28, 0, 0, 0);

            void Col(string name, string text, float weight)
            {
                int i = grid.Columns.Add(name, text);
                grid.Columns[i].Tag = text; // English key for Lang.T
                grid.Columns[i].FillWeight = weight;
                grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            Col("No", "#", 30);
            Col("Name", "Event Name", 160);
            Col("Date", "Date", 85);
            Col("Venue", "Venue", 115);
            Col("Guests", "Guests", 65);
            Col("Usd", "Total Gifts (USD)", 95);
            Col("Khr", "Total Gifts (KHR)", 95);
            Col("Status", "Status", 100);
            Col("Actions", "Actions", 50);
            grid.Columns["Actions"].DefaultCellStyle.Font = new Font("Segoe UI", 13f, FontStyle.Bold);

            grid.Rows.Add(1, "Sophea & Dara - Wedding Ceremony", "Apr 20, 2025", "Sofitel Phnom Penh", 186, "$12,480", "50,720,000", "Completed", "⋮");
            grid.Rows.Add(2, "Engagement Ceremony", "Mar 15, 2025", "Himawari Hotel", 124, "$8,250", "33,550,000", "Completed", "⋮");
            grid.Rows.Add(3, "Traditional Blessing (Water Ceremony)", "Mar 14, 2025", "Family Home", 98, "$4,360", "17,860,000", "Completed", "⋮");
            grid.Rows.Add(4, "Reception Dinner", "May 10, 2025", "Rosewood Phnom Penh", 0, "$0", "0", "Upcoming", "⋮");

            grid.CellPainting += PaintStatusPill;
            grid.SelectionChanged += (s, e) => grid.ClearSelection(); // ponytail: display-only table, no selection
        }

        static void PaintStatusPill(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var g = (DataGridView)sender;
            if (e.RowIndex < 0 || g.Columns[e.ColumnIndex].Name != "Status") return;

            e.PaintBackground(e.ClipBounds, false);
            string status = Convert.ToString(e.Value);
            bool done = status == "Completed";
            Color bg = done ? Color.FromArgb(226, 241, 230) : Color.FromArgb(252, 238, 196);
            Color fg = done ? Color.FromArgb(40, 110, 70) : Color.FromArgb(138, 100, 18);

            var gr = e.Graphics;
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 8, 100, e.CellBounds.Height - 16);
            using (var path = Gfx.Round(r, r.Height / 2))
            using (var b = new SolidBrush(bg))
                gr.FillPath(b, path);
            using (var dot = new SolidBrush(fg))
                gr.FillEllipse(dot, r.X + 10, r.Y + r.Height / 2 - 4, 8, 8);
            TextRenderer.DrawText(gr, Lang.T(status), Lang.F(9f), new Rectangle(r.X + 22, r.Y, r.Width - 24, r.Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            e.Handled = true;
        }

        internal static Image LoadImage(string file)
        {
            string path = Path.Combine(Application.StartupPath, "Images", file);
            return File.Exists(path) ? Image.FromFile(path) : null;
        }
    }

    /// <summary>Dashboard language (English / Khmer). Painted text calls T() and F() at paint time.</summary>
    static class Lang
    {
        public static bool Khmer { get; private set; }
        public static event Action Changed;

        public static void Set(bool khmer)
        {
            if (khmer == Khmer) return;
            Khmer = khmer;
            Changed?.Invoke();
        }

        public static string T(string en) => Khmer && Kh.TryGetValue(en, out var kh) ? kh : en;

        // Georgia has no Khmer glyphs; Khmer UI runs taller, so it is drawn slightly smaller.
        public static Font F(float size, FontStyle style = FontStyle.Regular) =>
            Khmer ? new Font(Form1.Khmer, size * 0.92f, style) : new Font(Form1.Serif, size, style);

        static readonly System.Collections.Generic.Dictionary<string, string> Kh = new System.Collections.Generic.Dictionary<string, string>
        {
            ["Dashboard"] = "ផ្ទាំងដើម",
            ["Events"] = "ព្រឹត្តិការណ៍",
            ["Guests"] = "ភ្ញៀវ",
            ["Reports"] = "របាយការណ៍",
            ["A  MODERN  WAY  TO  PRESERVE  TRADITION"] = "វិធីទំនើបក្នុងការថែរក្សាប្រពៃណី",
            ["Record every gift. Remember every connection."] = "កត់ត្រារាល់អំណោយ។ ចងចាំរាល់ទំនាក់ទំនង។",
            ["Keep track of your wedding gifts, guests, and blessings\nin one beautiful place — for today and for generations tomorrow."] =
                "តាមដានអំណោយ ភ្ញៀវ និងពរជ័យក្នុងពិធីមង្គលការរបស់អ្នក\nនៅកន្លែងតែមួយ — សម្រាប់ថ្ងៃនេះ និងសម្រាប់ជំនាន់ក្រោយ។",
            ["Create New Event"] = "បង្កើតព្រឹត្តិការណ៍ថ្មី",
            ["Learn more  ›"] = "ស្វែងយល់បន្ថែម  ›",
            ["Total Gifts"] = "អំណោយសរុប",
            ["(50,720,000 KHR)"] = "(50,720,000 រៀល)",
            ["Upcoming Events"] = "ព្រឹត្តិការណ៍ខាងមុខ",
            ["Recent Events"] = "ព្រឹត្តិការណ៍ថ្មីៗ",
            ["View all events  →"] = "មើលព្រឹត្តិការណ៍ទាំងអស់  →",
            ["Event Name"] = "ឈ្មោះព្រឹត្តិការណ៍",
            ["Date"] = "កាលបរិច្ឆេទ",
            ["Venue"] = "ទីកន្លែង",
            ["Total Gifts (USD)"] = "អំណោយសរុប (ដុល្លារ)",
            ["Total Gifts (KHR)"] = "អំណោយសរុប (រៀល)",
            ["Status"] = "ស្ថានភាព",
            ["Actions"] = "សកម្មភាព",
            ["Completed"] = "បានបញ្ចប់",
            ["Upcoming"] = "នឹងមកដល់",
            ["Events Calendar"] = "ប្រតិទិនព្រឹត្តិការណ៍",
            ["No events on this day."] = "មិនមានព្រឹត្តិការណ៍នៅថ្ងៃនេះទេ។",
            ["More than gifts, stronger relationships"] = "លើសពីអំណោយ គឺចំណងទំនាក់ទំនងកាន់តែរឹងមាំ",
            ["PEOPLE  •  BLESSINGS  •  A BRIGHTER TOMORROW"] = "មនុស្ស  •  ពរជ័យ  •  អនាគតដ៏ភ្លឺស្វាង",
        };
    }

    static class Gfx
    {
        public static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = Math.Max(1, radius * 2);
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Text(Graphics g, string s, Font f, Color c, int x, int y) =>
            TextRenderer.DrawText(g, s, f, new Point(x, y), c, TextFormatFlags.NoPadding);
    }

    class RoundedPanel : Panel
    {
        public int Radius = 14;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Form1.Cream);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(r, Radius))
            using (var b = new SolidBrush(BackColor))
            using (var pen = new Pen(Form1.Line))
            {
                e.Graphics.FillPath(b, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (var path = Gfx.Round(new Rectangle(0, 0, Width, Height), Radius))
                Region = new Region(path); // clips child grid to the rounded corners
        }
    }

    class NavBar : Control
    {
        static readonly string[] Items = { "Dashboard", "Events", "Guests", "Reports" };
        static readonly string[] Glyphs = { "", "", "", "" };
        readonly Rectangle[] hits = new Rectangle[Items.Length];
        public event EventHandler<string> Navigate;
        int active, hover = -1;
        Rectangle lang;

        public NavBar()
        {
            Dock = DockStyle.Top;
            Height = 68;
            BackColor = Color.FromArgb(253, 251, 247);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            var music = new MusicPlayer("song.mp3", "SONG") { BackColor = BackColor };
            Controls.Add(music);
            Resize += (s, e) => music.Location = new Point(Width - 330, (Height - music.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Brand
            Gfx.Text(g, "❦", new Font(Form1.Serif, 26f), Form1.Gold, 72, 12);
            Gfx.Text(g, "Kat Chamnong Dai", new Font(Form1.Serif, 17f, FontStyle.Bold), Form1.Maroon, 120, 8);
            Gfx.Text(g, "កត់ចំណងដៃ", new Font(Form1.Khmer, 11f), Form1.Maroon, 122, 38);

            // Menu
            int x = 470;
            var font = Lang.F(11f);
            var iconFont = new Font(Form1.Icons, 12f);
            for (int i = 0; i < Items.Length; i++)
            {
                string label = Lang.T(Items[i]);
                int textW = TextRenderer.MeasureText(label, font).Width;
                hits[i] = new Rectangle(x - 10, 0, textW + 50, Height);
                Color c = i == active ? Form1.Maroon : (i == hover ? Form1.Gold : Form1.Ink);
                Gfx.Text(g, Glyphs[i], iconFont, c, x, 26);
                Gfx.Text(g, label, font, c, x + 28, Lang.Khmer ? 22 : 24);
                if (i == active)
                    using (var b = new SolidBrush(Form1.Maroon))
                        g.FillRectangle(b, hits[i].X, Height - 3, hits[i].Width, 3);
                x += hits[i].Width + 24;
            }

            // Language toggle: the active half sits on a white chip.
            lang = new Rectangle(Width - 455, 16, 84, 36);
            using (var path = Gfx.Round(lang, 10))
            using (var b = new SolidBrush(Color.FromArgb(240, 234, 226)))
                g.FillPath(b, path);
            var kh = new Rectangle(lang.X + 3, lang.Y + 3, lang.Width / 2 - 3, lang.Height - 6);
            var en = new Rectangle(lang.X + lang.Width / 2, lang.Y + 3, lang.Width / 2 - 3, lang.Height - 6);
            using (var path = Gfx.Round(Lang.Khmer ? kh : en, 8))
                g.FillPath(Brushes.White, path);
            var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, "KH", new Font(Form1.Serif, 10f, Lang.Khmer ? FontStyle.Bold : FontStyle.Regular), kh, Lang.Khmer ? Form1.Maroon : Form1.Muted, center);
            TextRenderer.DrawText(g, "EN", new Font(Form1.Serif, 10f, Lang.Khmer ? FontStyle.Regular : FontStyle.Bold), en, Lang.Khmer ? Form1.Muted : Form1.Maroon, center);

            using (var pen = new Pen(Form1.Line))
            {
                g.DrawLine(pen, Width - 345, 14, Width - 345, Height - 14);
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }

        int HitTest(Point p) => Array.FindIndex(hits, r => r.Contains(p));

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            Cursor = h >= 0 || lang.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (lang.Contains(e.Location))
            {
                Lang.Set(e.X < lang.X + lang.Width / 2); // left half = KH, right half = EN
                return;
            }
            int h = HitTest(e.Location);
            if (h >= 0) { active = h; Invalidate(); Navigate?.Invoke(this, Items[h]); }
        }
    }

    class HeroPanel : Control
    {
        public event EventHandler CreateClicked;
        readonly Image photo = Form1.LoadImage("hero.png");
        readonly PillButton create;
        readonly LinkLabel learn;

        public HeroPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Form1.Cream;

            create = new PillButton { Text = "Create New Event", Location = new Point(82, 312), Size = new Size(288, 62) };
            create.Click += (s, e) => CreateClicked?.Invoke(this, e);
            learn = new LinkLabel
            {
                Text = "Learn more  ›",
                Font = new Font(Form1.Serif, 11f),
                LinkColor = Form1.Maroon,
                ActiveLinkColor = Form1.Gold,
                LinkBehavior = LinkBehavior.HoverUnderline,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(404, 332),
            };
            Controls.Add(create);
            Controls.Add(learn);
        }

        public void ApplyLanguage()
        {
            create.Font = Lang.F(13f);
            create.Text = Lang.T("Create New Event");
            learn.Font = Lang.F(11f);
            learn.Text = Lang.T("Learn more  ›");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            if (photo != null)
            {
                // Scale photo to panel height, pin to the right, fade its left edge into the cream.
                int h = Height;
                int w = photo.Width * h / photo.Height;
                var dest = new Rectangle(Width - w, 0, w, h);
                g.DrawImage(photo, dest);
                var fade = new Rectangle(dest.X - 1, 0, 260, h);
                using (var b = new LinearGradientBrush(fade, Form1.Cream, Color.FromArgb(0, Form1.Cream), LinearGradientMode.Horizontal))
                    g.FillRectangle(b, fade);
                if (dest.X > 0)
                    using (var b = new SolidBrush(Form1.Cream))
                        g.FillRectangle(b, 0, 0, dest.X, h);
            }

            Gfx.Text(g, Lang.T("A  MODERN  WAY  TO  PRESERVE  TRADITION"), Lang.Khmer ? Lang.F(10f) : new Font("Segoe UI", 9f), Form1.Muted, 82, Lang.Khmer ? 30 : 38);
            Gfx.Text(g, "Kat Chamnong Dai", new Font(Form1.Serif, 40f, FontStyle.Bold), Form1.Maroon, 76, 58);
            Gfx.Text(g, "កត់ចំណងដៃ", new Font(Form1.Khmer, 34f), Form1.Gold, 80, 124);
            Gfx.Text(g, Lang.T("Record every gift. Remember every connection."), Lang.F(16f), Form1.Ink, 82, 202);
            Gfx.Text(g, Lang.T("Keep track of your wedding gifts, guests, and blessings\nin one beautiful place — for today and for generations tomorrow."),
                Lang.F(12f), Form1.Muted, 82, 240);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) photo?.Dispose();
            base.Dispose(disposing);
        }
    }

    class StatCard : Control
    {
        readonly string title, value, sub, icon, iconFont, action;
        readonly Color tint, accent;

        public StatCard(string title, string value, string sub, string icon, string iconFont, string action, Color tint, Color accent)
        {
            this.title = title; this.value = value; this.sub = sub;
            this.icon = icon; this.iconFont = iconFont; this.action = action;
            this.tint = tint; this.accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Form1.Cream);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(r, 12))
            using (var b = new LinearGradientBrush(r, tint, Color.FromArgb(253, 251, 248), LinearGradientMode.Horizontal))
            using (var pen = new Pen(Color.FromArgb(232, 224, 214)))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }

            // Big icon disc
            var disc = new Rectangle(22, Height / 2 - 34, 68, 68);
            using (var b = new SolidBrush(Color.FromArgb(40, accent)))
                g.FillEllipse(b, disc);
            TextRenderer.DrawText(g, icon, new Font(iconFont, 20f), disc, accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int tx = 114;
            int ty = sub == null ? 30 : 20;
            Gfx.Text(g, Lang.T(title), Lang.F(11f), Form1.Ink, tx, ty - (Lang.Khmer ? 4 : 0));
            Gfx.Text(g, value, new Font(Form1.Serif, 21f, FontStyle.Bold), title == "Total Gifts" ? Form1.Maroon : Form1.Ink, tx, ty + 20);
            if (sub != null)
                Gfx.Text(g, Lang.T(sub), Lang.F(10f), Form1.Muted, tx, ty + 60);

            // Small action disc on the right
            var small = new Rectangle(Width - 66, Height / 2 - 22, 44, 44);
            using (var b = new SolidBrush(Color.FromArgb(28, accent)))
                g.FillEllipse(b, small);
            TextRenderer.DrawText(g, action, new Font(action == "↗" ? "Segoe UI" : Form1.Icons, action == "↗" ? 14f : 11f), small, accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    class PillButton : Button
    {
        bool hover;

        public PillButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font(Form1.Serif, 13f);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Form1.Cream);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(r, r.Height / 2))
            using (var b = new SolidBrush(hover ? Color.FromArgb(150, 40, 56) : Form1.Maroon))
                g.FillPath(b, path);

            var plus = new Rectangle(30, Height / 2 - 15, 30, 30);
            g.FillEllipse(Brushes.White, plus);
            TextRenderer.DrawText(g, "+", new Font("Segoe UI", 15f, FontStyle.Bold), plus, Form1.Maroon,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Text, Font, new Point(76, Height / 2 - 12), Color.White, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "›", new Font("Segoe UI", 16f), new Point(Width - 44, Height / 2 - 17), Color.White, TextFormatFlags.NoPadding);
        }
    }

    class CircleButton : Button
    {
        public CircleButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Form1.Maroon;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 22f);
            Text = "+";
            Cursor = Cursors.Hand;
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(0, 0, Width, Height);
                Region = new Region(p);
            }
        }
    }

    class FooterPanel : Control
    {
        public FooterPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Form1.Cream;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Gfx.Text(g, "❀", new Font(Form1.Serif, 22f), Form1.Gold, 68, 22);
            Gfx.Text(g, Lang.T("More than gifts, stronger relationships"), Lang.F(11f, Lang.Khmer ? FontStyle.Regular : FontStyle.Italic), Form1.Muted, 128, 32);

            string tag = Lang.T("PEOPLE  •  BLESSINGS  •  A BRIGHTER TOMORROW");
            var tagFont = Lang.Khmer ? Lang.F(9.5f) : new Font("Segoe UI", 8.5f);
            int tagW = TextRenderer.MeasureText(tag, tagFont).Width;
            Gfx.Text(g, tag, tagFont, Form1.Muted, Width - 68 - tagW, 34);
            using (var pen = new Pen(Form1.Line))
                g.DrawLine(pen, Width - 96 - tagW, 22, Width - 96 - tagW, 58);
            var kh = new Font(Form1.Khmer, 13f);
            string khText = "កត់ចំណងដៃ";
            int khW = TextRenderer.MeasureText(khText, kh).Width;
            Gfx.Text(g, khText, kh, Form1.Maroon, Width - 120 - tagW - khW, 26);
        }
    }
}
