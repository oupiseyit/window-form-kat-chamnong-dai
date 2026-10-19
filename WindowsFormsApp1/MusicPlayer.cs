using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Compact music controller: restart, play/pause, title, seekable progress, mute.
    /// Plays through the Windows MCI API (winmm.dll), present on every Windows edition; errors never throw.
    /// </summary>
    class MusicPlayer : Control
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        static extern int mciSendString(string command, StringBuilder result, int resultLength, IntPtr callback);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        static extern bool mciGetErrorString(int error, StringBuilder text, int length);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int GetShortPathName(string longPath, StringBuilder shortPath, int length);

        const string Alias = "kcdsong";

        readonly string title;
        readonly Timer tick = new Timer { Interval = 500 };
        readonly ToolTip tip = new ToolTip();
        string problem;

        // One shared MCI session: every MusicPlayer on screen controls the same song,
        // so playback carries on when switching between forms.
        static bool opened, muted, wantPlay;
        static int users;
        bool ready => opened;

        public Color Accent = Form1.Maroon;   // play button + progress knob
        public Color Progress = Form1.Gold;   // filled part of the bar
        public string FontName = "Segoe UI";
        Rectangle restartBtn, playBtn, muteBtn, bar;

        public MusicPlayer(string fileName, string title)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Size = new Size(270, 48);
            this.title = title;
            users++;

            // Next to the exe (copied on build), else the project folder (bin\Debug\..\..) in case the copy didn't happen.
            var candidates = new[] { Application.StartupPath, Path.Combine(Application.StartupPath, "..", "..") }
                .Select(dir => Path.GetFullPath(Path.Combine(dir, "Song", fileName))).ToArray();
            string file = candidates.FirstOrDefault(File.Exists);
            if (file == null)
            {
                Fail("Song file not found", "Looked for:\n" + string.Join("\n", candidates));
                return;
            }

            if (!opened)
            {
                bool ok = Mci($"open \"{ShortPath(file)}\" type mpegvideo alias {Alias}");
                opened = ok && Mci($"set {Alias} time format milliseconds");
                if (ok && !opened) mciSendString($"close {Alias}", null, 0, IntPtr.Zero);
                if (!opened) return;
                Mci($"setaudio {Alias} volume to 700");
            }

            tick.Tick += (s, e) =>
            {
                if (wantPlay && Status("mode") == "stopped") Play(" from 0"); // loop fallback
                Invalidate();
            };
            tick.Start();
        }

        /// <summary>Sends an MCI command; logs and remembers the error instead of throwing.</summary>
        bool Mci(string command, StringBuilder result = null)
        {
            int err;
            try { err = mciSendString(command, result, result?.Capacity ?? 0, IntPtr.Zero); }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Fail("Audio not supported", ex.Message);
                return false;
            }
            if (err == 0) return true;

            var text = new StringBuilder(256);
            mciGetErrorString(err, text, text.Capacity);
            Fail("Music unavailable", $"'{command}' failed ({err}): {text}");
            return false;
        }

        /// <summary>Shows a short reason on the control; full detail goes to the tooltip and VS Output window.</summary>
        void Fail(string shortText, string detail)
        {
            problem = shortText;
            tip.SetToolTip(this, detail);
            Debug.WriteLine("MusicPlayer: " + detail);
        }

        void Play(string from = "")
        {
            wantPlay = true;
            // "repeat" loops the song; if this MCI driver rejects it, play once and the tick restarts it.
            if (mciSendString($"play {Alias}{from} repeat", null, 0, IntPtr.Zero) != 0)
                Mci($"play {Alias}{from}");
        }

        string Status(string item)
        {
            if (!ready) return "";
            var sb = new StringBuilder(128);
            return mciSendString($"status {Alias} {item}", sb, sb.Capacity, IntPtr.Zero) == 0 ? sb.ToString() : "";
        }

        // MCI commands choke on some long/unusual paths; the 8.3 form avoids that when the volume has one.
        static string ShortPath(string path)
        {
            var sb = new StringBuilder(260);
            return GetShortPathName(path, sb, sb.Capacity) > 0 ? sb.ToString() : path;
        }

        bool IsPlaying => Status("mode") == "playing";
        double Position => double.TryParse(Status("position"), out var ms) ? ms / 1000 : 0;
        double Duration => double.TryParse(Status("length"), out var ms) ? ms / 1000 : 0;

        static string Clock(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"m\:ss");

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Form1.Cream);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            Color dim = ready ? Form1.Ink : Form1.Line;
            bool playing = IsPlaying;
            double pos = Position, len = Duration;

            restartBtn = new Rectangle(0, 10, 28, 28);
            playBtn = new Rectangle(32, 4, 40, 40);
            muteBtn = new Rectangle(Width - 28, 12, 26, 24);
            bar = new Rectangle(84, 31, Width - 84 - 36, 4);

            TextRenderer.DrawText(g, "", Gfx.Typeface(Form1.Icons, 10f), restartBtn, dim, center);

            using (var b = new SolidBrush(ready ? Accent : Form1.Line))
                g.FillEllipse(b, playBtn);
            // Nudge the play triangle right so it looks optically centred.
            var glyphBox = playing ? playBtn : new Rectangle(playBtn.X + 2, playBtn.Y, playBtn.Width, playBtn.Height);
            TextRenderer.DrawText(g, playing ? "" : "", Gfx.Typeface(Form1.Icons, 12f), glyphBox, Color.White, center);

            Gfx.Text(g, ready ? title : problem, Gfx.Typeface(FontName, 10f), ready ? Form1.Ink : Form1.Muted, bar.X, 7);
            if (ready)
            {
                string time = Clock(pos) + " / " + Clock(len);
                var small = Gfx.Typeface(FontName, 8.5f);
                int tw = TextRenderer.MeasureText(time, small).Width;
                Gfx.Text(g, time, small, Form1.Muted, bar.Right - tw + 6, 9);
            }

            using (var path = Gfx.Round(bar, 2))
            using (var b = new SolidBrush(Form1.Line))
                g.FillPath(b, path);
            if (len > 0)
            {
                int filled = (int)(bar.Width * Math.Min(1, pos / len));
                if (filled > 0)
                    using (var b = new SolidBrush(Progress))
                        g.FillRectangle(b, bar.X, bar.Y, filled, bar.Height);
                using (var b = new SolidBrush(Accent))
                    g.FillEllipse(b, bar.X + filled - 5, bar.Y - 3, 10, 10);
            }

            TextRenderer.DrawText(g, muted ? "" : "", Gfx.Typeface(Form1.Icons, 11f), muteBtn, dim, center);
        }

        Rectangle SeekArea => new Rectangle(bar.X, bar.Y - 8, bar.Width, bar.Height + 16);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = ready && (restartBtn.Contains(e.Location) || playBtn.Contains(e.Location) || muteBtn.Contains(e.Location) || SeekArea.Contains(e.Location));
            Cursor = hot ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (!ready) return;
            if (playBtn.Contains(e.Location))
            {
                // "play" without "from" resumes at the current position.
                if (IsPlaying) { wantPlay = false; Mci($"pause {Alias}"); }
                else Play();
            }
            else if (restartBtn.Contains(e.Location))
            {
                Play(" from 0");
            }
            else if (muteBtn.Contains(e.Location))
            {
                muted = !muted;
                Mci($"setaudio {Alias} volume to {(muted ? 0 : 700)}");
            }
            else if (SeekArea.Contains(e.Location) && Duration > 0)
            {
                int ms = (int)(Duration * 1000 * Math.Max(0, Math.Min(1, (e.X - bar.X) / (double)bar.Width)));
                if (IsPlaying) Play($" from {ms}"); else Mci($"seek {Alias} to {ms}");
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tick.Dispose();
                tip.Dispose();
                if (--users == 0 && opened)
                {
                    Mci($"close {Alias}");
                    opened = wantPlay = false;
                }
            }
            base.Dispose(disposing);
        }
    }
}
