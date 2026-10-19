using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    static class Anim
    {
        /// <summary>Runs step(0..1, eased) over ms milliseconds on the UI thread, then done().</summary>
        // ponytail: Timer tween (~60fps), fine for a handful of UI animations at once
        public static Timer Run(int ms, Action<double> step, Action done = null)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var timer = new Timer { Interval = 15 };
            timer.Tick += (s, e) =>
            {
                double t = Math.Min(1, clock.ElapsedMilliseconds / (double)Math.Max(1, ms));
                step(1 - Math.Pow(1 - t, 3)); // ease-out cubic
                if (t < 1) return;
                timer.Dispose();
                done?.Invoke();
            };
            timer.Start();
            return timer;
        }
    }

    class Guest
    {
        public string Name, Place, Misc;
        public decimal Amount;
        public bool IsRiel;
        public DateTime Recorded = DateTime.Now;

        public string Money => IsRiel ? Amount.ToString("N0") + " R" : "$" + Amount.ToString("N2");
    }

    public class MainForm : Form
    {
        public bool LoggedOut { get; private set; }

        // ponytail: in-memory list; persist to a DB/file when guests must survive a restart
        readonly List<Guest> guests = new List<Guest>();

        readonly Sidebar sidebar = new Sidebar();
        readonly Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Page };
        readonly Panel settingsPage = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Page, Visible = false };
        readonly StatTile totalGuests = new StatTile("Total guests", "people", "", Ui.Icons, Color.FromArgb(139, 92, 246));
        readonly StatTile totalRiel = new StatTile("Total riel", "R", "R", Ui.Font, Color.FromArgb(22, 160, 106));
        readonly StatTile totalDollar = new StatTile("Total dollars", "$", "$", Ui.Font, Ui.Blue);
        readonly StatTile today = new StatTile("Record today", "people", "", Ui.Icons, Color.FromArgb(245, 158, 11));
        readonly InputBox search = new InputBox("Search by name, location, or more...", "");
        readonly CurrencyToggle currency = new CurrencyToggle();
        readonly FlatButton addGuest = new FlatButton { Text = "Add guest", Glyph = "", Size = new Size(140, 46) };
        readonly RoundedPanel tableCard = new RoundedPanel { BackColor = Color.White };
        readonly DataGridView grid = new DataGridView();
        readonly MusicPlayer music = new MusicPlayer("song.mp3", "SONG") { Accent = Ui.Blue, Progress = Color.FromArgb(120, 170, 245), FontName = Ui.Font };

        public MainForm(string userName)
        {
            Text = "Tie the knot — " + userName;
            ClientSize = new Size(1440, 860);
            MinimumSize = new Size(1100, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Page;
            DoubleBuffered = true;
            Opacity = 0;
            Shown += (s, e) =>
            {
                Anim.Run(250, p => Opacity = p);
                var tiles = new[] { totalGuests, totalRiel, totalDollar, today };
                for (int i = 0; i < tiles.Length; i++) tiles[i].SlideIn(120 + i * 90);
            };

            BuildGrid();
            tableCard.Controls.Add(grid);
            content.Controls.AddRange(new Control[] { music, totalGuests, totalRiel, totalDollar, today, search, currency, addGuest, tableCard });
            content.Resize += (s, e) => LayoutContent();

            search.Box.TextChanged += (s, e) => RefreshView();
            currency.Changed += (s, e) => RefreshView();
            addGuest.Click += (s, e) => AddGuest();
            sidebar.Navigate += (s, item) =>
            {
                if (item == "Log out") { LoggedOut = true; Close(); return; }
                bool settings = item == "Settings";
                if (item == "Tie the knot" || settings)
                {
                    settingsPage.Visible = settings;
                    content.Visible = !settings;
                    return;
                }
                MessageBox.Show(this, item + " is coming soon.", Text);
            };
            BuildSettingsPage();

            Controls.Add(content);
            Controls.Add(settingsPage);
            Controls.Add(sidebar);
            LayoutContent();
            RefreshView();
        }

        void LayoutContent()
        {
            const int pad = 26, gap = 20, top = 64; // top row holds the music player
            int w = content.ClientSize.Width - pad * 2;
            music.Location = new Point(pad + w - music.Width, 12);
            int tileW = (w - gap * 3) / 4;
            var tiles = new[] { totalGuests, totalRiel, totalDollar, today };
            for (int i = 0; i < tiles.Length; i++)
                tiles[i].SetBounds(pad + i * (tileW + gap), top, tileW, 92);

            search.SetBounds(pad, top + 114, Math.Min(538, w - 420), 46);
            currency.SetBounds(search.Right + 16, search.Top, 254, 46);
            addGuest.Location = new Point(pad + w - addGuest.Width, search.Top);

            tableCard.SetBounds(pad, search.Bottom + 22, w, content.ClientSize.Height - search.Bottom - 22 - pad);
            grid.SetBounds(1, 1, tableCard.Width - 2, tableCard.Height - 2);
        }

        void BuildGrid()
        {
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Color.White;
            grid.GridColor = Color.FromArgb(236, 240, 246);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 52;
            grid.RowTemplate.Height = 46;

            var head = grid.ColumnHeadersDefaultCellStyle;
            head.BackColor = Color.FromArgb(244, 247, 251);
            head.SelectionBackColor = head.BackColor;
            head.ForeColor = Color.FromArgb(55, 65, 81);
            head.Font = Ui.F(10f, FontStyle.Bold);
            head.Padding = new Padding(20, 0, 0, 0);

            var cell = grid.DefaultCellStyle;
            cell.Font = Ui.F(10f);
            cell.ForeColor = Ui.Ink;
            cell.SelectionBackColor = Color.FromArgb(235, 243, 255);
            cell.SelectionForeColor = Ui.Ink;
            cell.Padding = new Padding(20, 0, 0, 0);

            void Col(string name, string text, float weight)
            {
                int i = grid.Columns.Add(name, text);
                grid.Columns[i].FillWeight = weight;
                grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            Col("No", "etc.", 50);
            Col("Name", "Guest name", 180);
            Col("Money", "Bond money", 180);
            Col("Place", "Place", 115);
            Col("Misc", "Miscellaneous", 260);
            Col("Activity", "Activity", 80);

            var activity = grid.Columns["Activity"];
            activity.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            activity.HeaderCell.Style.Padding = new Padding(0, 0, 20, 0);
            activity.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            activity.DefaultCellStyle.Padding = new Padding(0, 0, 20, 0);
            activity.DefaultCellStyle.ForeColor = Ui.Red;
            activity.DefaultCellStyle.SelectionForeColor = Ui.Red;

            grid.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Activity") return;
                var g = (Guest)grid.Rows[e.RowIndex].Tag;
                if (MessageBox.Show(this, "Delete " + g.Name + "?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                guests.Remove(g);
                RefreshView();
            };
            grid.CellMouseEnter += (s, e) =>
                grid.Cursor = e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Activity" ? Cursors.Hand : Cursors.Default;
        }

        static readonly string[] ExcelHeader = { "No", "Guest name", "Bond money", "Currency", "Place", "Miscellaneous", "Recorded" };

        void BuildSettingsPage()
        {
            settingsPage.Controls.Add(new Label { Text = "Settings", Font = Ui.F(20f, FontStyle.Bold), ForeColor = Ui.Ink, AutoSize = true, Location = new Point(24, 24) });

            var card = new RoundedPanel { BackColor = Color.White, Location = new Point(26, 84), Size = new Size(760, 250) };
            card.Controls.Add(new Label { Text = "Data", Font = Ui.F(13f, FontStyle.Bold), ForeColor = Ui.Ink, AutoSize = true, Location = new Point(28, 22) });

            void Row(int y, string title, string hint, string button, string glyph, Action click)
            {
                card.Controls.Add(new Label { Text = title, Font = Ui.F(11f, FontStyle.Bold), ForeColor = Ui.Ink, AutoSize = true, Location = new Point(28, y) });
                card.Controls.Add(new Label { Text = hint, Font = Ui.F(9.5f), ForeColor = Ui.Muted, AutoSize = true, Location = new Point(28, y + 26) });
                var b = new FlatButton { Text = button, Glyph = glyph, Size = new Size(130, 44), Location = new Point(card.Width - 158, y + 2) };
                b.Click += (s, e) => click();
                card.Controls.Add(b);
            }

            Row(72, "Export to Excel", "Save all guests to an .xlsx file you can open in Excel.", "Export", "", ExportExcel);
            card.Controls.Add(new Label { BackColor = Ui.Border, Location = new Point(28, 148), Size = new Size(card.Width - 56, 1) });
            Row(168, "Import from Excel", "Add guests from an .xlsx file. Use the same columns as Export.", "Import", "", ImportExcel);

            settingsPage.Controls.Add(card);
        }

        void ExportExcel()
        {
            using (var dlg = new SaveFileDialog { Title = "Export guests", Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = $"guests-{DateTime.Now:yyyy-MM-dd}.xlsx" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var rows = new List<object[]> { ExcelHeader };
                rows.AddRange(guests.Select((g, i) => new object[]
                {
                    i + 1, g.Name, g.Amount, g.IsRiel ? "R" : "$", g.Place, g.Misc, g.Recorded.ToString("yyyy-MM-dd HH:mm"),
                }));

                try { Xlsx.Write(dlg.FileName, rows); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(this, "Could not save the file. Is it open in Excel?\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                MessageBox.Show(this, $"Exported {guests.Count} guest(s) to:\n{dlg.FileName}", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void ImportExcel()
        {
            List<string[]> rows;
            using (var dlg = new OpenFileDialog { Title = "Import guests", Filter = "Excel workbook (*.xlsx)|*.xlsx" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { rows = Xlsx.Read(dlg.FileName); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
                {
                    MessageBox.Show(this, "Could not read the file. Make sure it is an .xlsx workbook and not open in Excel.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // Match columns by header name so their order in the sheet doesn't matter.
            var header = rows.Count > 0 ? rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList() : new List<string>();
            int Col(string column) => header.IndexOf(column.ToLowerInvariant());
            string Get(string[] row, int i) => i >= 0 && i < row.Length ? row[i].Trim() : "";
            int name = Col("Guest name"), money = Col("Bond money"), cur = Col("Currency"), place = Col("Place"), misc = Col("Miscellaneous"), rec = Col("Recorded");
            if (name < 0 || money < 0)
            {
                MessageBox.Show(this, "The first row must have the column names \"Guest name\" and \"Bond money\".\nTip: export once to get a ready-made template.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int added = 0, skipped = 0;
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                string guestName = Get(row, name);
                string rawMoney = Get(row, money);
                string currencyText = (Get(row, cur) + (rawMoney.Contains("$") ? "$" : "")).ToLowerInvariant();
                if (guestName.Length == 0 ||
                    !decimal.TryParse(rawMoney.Replace("$", "").Replace("R", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
                    amount < 0)
                {
                    skipped++;
                    continue;
                }
                guests.Add(new Guest
                {
                    Name = guestName,
                    Amount = amount,
                    IsRiel = !(currencyText.Contains("$") || currencyText.StartsWith("dollar") || currencyText == "usd"),
                    Place = Get(row, place),
                    Misc = Get(row, misc),
                    Recorded = ParseDate(Get(row, rec)),
                });
                added++;
            }

            RefreshView();
            MessageBox.Show(this, $"Imported {added} guest(s)." + (skipped > 0 ? $"\nSkipped {skipped} row(s) with a missing name or invalid bond money." : ""),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static DateTime ParseDate(string s)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial > 0 && serial < 2958466)
                return DateTime.FromOADate(serial); // Excel stores typed dates as serial numbers
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Now;
        }

        void AddGuest()
        {
            using (var dlg = new AddGuestForm())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                guests.Add(dlg.Guest);
                RefreshView();
            }
        }

        void RefreshView()
        {
            totalGuests.SetValue(guests.Count, "N0");
            totalRiel.SetValue(guests.Where(g => g.IsRiel).Sum(g => g.Amount), "N0");
            totalDollar.SetValue(guests.Where(g => !g.IsRiel).Sum(g => g.Amount), "N2");
            today.SetValue(guests.Count(g => g.Recorded.Date == DateTime.Today), "N0");

            string q = search.Text.Trim();
            var shown = guests.Where(g =>
                (currency.Selected == null || g.IsRiel == (currency.Selected == "R")) &&
                (q.Length == 0 || $"{g.Name} {g.Place} {g.Misc}".IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));

            grid.Rows.Clear();
            int n = 1;
            foreach (var g in shown)
            {
                int i = grid.Rows.Add(n++, g.Name, g.Money, g.Place, g.Misc, "Delete");
                grid.Rows[i].Tag = g;
            }
            grid.ClearSelection();
        }
    }

    class Sidebar : Control
    {
        static readonly string[] Items = { "Tie the knot", "Report", "Enter common data", "Settings", "Log out" };
        static readonly string[] Glyphs = { "", "", "", "", "" };
        public event EventHandler<string> Navigate;

        int active, hover = -1;
        bool collapsed, sliding;
        Rectangle toggle;

        public Sidebar()
        {
            Dock = DockStyle.Left;
            Width = 272;
            BackColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        Rectangle ItemRect(int i) => i == Items.Length - 1
            ? new Rectangle(8, Height - 76, Width - 16, 52)          // Log out pinned to the bottom
            : new Rectangle(8, 112 + i * 64, Width - 16, 52);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var logo = new Rectangle(12, 21, 46, 46);
            using (var path = Gfx.Round(logo, 12))
            using (var b = new LinearGradientBrush(logo, Color.FromArgb(250, 90, 100), Ui.Red, 45f))
                g.FillPath(b, path);
            TextRenderer.DrawText(g, "\U0001F381", new Font("Segoe UI Emoji", 15f), logo, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            toggle = collapsed ? new Rectangle(Width - 22, 32, 20, 24) : new Rectangle(Width - 46, 32, 24, 24);
            if (!collapsed)
                Gfx.Text(g, "Tie the knot", Ui.F(14.5f, FontStyle.Bold), Ui.Red, 70, 30);
            TextRenderer.DrawText(g, collapsed ? "" : "", new Font(Ui.Icons, 9f), toggle, Ui.Blue,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using (var pen = new Pen(Color.FromArgb(190, 210, 240), 2f))
                g.DrawLine(pen, 9, 91, Width - 16, 91);

            for (int i = 0; i < Items.Length; i++)
            {
                var r = ItemRect(i);
                if (i == hover && i != active)
                    using (var path = Gfx.Round(r, 10))
                    using (var b = new SolidBrush(Color.FromArgb(244, 247, 251)))
                        g.FillPath(b, path);
                Color c = i == active ? Ui.Blue : (i == Items.Length - 1 ? Ui.Red : Color.FromArgb(75, 85, 99));
                TextRenderer.DrawText(g, Glyphs[i], new Font(Ui.Icons, 12f), new Rectangle(r.X + 10, r.Y, 36, r.Height), c,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (!collapsed)
                    TextRenderer.DrawText(g, Items[i], Ui.F(11f, FontStyle.Bold), new Rectangle(r.X + 52, r.Y, r.Width - 52, r.Height), c,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }

            using (var pen = new Pen(Ui.Border))
                g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Items.Length; i++)
                if (ItemRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            Cursor = h >= 0 || toggle.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (toggle.Contains(e.Location))
            {
                if (sliding) return;
                sliding = true;
                bool collapse = !collapsed;
                if (!collapse) collapsed = false; // show labels while opening; hide them only after closing
                int from = Width, to = collapse ? 82 : 272;
                Anim.Run(220, p => Width = from + (int)((to - from) * p), () =>
                {
                    collapsed = collapse;
                    sliding = false;
                    Invalidate();
                });
                return;
            }
            int h = HitTest(e.Location);
            if (h < 0) return;
            if (h != Items.Length - 1) { active = h; Invalidate(); }
            Navigate?.Invoke(this, Items[h]);
        }
    }

    class StatTile : Control
    {
        readonly string title, unit, icon, iconFont;
        readonly Color accent;
        decimal shown, target;
        string format = "N0";
        double appear = 1; // 0 = hidden, 1 = fully in place
        Timer tween;

        public StatTile(string title, string unit, string icon, string iconFont, Color accent)
        {
            this.title = title; this.unit = unit; this.icon = icon; this.iconFont = iconFont; this.accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        /// <summary>Counts up (or down) from the number on screen to the new one.</summary>
        public void SetValue(decimal value, string format)
        {
            this.format = format;
            if (value == target && tween == null) { shown = value; Invalidate(); return; }
            tween?.Dispose();
            decimal from = shown;
            target = value;
            tween = Anim.Run(600, p => { shown = from + (target - from) * (decimal)p; Invalidate(); }, () => tween = null);
        }

        /// <summary>Slide up and fade in after a delay.</summary>
        public void SlideIn(int delayMs)
        {
            appear = 0;
            Invalidate();
            Anim.Run(delayMs, _ => { }, () => Anim.Run(420, p => { appear = p; Invalidate(); }));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.Page);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int dy = (int)((1 - appear) * 18);
            string value = shown.ToString(format);

            var r = new Rectangle(0, dy, Width - 1, Height - 1 - dy);
            using (var path = Gfx.Round(r, 12))
            {
                g.FillPath(Brushes.White, path);
                g.SetClip(path);
                using (var b = new SolidBrush(accent))
                    g.FillRectangle(b, 0, dy, 4, Height); // colored left edge
                g.ResetClip();
            }

            var tile = new Rectangle(24, Height / 2 - 22 + dy, 44, 44);
            using (var path = Gfx.Round(tile, 10))
            using (var b = new SolidBrush(Color.FromArgb(30, accent)))
                g.FillPath(b, path);
            TextRenderer.DrawText(g, icon, new Font(iconFont, iconFont == Ui.Icons ? 13f : 15f, FontStyle.Bold), tile, accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            Gfx.Text(g, title, Ui.F(9.5f), Color.FromArgb(55, 65, 81), 82, 20 + dy);
            var big = Ui.F(19f, FontStyle.Bold);
            Gfx.Text(g, value, big, Ui.Ink, 80, 40 + dy);
            int vw = TextRenderer.MeasureText(g, value, big, Size.Empty, TextFormatFlags.NoPadding).Width;
            Gfx.Text(g, unit, Ui.F(9.5f), Color.FromArgb(55, 65, 81), 86 + vw, 55 + dy);

            if (appear < 1) // fade: wash the tile with the page colour
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * (1 - appear)), Ui.Page)))
                    g.FillRectangle(b, ClientRectangle);
        }
    }

    /// <summary>Riel / Dollar segmented filter. Click the active one again to show both.</summary>
    class CurrencyToggle : Control
    {
        public event EventHandler Changed;
        public string Selected { get; private set; } // "R", "$" or null = both

        Rectangle Riel => new Rectangle(54, 7, 90, Height - 14);
        Rectangle Dollar => new Rectangle(150, 7, 90, Height - 14);

        public CurrencyToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.Page);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Gfx.Round(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (var pen = new Pen(Ui.Border))
            {
                g.FillPath(Brushes.White, path);
                g.DrawPath(pen, path);
            }
            Segment(g, Riel, "Riel (R)", Selected == "R");
            Segment(g, Dollar, "Dollar ($)", Selected == "$");
        }

        static void Segment(Graphics g, Rectangle r, string text, bool on)
        {
            if (on)
                using (var path = Gfx.Round(r, 8))
                using (var b = new SolidBrush(Color.FromArgb(232, 240, 254)))
                    g.FillPath(b, path);
            TextRenderer.DrawText(g, text, Ui.F(10f, FontStyle.Bold), r, on ? Ui.Blue : Color.FromArgb(55, 65, 81),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            string hit = Riel.Contains(e.Location) ? "R" : Dollar.Contains(e.Location) ? "$" : null;
            if (hit == null) return;
            Selected = Selected == hit ? null : hit;
            Invalidate();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    class AddGuestForm : Form
    {
        public Guest Guest { get; private set; }

        readonly InputBox name = new InputBox("Guest name");
        readonly InputBox amount = new InputBox("0");
        readonly ComboBox unit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.F(11f), FlatStyle = FlatStyle.Flat };
        readonly InputBox place = new InputBox("Place");
        readonly InputBox misc = new InputBox("Notes (optional)");
        readonly Label error = new Label { ForeColor = Ui.Red, Font = Ui.F(9.5f) };

        public AddGuestForm()
        {
            Text = "Add guest";
            ClientSize = new Size(460, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;

            int x = 30, w = ClientSize.Width - 60, y = 24;
            Controls.Add(new Label { Text = "Add guest", Font = Ui.F(16f, FontStyle.Bold), ForeColor = Ui.Ink, AutoSize = true, Location = new Point(x - 2, y) });
            y += 50;

            void Field(string label, Control c, int width)
            {
                Controls.Add(new Label { Text = label, Font = Ui.F(9.5f, FontStyle.Bold), ForeColor = Ui.Ink, AutoSize = true, Location = new Point(x, y) });
                c.SetBounds(x, y + 22, width, 46);
                Controls.Add(c);
            }

            Field("Guest name", name, w);
            y += 76;
            Field("Bond money", amount, w - 130);
            unit.Items.AddRange(new object[] { "Riel (R)", "Dollar ($)" });
            unit.SelectedIndex = 0;
            unit.SetBounds(x + w - 118, y + 30, 118, 30);
            Controls.Add(unit);
            y += 76;
            Field("Place", place, w);
            y += 76;
            Field("Miscellaneous", misc, w);
            y += 76;

            error.SetBounds(x, y, w, 20);
            Controls.Add(error);

            var cancel = new FlatButton { Text = "Cancel", Fill = Color.FromArgb(241, 244, 249), ForeColor = Ui.Ink, Location = new Point(x, y + 24), Width = (w - 12) / 2 };
            cancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            var save = new FlatButton { Text = "Save", Location = new Point(cancel.Right + 12, y + 24), Width = (w - 12) / 2 };
            save.Click += (s, e) => Save();
            Controls.Add(cancel);
            Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
            Shown += (s, e) => name.Box.Focus();
        }

        void Save()
        {
            if (name.Text.Trim().Length == 0) { error.Text = "Guest name is required."; name.Box.Focus(); return; }
            if (!decimal.TryParse(amount.Text, out var money) || money < 0) { error.Text = "Bond money must be a positive number."; amount.Box.Focus(); return; }

            Guest = new Guest
            {
                Name = name.Text.Trim(),
                Amount = money,
                IsRiel = unit.SelectedIndex == 0,
                Place = place.Text.Trim(),
                Misc = misc.Text.Trim(),
            };
            DialogResult = DialogResult.OK;
        }
    }
}
