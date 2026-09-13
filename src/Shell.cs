// Shell.cs -- the room you are in before you sit down at the machine.
//
// Two layers, deliberately unlike each other. This one is of now: dark, flat,
// quiet. Through it you go into a cabinet from 1966, and the distance between
// the two is the point.
//
// The idea of a room comes from the Hebrew ELIZA the Bloomfield Science Museum
// showed in 2010, whose one surviving screenshot is a hand-drawn therapist's
// office. The drawing here is our own; only the thought is borrowed. It also
// settles a question the original leaves open: the 1966 script has no way to
// say goodbye, and none was invented for it. You do not end the conversation.
// You get up and leave the room.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ElizaApp
{
    /*  Two rooms to be in.

        The first is of now: dark, flat, quiet, so that the cabinet from 1966
        on the other side of the door is the loud thing.

        The second remembers how the Bloomfield Science Museum showed her in
        2010 -- a therapist's office drawn in coloured pencil on paper. The one
        surviving screenshot of theirs is their artist's work and is not used
        here; what is borrowed is the thought that you are in a room, and the
        warmth of the medium. */
    static class Theme
    {
        public static Color Back, Panel, PanelHot, Line, Text, Muted, Faint, Accent;
        public static Color Ink, Pencil, Leaf;     // the drawing's own colours
        public static bool Light;

        /*  Six accents, each given twice: a bright one to carry on a dark
            ground and a deep one to carry on a light one. The same colour
            cannot do both -- a green that reads as a phosphor glow at night
            reads as a highlighter pen by day. */
        static readonly int[] OnDark =
        {
            0x40F06E, 0xFFB347, 0x5AB8FF, 0xB98CFF, 0xFF8A6B, 0xA9B4C4,
        };
        static readonly int[] OnLight =
        {
            // Amber and coral failed on headings and on white labels in the
            // light theme; these are the same six colours, darkened until they
            // pass on Back, on PanelHot, and under white.
            0x276943, 0x8F5E12, 0x1F6FB2, 0x6C4BB6, 0xA8453A, 0x55606E,
        };

        public static int Count { get { return OnDark.Length; } }

        /*  Which way the reading runs. Kept here rather than asked of Say on
            every call, because the controls need it on every paint and a
            static bool is the cheapest question there is. */
        public static bool Mirrored;

        static TextFormatFlags Basic
        {
            get
            {
                return TextFormatFlags.NoPrefix |
                       TextFormatFlags.PreserveGraphicsClipping |
                       (Mirrored ? TextFormatFlags.RightToLeft : TextFormatFlags.Default);
            }
        }

        public static TextFormatFlags Start
        {
            get
            {
                return Basic | TextFormatFlags.WordEllipsis |
                       (Mirrored ? TextFormatFlags.Right : TextFormatFlags.Left);
            }
        }

        public static TextFormatFlags Centre
        {
            get { return Basic | TextFormatFlags.HorizontalCenter; }
        }

        public static TextFormatFlags Wrap
        {
            get
            {
                return Basic | TextFormatFlags.WordBreak |
                       (Mirrored ? TextFormatFlags.Right : TextFormatFlags.Left);
            }
        }

        // The colour itself, for a swatch to be painted in. Naming colours in
        // words was the mistake this replaces.
        public static Color AccentAt(int i)
        {
            if (i < 0 || i >= OnDark.Length) i = 0;
            return Rgb(Light ? OnLight[i] : OnDark[i]);
        }

        static Theme() { Apply("system", 0); }

        /*  mode is "system", "dark" or "light". Windows keeps the system
            answer in the registry; if it is not there, or the key cannot be
            read, dark is assumed, because that is what this program looked
            like before it had a light mode at all. */
        public static bool SystemIsLight()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key == null) return false;
                    object v = key.GetValue("AppsUseLightTheme");
                    return v is int && (int)v != 0;
                }
            }
            catch { return false; }
        }

        /*  Windows is in a high contrast scheme. Not a preference: somebody
            has told the system that these colours and no others are readable
            for them, and a program that paints its own palette over that is
            simply unreadable to them, however carefully the palette was
            measured. So the program gives up its colours and keeps its
            shapes. */
        public static bool Contrast;

        public static void Apply(string mode, int accent)
        {
            Contrast = false;
            try { Contrast = SystemInformation.HighContrast; }
            catch { }

            if (Contrast)
            {
                /*  Every colour comes from the scheme the person chose. The
                    panels are the window colour and not a shade of it, because
                    a high contrast scheme has no shades: what separates one
                    thing from another is the line around it, and that line is
                    the text colour. What was carried by a lighter fill --
                    which row is chosen, what the pointer is on -- is carried
                    by the accent, and the accent is the system's own
                    highlight. */
                Light = SystemColors.Window.GetBrightness() > 0.5f;
                Back = Panel = PanelHot = SystemColors.Window;
                Line = SystemColors.WindowText;
                Text = Muted = SystemColors.WindowText;
                Faint = SystemColors.GrayText;
                Accent = SystemColors.Highlight;
                Ink = Pencil = Leaf = SystemColors.WindowText;
                return;
            }

            Light = mode == "light" || (mode != "dark" && SystemIsLight());
            if (accent < 0 || accent >= OnDark.Length) accent = 0;
            Accent = Rgb(Light ? OnLight[accent] : OnDark[accent]);

            if (Light)
            {
                Back = Color.FromArgb(246, 246, 244);
                Panel = Color.FromArgb(255, 255, 255);
                PanelHot = Color.FromArgb(238, 238, 235);
                Line = Color.FromArgb(224, 224, 219);
                Text = Color.FromArgb(32, 36, 42);
                Muted = Color.FromArgb(88, 95, 105);
                /*  The line under every setting is written in this, and so are
                    the group headings and the link notes -- the most-repeated
                    prose in the program. At (154,160,168) on white it came out
                    at about 2.6 to 1, against the 4.5 that body text needs, so
                    the explanations nobody could do without were the hardest
                    thing on the page to read. */
                // 4.17 to 1 on PanelHot, which is the ground under the
                // pointer -- and these are the explanations under every
                // setting, read exactly when the pointer is on them.
                Faint = Color.FromArgb(100, 106, 116);
                Ink = Color.FromArgb(72, 78, 88);
            }
            else
            {
                Back = Color.FromArgb(18, 20, 24);
                Panel = Color.FromArgb(26, 29, 35);
                PanelHot = Color.FromArgb(34, 38, 46);
                Line = Color.FromArgb(44, 49, 58);
                Text = Color.FromArgb(232, 236, 242);
                /*  It differed from Faint by 1.02 to 1: in the dark theme
                    the two quiet tiers were the same colour, so prose written
                    in the quieter one said something in light mode and nothing
                    in dark. Raised rather than lowering Faint, which is
                    already squeezed from below by PanelHot. */
                Muted = Color.FromArgb(176, 184, 196);
                Faint = Color.FromArgb(148, 156, 168);
                Ink = Accent;
            }
            Pencil = Accent;
            Leaf = Accent;
        }

        static Color Rgb(int v)
        {
            return Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
        }
    }

    // A rectangle that does something when it is clicked.
    class Hotspot
    {
        public Rectangle Where;
        public Action Do;
    }

    class ShellForm : Form
    {
        /*  The models have a page of their own.

            They were a second shelf on the opening screen, under ELIZA's own
            two scripts, and that is not enough separation: what they are is a
            different kind of thing, and a heading over a row of the same
            cards says they are the same kind of thing in two groups. */
        enum Page { Home, Models, About, Transcripts, Preferences, Reading }

        readonly List<ScriptEntry> scripts;
        readonly Settings settings;
        readonly List<Hotspot> hotspots = new List<Hotspot>();

        Page page = Page.Home;
        string reading = "", readingName = "";

        float scale = 1f;
        Rectangle closeButton, minButton, maxButton;

        const int Bar = 46, Nav = 196, Pad = 34, Grip = 6;

        public ShellForm(List<ScriptEntry> scripts, Settings settings)
        {
            this.scripts = scripts;
            this.settings = settings;
            Say.Hebrew = settings.Hebrew;
            Theme.Apply(settings.Mode, settings.Accent);
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemColoursChanged;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Back;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "ELIZA";
            /*  The icon on the taskbar and in Alt+Tab.

                A Form does not take the executable's icon by itself; left
                alone it shows the stock WinForms one, so the program had a
                drawn icon on disk and a generic one everywhere a person would
                actually see it. */
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            /*  The screen the window is about to be put on, not the primary
                one. It was sized against the primary and centred on the one
                under the pointer, so on a two-monitor desk it was measured
                for a screen it was not going to appear on. */
            var room = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(
                Math.Min(S(980), room.Width - S(60)),
                Math.Min(S(660), room.Height - S(60)));
            /*  Clamped to the screen it will be shown on.

                MinimumSize is itself scaled, and WinForms enforces it: at 150
                per cent on a 1366 by 768 laptop the minimum height is 780
                against a work area of about 696, so the window came up taller
                than the screen with no system title bar to drag it back by.
                Both those scale factors are ones Windows offers. */
            MinimumSize = new Size(Math.Min(S(720), room.Width),
                                   Math.Min(S(520), room.Height));
            CentreOnScreen();

            MakeHost();

            /*  A resize settles before the page is rebuilt.

                Dragging a window edge sends WM_SIZE dozens of times a second,
                and rebuilding on each one tore down and re-created every
                control -- throwing away the reader's place in the text and the
                keyboard's place in the page, dozens of times a second, and
                re-measuring a hundred-block document each time. It waits for
                the dragging to stop, and puts the reader back where they were. */
            settle.Interval = 120;
            settle.Tick += (s, e) =>
            {
                settle.Stop();
                int was = -body.AutoScrollPosition.Y;
                // And the keyboard's place, not only the page's.
                string had = ActiveControl != null ? ActiveControl.AccessibleName : null;
                Rebuild();
                if (was > 0) body.ScrollTo(was);
                Restore(had);
                Invalidate();
            };
            Resize += (s, e) => { settle.Stop(); settle.Start(); PlaceHost(); Invalidate(); };
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
        }

        int S(int v) { return (int)Math.Round(v * scale); }

        /*  Centre the window after it has been sized.

            StartPosition.CenterScreen is applied while the form is still its
            default 300 by 300, so a window that is resized afterwards grows
            down and to the right from a place chosen for a much smaller one --
            and ends up hanging off the screen. Placing it by hand once the
            size is final is the only way to have it actually centred. */
        void CentreOnScreen()
        {
            var room = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(
                room.X + Math.Max(0, (room.Width - Width) / 2),
                room.Y + Math.Max(0, (room.Height - Height) / 2));
        }


        // In Hebrew the whole layout is mirrored across the window.
        Rectangle Flip(Rectangle r)
        {
            if (!Say.Hebrew) return r;
            return new Rectangle(ClientSize.Width - r.Right, r.Y, r.Width, r.Height);
        }

        /*  PreserveGraphicsClipping on everything.

            TextRenderer draws through GDI, and GDI does not look at the GDI+
            clipping region unless it is asked to. Without the flag, a page
            that is scrolled paints its text straight up through the title bar
            -- which is what it did, and it took a screenshot to see it,
            because the shapes behind the text were clipped correctly and only
            the words came through. */
        TextFormatFlags Flags(bool centre)
        {
            var f = TextFormatFlags.NoPrefix | TextFormatFlags.WordEllipsis;
            if (centre) return f | TextFormatFlags.HorizontalCenter |
                               (Say.Hebrew ? TextFormatFlags.RightToLeft : 0);
            return f | (Say.Hebrew
                ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                : TextFormatFlags.Left);
        }

        /*  From the cache the widgets use, not a new one each time.

            Two methods called Face, on unrelated types, with opposite rules
            about who owns what came back: the widgets' returns a shared font
            and must not be disposed, this one returned a new one and had to
            be. One paint of the opening screen made and destroyed nine GDI
            font handles, at the rate the mouse moves. */
        Font Face(float points, FontStyle style)
        {
            return Fonts.Get(points, style);
        }

        void Add(Rectangle where, Action what)
        {
            hotspots.Add(new Hotspot { Where = where, Do = what });
        }

        // ---- painting ---------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Back);

            hotspots.Clear();

            PaintTitleBar(g);
            PaintNav(g);

            var body = Flip(new Rectangle(Nav2, S(Bar),
                ClientSize.Width - Nav2, ClientSize.Height - S(Bar)));

            // A page draws inside the body and nowhere else, whatever it
            // thinks its coordinates are.
            g.SetClip(body);

            // Only the room is still painted by the window itself. It is a
            // picture rather than a page, and a picture is the one thing this
            // way of working was ever right for.
            if (page == Page.Home) PaintHome(g, body);

            g.ResetClip();
        }

        int Nav2 { get { return S(Nav); } }

        void PaintTitleBar(Graphics g)
        {
            using (var b = new SolidBrush(Theme.Panel))
                g.FillRectangle(b, 0, 0, ClientSize.Width, S(Bar));
            using (var p = new Pen(Theme.Line))
                g.DrawLine(p, 0, S(Bar) - 1, ClientSize.Width, S(Bar) - 1);

            var lamp = Flip(new Rectangle(S(22), S(Bar) / 2 - S(5), S(10), S(10)));
            using (var glow = new SolidBrush(Color.FromArgb(60, Theme.Accent)))
                g.FillEllipse(glow, Rectangle.Inflate(lamp, S(4), S(4)));
            using (var b = new SolidBrush(Theme.Accent))
                g.FillEllipse(b, lamp);

            var title = Flip(new Rectangle(S(44), 0, S(200), S(Bar)));
            TextRenderer.DrawText(g, "E L I Z A", Face(10.5f, FontStyle.Bold),
                title, Theme.Text, Flags(false) | TextFormatFlags.VerticalCenter);

            /*  Minimise, maximise, close: full height and flush to the
                corner, which is where Windows puts them and where a person
                throws the pointer without looking. They were eighteen pixels
                square and twenty short of the edge, and there was no maximise
                at all -- the window could not be maximised by any means the
                system offers.

                They sit opposite the name, so in Hebrew they cross over. */
            int bw = S(46), bh = S(Bar);
            int edge = Say.Hebrew ? 0 : ClientSize.Width - bw;
            int away = Say.Hebrew ? 1 : -1;
            closeButton = new Rectangle(edge, 0, bw, bh);
            maxButton = new Rectangle(edge + away * bw, 0, bw, bh);
            minButton = new Rectangle(edge + away * bw * 2, 0, bw, bh);

            bool onClose = hotButton == HTCLOSE || pressedButton == HTCLOSE;
            bool onMax = hotButton == HTMAXBUTTON || pressedButton == HTMAXBUTTON;
            bool onMin = hotButton == HTMINBUTTON || pressedButton == HTMINBUTTON;

            if (onClose)
                using (var b2 = new SolidBrush(pressedButton == HTCLOSE
                           ? Color.FromArgb(150, 30, 22) : Color.FromArgb(200, 44, 32)))
                    g.FillRectangle(b2, closeButton);
            else if (onMax || onMin)
                using (var b2 = new SolidBrush(pressedButton != 0 ? Theme.Line : Theme.PanelHot))
                    g.FillRectangle(b2, onMax ? maxButton : minButton);

            int r = S(5), cy = bh / 2;
            using (var p2 = new Pen(onClose ? Color.White : Theme.Muted, 1.4f))
            {
                int cx = closeButton.X + bw / 2;
                g.DrawLine(p2, cx - r, cy - r, cx + r, cy + r);
                g.DrawLine(p2, cx + r, cy - r, cx - r, cy + r);
            }
            using (var p2 = new Pen(Theme.Muted, 1.4f))
            {
                int cx = minButton.X + bw / 2;
                g.DrawLine(p2, cx - r, cy, cx + r, cy);

                var box = new Rectangle(maxButton.X + bw / 2 - r, cy - r, r * 2, r * 2);
                if (WindowState == FormWindowState.Maximized)
                {
                    g.DrawRectangle(p2, box.X + S(3), box.Y - S(3), box.Width, box.Height);
                    using (var fill = new SolidBrush(Theme.Panel))
                        g.FillRectangle(fill, box);
                }
                g.DrawRectangle(p2, box.X, box.Y, box.Width, box.Height);
            }
        }

        void PaintNav(Graphics g)
        {
            var strip = Flip(new Rectangle(0, S(Bar), Nav2, ClientSize.Height - S(Bar)));
            using (var b = new SolidBrush(Theme.Panel))
                g.FillRectangle(b, strip);
            using (var p = new Pen(Theme.Line))
            {
                int edge = Say.Hebrew ? strip.Left : strip.Right - 1;
                g.DrawLine(p, edge, strip.Top, edge, strip.Bottom);
            }

            /*  The five rows themselves are real controls now -- NavRow, placed
                by PlaceHost and dressed by DressNav -- so that they answer to
                the keyboard and to a screen reader as well as to the pointer.
                What is left here is the strip they stand on. */

            var note = new Rectangle(strip.X + S(16), strip.Bottom - S(50),
                                     strip.Width - S(32), S(36));
            TextRenderer.DrawText(g, "PROJECT MAC" + Environment.NewLine +
                "M.I.T. · 1966", Face(7.5f, FontStyle.Regular), note,
                Theme.Faint, TextFormatFlags.NoPrefix |
                    (Say.Hebrew ? TextFormatFlags.Right : TextFormatFlags.Left));
        }

        // ---- the room ----------------------------------------------------

        /*  Two shelves: ELIZA herself, and the models built on her.

            They are not the same kind of thing and the opening screen should
            not offer them as though they were. A script says which it is with
            its own (FAMILY ...) directive; a script that says nothing is
            ELIZA, which is what every script in this program was until there
            were models. */
        void PaintHome(Graphics g, Rectangle body)
        {
            int x = body.X + S(Pad), w = body.Width - S(Pad) * 2;

            var head = new Rectangle(x, body.Y + S(22), w, S(40));
            TextRenderer.DrawText(g, "ELIZA", Face(22f, FontStyle.Bold), head,
                Theme.Text, Flags(false));

            var sub = new Rectangle(x, head.Bottom + S(2), w, S(24));
            TextRenderer.DrawText(g, Say.Subtitle, Face(10f, FontStyle.Regular), sub,
                Theme.Muted, Flags(false));

            /*  ELIZA, and only ELIZA. What is built on her is a page of its
                own, one place along the nav. */
            var hers = new List<ScriptEntry>();
            bool anyModels = false;
            foreach (var entry in scripts)
            {
                if (entry.IsModel) anyModels = true;
                else hers.Add(entry);
            }

            var scene = new Rectangle(x, sub.Bottom + S(16), w, S(176));
            PaintRoom(g, scene);

            int y = scene.Bottom + S(18);
            y = Shelf(g, body, x, w, y, Say.ChooseScript, hers);

            // And a line that says there is somewhere else to go.
            if (anyModels)
            {
                var more = new Rectangle(x, y + S(26), w, S(24));
                TextRenderer.DrawText(g, Say.AlsoModels, Face(9.5f, FontStyle.Regular),
                    more, Theme.Faint, Flags(false) | TextFormatFlags.VerticalCenter);
                Add(more, () => Go(Page.Models));
                y = more.Bottom;
            }

            /*  And, if somebody asked to be told, that there is a newer one.

                One line, in the accent colour, at the foot of the page: no
                dialog on startup and nothing that has to be dismissed. It is
                a link to the page, and the person downloads it themselves. */
            if (Updater.Newest != null && Updater.Where != null &&
                Updater.Newer(Updater.Newest, Updater.Version))
            {
                var note = new Rectangle(x, y + S(10), w, S(24));
                TextRenderer.DrawText(g, Say.NewHere + " — " + Updater.Newest,
                    Face(9.5f, FontStyle.Bold), note, Theme.Accent,
                    Flags(false) | TextFormatFlags.VerticalCenter);
                // To the settings, where the button that does it is -- not to
                // a browser. The program can put it in place itself now.
                Add(note, () => Go(Page.Preferences));
            }
        }

        /*  The models, on a page of their own and in a shape of their own.

            Not the same card as ELIZA's, because they are not the same thing:
            a wide row, a round mark in the colours of the room the model is
            talked to in, and a round button. They are real controls rather
            than painted rectangles, so the keyboard reaches them. */
        void BuildModels()
        {
            Title(Say.Models, S(22));

            var note = new Label
            {
                Text = Say.OursModels + " — " + Say.NoteModels,
                Font = Fonts.Get(9.5f, FontStyle.Regular),
                ForeColor = Theme.Muted,
                BackColor = Theme.Back,
                AutoSize = false,
                Bounds = new Rectangle(S(Pad), S(62), header.Width - S(Pad) * 2, S(42)),
                RightToLeft = Say.Hebrew ? RightToLeft.Yes : RightToLeft.No,
                TextAlign = ContentAlignment.TopLeft
            };
            header.Controls.Add(note);

            /*  Six of them now, and stacked one to a row they ran off the
                bottom: the last two could only be reached by scrolling, which
                is the wrong shape for a page whose whole point is that they
                are alternatives to each other. So they go across as well as
                down, as many to a row as fit while a card is still wide
                enough for its own sentence, and never more than three. */
            int gap = S(14);
            int area = Math.Max(S(240), body.ClientSize.Width - S(Pad) * 2 - S(16));
            int across = Math.Max(1, Math.Min(3, (area + gap) / (S(300) + gap)));
            int w = (area - gap * (across - 1)) / across;
            int right = S(Pad) + area;
            int seat = 0;

            /*  One height for all of them, and it is the tallest one's. Asked
                rather than assumed: at two to a row half the sentences wrap,
                and a fixed height cut the second line through the middle. */
            int tall = 0;
            var made = new List<ModelCard>();

            foreach (var entry in scripts)
            {
                if (!entry.IsModel) continue;
                var index = scripts.IndexOf(entry);
                var card = new ModelCard
                {
                    Title2 = entry.Name.Length > 0 ? entry.Name : Say.TalkTo(entry.RightToLeft),
                    Note = entry.About,
                    Go = Say.EnterFor(entry.RightToLeft),
                    Mirror = entry.RightToLeft,
                    Scene = Scene.For(entry.Scene.Length > 0 ? entry.Scene
                                      : entry.RightToLeft ? "MUSEUM" : "TELETYPE"),
                    Zoom = scale,
                    BackColor = Theme.Back,
                    Width = w
                };
                card.Announce(card.Title2, AccessibleRole.PushButton, card.Note);
                card.Pressed += (s2, e2) => Converse(index);
                made.Add(card);
                if (card.Wants(w) > tall) tall = card.Wants(w);
            }

            foreach (var card in made)
            {
                int col = seat % across, row = seat / across;
                seat++;
                card.Bounds = new Rectangle(
                    Say.Hebrew ? right - w - col * (w + gap) : S(Pad) + col * (w + gap),
                    S(10) + row * (tall + gap), w, tall);
                body.Controls.Add(card);
            }
        }

        /*  A heading and a row of cards under it, and how far down the page
            that got to. */
        int Shelf(Graphics g, Rectangle body, int x, int w, int y,
                  string heading, List<ScriptEntry> entries)
        {
            if (entries.Count == 0) return y;

            var title = new Rectangle(x, y, w, S(22));
            TextRenderer.DrawText(g, heading, Face(9.5f, FontStyle.Bold), title,
                Theme.Faint, Flags(false));

            /*  Room for the gaps as well as the cards. The divisor counted
                one gap however many cards there were, so the third card and
                everything after it hung off the edge in silence. */
            int gap = S(16);
            /*  As many as fit at a width a card is still readable at, and
                then a second row. A card narrower than this is a title with
                nowhere for the sentence under it, and a row that simply runs
                off the edge loses the card at the end of it in silence --
                which is what happened the moment anybody accepted the
                program's standing invitation to write a script. */
            int most = Math.Max(1, (w + gap) / (S(210) + gap));
            int across = Math.Min(entries.Count, most);
            int down = (entries.Count + across - 1) / across;
            int cardW = Math.Min(S(300), (w - gap * (across - 1)) / across);

            /*  Tall enough for two lines of what the script says about
                itself. At 118 the second line of a model's sentence ran
                under its own button. */
            int tall = S(132);
            int top = title.Bottom + S(6);

            for (int i = 0; i < entries.Count; i++)
            {
                int col = i % across, row = i / across;
                var card = new Rectangle(
                    Say.Hebrew ? body.Right - S(Pad) - cardW - col * (cardW + gap)
                               : x + col * (cardW + gap),
                    top + row * (tall + gap), cardW, tall);
                PaintCard(g, card, entries[i], scripts.IndexOf(entries[i]));
            }
            return top + down * tall + (down - 1) * gap;
        }

        /*  A desk, a lamp, a plant, and the machine you are about to sit at.
            Thin lines, no fill: it should read as a sketch on the wall rather
            than compete with the cabinet on the other side of the door. */
        void PaintRoom(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(Theme.Panel))
            using (var path = Rounded(r, S(12)))
                g.FillPath(b, path);
            using (var p = new Pen(Theme.Line))
            using (var path = Rounded(r, S(12)))
                g.DrawPath(p, path);

            /*  Saved and restored, not copied and assigned: g.Clip hands
                back a Region that has to be disposed and was not, so two
                leaked on every paint of this page -- and this page repaints
                on every movement of the mouse. SetClip also REPLACES the
                clip, so the room was relying on its own rectangle lying
                inside the one the caller had set. */
            var state = g.Save();
            g.SetClip(r, CombineMode.Intersect);

            float u = r.Height / 168f * scale;                 // one drawing unit
            float baseY = r.Bottom - 30 * u;
            float cx = r.X + r.Width * (Say.Hebrew ? 0.63f : 0.37f);

            // On paper the pencil presses harder than a phosphor line glows.
            int strong = Theme.Light ? 190 : 70;
            int soft = Theme.Light ? 120 : 34;

            using (var ink = new Pen(Color.FromArgb(strong, Theme.Ink), 1.4f * u))
            using (var faint = new Pen(Color.FromArgb(soft, Theme.Ink), 1.2f * u))
            {
                // the desk
                g.DrawLine(ink, r.X + 20 * u, baseY, r.Right - 20 * u, baseY);
                g.DrawLine(faint, cx - 46 * u, baseY, cx - 46 * u, baseY + 22 * u);
                g.DrawLine(faint, cx + 46 * u, baseY, cx + 46 * u, baseY + 22 * u);

                // the terminal, the one thing that is lit
                var box = new RectangleF(cx - 38 * u, baseY - 52 * u, 76 * u, 52 * u);
                using (var path = Rounded(Rectangle.Round(box), (int)(5 * u)))
                {
                    using (var glow = new SolidBrush(Color.FromArgb(
                               Theme.Light ? 26 : 18, Theme.Pencil)))
                        g.FillPath(glow, path);
                    g.DrawPath(ink, path);
                }
                var glass = new RectangleF(box.X + 8 * u, box.Y + 8 * u,
                                           box.Width - 16 * u, box.Height - 24 * u);
                using (var screen = new SolidBrush(Color.FromArgb(
                           Theme.Light ? 70 : 46, Theme.Pencil)))
                    g.FillRectangle(screen, glass);
                using (var lines = new Pen(Color.FromArgb(
                           Theme.Light ? 200 : 120, Theme.Pencil), 1f * u))
                    for (int i = 1; i <= 3; i++)
                        g.DrawLine(lines, glass.X + 5 * u, glass.Y + i * 7 * u,
                                   glass.X + (i == 3 ? 22 : 42) * u, glass.Y + i * 7 * u);
                g.DrawLine(ink, box.X + 10 * u, box.Bottom - 8 * u,
                           box.Right - 10 * u, box.Bottom - 8 * u);

                // the lamp
                float lx = cx + (Say.Hebrew ? -96 : 96) * u;
                g.DrawLine(faint, lx, baseY + 22 * u, lx, baseY - 62 * u);
                var shade = new PointF[]
                {
                    new PointF(lx - 15 * u, baseY - 62 * u),
                    new PointF(lx + 15 * u, baseY - 62 * u),
                    new PointF(lx + 10 * u, baseY - 80 * u),
                    new PointF(lx - 10 * u, baseY - 80 * u),
                };
                g.DrawPolygon(ink, shade);
                using (var warm = new SolidBrush(Color.FromArgb(
                           Theme.Light ? 60 : 20, Theme.Pencil)))
                    g.FillPolygon(warm, shade);

                // the plant: a pot on the floor, and leaves that fall outward
                float px = cx + (Say.Hebrew ? 112 : -112) * u;
                float potTop = baseY + 4 * u, potBottom = baseY + 24 * u;
                g.DrawLine(ink, px - 11 * u, potTop, px - 8 * u, potBottom);
                g.DrawLine(ink, px + 11 * u, potTop, px + 8 * u, potBottom);
                g.DrawLine(ink, px - 8 * u, potBottom, px + 8 * u, potBottom);
                g.DrawLine(ink, px - 11 * u, potTop, px + 11 * u, potTop);
                using (var leaf = new Pen(Color.FromArgb(soft + 40, Theme.Leaf), 1.2f * u))
                foreach (float lean in new[] { -1.0f, -0.55f, 0f, 0.55f, 1.0f })
                    g.DrawCurve(leaf, new[]
                    {
                        new PointF(px, potTop),
                        new PointF(px + lean * 10 * u, potTop - 20 * u),
                        new PointF(px + lean * 21 * u, potTop - 30 * u),
                        new PointF(px + lean * 27 * u, potTop - 24 * u),
                    }, 0.6f);

                // a chair, with its back to us
                float chx = cx + (Say.Hebrew ? -8 : 8) * u;
                g.DrawLine(faint, chx - 26 * u, r.Bottom - 4 * u, chx - 26 * u, baseY + 26 * u);
                g.DrawLine(faint, chx + 26 * u, r.Bottom - 4 * u, chx + 26 * u, baseY + 26 * u);
            }

            g.Restore(state);
        }

        void PaintCard(Graphics g, Rectangle card, ScriptEntry entry, int index)
        {
            bool hot = Hovering(card);
            using (var b = new SolidBrush(hot ? Theme.PanelHot : Theme.Panel))
            using (var path = Rounded(card, S(10)))
                g.FillPath(b, path);
            using (var p = new Pen(hot ? Theme.Accent : Theme.Line, hot ? 1.4f : 1f))
            using (var path = Rounded(card, S(10)))
                g.DrawPath(p, path);

            int x = card.X + S(18), w = card.Width - S(36);

            /*  Tall enough for the letters that hang below the line: ך, ן, ף,
                ץ and ק all descend, and at a fixed height the bottom of a
                final nun is cut off, which turns השדכן into השדכו. */
            var face = Face(12f, FontStyle.Bold);
            var name = new Rectangle(x, card.Y + S(18), w,
                Math.Max(S(24), TextRenderer.MeasureText("ןקp", face).Height + S(2)));
            TextRenderer.DrawText(g, entry.Name.Length > 0
                        ? entry.Name : Say.TalkTo(entry.RightToLeft),
                    face, name, Theme.Text,
                    (entry.RightToLeft
                        ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                        : TextFormatFlags.Left) |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            var flat = entry.RightToLeft
                ? TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix
                : TextFormatFlags.Left | TextFormatFlags.NoPrefix;

            /*  What the script says about itself, and failing that which
                language it is in. A card for a model that said only "Hebrew"
                would be telling you the one thing about it you could already
                see. */
            var note = new Rectangle(x, name.Bottom + S(1), w, S(38));
            string says = entry.About.Length > 0
                ? entry.About : (entry.RightToLeft ? "עברית" : "English");
            TextRenderer.DrawText(g, says, Face(8.5f, FontStyle.Regular), note,
                Theme.Faint,
                flat | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

            /*  On the card's own side, in the card's own language. The
                whole card is laid out by entry.RightToLeft; only the button
                was laid out by the interface. */
            int goW = Math.Min(S(96), card.Width - S(36));
            var go = new Rectangle(
                entry.RightToLeft ? card.Right - S(18) - goW : x,
                card.Bottom - S(38), goW, S(28));
            using (var b = new SolidBrush(hot ? Theme.Accent : Theme.Line))
            using (var path = Rounded(go, S(6)))
                g.FillPath(b, path);
            TextRenderer.DrawText(g, Say.EnterFor(entry.RightToLeft),
                    Face(9f, FontStyle.Bold), go,
                    hot ? Theme.Back : Theme.Text,
                    TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    (entry.RightToLeft ? TextFormatFlags.RightToLeft : 0));

            Add(card, () => Converse(index));
        }

        // ---- the other pages ---------------------------------------------
        //
        // Everything below this line used to be painted by hand into the one
        // OnPaint of the window, with a list of clickable rectangles filled as
        // it drew. It is real controls now, in a panel that scrolls the way
        // Windows scrolls, and the whole family of faults that came of the old
        // arrangement -- clicks landing a row out, rows that had scrolled off
        // the top still answering the mouse, a wheel that redrew the world --
        // went with it.

        readonly Timer settle = new Timer();       // waits for a resize to finish
        readonly Panel header = new Panel();       // stays put while the body moves
        readonly ScrollBox body = new ScrollBox();
        ArticleWidget article;
        TabsWidget chapterBar;

        readonly List<NavRow> navRows = new List<NavRow>();
        static readonly Page[] NavTargets =
        {
            Page.Home, Page.Models, Page.About, Page.Transcripts, Page.Preferences,
        };

        void MakeNav()
        {
            for (int i = 0; i < NavTargets.Length; i++)
            {
                var row = new NavRow { Hint = "Ctrl+" + (i + 1), BackColor = Theme.Panel };
                var target = NavTargets[i];
                row.Pressed += (s, e) => Go(target);
                navRows.Add(row);
                Controls.Add(row);
                row.BringToFront();
            }
        }

        /*  The labels and which one is lit. Called on every rebuild, because
            the language can change under them and so can the page. */
        void DressNav()
        {
            string[] labels =
            {
                Say.Converse, Say.Models, Say.About, Say.Transcripts, Say.Preferences,
            };
            for (int i = 0; i < navRows.Count; i++)
            {
                var row = navRows[i];
                bool active = page == NavTargets[i] ||
                              (page == Page.Reading && NavTargets[i] == Page.Transcripts);
                row.Label = labels[i];
                row.Active = active;
                row.Mirror = Say.Hebrew;
                row.Zoom = scale;
                row.BackColor = Theme.Panel;
                row.Announce(labels[i], AccessibleRole.PageTab,
                             row.Hint + (active ? " " + (Say.Hebrew ? "נבחר" : "selected") : ""));
                row.Invalidate();
            }
        }

        void MakeHost()
        {
            MakeNav();
            header.BackColor = Theme.Back;
            body.BackColor = Theme.Back;
            // The native bar is hidden; Slim is placed on the correct side
            // by PlaceBar, so the panel does not need mirroring for it.
            header.Visible = body.Visible = false;
            Controls.Add(header);
            Controls.Add(body);

            // The chapter the reading has reached lights up as it passes --
            // however the reader got there: the bar, the wheel, or Page Down.
            body.Moved += (s2, e2) => FollowScroll();
            body.MouseWheel += (s2, e2) => FollowScroll();

            /*  And put them somewhere before anything else does. The opening
                page is painted by the window rather than built out of
                controls, so the first Rebuild -- which is what calls PlaceHost
                -- does not happen until somebody leaves that page. The
                navigation rows were therefore on screen, at nothing by nothing,
                from the moment the program started until the moment you
                managed to change page without them. */
            PlaceHost();
            DressNav();

            // And, if it was asked for, the one question this program puts to
            // the network. It answers on its own thread; nothing waits for it.
            if (settings.CheckUpdates) LookForNew();
        }

        bool jumping;

        void FollowScroll()
        {
            if (jumping || article == null || chapterBar == null) return;
            int now = article.ChapterAt(-body.AutoScrollPosition.Y);
            if (now == chapterBar.Chosen) return;
            chapterBar.Chosen = now;
            chapterBar.Invalidate();
        }

        void PlaceHost()
        {
            var strip = Flip(new Rectangle(0, S(Bar), Nav2, ClientSize.Height - S(Bar)));
            int at = strip.Top + S(22);
            foreach (var row in navRows)
            {
                row.Bounds = new Rectangle(strip.X + S(12), at,
                                           Math.Max(S(40), strip.Width - S(24)), S(40));
                at += S(46);
            }

            var area = Flip(new Rectangle(Nav2, S(Bar),
                ClientSize.Width - Nav2, ClientSize.Height - S(Bar)));
            int tall = HeaderHeight();
            header.Bounds = new Rectangle(area.X, area.Y, area.Width, tall);
            body.Bounds = new Rectangle(area.X, area.Y + tall, area.Width, area.Height - tall);
            body.PlaceBar(S(12));
        }

        /*  How tall the part that does not scroll has to be. The chapter bar
            wraps when eight chapters will not fit across, so this is measured
            rather than assumed: assuming it cost the eighth chapter, which
            simply ran off the edge and could not be reached. */
        int headerTall;

        int HeaderHeight()
        {
            switch (page)
            {
                case Page.Models: return S(112);
                case Page.About: return headerTall > 0 ? headerTall : S(150);
                case Page.Preferences: return S(140);
                case Page.Transcripts: return S(96);
                case Page.Reading: return S(104);
                default: return 0;
            }
        }

        static void Empty(Control host)
        {
            var was = new Control[host.Controls.Count];
            host.Controls.CopyTo(was, 0);
            host.Controls.Clear();
            foreach (var c in was) c.Dispose();
        }

        /*  Rebuild once the event that asked for it has finished.

            Six of the settings handlers ask for a rebuild, and a rebuild
            disposes the control whose Changed handler is on the stack --
            destroying, mid-keystroke, the thing the keyboard was on. The
            focus fell back to the window, so an arrow press moved a setting
            once and then went nowhere: reaching the fifth accent colour cost
            five separate journeys through the tab order.

            So it is queued behind the current message, and the keyboard is
            put back on the control it was on, found again by the name that
            control announces. */
        void Later(string keepFocusOn)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((Action)(() => { Rebuild(); Restore(keepFocusOn); }));
        }

        void Restore(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (Control c in body.Controls)
                foreach (Control inner in c.Controls)
                    if (inner is Widget && inner.AccessibleName == name)
                    { inner.Focus(); return; }
            foreach (Control c in header.Controls)
                if (c is Widget && c.AccessibleName == name) { c.Focus(); return; }
        }

        /*  Build the page. Called when the page changes, when the language or
            the theme changes, and when a resize has settled -- never during
            painting, and never from inside the event of a control it is about
            to dispose. */
        void Rebuild()
        {
            /*  Whatever the settle timer was about to do, this is it.

                It is not in the form's component container and every
                MessageBox and the conversation window pump a nested message
                loop, so dragging the window's edge, letting go, and pressing
                Enter within the timer's 120ms ran a whole rebuild of a hidden
                window from inside the modal loop. */
            settle.Stop();
            Theme.Mirrored = Say.Hebrew;
            article = null;
            chapterBar = null;
            headerTall = 0;
            DressNav();

            /*  Out of sight while it is being rebuilt.

                A page is rebuilt by disposing every control on it and making
                new ones, and the panel they live in has WS_EX_COMPOSITED --
                which does not repaint when it is told to but when the message
                queue next empties. So the half-built page was on the screen
                while it was being built: the old page's holes, the new page's
                controls arriving one at a time, and the panel's own idea of
                how far it scrolls still belonging to the page before. A
                hidden window paints nothing, so there is nothing to see until
                there is a whole page to show.

                And a new page opens at the top. The scroll position belonged
                to the panel, not to the page, so arriving on a short page
                after a long one showed it already scrolled past its own
                end. */
            SuspendLayout();
            header.SuspendLayout();
            body.SuspendLayout();
            header.Visible = body.Visible = false;
            Empty(header);
            Empty(body);
            /*  Where the page was. A rebuild is not only a page change --
                six settings ask for one when they are changed -- so the
                position is kept by default and thrown away only when
                somebody actually went somewhere. Without the keeping, moving
                a switch at the foot of a tab jumped back to the top of it;
                without the throwing, arriving on a short page after a long
                one opened it already scrolled past its end. */
            int keepAt = startAtTop ? 0 : -body.AutoScrollPosition.Y;
            startAtTop = false;
            body.AutoScrollPosition = new Point(0, 0);
            PlaceHost();

            if (page == Page.Home)
            {
                body.ResumeLayout(false);
                header.ResumeLayout(false);
                ResumeLayout();
                Invalidate();
                return;
            }

            header.BackColor = body.BackColor = Theme.Back;

            switch (page)
            {
                case Page.Models: BuildModels(); break;
                case Page.About: BuildAbout(); break;
                case Page.Transcripts: BuildTranscripts(); break;
                case Page.Reading: BuildReading(); break;
                default: BuildPreferences(); break;
            }

            body.PlaceBar(S(12));
            /*  Somewhere for Page Down to go. Rebuild focused nothing and the
                panel is not a tab stop, so arriving on a long page left every
                paging key dead until something inside it was clicked.
                Later()'s Restore still wins, because it runs after this. */
            if (ActiveControl == null) body.Focus();

            body.ResumeLayout(false);
            header.ResumeLayout(false);
            header.Visible = body.Visible = true;
            ResumeLayout();

            /*  Painted now, not when the queue happens to empty. Invalidate
                alone only marks it; Update is what makes the whole page
                appear at once instead of arriving in pieces. */
            if (keepAt > 0) body.ScrollTo(keepAt);
            /*  The bar before the paint, not after it: placed afterwards it
                arrived a frame late, and a screen capture 90ms into a page
                change caught the new page with the thumb still where the old
                one had left it. */
            body.PlaceBar(S(12));
            Invalidate();
            header.Refresh();
            body.Refresh();
        }

        // Set by whatever moved the reader somewhere new.
        bool startAtTop;

        // A page title, in the part that does not scroll.
        Label Title(string text, int y)
        {
            var l = new Label
            {
                Text = text,
                Font = Fonts.Get(18f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Theme.Back,
                AutoSize = false,
                Bounds = new Rectangle(S(Pad), y, header.Width - S(Pad) * 2, S(34)),
                /*  RightToLeft on a Label mirrors ContentAlignment as well as
                    the text: MiddleLeft with RightToLeft on renders
                    right-aligned. Which is what is wanted, and it also gives
                    the label a right-to-left PARAGRAPH -- without which a
                    Hebrew date came apart, the day number torn off the front
                    and parked next to the time. */
                RightToLeft = Say.Hebrew ? RightToLeft.Yes : RightToLeft.No,
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(l);
            return l;
        }

        // ---- on eliza ------------------------------------------------------

        void BuildAbout()
        {
            Title(Say.About, S(22));

            var names = AboutText.Chapters;
            chapterBar = new TabsWidget
            {
                Labels = names,
                Wraps = true,
                Zoom = scale,
                BackColor = Theme.Back,
                Bounds = new Rectangle(S(Pad), S(74), header.Width - S(Pad) * 2, S(46))
            };
            header.Controls.Add(chapterBar);
            chapterBar.Announce(Say.About, AccessibleRole.PageTabList,
                                names[Math.Min(chapterBar.Chosen, names.Length - 1)]);

            int needs = chapterBar.Relayout();
            chapterBar.Height = needs;
            headerTall = S(74) + needs + S(8);
            PlaceHost();

            article = new ArticleWidget
            {
                Zoom = scale,
                BackColor = Theme.Back,
                Bounds = new Rectangle(S(Pad), S(10),
                                       Math.Max(S(240), body.ClientSize.Width - S(Pad) * 2 - S(16)),
                                       S(200))
            };
            article.LinkPressed += OpenLink;
            body.Controls.Add(article);
            article.Build(names, AboutText.Chapter);

            chapterBar.Changed += (s2, e2) =>
            {
                // AutoScrollPosition reads back negative and is set positive.
                /*  Through ScrollTo, which is the method that exists to
                    move the drawn thumb with the content. Setting
                    AutoScrollPosition behind its back left the thumb where it
                    was. */
                /*  And the move this makes must not answer back. The last
                    chapter cannot be scrolled to its own top -- there is not
                    a screenful under it -- so reading the position again
                    would land in the chapter before it and undo the press. */
                jumping = true;
                try { body.ScrollTo(article.ChapterTop(chapterBar.Chosen)); }
                finally { jumping = false; }
                // And the keyboard stays on the strip the arrow key is walking.
                if (!chapterBar.PickedByKey) body.Focus();
            };
        }

        void OpenLink(string url)
        {
            try
            {
                if (url.StartsWith("https://") || url.StartsWith("http://"))
                    System.Diagnostics.Process.Start(url);
            }
            catch { }
        }

        // ---- transcripts ---------------------------------------------------

        void BuildTranscripts()
        {
            var title = Title(Say.Transcripts, S(22));

            var files = TranscriptFiles();
            if (files.Length > 0)
            {
                /*  There was no way to empty this at all. Each conversation
                    could be thrown away one at a time, and the only other
                    route was the reset button in the settings, which also
                    throws away every preference in the program. */
                var all = new IconWidget { Glyph = IconWidget.Mark.Bin };
                all.Announce(Say.DeleteAll, AccessibleRole.PushButton, "");
                all.Pressed += (s2, e2) => BinAll(files);
                Beside(title, all);
            }
            if (files.Length == 0)
            {
                var none = new Label
                {
                    Text = Say.NoTranscripts,
                    Font = Fonts.Get(10f, FontStyle.Regular),
                    ForeColor = Theme.Muted,
                    BackColor = Theme.Back,
                    AutoSize = false,
                    Bounds = new Rectangle(S(Pad), S(10), body.ClientSize.Width - S(Pad) * 2, S(40)),
                    TextAlign = Say.Hebrew ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
                };
                body.Controls.Add(none);
                return;
            }

            int w = Math.Max(S(240), body.ClientSize.Width - S(Pad) * 2 - S(16));
            int y = S(8);
            foreach (var path in files)
            {
                var row = new TranscriptRow
                {
                    Title = Describe(path),
                    Note = Excerpt(path),
                    Path = path,
                    Zoom = scale,
                    Bounds = new Rectangle(S(Pad), y, w, S(66))
                };
                row.Open += OpenTranscript;
                row.Delete += Bin;
                body.Controls.Add(row);
                row.Dress(Say.Delete);
                y += S(74);
            }
        }

        /*  A line out of a saved conversation, so that a list of them is not
            four identical dates. The first thing the person typed is what
            makes one afternoon's conversation recognisable from another's;
            failing that, the first thing she said back. */
        static string Excerpt(string path)
        {
            try
            {
                string hers = null;
                bool heading = true;
                foreach (var raw in File.ReadLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (heading) { if (line.Length == 0) heading = false; continue; }
                    if (line.Length == 0) continue;
                    if (line.StartsWith("\u00B7"))
                        return Shorten(line.Substring(1).Trim());
                    if (hers == null) hers = line;
                }
                return hers == null ? "" : Shorten(hers);
            }
            catch { return ""; }
        }

        static string Shorten(string line)
        {
            return line.Length > 100 ? line.Substring(0, 98).TrimEnd() + "\u2026" : line;
        }

        /*  Throwing a conversation away.

            A real dialog, with Cancel as the button the keyboard lands on.
            The two-click "click again to be sure" this replaces could be got
            past by a double-click -- two button-downs arrive faster than the
            repaint that would have shown the warning, so the warning was never
            on the screen at all. */
        void Bin(string path)
        {
            var answer = MessageBox.Show(this,
                Say.DeleteAsk + Environment.NewLine + Environment.NewLine + Describe(path),
                Say.DeleteTitle,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2, Reading(Say.Hebrew));
            if (answer != DialogResult.OK) return;

            /*  To the recycle bin, not out of existence. There is no undo in
                this program, and a conversation somebody had is not a thing to
                make unrecoverable on one click. */
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Say.DeleteFailed + Environment.NewLine +
                                Environment.NewLine + ex.Message, Say.DeleteTitle,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning,
                                MessageBoxDefaultButton.Button1, Reading(Say.Hebrew));
            }
            Rebuild();
        }

        /*  Buttons on the title's line, at the other end of it.

            They were two word-buttons on a row of their own underneath, which
            cost a whole line of the page to say "Back" and "Copy" -- and the
            title line, which is where every program in the world puts them,
            was empty from the end of the date to the edge of the window. */
        IconWidget[] Beside(Label title, params IconWidget[] made)
        {
            int side = S(38), gap = S(10);
            int y = S(22) + (S(34) - side) / 2;
            int room = made.Length * (side + gap);

            for (int i = 0; i < made.Length; i++)
            {
                int x = Say.Hebrew ? S(Pad) + i * (side + gap)
                                   : header.Width - S(Pad) - side - i * (side + gap);
                made[i].Zoom = scale;
                made[i].BackColor = Theme.Back;
                made[i].Bounds = new Rectangle(x, y, side, side);
                header.Controls.Add(made[i]);
                made[i].BringToFront();
            }

            // And the title gives up the room, so a long date cannot run under
            // them.
            title.Bounds = new Rectangle(
                Say.Hebrew ? S(Pad) + room : S(Pad), title.Top,
                Math.Max(S(80), header.Width - S(Pad) * 2 - room), title.Height);
            return made;
        }

        /*  All of them, in one press and one question.

            To the recycle bin, like the single one: there is no undo in this
            program, and conversations somebody actually had are not a thing to
            make unrecoverable on one click. */
        void BinAll(string[] files)
        {
            var answer = MessageBox.Show(this,
                Say.DeleteAllAsk + Environment.NewLine + Environment.NewLine +
                    files.Length.ToString(CultureInfo.InvariantCulture),
                Say.DeleteAll,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2, Reading(Say.Hebrew));
            if (answer != DialogResult.OK) return;

            string trouble = null;
            foreach (var path in files)
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                catch (Exception ex) { trouble = ex.Message; }
            }
            if (trouble != null)
                MessageBox.Show(this, Say.DeleteFailed + Environment.NewLine +
                                Environment.NewLine + trouble, Say.DeleteAll,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning,
                                MessageBoxDefaultButton.Button1, Reading(Say.Hebrew));
            Rebuild();
        }

        void BuildReading()
        {
            var title = Title(readingName, S(22));

            var back = new IconWidget { Glyph = IconWidget.Mark.Back, Mirror = Say.Hebrew };
            back.Announce(Say.Back, AccessibleRole.PushButton, "");
            back.Pressed += (s2, e2) => { startAtTop = true; page = Page.Transcripts; Rebuild(); };

            // The whole conversation, for somewhere else. It answers with a
            // tick, because a button that answers nothing is pressed twice.
            var copy = new IconWidget { Glyph = IconWidget.Mark.Copy };
            copy.Announce(Say.CopyAll, AccessibleRole.PushButton, "");
            copy.Pressed += (s2, e2) =>
            {
                try
                {
                    if (reading.Length == 0) return;
                    Clipboard.SetText(reading);
                    copy.Confirm();
                }
                catch { }
            };

            Beside(title, back, copy);

            /*  The conversation on a card, her turns and yours told apart.

                It was the file dropped whole into a borderless text box: one
                weight, one colour, the banner in the same type as the talking,
                the echo marker still down the left-hand side, and no space
                between turns. It read as a wall, and the wheel did not move
                it. */
            int wide = Math.Max(S(240), body.ClientSize.Width - S(Pad) * 2 - S(16));
            int inset = S(20);

            var card = new CardPanel
            {
                Zoom = scale,
                BackColor = Theme.Back,
                Bounds = new Rectangle(S(Pad), S(8), wide, S(160))
            };
            body.Controls.Add(card);

            /*  The direction of the conversation, not of the interface.

                A saved transcript records nothing about which script wrote it,
                so the reader took the shell's language -- and with the shell in
                Hebrew every English conversation was laid out right to left,
                which turns a question mark into the first character of the
                line. It cannot be answered from a stamp either: F2 switches
                script mid-session and one file can hold both languages. So it
                is answered by looking. */
            var text = new ReaderBox
            {
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                Font = Fonts.Get(10.5f, FontStyle.Regular),
                /*  Short to begin with. ContentsResized reports the height
                    the content needs but never less than the control already
                    is, so starting it tall left the card padded with empty
                    panel below the last line. */
                Bounds = new Rectangle(inset, inset, wide - inset * 2, S(24))
            };
            card.Controls.Add(text);

            // However tall the conversation turns out to be, once it is laid
            // out at the width it actually got.
            text.ContentsResized += (s2, e2) =>
            {
                text.Height = e2.NewRectangle.Height + S(6);
                card.Height = text.Height + inset * 2;
            };
            text.Fill(reading, MostlyHebrew(reading), scale);
        }

        // ---- settings ------------------------------------------------------

        void BuildPreferences()
        {
            Title(Say.Preferences, S(22));

            var strip = new TabsWidget
            {
                Labels = new[] { Say.General, Say.Appearance, Say.Talking },
                Chosen = settings.Tab,
                Zoom = scale,
                BackColor = Theme.Back,
                Bounds = new Rectangle(S(Pad), S(74), header.Width - S(Pad) * 2, S(46))
            };
            strip.Relayout();
            strip.Announce(Say.Preferences, AccessibleRole.PageTabList,
                           strip.Labels[strip.Chosen]);
            strip.Changed += (s2, e2) =>
            {
                settings.Tab = strip.Chosen;
                startAtTop = true;
                Keep2();
                /*  Back onto the strip, not onto nothing. Restore matches
                    on AccessibleName, so what is handed over has to be the
                    strip's name and not the label of the tab that was picked
                    -- which matched nothing, and left the keyboard on the
                    window exactly as before. */
                Later(strip.AccessibleName);
            };
            header.Controls.Add(strip);

            int w = Math.Max(S(240), body.ClientSize.Width - S(Pad) * 2 - S(16));
            int y = S(8);

            if (settings.Tab == 0)
            {
                var lang = new SegmentWidget
                {
                    Options = new[] { "עברית", "English" },
                    Chosen = Say.Hebrew ? 0 : 1
                };
                lang.Changed += (s2, e2) =>
                {
                    settings.Hebrew = lang.Chosen == 0;
                    Say.Hebrew = settings.Hebrew;
                    Theme.Mirrored = Say.Hebrew;
                    Keep2();
                    Later(Say.Language);
                };
                y = Row(ref y, w, Say.Language, Say.NoteLanguage, S(190), lang);

                var keep = new SwitchWidget { On = settings.KeepTranscripts };
                keep.Changed += (s2, e2) => { settings.KeepTranscripts = keep.On; Keep2(); };
                y = Row(ref y, w, Say.Keep, Say.NoteKeep, S(60), keep);

                /*  A dead control with no explanation is worse than no
                    control, so the note says which of the two it is. */
                bool canUpdate = settings.UpdateRepository.Length > 0;
                var up = new SwitchWidget
                {
                    On = canUpdate && settings.CheckUpdates,
                    Live = canUpdate
                };
                up.Changed += (s2, e2) =>
                {
                    settings.CheckUpdates = up.On;
                    Keep2();
                    if (up.On) LookForNew();
                };
                y = Row(ref y, w, Say.Updates,
                        canUpdate ? Say.NoteUpdates + " " + Say.Running + " " + Updater.Version
                                  : Say.NoRepository, S(60), up);

                /*  And the answer, in words, under the switch. Which version
                    is running, whether that is the newest, and a way to the
                    page if it is not. */
                if (canUpdate)
                {
                    bool there = Updater.Newest != null &&
                                 Updater.Newer(Updater.Newest, Updater.Version) &&
                                 Updater.Asset != null;
                    var look = new PushWidget { Label = there ? Say.GetNow : Say.LookNow };
                    look.Pressed += (s2, e2) =>
                    {
                        if (there) GetNewOne(look);
                        else LookForNew();
                    };
                    y = Row(ref y, w, Say.Newest, UpdateWord(), S(150), look);
                }

                y = Group(ref y, w, Say.More);

                var folder = new PushWidget { Label = Say.Open };
                folder.Pressed += (s2, e2) => OpenScriptFolder();
                y = Row(ref y, w, Say.Folder2, Say.NoteFolder, S(130), folder);

                var reset = new PushWidget { Label = Say.DoReset };
                reset.Pressed += (s2, e2) => AskThen(Say.Reset, Say.NoteReset, () =>
                {
                    settings.ResetToDefaults();
                    Say.Hebrew = settings.Hebrew;
                    Theme.Mirrored = Say.Hebrew;
                    Theme.Apply(settings.Mode, settings.Accent);
                    BackColor = Theme.Back;
                    settings.Tab = 0;
                    Rebuild();
                });
                y = Row(ref y, w, Say.Reset, Say.NoteReset, S(130), reset);

                var erase = new PushWidget { Label = Say.Delete, Danger = true };
                erase.Pressed += (s2, e2) => AskThen(Say.Erase, Say.EraseAsk, () =>
                {
                    /*  It says what happened. EraseEverything returns the
                        list of what it could not remove -- its own comment
                        explains why it does not stop at the first failure --
                        and the caller dropped it, so a transcript held open
                        by a text editor stayed on the disk and the program
                        said it was gone. */
                    var stayed = Settings.EraseEverything();
                    settings.Erased = true;
                    Rebuild();
                    MessageBox.Show(this,
                        stayed.Count == 0 ? Say.Erased
                            : Say.EraseLeft + Environment.NewLine + Environment.NewLine +
                              string.Join(Environment.NewLine, stayed.ToArray()),
                        Say.Erase, MessageBoxButtons.OK,
                        stayed.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1, Reading(Say.Hebrew));
                });
                y = Row(ref y, w, Say.Erase, Say.NoteErase, S(130), erase);
            }
            else if (settings.Tab == 1)
            {
                var mode = new SegmentWidget
                {
                    Options = Say.Themes,
                    Chosen = settings.Mode == "dark" ? 1 : settings.Mode == "light" ? 2 : 0
                };
                mode.Changed += (s2, e2) =>
                {
                    settings.Mode = mode.Chosen == 1 ? "dark"
                                  : mode.Chosen == 2 ? "light" : "system";
                    Theme.Apply(settings.Mode, settings.Accent);
                    BackColor = Theme.Back;
                    Keep2();
                    Later(Say.Theme);
                };
                y = Row(ref y, w, Say.Theme, Say.NoteTheme, S(260), mode);

                var colour = new SwatchWidget { Chosen = settings.Accent };
                colour.Changed += (s2, e2) =>
                {
                    settings.Accent = colour.Chosen;
                    Theme.Apply(settings.Mode, settings.Accent);
                    Keep2();
                    Later(Say.Accent);
                };
                y = Row(ref y, w, Say.Accent, Say.NoteAccent, S(230), colour);

                // Not Say.Talking: that is the name of the third tab, and a
                // group inside the second one called "השיחה" reads as a
                // second copy of it.
                y = Group(ref y, w, Say.TalkWindow);

                /*  The conversation's own look, out here as well as inside it.

                    It lived only on the cabinet, and somebody looking for it
                    in the settings did not find it and had no reason to think
                    of looking anywhere else. A setting that exists in one
                    place, and that place is not the settings, is a setting
                    most people never find. */
                var look = new SegmentWidget
                {
                    Options = Say.Screens,
                    Chosen = settings.ScreenStyle
                };
                look.Changed += (s2, e2) =>
                {
                    settings.ScreenStyle = look.Chosen;
                    Keep2();
                    Later(Say.Screen);
                };
                y = Row(ref y, w, Say.Screen, Say.NoteScreen, S(300), look);

                // The sheet of paper has no tube, so it is offered no colour.
                if (settings.ScreenStyle != 2)
                {
                    var tube = new SegmentWidget
                    {
                        Options = Say.Tubes,
                        Chosen = settings.Phosphor
                    };
                    tube.Changed += (s2, e2) => { settings.Phosphor = tube.Chosen; Keep2(); };
                    y = Row(ref y, w, Say.Tube, Say.NoteTube, S(230), tube);
                }

                var big = new StepperWidget { Value = settings.FontSize };
                big.Changed += (s2, e2) => { settings.FontSize = big.Value; Keep2(); };
                y = Row(ref y, w, Say.Size, Say.NoteSize, S(130), big);
            }
            else
            {
                var speed = new SegmentWidget { Options = Say.Speeds, Chosen = settings.Speed };
                speed.Changed += (s2, e2) => { settings.Speed = speed.Chosen; Keep2(); };
                y = Row(ref y, w, Say.Typing, Say.NoteTyping, S(270), speed);

                y = Group(ref y, w, Say.More);

                int order = settings.Order == "original" ? 0
                          : settings.Order == "shuffled" ? 2 : 1;
                var howChosen = new SegmentWidget { Options = Say.Orders, Chosen = order };
                howChosen.Changed += (s2, e2) =>
                {
                    settings.Order = howChosen.Chosen == 0 ? "original"
                                   : howChosen.Chosen == 2 ? "shuffled" : "varied";
                    Keep2();
                    Later(Say.Order);
                };
                y = Row(ref y, w, Say.Order, Say.OrderNotes[order], S(340), howChosen);

                int who = settings.Address == "m" ? 1 : settings.Address == "f" ? 2 : 0;
                var addressed = new SegmentWidget { Options = Say.Addresses, Chosen = who };
                addressed.Changed += (s2, e2) =>
                {
                    settings.Address = addressed.Chosen == 1 ? "m"
                                     : addressed.Chosen == 2 ? "f" : "auto";
                    Keep2();
                    Later(Say.Address);
                };
                y = Row(ref y, w, Say.Address, Say.AddressNotes[who], S(290), addressed);
            }
        }

        /*  Saving, and saying so when it does not work.

            It used to swallow the failure, so a read-only settings file meant
            every preference was forgotten on every run with nothing said --
            and "reset settings" reported success over a file it had not
            managed to write. */
        void Keep2()
        {
            if (settings.Save()) return;
            MessageBox.Show(this, Say.SaveFailed + Environment.NewLine + Environment.NewLine +
                            (settings.LastError ?? ""), "ELIZA",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button1, Reading(Say.Hebrew));
        }

        /*  Windows will lay a message box out right to left, mirror its
            buttons and right-align its text, but only if it is asked -- and
            nothing in the program asked. Every dialog in a Hebrew shell was
            left-aligned with the buttons the wrong way round, and the delete
            confirmation, which quotes a Hebrew date, asked you to confirm a
            scrambled one. */
        public static MessageBoxOptions Reading(bool hebrew)
        {
            return hebrew
                ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                : (MessageBoxOptions)0;
        }

        void AskThen(string title, string what, Action go)
        {
            var answer = MessageBox.Show(this, what, title,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2, Reading(Say.Hebrew));
            if (answer == DialogResult.OK) go();
        }

        int Row(ref int y, int w, string label, string note, int controlW, Control widget)
        {
            var row = new RowWidget
            {
                Label = label,
                Note = note,
                Zoom = scale,
                ControlWidth = controlW,
                Widget = widget
            };
            using (var g = body.CreateGraphics())
                row.Bounds = new Rectangle(S(Pad), y, w, row.Wants(g, w));
            row.Controls.Add(widget);
            if (widget is Widget)
            {
                var w2 = (Widget)widget;
                w2.Zoom = scale;

                // The label belongs to the control as far as anything not
                // looking at the screen is concerned: the two are drawn side
                // by side, but only one of them can be focused and read out.
                var role = widget is SwitchWidget ? AccessibleRole.CheckButton
                         : widget is PushWidget ? AccessibleRole.PushButton
                         : widget is StepperWidget ? AccessibleRole.SpinButton
                         : AccessibleRole.ComboBox;
                /*  Read into a local before anything is done with it: a
                    field on a Control is a field on a marshal-by-reference
                    class, and calling a method on one in place is what the
                    compiler warns about. */
                string state = "";
                if (widget is SwitchWidget)
                    state = ((SwitchWidget)widget).On ? Say.On : Say.Off;
                else if (widget is SegmentWidget)
                {
                    var seg = (SegmentWidget)widget;
                    if (seg.Chosen >= 0 && seg.Chosen < seg.Options.Length)
                        state = seg.Options[seg.Chosen];
                }
                else if (widget is StepperWidget)
                {
                    int at = ((StepperWidget)widget).Value;
                    state = at.ToString();
                }
                else if (widget is SwatchWidget)
                {
                    int at = ((SwatchWidget)widget).Chosen;
                    state = Say.Accents[Math.Min(Math.Max(0, at), Say.Accents.Length - 1)];
                }
                else if (widget is PushWidget)
                    state = ((PushWidget)widget).Label;
                w2.Announce(label, role, state);
                if (widget is SwitchWidget)
                {
                    var sw = (SwitchWidget)widget;
                    sw.OnWord = Say.On;
                    sw.OffWord = Say.Off;
                }
            }
            widget.BackColor = Theme.Panel;
            row.Place();
            body.Controls.Add(row);
            y = row.Bottom + S(8);
            return y;
        }

        int Group(ref int y, int w, string label)
        {
            var h = new HeadingWidget
            {
                Label = label,
                Zoom = scale,
                Bounds = new Rectangle(S(Pad), y, w, S(44))
            };
            body.Controls.Add(h);
            y = h.Bottom + S(2);
            return y;
        }

        // ---- doing things --------------------------------------------------

        void Converse(int index)
        {
            settings.Script = scripts[index].Name;
            settings.Save();

            Hide();
            try
            {
                using (var terminal = new MainForm(scripts, index, settings))
                {
                    terminal.ShowDialog();
                    if (settings.KeepTranscripts) Keep(terminal.Transcript);
                }
            }
            catch (Exception ex)
            {
                /*  A conversation that cannot start must not take the shell
                    with it. The script is a text file and the program invites
                    people to edit it, so the interesting case here is a file
                    that has just been changed by hand -- and the right end to
                    that is a sentence and a way back to this room, not a
                    program that is simply gone. */
                MessageBox.Show(this,
                    (Say.Hebrew
                        ? "לא ניתן היה לפתוח את השיחה:\n\n"
                        : "The conversation would not open:\n\n") + ex.Message,
                    "ELIZA", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, Reading(Say.Hebrew));
            }
            finally
            {
                // The language may have been changed from inside the terminal.
                Say.Hebrew = settings.Hebrew;
                Theme.Mirrored = Say.Hebrew;
                Rebuild();
                Show();
                Activate();
                // Refresh, not Invalidate: coming back from a hidden window the
                // repaint must happen now, or the room is drawn half torn.
                Redraw();
            }
        }

        /*  Asking GitHub what the newest release is called.

            The whole of the program's contact with the network is this, and
            it happens only when somebody has turned it on: no download, no
            install, and nothing sent about the machine. If there is a newer
            one the program says so and offers the page; the person does the
            rest, which is how a program that people carry on a stick should
            behave. */
        void LookForNew()
        {
            if (!settings.CheckUpdates) return;
            Updater.Trouble = null;
            Updater.Ask(settings.UpdateRepository, () =>
            {
                if (!IsHandleCreated || IsDisposed) return;
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (IsDisposed) return;
                        if (page == Page.Preferences) Rebuild();
                        else Invalidate();
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        /*  Fetching it and putting it in place, from inside the program.

            The file is downloaded next to the running one and not into the
            temporary folder, because the swap that follows is a rename and a
            rename across two volumes is a copy -- which is the one thing that
            cannot be done to a file that is in use. If the folder cannot be
            written to, which is what happens under Program Files, the
            download fails there and says so rather than half-installing. */
        void GetNewOne(PushWidget button)
        {
            if (Updater.Asset == null) return;
            var answer = MessageBox.Show(this,
                Say.AskUpdate + Environment.NewLine + Environment.NewLine + Updater.Newest,
                Say.Updates, MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1, Reading(Say.Hebrew));
            if (answer != DialogResult.OK) return;

            string beside = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath),
                "ELIZA-" + Updater.Newest + ".exe");

            button.Label = "0%";
            button.Enabled = false;
            button.Invalidate();

            Updater.Fetch(Updater.Asset, beside,
                percent =>
                {
                    if (!IsHandleCreated || IsDisposed) return;
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            if (button.IsDisposed) return;
                            button.Label = percent + "%";
                            button.Invalidate();
                        }));
                    }
                    catch (InvalidOperationException) { }
                },
                trouble =>
                {
                    if (!IsHandleCreated || IsDisposed) return;
                    try
                    {
                        BeginInvoke((Action)(() => Installed(beside, trouble)));
                    }
                    catch (InvalidOperationException) { }
                });
        }

        void Installed(string fetched, string trouble)
        {
            Updater.Note(trouble == null ? "downloaded " + fetched
                                         : "download failed: " + trouble);
            if (IsDisposed) return;
            if (trouble == null) trouble = Updater.Swap(fetched);
            if (trouble != null)
            {
                try { File.Delete(fetched); } catch { }
                MessageBox.Show(this,
                    Say.UpdateFailed + Environment.NewLine + Environment.NewLine + trouble,
                    Say.Updates, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, Reading(Say.Hebrew));
                Rebuild();
                return;
            }
            // The batch is waiting for this process to be gone.
            settings.Save();
            Application.Exit();
        }

        /*  What to say about it, in the settings. */
        string UpdateWord()
        {
            if (Updater.Newest == null)
                return Updater.Trouble == null ? Say.NoteLooking : Say.NoteNoAnswer;
            return Updater.Newer(Updater.Newest, Updater.Version)
                ? Say.NoteNewer + " " + Updater.Newest
                : Say.NoteCurrent;
        }

        [DllImport("user32.dll")]
        static extern bool RedrawWindow(IntPtr h, IntPtr rect, IntPtr region, uint flags);

        /*  The whole window, this instant, frame and children included.

            Refresh() was not enough, and the way it failed is worth writing
            down. This window paints itself: UserPaint and AllPaintingInWmPaint
            are set, so nothing erases the background for it, and whatever the
            paint does not cover keeps what was there before. It is also
            composited, so the form and every child are drawn into one buffer
            and presented together -- and coming back from Hide() that buffer
            is new and empty.

            Put those two together and a paint that arrives clipped to part of
            the window leaves the rest of it as a hole; and a hole in a
            composited window is not black, it is transparent. Somebody came
            out of a conversation and photographed his own desktop showing
            through the page: the cards and the panels were there, because
            those are child controls that paint themselves, and the title bar,
            the background and two of the five navigation rows were not,
            because those are the form's own painting and the form's update
            region did not include them.

            A null rectangle here means the entire window, and UPDATENOW means
            before this call returns. */
        void Redraw()
        {
            const uint RDW_INVALIDATE = 0x0001, RDW_ERASE = 0x0004,
                       RDW_ALLCHILDREN = 0x0080, RDW_UPDATENOW = 0x0100,
                       RDW_FRAME = 0x0400;
            if (!IsHandleCreated) { Refresh(); return; }
            RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero,
                RDW_INVALIDATE | RDW_ERASE | RDW_FRAME |
                RDW_ALLCHILDREN | RDW_UPDATENOW);
        }

        void Keep(string transcript)
        {
            // Only what someone actually said is worth keeping.
            if (transcript == null) return;
            var lines = transcript.Replace("\r", "").Split('\n');
            if (!lines.Any(l => l.StartsWith("·", StringComparison.Ordinal))) return;

            /*  InvariantCulture, and it matters. A date formatted in the
                machine's own culture follows the machine's calendar, and on a
                Hebrew system that comes back with gershayim in it -- a
                character a file name may not contain. The write then throws,
                and a swallowed exception makes it look as though nothing was
                ever said. */
            // Seconds as well: two conversations in the same minute would
            // otherwise share a name, and the second would erase the first.
            string name = DateTime.Now.ToString("yyyy-MM-dd HHmmss",
                                                CultureInfo.InvariantCulture) + ".txt";
            try
            {
                Directory.CreateDirectory(Settings.TranscriptFolder);
                File.WriteAllText(System.IO.Path.Combine(Settings.TranscriptFolder, name),
                                  transcript, new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                // Never lose it in silence: say so where it would have gone.
                try
                {
                    File.WriteAllText(
                        System.IO.Path.Combine(Settings.Folder, "transcript-error.txt"),
                        name + Environment.NewLine + ex, new UTF8Encoding(true));
                }
                catch { }
            }
        }

        static string[] TranscriptFiles()
        {
            try
            {
                if (!Directory.Exists(Settings.TranscriptFolder)) return new string[0];
                var files = Directory.GetFiles(Settings.TranscriptFolder, "*.txt");
                Array.Sort(files, (a, b) => string.CompareOrdinal(b, a));
                return files;
            }
            catch { return new string[0]; }
        }

        /*  A saved conversation, named the way a person would name it.

            The file has to be called something a filesystem will accept in any
            language, so it is stamped "2026-09-11 065125" in the invariant
            culture -- a date formatted in the machine's own culture produces
            gershayim on a Hebrew system, which is not a legal filename, and
            that is how transcripts once failed to save at all, in silence.

            But that stamp is a filename, not a title. Read it back and show
            the date the way the reader's language writes dates. */
        static string Describe(string path)
        {
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            DateTime when;
            if (DateTime.TryParseExact(stem, "yyyy-MM-dd HHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out when))
            {
                var culture = Say.Hebrew
                    ? CultureInfo.GetCultureInfo("he-IL")
                    : CultureInfo.GetCultureInfo("en-GB");
                return when.ToString("d MMMM yyyy, HH:mm", culture);
            }
            return stem;
        }

        /*  Which way a piece of text runs, by counting its letters. */
        static bool MostlyHebrew(string text)
        {
            int hebrew = 0, latin = 0;
            foreach (char c in text)
            {
                if (c >= '\u0590' && c <= '\u05FF') hebrew++;
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) latin++;
            }
            return hebrew >= latin;
        }

        void OpenTranscript(string path)
        {
            try { reading = File.ReadAllText(path, Encoding.UTF8); }
            catch { reading = ""; }
            readingName = Describe(path);
            startAtTop = true;
            page = Page.Reading;
            Rebuild();
        }

        void OpenScriptFolder()
        {
            try
            {
                string folder = System.IO.Path.Combine(Settings.Folder, "scripts");
                if (Directory.Exists(folder))
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch { }
        }

        // ---- mouse, keys, window -------------------------------------------

        bool Hovering(Rectangle r) { return r.Contains(PointToClient(MousePosition)); }

        Rectangle hovered;             // what the pointer was last over

        /*  Only what changed, and only when it changes.

            It invalidated the whole window on every WM_MOUSEMOVE. On the
            opening screen that redrew the title bar, the nav strip, the whole
            drawing and every card at the rate the mouse moves -- and each of
            those paints built fonts and leaked regions. Hotspots are rebuilt
            on every paint, so they are compared by the rectangle rather than
            by reference. */
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var under = hotspots.LastOrDefault(h => h.Where.Contains(e.Location));
            Cursor = under != null ? Cursors.Hand : Cursors.Default;

            var box = under == null ? Rectangle.Empty : under.Where;
            if (box == hovered) return;
            if (!hovered.IsEmpty) Invalidate(Rectangle.Inflate(hovered, S(3), S(3)));
            if (!box.IsEmpty) Invalidate(Rectangle.Inflate(box, S(3), S(3)));
            hovered = box;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (!hovered.IsEmpty)
            {
                Invalidate(Rectangle.Inflate(hovered, S(3), S(3)));
                hovered = Rectangle.Empty;
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Only the left button. A right-click used to work every control
            // in the window, which is wrong everywhere and was dangerous on
            // the rows that destroy something.
            if (e.Button != MouseButtons.Left) return;

            /*  Remembered, not run. A hotspot used to fire the instant the
                button went down, so a press that started on Close and slid
                away still closed the program. Every control on Windows
                commits on release over the same target, and that is what
                makes a mis-aimed click something you can take back. */
            pressed = hotspots.LastOrDefault(h => h.Where.Contains(e.Location));
            if (pressed != null) return;
            // Dragging the bar is Windows' job now: it answers HTCAPTION,
            // and with it come snapping, the preview and the keyboard move.
        }

        Hotspot pressed;

        protected override void OnMouseUp(MouseEventArgs e)
        {
            var was = pressed;
            pressed = null;
            if (was != null && e.Button == MouseButtons.Left &&
                was.Where.Contains(e.Location))
                was.Do();
        }

        /*  The wheel, the keyboard and the scrollbar all belong to the panel
            now, which is the whole reason the pages were moved into one. What
            is left here is the window, and a window does not scroll. */

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // The pages by number, and the settings where every program on
            // this machine keeps them.
            if ((keyData & Keys.Control) == Keys.Control)
            {
                switch (keyData & Keys.KeyCode)
                {
                    case Keys.D1: return Go(Page.Home);
                    case Keys.D2: return Go(Page.Models);
                    case Keys.D3: return Go(Page.About);
                    case Keys.D4: return Go(Page.Transcripts);
                    case Keys.D5: case Keys.Oemcomma: return Go(Page.Preferences);
                }
            }

            if (page == Page.Home)
            {
                if (keyData == Keys.Enter) { Converse(0); return true; }
                for (int i = 0; i < scripts.Count && i < 9; i++)
                    if (keyData == Keys.D1 + i || keyData == Keys.NumPad1 + i)
                    {
                        Converse(i);
                        return true;
                    }
            }

            if (keyData == Keys.Escape)
            {
                if (page == Page.Reading) return Go(Page.Transcripts);
                if (page != Page.Home) return Go(Page.Home);
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        bool Go(Page where)
        {
            startAtTop = page != where;
            page = where;
            Rebuild();
            return true;
        }

        /*  Turning high contrast on is a thing people do while a program is
            already open -- that is what the keyboard shortcut is for -- and a
            window that only reads the setting at startup answers the request
            by staying exactly as unreadable as it was. */
        void SystemColoursChanged(object sender,
                                  Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            var what = e.Category;
            if (what != Microsoft.Win32.UserPreferenceCategory.Color &&
                what != Microsoft.Win32.UserPreferenceCategory.Accessibility &&
                what != Microsoft.Win32.UserPreferenceCategory.VisualStyle &&
                what != Microsoft.Win32.UserPreferenceCategory.General) return;
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed) return;
                Theme.Apply(settings.Mode, settings.Accent);
                BackColor = Theme.Back;
                Rebuild();
                Redraw();
            }));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            settings.Save();
            base.OnFormClosing(e);
        }

        /*  SystemEvents keeps its handlers in a static list of its own, so a
            window that forgets to take its own out of it is kept alive by it
            for the life of the process, and is handed events after it has
            been disposed. */
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemColoursChanged;
            base.Dispose(disposing);
        }

        /*  Where a maximised borderless window is allowed to reach.

            It was set inside the caption button's own handler, so the button
            put the window in the right place and Windows' three other routes
            to the same state -- drag to the top, Win+Up, double-click the
            caption -- covered the taskbar. And the value went stale the moment
            the window was moved to another monitor. */
        protected override void OnMove(EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
            base.OnMove(e);
        }

        /*  A borderless form is given no system menu.

            WinForms adds WS_SYSMENU only when FormBorderStyle is not None, so
            this window had none -- measured, GetWindowLong returns 0x16010000
            -- and a window with no system menu gets no caption-button
            tracking from DefWindowProc, no Alt+Space, no Close on the
            taskbar's right-click, and no snap-layouts flyout over a maximise
            button however it answers the hit test. */
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= unchecked((int)0x00080000)    // WS_SYSMENU
                          | unchecked((int)0x00020000)    // WS_MINIMIZEBOX
                          | unchecked((int)0x00010000);   // WS_MAXIMIZEBOX

                /*  WS_EX_COMPOSITED, on the window rather than on one panel
                    inside it.

                    Changing page takes between 24 and 251 milliseconds --
                    About is the slow one, because it measures a ten-thousand
                    pixel document -- and the form and its two panels were
                    repainting in separate passes, so what was on the screen
                    during those milliseconds was a header from the new page
                    over a body from the old one. Measured with screen
                    captures 25ms apart: four to ten frames per page change
                    showing a page that does not exist.

                    Composited, the form and every child are drawn off-screen
                    and presented together, and the window goes from one whole
                    page to the next. Measured the same way afterwards: none. */
                cp.ExStyle |= unchecked((int)0x02000000);
                return cp;
            }
        }

        const int WM_NCHITTEST = 0x0084;
        /*  0x00A2, and the whole of the close button turned on it.

            It was written 0x00A5, which is WM_NCRBUTTONUP -- the RIGHT button
            released over the non-client area. So the handler waiting for a
            release waited for a message a left-click never sends, and the
            only gesture that closed the program was a right-click on the X.
            Which nobody does. */
        const int WM_NCLBUTTONDOWN = 0x00A1, WM_NCLBUTTONUP = 0x00A2;
        const int WM_NCMOUSEMOVE = 0x00A0, WM_NCMOUSELEAVE = 0x02A2;

        int pressedButton;             // which caption button the press landed on
        int hotButton;                 // and which one the pointer is over
        const int HTCLIENT = 1, HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12,
                  HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15,
                  HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17,
                  HTMINBUTTON = 8, HTMAXBUTTON = 9, HTCLOSE = 20;

        /*  Telling Windows where the caption is, instead of dragging by hand.

            The bar was drawn and then moved with a hand-rolled drag, and a
            window that never answers HTCAPTION is not, as far as Windows is
            concerned, a window with a title bar: no snap to an edge, no snap
            preview, no drag to the top to maximise, no shake, no Alt+Space and
            no move from the keyboard. None of it looks missing until somebody
            tries. Answering the hit test gives all of it back, and reporting
            HTMAXBUTTON over the maximise button is what makes the snap-layouts
            flyout appear over it the way it does in every other program. */
        protected override void WndProc(ref Message m)
        {
            /*  The caption buttons, here rather than in DefWndProc.

                They were handled by waiting for WM_NCLBUTTONUP to arrive
                through DefWndProc, which assumed DefWindowProc would track
                the press and send one. On a window with no system menu it
                does not, and it did not: the button lit, and the program
                did not close. Taking the press ourselves -- and not passing
                it on, so nothing else starts tracking it -- makes the release
                arrive as an ordinary message.

                Still on the release, and still only if the release is over
                the same button, so a press that lands on Close and slides
                away is a press you can take back. */
            /*  The pointer over a caption button.

                The hover fill is worked out while painting, from where the
                mouse is -- and the only thing that asked for a repaint when
                the mouse moved was OnMouseMove, which is raised for the
                CLIENT area. Over the title bar the hit test answers HTCLOSE
                or HTCAPTION, so the window gets WM_NCMOUSEMOVE and never
                WM_MOUSEMOVE: measured, the pointer parked on the close button
                for more than a second left the pixel under it at exactly the
                colour it has when the pointer is elsewhere. The button gave
                no sign it was a button at all. */
            if (m.Msg == WM_NCMOUSEMOVE || m.Msg == WM_NCMOUSELEAVE)
            {
                var q = PointToClient(new Point(
                    (short)((long)m.LParam & 0xFFFF),
                    (short)(((long)m.LParam >> 16) & 0xFFFF)));
                int over = m.Msg == WM_NCMOUSELEAVE ? 0
                    : closeButton.Contains(q) ? HTCLOSE
                    : maxButton.Contains(q) ? HTMAXBUTTON
                    : minButton.Contains(q) ? HTMINBUTTON : 0;
                if (over != hotButton)
                {
                    hotButton = over;
                    Invalidate(new Rectangle(0, 0, ClientSize.Width, S(Bar)));
                }
            }

            if (m.Msg == WM_NCLBUTTONDOWN)
            {
                int hit = (int)m.WParam;
                if (hit == HTCLOSE || hit == HTMINBUTTON || hit == HTMAXBUTTON)
                {
                    pressedButton = hit;
                    Invalidate();
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == WM_NCLBUTTONUP)
            {
                int hit = (int)m.WParam;
                int was = pressedButton;
                pressedButton = 0;
                Invalidate();
                if (was != 0 && was == hit)
                {
                    CaptionButton(hit);
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == WM_NCHITTEST)
            {
                var q = PointToClient(new Point(
                    (short)((long)m.LParam & 0xFFFF),
                    (short)(((long)m.LParam >> 16) & 0xFFFF)));

                if (closeButton.Contains(q)) { m.Result = (IntPtr)HTCLOSE; return; }
                if (maxButton.Contains(q)) { m.Result = (IntPtr)HTMAXBUTTON; return; }
                if (minButton.Contains(q)) { m.Result = (IntPtr)HTMINBUTTON; return; }
            }

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                var p = PointToClient(new Point(
                    (short)((long)m.LParam & 0xFFFF),
                    (short)(((long)m.LParam >> 16) & 0xFFFF)));
                int grip = S(Grip);
                bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
                bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;

                int hit = HTCLIENT;
                if (top && left) hit = HTTOPLEFT;
                else if (top && right) hit = HTTOPRIGHT;
                else if (bottom && left) hit = HTBOTTOMLEFT;
                else if (bottom && right) hit = HTBOTTOMRIGHT;
                else if (left) hit = HTLEFT;
                else if (right) hit = HTRIGHT;
                else if (top) hit = HTTOP;
                else if (bottom) hit = HTBOTTOM;

                if (hit != HTCLIENT) { m.Result = (IntPtr)hit; return; }
            }

            // The rest of the bar is the caption, and Windows can have it.
            if (m.Msg == WM_NCHITTEST)
            {
                var q = PointToClient(new Point(
                    (short)((long)m.LParam & 0xFFFF),
                    (short)(((long)m.LParam >> 16) & 0xFFFF)));
                if (q.Y >= 0 && q.Y < S(Bar) && q.X >= 0 && q.X < ClientSize.Width)
                {
                    m.Result = (IntPtr)HTCAPTION;
                    return;
                }
            }

            base.WndProc(ref m);
        }

        void CaptionButton(int which)
        {
            if (which == HTCLOSE) { Close(); return; }
            if (which == HTMINBUTTON) { WindowState = FormWindowState.Minimized; return; }
            if (which == HTMAXBUTTON)
            {
                /*  A borderless form maximises to the whole monitor, not to
                    the part of it that is not the taskbar, so maximising hid
                    the taskbar under the window. */
                if (WindowState != FormWindowState.Maximized)
                    MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
            }
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
