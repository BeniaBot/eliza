// Ui.cs -- a 1960s terminal, drawn from scratch.
//
// A painted plastic cabinet around a phosphor screen: scan lines, a soft
// bloom, a blinking block cursor, and a teletype that can run at ten
// characters a second or as fast as the machine can paint.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ElizaApp
{
    // Paper is not a phosphor at all: ink on a warm page, the way the
    // conversation looked at the Bloomfield Science Museum in 2010.
    enum Phosphor { Green, Amber, White, Paper }

    /*  What the machine around the screen is.

        Not a colour -- a colour is what the tube is made of. These are three
        different objects on three different desks:

          Terminal  a flat dark surround and nothing else, the way a terminal
                    window looks now. No cabinet, no scanlines, no engraving.
          Machine   the console of 1966: moulded plastic, a recessed tube,
                    the maker's name pressed into the case, a power lamp.
          Museum    paper. The Bloomfield Science Museum showed her in 2010
                    as a drawing rather than a machine, and there is something
                    right about that: what she is made of is not electronics.
                    The drawing here is this program's own.

        The choice lives in this window and not in the shell, because it is
        not a preference about the program -- it is the room you are sitting
        in while you talk to her, and you change it from inside the room. */
    /*  The third one used to be the museum, which is one script's page.
        With more than one script that has a character of its own it is "in
        character", and which character is the script's business: it says so
        with (SCENE ...) and Scenes.cs draws it. The stored number does not
        change, so a settings file written by an older copy still means what
        it meant. */
    enum ScreenStyle { Terminal, Machine, Tailored }
    enum TypingSpeed { Instant, Fast, Teletype }

    /*  A font that carries how it wants to be drawn.

        Every line on the screen is drawn with TextRenderer, which goes
        through GDI -- and GDI does not read GDI+'s TextRenderingHint. So
        setting the hint on the Graphics, which is what this program did,
        changed nothing at all: the terminal, the museum's page and the 1966
        console all came out as the same aliased bitmap type, and the
        difference the code claimed to make was not on the screen.

        GDI's own switch is lfQuality, a field on the logical font. */
    static class Typeface
    {
        public const byte Aliased = 3;         // NONANTIALIASED_QUALITY
        public const byte Smooth = 5;          // CLEARTYPE_QUALITY

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class LogFont
        {
            public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
            public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet;
            public byte lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName = "";
        }

        public static Font At(string family, float points, byte quality)
        {
            var made = new Font(family, points);
            try
            {
                var lf = new LogFont();
                made.ToLogFont(lf);
                lf.lfQuality = quality;
                var asked = Font.FromLogFont(lf);
                made.Dispose();
                return asked;
            }
            catch { return made; }      // a face that will not round-trip
        }
    }

    class Palette
    {
        public Color Bright, Normal, Dim, User, Screen;

        /*  What the line you are typing sits in. On a tube it is the screen
            itself -- there is nowhere else for it to be. In the terminal it
            is a field lifted off the ground, the way every editor and every
            terminal has drawn its prompt for fifteen years. */
        Color field;
        public Color Field
        {
            get { return field.A == 0 ? Screen : field; }
            set { field = value; }
        }

        /*  Two of the three screen styles were drawing the same thing.

            The terminal and the console of 1966 shared one palette, so the
            only difference between them was a moulded surround and a lamp:
            side by side, nobody could say which was which, and the terminal
            was the 1966 console with the furniture taken away rather than a
            thing of its own.

            A terminal today is not a dim phosphor behind glass. It is a flat
            slate ground, text that does not glow and is not aliased, and a
            colour scheme rather than a tube. So the terminal gets one of
            those, and the phosphor -- which is now what makes the console
            1966 -- stays where it belongs. */
        public static Palette For(Phosphor p, bool flat)
        {
            /*  And in a high contrast scheme, none of them. A phosphor is a
                colour chosen for how it looked on a tube in 1966, and somebody
                running Windows in high contrast has said that the only colours
                they can read are these ones. The screen is where this program
                actually happens, so leaving it in green while the rest of the
                window obeys would be obeying the letter of the thing and none
                of the point.

                Which of them carries what: the window colour is the screen,
                the window text is what is written on it, the grey text is the
                quiet tier -- defined and legible in every high contrast
                scheme, unlike a grey of our own -- and the highlight is what
                the person typed, so their own words still stand apart from
                hers. */
            if (Theme.Contrast)
                return new Palette
                {
                    Bright = SystemColors.WindowText,
                    Normal = SystemColors.WindowText,
                    Dim = SystemColors.GrayText,
                    User = SystemColors.Highlight,
                    Screen = SystemColors.Window
                };

            return flat && p != Phosphor.Paper ? Flat(p) : For(p);
        }

        static Palette Flat(Phosphor p)
        {
            switch (p)
            {
                case Phosphor.Amber:
                    return new Palette
                    {
                        Bright = Color.FromArgb(255, 209, 138),
                        Normal = Color.FromArgb(231, 167, 76),
                        Dim = Color.FromArgb(150, 138, 118),
                        User = Color.FromArgb(240, 236, 230),
                        Screen = Color.FromArgb(19, 18, 16),
                        Field = Color.FromArgb(31, 29, 25)
                    };
                case Phosphor.White:
                    return new Palette
                    {
                        Bright = Color.FromArgb(255, 255, 255),
                        Normal = Color.FromArgb(201, 209, 217),
                        Dim = Color.FromArgb(139, 148, 158),
                        User = Color.FromArgb(121, 192, 255),
                        Screen = Color.FromArgb(13, 17, 23),
                        Field = Color.FromArgb(22, 27, 34)
                    };
                default:
                    return new Palette
                    {
                        Bright = Color.FromArgb(165, 243, 176),
                        Normal = Color.FromArgb(86, 211, 100),
                        Dim = Color.FromArgb(125, 133, 144),
                        User = Color.FromArgb(230, 237, 243),
                        Screen = Color.FromArgb(13, 17, 23),
                        Field = Color.FromArgb(22, 27, 34)
                    };
            }
        }

        public static Palette For(Phosphor p)
        {
            switch (p)
            {
                case Phosphor.Amber:
                    return new Palette
                    {
                        Bright = Color.FromArgb(255, 214, 130),
                        Normal = Color.FromArgb(255, 176, 0),
                        Dim = Color.FromArgb(188, 132, 12),
                        User = Color.FromArgb(255, 236, 190),
                        Screen = Color.FromArgb(20, 13, 4)
                    };
                case Phosphor.White:
                    return new Palette
                    {
                        Bright = Color.FromArgb(255, 255, 255),
                        Normal = Color.FromArgb(208, 216, 224),
                        Dim = Color.FromArgb(150, 158, 166),
                        User = Color.FromArgb(255, 255, 255),
                        Screen = Color.FromArgb(10, 12, 14)
                    };
                /*  The Bloomfield Science Museum's Hebrew ELIZA, measured
                    rather than imagined.

                    Eran Hadas built it in 2010-11 and it was shown in the
                    CAPTCHA exhibition from November 2012. The site is long
                    dead, but a capture of it survives in the Internet
                    Archive, and these are its own numbers: white ground,
                    20px Courier New, and text in pure blue -- #0000FF, the
                    unmixed blue of a default HTML link, which nobody would
                    choose today and which is exactly why the thing looked the
                    way it did.

                    What is not taken is the drawing. The room behind the
                    chat was a therapist's office in coloured pencil, and it
                    is an artist's work; the pencil lines here are this
                    program's own. A palette is not a picture. */
                case Phosphor.Paper:
                    return new Palette
                    {
                        Bright = Color.FromArgb(0, 0, 255),
                        Normal = Color.FromArgb(0, 0, 255),
                        Dim = Color.FromArgb(86, 86, 190),
                        User = Color.FromArgb(0, 0, 160),
                        Screen = Color.FromArgb(255, 255, 255)
                    };
                default:
                    return new Palette
                    {
                        Bright = Color.FromArgb(186, 255, 186),
                        Normal = Color.FromArgb(64, 240, 110),
                        /*  The banner, the whole of F1, every mode note and
                            the trace are written in this. It was 4.0:1 on its
                            own and 2.2:1 under the vignette at the foot of the
                            screen -- which is where the only written record of
                            any keyboard shortcut in the program lives. */
                        Dim = Color.FromArgb(58, 186, 96),
                        User = Color.FromArgb(206, 255, 214),
                        Screen = Color.FromArgb(6, 18, 9)
                    };
            }
        }
    }

    // ------------------------------------------------------------------

    class ScreenLine
    {
        public string Text = "";
        public int Style;          // 0 machine, 1 user, 2 dim, 3 bright

        /*  Chatter: on the screen, and not in the record.

            F1's help page, the note F4 prints when the style changes and the
            whole of F6's trace all went through the same list the transcript
            is read from, so a saved conversation had the help text sitting in
            the middle of what somebody had said. The banner is written in the
            same colour and does belong in the record, so the flag cannot be
            the colour. */
        public bool Aside;
    }

    class CrtScreen : Control
    {
        readonly List<ScreenLine> lines = new List<ScreenLine>();
        readonly List<ScreenLine> wrapped = new List<ScreenLine>();

        Palette palette = Palette.For(Phosphor.Green);
        Font font;
        int charWidth = 8, lineHeight = 16;
        int scrollOffset;                  // lines scrolled back from the end

        readonly Queue<ScreenLine> pending = new Queue<ScreenLine>();
        ScreenLine typing;
        string typingFull = "";
        int typingShown;
        readonly Timer typeTimer = new Timer();
        TypingSpeed speed = TypingSpeed.Fast;

        readonly Timer blinkTimer = new Timer();
        bool cursorOn = true;

        public bool Mirrored;              // the transcript's own direction
        Rectangle caret;                   // where the block cursor last was
        public event EventHandler TypingFinished;

        public CrtScreen()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            /*  The screen is a thing to read, not a thing to focus.

                A plain Control is selectable and in the tab order by default,
                so clicking anywhere on the conversation moved the focus off
                the input box onto a control with no key handling at all: the
                caret vanished and everything typed after that went nowhere.
                Clicking what you are reading is not an unusual thing to do. */
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;

            // Never transparent: that makes GDI paint the parent underneath too.
            BackColor = palette.Screen;

            typeTimer.Tick += (s, e) => TypeTick();
            blinkTimer.Interval = 500;
            /*  Only the caret's own line. Invalidating the whole control
                twice a second rebuilt the tube twice a second, forever, on an
                idle window. */
            blinkTimer.Tick += (s, e) =>
            {
                cursorOn = !cursorOn;
                /*  The caret's own rectangle, and only while there is one.

                    The else branch repainted the whole control -- every line
                    of text and the whole tube bitmap -- twice a second for as
                    long as the window was open, on a window where nothing was
                    happening. The caret is only ever drawn while she is
                    typing, so when she is not there is nothing to blink. */
                if (caret.Width > 0) Invalidate(Rectangle.Inflate(caret, 2, 2));
            };
            ApplySpeed();
        }

        // And a click on it hands the keyboard back to whatever wants it.
        public event EventHandler Poked;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Poked != null)
                Poked(this, EventArgs.Empty);
            base.OnMouseDown(e);
        }

        bool announceDue;

        public bool Busy { get { return typing != null || pending.Count > 0; } }

        public Palette Colours
        {
            get { return palette; }
            set { palette = value; BackColor = palette.Screen; Invalidate(); }
        }

        public TypingSpeed Speed
        {
            get { return speed; }
            set { speed = value; ApplySpeed(); }
        }

        void ApplySpeed()
        {
            typeTimer.Interval = speed == TypingSpeed.Teletype ? 100 : 15;
        }

        public void SetFont(Font f)
        {
            var old = font;
            font = f;
            using (var g = CreateGraphics())
            {
                var size = TextRenderer.MeasureText(g, new string('M', 20), font,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                charWidth = Math.Max(1, (int)Math.Round(size.Width / 20.0));
                lineHeight = size.Height + 2;
            }
            if (old != null) old.Dispose();
            Rewrap();
            Invalidate();
        }

        public int Columns
        {
            get { return Math.Max(20, (ClientSize.Width - 24) / charWidth); }
        }

        // ---- content ---------------------------------------------------

        /*  What is on the screen, for anything that is not looking at it.

            Every word of this program's output is painted glyphs: the banner,
            everything she says, everything you said back, the whole of the
            help page. To a screen reader the control was an empty rectangle.
            It cannot be selected either, so there was no way to reach the text
            at all without a pair of eyes.

            The name says what the control is; the description carries what is
            in it, kept current as lines arrive. A reader that inspects the
            control now finds the conversation. */
        void Announce()
        {
            AccessibleRole = AccessibleRole.Text;
            var sb = new StringBuilder();
            int from = Math.Max(0, lines.Count - 40);
            for (int i = from; i < lines.Count; i++)
                if (lines[i].Text.Length > 0) sb.AppendLine(lines[i].Text);
            AccessibleDescription = sb.ToString();
        }

        public void Clear()
        {
            lines.Clear();
            pending.Clear();
            typing = null;
            typeTimer.Stop();
            scrollOffset = 0;
            Rewrap();
            Invalidate();
        }

        public void AddInstant(string text, int style) { AddInstant(text, style, false); }

        public void AddInstant(string text, int style, bool aside)
        {
            announceDue = true;
            lines.Add(new ScreenLine { Text = text, Style = style, Aside = aside });
            scrollOffset = 0;
            Rewrap();
            Invalidate();
        }

        public void AddTyped(string text, int style)
        {
            pending.Enqueue(new ScreenLine { Text = text, Style = style });
            if (typing == null) StartNext();
        }

        void StartNext()
        {
            if (pending.Count == 0)
            {
                typing = null;
                typeTimer.Stop();
                blinkTimer.Stop();
                if (caret.Width > 0) Invalidate(Rectangle.Inflate(caret, 2, 2));
                var h = TypingFinished;
                if (h != null) h(this, EventArgs.Empty);
                return;
            }

            blinkTimer.Start();
            var next = pending.Dequeue();
            typingFull = next.Text;
            typingShown = 0;
            typing = new ScreenLine { Text = "", Style = next.Style };
            lines.Add(typing);
            scrollOffset = 0;

            if (speed == TypingSpeed.Instant)
            {
                typing.Text = typingFull;
                typing = null;
                Rewrap();
                Invalidate();
                StartNext();
                return;
            }
            typeTimer.Start();
        }

        void TypeTick()
        {
            if (typing == null) { typeTimer.Stop(); return; }

            int step = speed == TypingSpeed.Teletype ? 1 : 4;
            typingShown = Math.Min(typingFull.Length, typingShown + step);
            typing.Text = typingFull.Substring(0, typingShown);
            Rewrap();
            Invalidate();

            if (typingShown >= typingFull.Length)
            {
                typing = null;
                typeTimer.Stop();
                StartNext();
            }
        }

        public void FinishTyping()
        {
            announceDue = true;
            if (!Busy) return;
            if (typing != null)
            {
                typing.Text = typingFull;
                typing = null;
            }
            typeTimer.Stop();
            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                lines.Add(new ScreenLine { Text = next.Text, Style = next.Style });
            }
            Rewrap();
            Invalidate();
            var h = TypingFinished;
            if (h != null) h(this, EventArgs.Empty);
        }

        public string Transcript()
        {
            var sb = new StringBuilder();
            foreach (var l in lines) if (!l.Aside) sb.AppendLine(l.Text);
            return sb.ToString();
        }

        // ---- wrapping --------------------------------------------------

        void Rewrap()
        {
            wrapped.Clear();
            int cols = Columns;
            foreach (var line in lines)
            {
                if (line.Text.Length == 0)
                {
                    wrapped.Add(new ScreenLine { Text = "", Style = line.Style });
                    continue;
                }
                foreach (var piece in WrapOne(line.Text, cols))
                    wrapped.Add(new ScreenLine { Text = piece, Style = line.Style });
            }
        }

        static IEnumerable<string> WrapOne(string text, int cols)
        {
            var cur = new StringBuilder();
            foreach (var word in text.Split(' '))
            {
                string w = word;
                while (w.Length > cols)
                {
                    if (cur.Length > 0) { yield return cur.ToString(); cur.Length = 0; }
                    yield return w.Substring(0, cols);
                    w = w.Substring(cols);
                }
                if (cur.Length == 0) cur.Append(w);
                else if (cur.Length + 1 + w.Length <= cols) cur.Append(' ').Append(w);
                else { yield return cur.ToString(); cur.Length = 0; cur.Append(w); }
            }
            yield return cur.ToString();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Rewrap();
        }

        // ---- scrolling -------------------------------------------------

        int VisibleLines { get { return Math.Max(1, (ClientSize.Height - 16) / lineHeight); } }

        public void ScrollBy(int delta)
        {
            int max = Math.Max(0, wrapped.Count - VisibleLines);
            int was = scrollOffset;
            scrollOffset = Math.Min(max, Math.Max(0, scrollOffset + delta));
            if (scrollOffset != was) Invalidate();
        }

        // ---- painting --------------------------------------------------

        Color ColourFor(int style)
        {
            switch (style)
            {
                case 1: return palette.User;
                case 2: return palette.Dim;
                case 3: return palette.Bright;
                default: return palette.Normal;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (announceDue) { announceDue = false; Announce(); }

            var g = e.Graphics;
            g.Clear(palette.Screen);
            if (font == null) return;

            /*  Nothing set here. TextRenderer draws through GDI, which does
                not read this hint; the aliasing is chosen where the font is
                built, in Typeface.At, and only the 1966 console asks for the
                aliased one. */

            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            if (Mirrored) flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;

            int visible = VisibleLines;
            int first = Math.Max(0, wrapped.Count - visible - scrollOffset);
            int last = Math.Min(wrapped.Count, first + visible);

            int y = 8, left = 12, width = ClientSize.Width - 24;

            for (int i = first; i < last; i++)
            {
                var line = wrapped[i];
                if (line.Text.Length > 0)
                {
                    var rect = new Rectangle(left, y, width, lineHeight);
                    var c = ColourFor(line.Style);

                    /*  There was a "cheap bloom" here: the same glyphs a
                        pixel down and right, at alpha 70, to suggest a
                        phosphor glow.

                        It never did. TextRenderer throws a colour's alpha
                        away, so the dim copy was drawn at full strength and
                        every line on the screen was a hard double image --
                        measured at 240 in the brightest channel either way.
                        Drawing it through GDI+ instead makes the alpha real
                        and the positions wrong, because GDI+ and GDI lay
                        glyphs out differently and the ghost no longer sits
                        under the letters it belongs to.

                        A real glow means rendering the line to a bitmap and
                        compositing it twice, which is a great deal of work per
                        frame for an effect nobody ever actually saw. The
                        scanlines and the vignette carry the tube on their own. */
                    TextRenderer.DrawText(g, line.Text, font, rect, c, flags);
                }
                y += lineHeight;
            }

            if (cursorOn && scrollOffset == 0 && Busy && last > first)
            {
                int cy = 8 + (last - first - 1) * lineHeight;
                int len = wrapped[wrapped.Count - 1].Text.Length;
                int cx = Mirrored
                    ? left + width - (len + 1) * charWidth
                    : left + len * charWidth;
                cx = Math.Max(left, Math.Min(left + width - charWidth, cx));

                // Remembered, so the blink can repaint this and nothing else.
                caret = new Rectangle(cx, cy + 2, charWidth, lineHeight - 5);
                using (var b = new SolidBrush(Color.FromArgb(190, palette.Bright)))
                    g.FillRectangle(b, caret);
            }
            else caret = Rectangle.Empty;

            /*  How far back you have scrolled.

                It used to be written at a fixed offset from the right edge, on
                top of the first visible line -- which in a right-to-left
                transcript is every time, because the text is right-aligned and
                always reaches that far. The offsets were raw pixels against a
                font that scales with the screen, so at 150 per cent the number
                ran off the edge as well.

                Measured, and on its own patch of screen. */
            if (scrollOffset > 0)
            {
                string mark = "▲ " + scrollOffset;
                using (var f = new Font("Consolas", 8f * (lineHeight / 16f)))
                {
                    var size = TextRenderer.MeasureText(g, mark, f);
                    var box = new Rectangle(
                        Mirrored ? 6 : ClientSize.Width - size.Width - 14, 4,
                        size.Width + 8, size.Height + 2);
                    using (var back = new SolidBrush(palette.Screen))
                        g.FillRectangle(back, box);
                    TextRenderer.DrawText(g, mark, f, box, palette.Dim,
                        TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter);
                }
            }

            // The input row sits directly below; this is the line between
            // them. The terminal draws the row as a field of its own instead.
            if (Framed)
                using (var pen = new Pen(Color.FromArgb(90, palette.Dim)))
                    g.DrawLine(pen, 12, ClientSize.Height - 1,
                                    ClientSize.Width - 12, ClientSize.Height - 1);

            PaintTube(g);
        }

        public bool Tube = true;       // scanlines and vignette: the 1966 console only
        public bool Framed;            // the rule under the text: the tube styles only



        /*  The scanlines and the vignette, drawn once and kept.

            They do not change unless the window does, and they were being
            built from scratch on every paint: a hundred-odd filled rectangles
            and a path gradient across the whole client area, measured at 16 ms
            at the opening size and 47 ms maximised -- on a typing timer that
            fires every 15 ms, and a caret that made the whole thing happen
            twice a second forever while nothing was happening at all. */
        Bitmap tube;

        void PaintTube(Graphics g)
        {
            if (!Tube) return;
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;

            if (tube == null || tube.Width != ClientSize.Width ||
                tube.Height != ClientSize.Height)
            {
                if (tube != null) tube.Dispose();
                tube = new Bitmap(ClientSize.Width, ClientSize.Height,
                                  System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var into = Graphics.FromImage(tube))
                {
                    into.Clear(Color.Transparent);
                    using (var dark = new SolidBrush(Color.FromArgb(38, 0, 0, 0)))
                        for (int y = 0; y < tube.Height; y += 3)
                            into.FillRectangle(dark, 0, y, tube.Width, 1);

                    // A vignette, brightest in the middle of the tube.
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(new Rectangle(
                            -tube.Width / 4, -tube.Height / 4,
                            tube.Width * 3 / 2, tube.Height * 3 / 2));
                        using (var brush = new PathGradientBrush(path))
                        {
                            brush.CenterColor = Color.FromArgb(0, 0, 0, 0);
                            brush.SurroundColors = new[] { Color.FromArgb(105, 0, 0, 0) };
                            into.FillRectangle(brush, 0, 0, tube.Width, tube.Height);
                        }
                    }
                }
            }

            g.DrawImageUnscaled(tube, 0, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                typeTimer.Dispose();
                blinkTimer.Dispose();
                if (font != null) font.Dispose();
                if (tube != null) tube.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------------------

    class MainForm : Form, IMessageFilter
    {
        readonly CrtScreen screen = new CrtScreen();
        readonly TextBox input = new TextBox();
        readonly Label prompt = new Label();

        ElizaEngine engine;
        ScriptEntry current;
        bool addressingWoman;
        readonly List<ScriptEntry> scripts;
        readonly List<string> history = new List<string>();
        int historyAt = -1;
        string halfWritten = "";       // the line being composed, while walking back

        Phosphor phosphor = Phosphor.Green;
        Palette palette = Palette.For(Phosphor.Green);
        ScreenStyle style = ScreenStyle.Machine;
        Scene scene = Scene.For("");

        // Whether the window is dressed as the script's own character.
        bool Dressed { get { return style == ScreenStyle.Tailored; } }
        int colour;                    // 0 green, 1 amber, 2 white
        readonly Rectangle[] styleSpots = new Rectangle[3];
        readonly Rectangle[] colourSpots = new Rectangle[3];
        bool traceMode;
        float scale = 1f;
        float fontPoints = 12f;

        Rectangle closeButton, minButton, powerLed;

        const int Pad = 26, TitleBar = 34, Footer = 26, Grip = 6;

        readonly Settings settings;
        readonly int startIndex;

        // What was said, for the shell to keep once the window is closed.
        /*  Everything said in this window, not only since the last F5.

            The shell saves this when the window closes, and StartConversation
            clears the screen -- so a long conversation, then F5 for a fresh
            one, then closing, saved the second and lost the first entirely,
            with no warning, from a program whose transcripts are a page of
            their own. F2, which switches script, did the same.

            Sessions are kept as they end instead, and what is on the screen
            is added to them at the moment of asking. */
        readonly StringBuilder earlier = new StringBuilder();

        public string Transcript
        {
            get
            {
                // Whatever she is still typing belongs in it too.
                screen.FinishTyping();
                return earlier + screen.Transcript();
            }
        }

        void KeepWhatIsOnTheScreen()
        {
            string had = screen.Transcript();
            if (had.Trim().Length == 0) return;
            earlier.Append(had);
            earlier.AppendLine();
            earlier.AppendLine(new string('-', 40));
            earlier.AppendLine();
        }

        public MainForm(List<ScriptEntry> scripts, int startIndex, Settings settings)
        {
            this.scripts = scripts;
            this.settings = settings;
            this.startIndex = Math.Max(0, Math.Min(scripts.Count - 1, startIndex));

            style = (ScreenStyle)settings.ScreenStyle;
            /*  A console of 1966 is a piece of furniture, and a piece of
                furniture is made of colours somebody chose. In a high contrast
                scheme there is only the flat one, where the screen is a pane
                with text in it and nothing else is drawn. */
            if (Theme.Contrast) style = ScreenStyle.Terminal;
            colour = settings.Phosphor;
            fontPoints = settings.FontSize;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(38, 38, 42);
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "ELIZA";
            /*  The icon on the taskbar and in Alt+Tab.

                A Form does not take the executable's icon by itself; left
                alone it shows the stock WinForms one, so the program had a
                drawn icon on disk and a generic one everywhere a person would
                actually see it. */
            try { Icon = taken = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            var room = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(
                Math.Min((int)(900 * scale), room.Width - S(40)),
                Math.Min((int)(670 * scale), room.Height - S(40)));
            MinimumSize = new Size((int)(520 * scale), (int)(360 * scale));
            CentreOnScreen();

            Controls.Add(screen);

            prompt.AutoSize = false;
            prompt.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(prompt);

            input.BorderStyle = BorderStyle.None;
            /*  A single-line TextBox is AutoSize by default and overrides the
                height it is given with the height of its font. At the larger
                text sizes it grew out through the bottom of the bezel as a
                screen-coloured rectangle sitting on the plastic. */
            input.AutoSize = false;
            input.KeyDown += InputKeyDown;
            Controls.Add(input);

            screen.TypingFinished += (s, e) =>
            {
                input.Enabled = true;
                if (CanFocus) input.Focus();
            };

            // Clicking the conversation puts the caret back where typing goes,
            // which is where a person who just clicked expects it to be.
            screen.Poked += (s, e) => { if (input.Enabled && CanFocus) input.Focus(); };
            Load += (s, e) => BuildMenu();

            Load += (s, e) =>
            {
                screen.Speed = (TypingSpeed)settings.Speed;
                LoadScript(scripts[this.startIndex]);
                input.Focus();
            };
            Resize += (s, e) => { LayoutChildren(); Invalidate(); };

            Application.AddMessageFilter(this);
            ApplyPalette();
            LayoutChildren();
        }

        Font shared;                   // the face the conversation is set in
        Icon taken;                    // and the icon pulled out of the exe

        /*  After the children, not before: the screen owns its own font and
            disposes it, and the text box is still using this one until it is
            gone. ApplyFonts already disposes each font as it replaces it --
            this is the last one, which nothing replaced. */
        protected override void Dispose(bool disposing)
        {
            if (disposing) Application.RemoveMessageFilter(this);
            base.Dispose(disposing);
            if (!disposing) return;
            if (shared != null) { shared.Dispose(); shared = null; }
            if (taken != null) { taken.Dispose(); taken = null; }
        }

        // Whatever was changed with the function keys is the new preference.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (settings != null)
            {
                settings.ScreenStyle = (int)style;
                settings.Phosphor = colour;
                settings.Speed = (int)screen.Speed;
                settings.FontSize = (int)Math.Round(fontPoints);
                if (current != null) settings.Script = current.Name;
                settings.Save();
            }
            base.OnFormClosing(e);
        }

        // ---- layout ----------------------------------------------------

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


        /*  How much of the window the text gets.

            All of it, except in the museum. That version was never a terminal
            filling a screen: it was a white chat box sitting in a drawing of a
            psychotherapist's room, and the drawing is most of why anybody
            remembers it. So the box gives up a band at the foot of the window
            and a column at the side, and the room is drawn in what is left. */
        /*  How much of the window the room may take.

            Fixed amounts were fine at the size the window opens at and wrong
            at the size it is allowed to shrink to: at 720 by 520 the band and
            the plaque between them left the chat box a sliver, and the drawing
            ran underneath it. The room is a proportion of the window with a
            ceiling, and below a certain size there is no room at all -- the
            style is still the museum's white and blue and its dotted border,
            which is the part that has to be legible. */
        bool RoomFits
        {
            get
            {
                return Dressed &&
                       ClientSize.Height >= S(440) && ClientSize.Width >= S(560);
            }
        }

        int RoomBand
        {
            get
            {
                return RoomFits
                    ? Math.Min(S(190), (int)(ClientSize.Height * scene.Band)) : 0;
            }
        }

        /*  Nothing, since the room was rebuilt.

            The lamp and the plant used to stand in a column beside the chat
            box; they stand on the floor now, with everything else. The column
            went on being subtracted from the box's width -- a seventh of the
            window, up to 175 pixels, of blank cabinet that nothing has drawn
            in for two revisions. */
        int RoomSide { get { return 0; } }

        int RoomTop
        {
            get
            {
                return RoomFits
                    ? Math.Min(S(54), (int)(ClientSize.Height * scene.Top)) : 0;
            }
        }

        // The three pieces of the window the drawing gets, worked out from the
        // window itself rather than from where the chat box happens to end.
        Rectangle RoomFloor
        {
            get
            {
                int pad = S(Pad);
                return new Rectangle(pad,
                    ClientSize.Height - S(Footer) - RoomBand + S(6),
                    ClientSize.Width - pad * 2, RoomBand - S(12));
            }
        }

        Rectangle RoomColumn
        {
            get
            {
                int pad = S(Pad);
                var area = ScreenArea;
                return Rtl
                    ? new Rectangle(pad, area.Y, RoomSide - S(12), area.Height)
                    : new Rectangle(area.Right + S(12), area.Y, RoomSide - S(12), area.Height);
            }
        }

        Rectangle RoomPlaque
        {
            get
            {
                // The plaque lives in the strip the room gives it. With no
                // room there is no strip, and it would be drawn across the
                // chat box instead.
                int wide = Math.Min(S(380), ClientSize.Width - S(260));
                if (RoomTop <= 0 || wide < S(140))
                    return Rectangle.Empty;
                // Below the row the window's own buttons and the style
                // selector live in, so it cannot sit on top of either.
                // Off centre, away from the row the style selector sits in.
                int mid = (ClientSize.Width - wide) / 2 + (Rtl ? -S(40) : S(40));
                return new Rectangle(mid, S(Pad) + S(TitleBar) - S(2), wide, RoomTop);
            }
        }

        Rectangle ScreenArea
        {
            get
            {
                int pad = S(Pad);
                var all = new Rectangle(pad, pad + S(TitleBar),
                    ClientSize.Width - pad * 2,
                    ClientSize.Height - pad * 2 - S(TitleBar) - S(Footer));

                if (!Dressed) return all;

                // The drawing takes the foot of the window and one side; which
                // side is the side the reading runs away from.
                all.Y += RoomTop;
                all.Height -= RoomTop + RoomBand;
                all.Width -= RoomSide;
                if (Rtl) all.X += RoomSide;
                if (all.Height < S(140)) all.Height = S(140);
                return all;
            }
        }

        void LayoutChildren()
        {
            int pad = S(Pad);
            closeButton = new Rectangle(ClientSize.Width - pad - S(20), pad + S(6), S(18), S(18));
            minButton = new Rectangle(closeButton.Left - S(26), closeButton.Top, S(18), S(18));
            powerLed = new Rectangle(pad + S(4), pad + S(12), S(9), S(9));

            var area = ScreenArea;
            if (area.Width < 60 || area.Height < 90) return;

            /*  As tall as the font asks for.

                It was a fixed 28 units whatever the text size, leaving the box
                20 units of interior -- and Consolas at 13pt already wants 21,
                Courier at 23.5pt wants 35, Miriam Fixed 31. A single-line text
                box top-aligns, so what is cut is the bottom: descenders in
                English, and most of the letter body in Hebrew, where the line
                being typed becomes a row of broken shapes. The conversation
                gives up a line instead, which is the right trade. */
            int inputH = Math.Max(S(28), input.Font.Height + S(10));
            screen.Bounds = new Rectangle(area.X, area.Y, area.Width, area.Height - inputH);

            bool rtl = current != null && current.RightToLeft;
            int promptW = S(26);
            int inputY = area.Bottom - inputH;

            if (rtl)
            {
                prompt.Bounds = new Rectangle(area.Right - promptW, inputY, promptW, inputH);
                input.Bounds = new Rectangle(area.X + S(6), inputY + S(5),
                    area.Width - promptW - S(10), inputH - S(8));
            }
            else
            {
                prompt.Bounds = new Rectangle(area.X, inputY, promptW, inputH);
                input.Bounds = new Rectangle(area.X + promptW, inputY + S(5),
                    area.Width - promptW - S(6), inputH - S(8));
            }
        }

        // ---- scripts ---------------------------------------------------

        static string FirstInstalled(string[] names)
        {
            using (var installed = new InstalledFontCollection())
            {
                var have = new HashSet<string>(installed.Families.Select(f => f.Name),
                                               StringComparer.OrdinalIgnoreCase);
                foreach (var n in names) if (have.Contains(n)) return n;
            }
            return "Courier New";
        }

        void ApplyFonts()
        {
            // Miriam Fixed is the monospaced Hebrew face that ships with Windows,
            // and it looks the part; Consolas has no Hebrew at all.
            /*  Courier for the museum, whichever language: its page asked for
                'Courier New', Courier, monospace and got it, and the letter
                shapes are half of why it looked the way it did. The other two
                styles keep the faces that suit a tube. */
            string family = Dressed
                ? FirstInstalled(scene.Faces(current.RightToLeft))
                : FirstInstalled(current.RightToLeft
                    ? new[] { "Miriam Fixed", "Courier New", "Consolas" }
                    : new[] { "Consolas", "Lucida Console", "Courier New" });

            float points = current.RightToLeft ? fontPoints + 1.5f : fontPoints;

            /*  Aliased for the console of 1966 and smoothed for everything
                else. It is the single loudest difference between a terminal
                and a tube, and until now both were aliased. */
            byte quality = style == ScreenStyle.Machine
                ? Typeface.Aliased : Typeface.Smooth;
            screen.SetFont(Typeface.At(family, points, quality));

            var was = shared;
            var f = Typeface.At(family, points, quality);
            shared = f;
            Wear(input, f);
            Wear(prompt, f);
            if (was != null) was.Dispose();
        }

        /*  Put a font on a control, and be sure it took it.

            Control.Font ignores a font it considers the same as the one it
            already has -- and Font.Equals looks at the family, the size and
            the style, never at lfQuality. The aliased face and the smoothed
            one are the same font by that test, so switching between the
            console of 1966 and the terminal in the same language left the
            text box holding the instance that was about to be disposed: the
            next line to ask its height got "Parameter is not valid", the
            window put up the unexpected-error box, and from then on its close
            button did nothing. Clearing the font first makes the assignment
            one the control cannot decline. */
        static void Wear(Control c, Font f)
        {
            c.Font = null;
            c.Font = f;
        }

        void LoadScript(ScriptEntry entry)
        {
            current = entry;
            addressingWoman = entry.Feminine != null && settings.Address == "f";
            engine = new ElizaEngine(ElizaScript.Parse(
                addressingWoman ? entry.Feminine.Text : entry.Text));

            screen.Mirrored = entry.RightToLeft;
            input.RightToLeft = entry.RightToLeft
                ? System.Windows.Forms.RightToLeft.Yes : System.Windows.Forms.RightToLeft.No;
            // WinForms mirrors the alignment when RightToLeft is on, so Left
            // is what puts the caret against the right-hand edge.
            input.TextAlign = HorizontalAlignment.Left;
            prompt.Text = entry.RightToLeft ? "◀" : "▶";

            // The scene belongs to the script, so it is chosen here and
            // not only when the style is changed.
            ApplyPalette();
            ApplyFonts();
            LayoutChildren();
            StartConversation();
        }

        void StartConversation()
        {
            KeepWhatIsOnTheScreen();
            engine.Reset();
            history.Clear();
            historyAt = -1;
            halfWritten = "";
            screen.Clear();
            foreach (var line in Banner()) screen.AddInstant(line, 2);
            screen.AddInstant("", 0);

            // A different opening each time you come in. The count lives in
            // the settings, not in her.
            settings.Visits = settings.Visits + 1;

            /*  After Reset, and not before it.

                ApplyOrder used to be called when the engine was built, one
                line before this method, whose first statement is Reset -- and
                Reset sets every rotation back to zero. So the whole of
                "מתחלף בין שיחות", the default, did nothing whatever: every
                offset it worked out was wiped before the first reply. It
                passed its own tests because those drive StartAt directly and
                never go near Reset.

                The visit number is the same one the greeting uses, so the
                opening line and the rotation move together. */
            ApplyOrder();
            Speak(engine.GreetingFor(settings.Visits - 1), 3);
            Invalidate();
        }

        IEnumerable<string> Banner()
        {
            if (current.RightToLeft)
            {
                yield return "המכון הטכנולוגי של מסצ'וסטס · פרויקט MAC · 1966";
                yield return "אלייזה · תסריט: " + current.Name;
                yield return "F1 לעזרה";
            }
            else
            {
                yield return "MASSACHUSETTS INSTITUTE OF TECHNOLOGY · PROJECT MAC · 1966";
                yield return "ELIZA · SCRIPT: " + current.Name;
                yield return "PRESS F1 FOR HELP";
            }
        }

        // ---- interaction -----------------------------------------------

        // Always disable the input BEFORE queueing: at instant speed the
        // finished event fires from inside AddTyped.
        /*  She answers at the speed of a teletype if you ask her to, and a
            sixty-character reply then takes six seconds. Closing the input for
            those six seconds swallows everything typed into them, with nothing
            on the screen to say the door is shut.

            So the box stays open and takes the keystrokes. Pressing Enter
            while she is still speaking finishes what she was saying at once
            and then answers -- which is what a terminal does, and what
            somebody who has already decided what to say expects. */
        void Speak(string text, int style)
        {
            screen.AddTyped(text, style);
            if (CanFocus) input.Focus();
        }

        void InputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                if (history.Count == 0) return;
                e.Handled = e.SuppressKeyPress = true;
                /*  The half-written line is kept, and comes back when you walk
                    down past the end of the history -- which is what every
                    shell does. Down on a fresh line used to jump to the end of
                    the history and assign the empty string over whatever was
                    being typed, and because the assignment is programmatic
                    Ctrl+Z would not bring it back. */
                if (historyAt < 0)
                {
                    if (e.KeyCode == Keys.Down) return;
                    halfWritten = input.Text;
                    historyAt = history.Count;
                }
                historyAt += e.KeyCode == Keys.Up ? -1 : 1;
                historyAt = Math.Max(0, Math.Min(history.Count, historyAt));
                input.Text = historyAt < history.Count ? history[historyAt] : halfWritten;
                input.SelectionStart = input.Text.Length;
                return;
            }

            if (e.KeyCode != Keys.Enter) return;
            e.Handled = e.SuppressKeyPress = true;

            // If she is mid-sentence, let her finish it instantly rather than
            // stacking one reply on top of another.
            if (screen.Busy) screen.FinishTyping();

            string text = input.Text.Trim();
            input.Clear();

            if (text.Length == 0)
            {
                // Nothing typed. The 1966 script has nothing to say to that and
                // stays quiet; a script carrying answers to silence speaks.
                if (engine.AnswersSilence && !screen.Busy)
                    Speak(engine.RespondToSilence(), 0);
                return;
            }

            history.Add(text);
            historyAt = -1;
            halfWritten = "";

            screen.AddInstant("· " + text, 1);

            /*  Hebrew marks the speaker's gender on the verb, so a script
                written for a man addresses a woman wrongly from her first
                sentence. When the speaker gives themselves away, hand the rest
                of the conversation to the script written for them. The engine
                learns nothing: it is handed different data, which is the idea.

                In both directions and as often as it takes. Somebody who says
                אני בת and then אני בן is correcting the program, and a program
                that could only be corrected once would be worse than one that
                never listened. */
            if (current.Feminine != null && settings.Address == "auto")
            {
                int sounds = engine.SpeakerSounds(text);
                if (sounds != 0 && (sounds > 0) != addressingWoman)
                {
                    addressingWoman = sounds > 0;
                    /*  She is handed a different script, not a different
                        mind. Without carrying the state across she starts the
                        conversation again from nothing the moment a woman
                        says so: the counter that decides when a memory
                        surfaces goes back to one, everything she had stored
                        to bring up later is gone, and every rotation returns
                        to its first phrasing -- which is to say she begins
                        repeating herself exactly when the program has just
                        shown how closely it was listening. */
                    var was = engine;
                    engine = new ElizaEngine(ElizaScript.Parse(
                        addressingWoman ? current.Feminine.Text : current.Text));
                    ApplyOrder();
                    engine.CarryOn(was);
                }
            }

            string reply = engine.Respond(text);
            if (traceMode)
                foreach (var t in engine.Trace) screen.AddInstant("    " + t, 2, true);

            Speak(reply, 0);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F1: ShowHelp(); return true;
                case Keys.F2: SwitchScript(); return true;
                case Keys.F3: CycleSpeed(); return true;
                case Keys.F4: CycleStyle(); return true;
                case Keys.F5: StartConversation(); return true;
                case Keys.F6: ToggleTrace(); return true;
                case Keys.F7: ChangeFontSize(1); return true;
                case Keys.F8: ChangeFontSize(-1); return true;
                case Keys.Control | Keys.S: SaveTranscript(); return true;
                case Keys.Control | Keys.C: CopyAll(); return true;
                case Keys.PageUp: screen.ScrollBy(5); return true;
                case Keys.PageDown: screen.ScrollBy(-5); return true;
                case Keys.Escape:
                    if (screen.Busy) { screen.FinishTyping(); return true; }
                    Close();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // The text box has the focus, so wheel messages never reach the screen
        // control on their own. Catch them here and forward by cursor position.
        const int WM_MOUSEWHEEL = 0x020A;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;
            var over = screen.RectangleToScreen(screen.ClientRectangle);
            if (!over.Contains(Cursor.Position)) return false;
            int delta = (short)((long)m.WParam >> 16);
            screen.ScrollBy(delta > 0 ? 3 : -3);
            return true;
        }

        void Note(params string[] lines)
        {
            /*  Finish whatever she is in the middle of saying first.

                A note appends to the same list the half-typed line is still
                growing in, so it used to land underneath it -- the sentence
                went on filling itself in above the note, and the cursor blinked
                on a blank line several rows below the text being written. */
            screen.FinishTyping();

            foreach (var l in lines) screen.AddInstant(l, 2);
            screen.AddInstant("", 0);
        }

        void ShowHelp()
        {
            if (current.RightToLeft)
                Note("",
                    "F1 עזרה    F2 החלפת תסריט    F3 מהירות הקלדה    F4 סגנון המסך",
                    "F5 שיחה חדשה    F6 מעקב אחרי הכללים    F7 F8 גודל",
                    "Ctrl+S שמירת התמליל    חצים למעלה ולמטה: מה שהוקלד קודם",
                    "Esc מדלג על ההקלדה, ושוב יוצא",
                    "",
                    "זו אלייזה של וייצנבאום משנת 1966. אין כאן הבנה, רק",
                    "כללי התאמה שפועלים על סדר המילים. נסו לספר לה על המשפחה שלכם.");
            else
                Note("",
                    "F1 HELP    F2 SWITCH SCRIPT    F3 TYPING SPEED    F4 SCREEN STYLE",
                    "F5 NEW SESSION    F6 TRACE THE RULES    F7 F8 SIZE",
                    "CTRL+S SAVE TRANSCRIPT    UP AND DOWN ARROWS RECALL WHAT YOU TYPED",
                    "ESC SKIPS THE TYPING, AGAIN TO QUIT",
                    "",
                    "THIS IS WEIZENBAUM'S ELIZA OF 1966. THERE IS NO",
                    "UNDERSTANDING HERE, ONLY PATTERNS OVER WORD ORDER.",
                    "TRY TELLING HER ABOUT YOUR FAMILY.");
        }

        /*  Getting a sentence out of it.

            The screen is painted text, so there is nothing to select and
            nothing for Ctrl+C to find -- and taking a line out of a
            conversation is the most natural thing anybody wants to do with
            one. A menu on the right button is the least a window can offer,
            and Ctrl+C copies the lot. */
        void BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.RenderMode = ToolStripRenderMode.System;

            var copy = new ToolStripMenuItem(
                Rtl ? "העתקת השיחה" : "Copy the conversation");
            copy.ShortcutKeyDisplayString = "Ctrl+C";
            copy.Click += (s2, e2) => CopyAll();

            var save = new ToolStripMenuItem(
                Rtl ? "שמירת השיחה כקובץ" : "Save the conversation");
            save.ShortcutKeyDisplayString = "Ctrl+S";
            save.Click += (s2, e2) => SaveTranscript();

            menu.Items.Add(copy);
            menu.Items.Add(save);
            menu.RightToLeft = Rtl ? RightToLeft.Yes : RightToLeft.No;

            ContextMenuStrip = menu;
            screen.ContextMenuStrip = menu;
            input.ContextMenuStrip = null;      // the box keeps its own
        }

        void CopyAll()
        {
            try
            {
                string all = Transcript;
                if (all.Trim().Length == 0) return;
                Clipboard.SetText(all);
                Note(Rtl ? "השיחה הועתקה." : "THE CONVERSATION IS ON THE CLIPBOARD.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                                (Rtl ? "לא ניתן היה להעתיק." : "It could not be copied.") +
                                Environment.NewLine + Environment.NewLine + ex.Message,
                                "ELIZA", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                                MessageBoxDefaultButton.Button1, ShellForm.Reading(Rtl));
            }
        }

        void SwitchScript()
        {
            if (scripts.Count < 2) return;
            LoadScript(scripts[(scripts.IndexOf(current) + 1) % scripts.Count]);
        }

        void CycleSpeed()
        {
            screen.Speed = (TypingSpeed)(((int)screen.Speed + 1) % 3);
            string[] he = { "מיידית", "מהירה", "טלפרינטר, עשרה תווים בשנייה" };
            string[] en = { "INSTANT", "FAST", "TELETYPE, TEN CHARACTERS A SECOND" };
            Note((current.RightToLeft ? "מהירות: " : "SPEED: ") +
                 (current.RightToLeft ? he : en)[(int)screen.Speed]);
        }

        void CycleStyle()
        {
            SetStyle((ScreenStyle)(((int)style + 1) % 3));
        }

        void SetStyle(ScreenStyle s)
        {
            style = s;
            ApplyPalette();
            ApplyFonts();          // the museum is set in Courier, the others are not
            /*  And the museum gives up part of the window to the drawing, so
                the screen is a different size and everything inside it has to
                be put somewhere else. Without this the text box stayed where
                the previous style had left it, floating over the room. */
            LayoutChildren();
            Note(StyleNames[(int)style]);
        }

        void SetColour(int c)
        {
            colour = c;
            ApplyPalette();
            if (!Dressed) Note(ColourNames[colour]);
        }

        /*  The museum drew her on paper, so that style has no tube colour to
            choose; the other two do. Keeping the choice even while it is not
            in use means switching back does not lose it. */
        void ApplyPalette()
        {
            /*  In character, the scene answers for all of it: the colours,
                the chrome's two inks and the surround. Out of character there
                is a tube colour to choose, and the scene is not consulted. */
            /*  A script that does not name a scene gets the one that
                belongs to its language. Weizenbaum's file is a verbatim
                transcription of the 1966 paper and nothing is added to it,
                not even a directive -- and in 1966 nobody saw ELIZA on a
                screen, so what belongs to it is the teletype. */
            scene = Scene.For(current == null ? ""
                : current.Scene.Length > 0 ? current.Scene
                : current.RightToLeft ? "MUSEUM" : "TELETYPE");
            if (Dressed)
            {
                phosphor = Phosphor.Paper;      // no tube colour to choose
                palette = scene.Colours;
            }
            else
            {
                phosphor = (Phosphor)Math.Max(0, Math.Min(2, colour));
                palette = Palette.For(phosphor, style == ScreenStyle.Terminal);
            }
            screen.Tube = style == ScreenStyle.Machine;
            screen.Framed = style != ScreenStyle.Terminal;
            screen.Colours = palette;
            input.BackColor = palette.Field;
            input.ForeColor = palette.User;
            prompt.BackColor = palette.Field;
            prompt.ForeColor = style == ScreenStyle.Terminal
                ? palette.Normal : palette.Dim;
            Invalidate();
        }

        void ChangeFontSize(int step)
        {
            fontPoints = Math.Max(8f, Math.Min(22f, fontPoints + step));
            ApplyFonts();
            LayoutChildren();
        }

        void ToggleTrace()
        {
            traceMode = !traceMode;
            if (current.RightToLeft)
                Note(traceMode
                    ? "מעקב פועל. בכל תשובה יוצג איזה כלל נתפס וכיצד נבנתה התשובה."
                    : "מעקב כבוי.");
            else
                Note(traceMode
                    ? "TRACE ON. EACH REPLY WILL SHOW WHICH RULE FIRED AND HOW IT WAS BUILT."
                    : "TRACE OFF.");
        }

        void SaveTranscript()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Text|*.txt";
                dlg.FileName = "eliza-" + DateTime.Now.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture) + ".txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                /*  The only write in the program that had no guard at all.
                    A read-only folder or a full disk threw out into the
                    program's own safety net, which answers with "if you have
                    edited a script, that is the first place to look" -- a
                    confident and completely wrong diagnosis for a failed
                    Save As. */
                try
                {
                    File.WriteAllText(dlg.FileName, screen.Transcript(),
                                      new UTF8Encoding(true));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        (Rtl ? "לא ניתן היה לשמור את הקובץ."
                             : "The file could not be saved.") +
                        Environment.NewLine + Environment.NewLine + ex.Message,
                        "ELIZA", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1, ShellForm.Reading(Rtl));
                }
            }
        }

        // ---- the cabinet -----------------------------------------------

        // Dark plastic around a sheet of paper reads as a mistake, so the
        // cabinet lightens with the screen it holds.
        /*  Settings.Order, applied to a freshly built engine. Called after
            every rebuild, including the one that happens mid-conversation
            when the speaker turns out to be a woman. */
        void ApplyOrder()
        {
            engine.ChooseAtRandom = settings.Order == "shuffled";
            if (settings.Order == "varied")
                engine.StartAt(Math.Max(0, settings.Visits - 1));
        }

        /*  In a high contrast scheme all of these come from the scheme
            instead. The surround is the window colour, the engraving is the
            window text, and the resting legend along the foot is the grey the
            scheme itself defines -- which, unlike a grey of our own, was
            chosen to be legible on that background. */
        Color Shell1 { get { return Theme.Contrast ? Theme.Back
                                  : Dressed ? scene.Surround1
                                            : Color.FromArgb(70, 69, 74); } }
        Color Shell2 { get { return Theme.Contrast ? Theme.Back
                                  : Dressed ? scene.Surround2
                                            : Color.FromArgb(26, 26, 30); } }
        Color Engraved { get { return Theme.Contrast ? Theme.Text
                                    : Dressed ? scene.Engraved
                                              : Color.FromArgb(188, 188, 196); } }
        /*  The resting colour of the two word selectors and of the key
            legend along the foot -- which is the only written record of any
            keyboard shortcut anywhere in the program. Measured at 3.1 to 4.4
            against the surround it is drawn on, against the 4.5 that text
            has to reach. Engraved stays a visible step above it, so the
            hover still reads as a hover. */
        Color Etched { get { return Theme.Contrast ? Theme.Faint
                                  : Dressed ? scene.Etched
                                            : Color.FromArgb(160, 160, 168); } }

        bool Rtl { get { return current != null && current.RightToLeft; } }

        // The same three names the settings page uses. They were three
        // different sets of names in three places.
        static readonly string[] StylesHe = { "מסוף", "מחשב 1966", "מותאם לדמות" };
        static readonly string[] StylesEn = { "Terminal", "1966 machine", "In character" };
        static readonly string[] ColoursHe = { "ירוק", "ענבר", "לבן" };
        static readonly string[] ColoursEn = { "Green", "Amber", "White" };

        /*  The third name is the room, not the setting.

            "In character" is a word about the program: it belongs on the
            settings page, where the choice is made before you know which
            character it will be. Here you are standing in the room, and
            what the strip should say is which one. */
        string[] StyleNames
        {
            get
            {
                var names = (string[])(Rtl ? StylesHe : StylesEn).Clone();
                if (scene != null) names[2] = scene.Called(Rtl);
                return names;
            }
        }
        string[] ColourNames { get { return Rtl ? ColoursHe : ColoursEn; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int pad = S(Pad);

            /*  What the screen is set into. Three different objects, and the
                difference is not decoration: a terminal is a window with text
                in it, a console of 1966 is a piece of furniture, and a sheet
                of paper is neither. */
            if (style == ScreenStyle.Terminal)
            {
                using (var flat = new SolidBrush(
                    Theme.Contrast ? Theme.Back : Color.FromArgb(14, 16, 20)))
                    g.FillRectangle(flat, ClientRectangle);
            }
            else
            {
                using (var shell = new LinearGradientBrush(ClientRectangle, Shell1, Shell2, 64f))
                    g.FillRectangle(shell, ClientRectangle);

                // Brushed plastic on the console; the tooth of the paper on the sheet.
                using (var grain = new Pen(Color.FromArgb(Dressed ? scene.Grain : 9,
                                                          255, 255, 255)))
                    for (int y = 0; y < ClientSize.Height; y += 4)
                        g.DrawLine(grain, 0, y, ClientSize.Width, y);
            }

            var well = Rectangle.Inflate(ScreenArea, S(7), S(7));
            if (well.Width > 20 && well.Height > 20)
            {
                if (style == ScreenStyle.Terminal)
                {
                    /*  A pane, not a bezel: rounded corners, one hairline, and
                        the ground of the text carried out to the edge so the
                        corners are the pane's colour and not the window's. */
                    using (var b2 = new SolidBrush(palette.Screen))
                    using (var path = Rounded(well, S(10)))
                        g.FillPath(b2, path);
                    using (var pen = new Pen(
                        Theme.Contrast ? Theme.Line : Color.FromArgb(40, 44, 52), 1f))
                    using (var path = Rounded(well, S(10)))
                        g.DrawPath(pen, path);

                    // And the line being typed sits in a field of its own.
                    var row = new Rectangle(ScreenArea.X + S(2), prompt.Top + S(2),
                                            ScreenArea.Width - S(4),
                                            Math.Max(S(20), prompt.Height - S(4)));
                    using (var b2 = new SolidBrush(palette.Field))
                    using (var path = Rounded(row, S(8)))
                        g.FillPath(b2, path);
                    using (var pen = new Pen(
                        Theme.Contrast ? Theme.Line : Color.FromArgb(46, 52, 62), 1f))
                    using (var path = Rounded(row, S(8)))
                        g.DrawPath(pen, path);
                }
                else if (Dressed)
                {
                    /*  Whatever this character's chat box is set into. Drawn
                        on the edge of the box rather than outside it: outside,
                        the museum's dotted border climbed into the title bar
                        and struck through the name of the program. */
                    scene.Frame(g, well, scale);
                }
                else
                {
                    using (var shadow = new LinearGradientBrush(well,
                        Color.FromArgb(10, 10, 12), Color.FromArgb(52, 52, 58), 64f))
                    using (var path = Rounded(well, S(10)))
                        g.FillPath(shadow, path);
                    using (var pen = new Pen(Color.FromArgb(14, 14, 16), 2f))
                    using (var path = Rounded(well, S(10)))
                        g.DrawPath(pen, path);

                }
            }

            /*  The room the chat box sits in.

                Drawn into the band along the foot of the window and the column
                beside the box -- the space ScreenArea gives up for it in this
                style, and only in this style. */
            if (Dressed && RoomFloor.Height > S(70))
                scene.Floor(g, RoomFloor, scale, Rtl);

            // ---- the name, and what kind of machine this is ---------------
            if (Dressed && RoomPlaque.Width > 0)
            {
                /*  The character's own sign, in place of the name pressed
                    into a plastic case: the museum's hand-lettered plaque,
                    the teletype's typed header, a carved board over a study
                    hall's door, a brass plate. */
                scene.Sign(g, RoomPlaque, scale, Rtl);
            }
            else if (style == ScreenStyle.Terminal)
            {
                /*  A terminal's own title strip: a mark and a name set in the
                    interface face. The letter-spaced name engraved into a
                    plastic case belongs to the case, and drawing it here was
                    half of why the two styles looked alike. */
                var mark = new Rectangle(pad + S(16), pad + S(7), S(17), S(17));
                using (var b = new SolidBrush(palette.Normal))
                using (var path = Rounded(mark, S(5)))
                    g.FillPath(b, path);
                using (var f = new Font("Consolas", 7f * scale, FontStyle.Bold))
                    TextRenderer.DrawText(g, ">_", f, mark, palette.Screen,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding);
                using (var f = new Font("Segoe UI Semibold", 9.5f * scale))
                    TextRenderer.DrawText(g, "ELIZA", f,
                        new Rectangle(mark.Right + S(9), pad + S(6), S(120), S(19)),
                        Color.FromArgb(216, 222, 230),
                        TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPrefix);
            }
            else if (!Dressed)
            {
                // In character the sign IS the title, so a window too small
                // for the sign has no title -- rather than the engraved name
                // from a plastic case that this style does not have, drawn
                // over the top of the chat box.
                using (var f = new Font("Segoe UI", 9.5f * scale, FontStyle.Bold))
                using (var b = new SolidBrush(Engraved))
                    g.DrawString("E L I Z A", f, b, pad + S(20), pad + S(7));
            }

            /*  The two selectors. Words rather than buttons: a button drawn on
                a 1966 console is an anachronism, a button drawn on a sheet of
                paper is a nonsense, and a word is neither. */
            PaintSelector(g, StyleNames, (int)style, styleSpots,
                          minButton.Left - S(22), pad + S(9));

            for (int i = 0; i < colourSpots.Length; i++) colourSpots[i] = Rectangle.Empty;
            if (!Dressed)
                PaintSelector(g, ColourNames, colour, colourSpots,
                              Rtl ? pad : ClientSize.Width - pad,
                              ClientSize.Height - S(Footer) + S(4), Rtl);

            using (var f = new Font("Segoe UI", 7.5f * scale))
            using (var b = new SolidBrush(Etched))
            {
                // Only the console of 1966 carries the maker's name, because
                // only the console of 1966 was made by anybody.
                if (style == ScreenStyle.Machine)
                    g.DrawString("PROJECT MAC · M.I.T.", f, b, pad + S(96), pad + S(10));

                /*  One block per key, laid out by hand.

                    The strip is Hebrew words and Latin key names in one line,
                    and handing that to the text engine as a single string is
                    a bidi trap: the Latin runs are reordered against the
                    Hebrew ones and the items land on top of each other. Each
                    item drawn on its own is its own paragraph, so nothing can
                    be reordered across the gaps, and walking from the reading
                    edge puts F1 where a reader looks first. */
                var keys = Rtl
                    ? new[] { "F1 עזרה", "F2 תסריט", "F3 מהירות", "F4 סגנון",
                              "F5 שיחה חדשה", "F6 מעקב", "Esc יציאה" }
                    : new[] { "F1 HELP", "F2 SCRIPT", "F3 SPEED", "F4 STYLE",
                              "F5 RESTART", "F6 TRACE", "ESC QUIT" };

                var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                            (Rtl ? TextFormatFlags.RightToLeft : TextFormatFlags.Default);
                int gap = S(16);
                int walk = Rtl ? ClientSize.Width - pad : pad;

                // The colour selector sits at the other end of the same strip.
                int wall = pad;
                foreach (var spot in colourSpots)
                    if (!spot.IsEmpty)
                        wall = Rtl ? Math.Max(wall, spot.Right + S(20)) : wall;
                int far = Rtl ? wall : ClientSize.Width - pad;
                foreach (var spot in colourSpots)
                    if (!spot.IsEmpty && !Rtl) far = Math.Min(far, spot.Left - S(20));
                int row = ClientSize.Height - S(Footer) + S(3);

                foreach (var key in keys)
                {
                    int wide = TextRenderer.MeasureText(g, key, f,
                        new Size(int.MaxValue, int.MaxValue), flags).Width;
                    if (Rtl)
                    {
                        if (walk - wide < far) break;
                        walk -= wide;
                    }
                    else if (walk + wide > far) break;

                    TextRenderer.DrawText(g, key, f,
                        new Rectangle(walk, row, wide, S(Footer) - S(4)), Engraved, flags);

                    walk = Rtl ? walk - gap : walk + wide + gap;
                }
            }

            // ---- the lamp, and the window buttons -------------------------
            if (style == ScreenStyle.Machine)
            {
                using (var glow = new SolidBrush(Color.FromArgb(55, palette.Bright)))
                    g.FillEllipse(glow, Rectangle.Inflate(powerLed, S(4), S(4)));
                using (var lamp = new SolidBrush(palette.Normal))
                    g.FillEllipse(lamp, powerLed);
            }

            using (var pen = new Pen(Etched, 1.6f))
            {
                g.DrawLine(pen, minButton.Left + S(3), minButton.Bottom - S(5),
                                minButton.Right - S(3), minButton.Bottom - S(5));
                g.DrawLine(pen, closeButton.Left + S(4), closeButton.Top + S(4),
                                closeButton.Right - S(4), closeButton.Bottom - S(4));
                g.DrawLine(pen, closeButton.Right - S(4), closeButton.Top + S(4),
                                closeButton.Left + S(4), closeButton.Bottom - S(4));
            }
        }

        /*  A row of words ending at `right`, the chosen one lit. The boxes are
            remembered so that a click can find them; they are recomputed on
            every paint, and a paint always happens before a click arrives. */
        /*  anchor is the edge the row is measured from: its right-hand end
            normally, or its left-hand one when fromLeft is set. The colour
            selector needs the left in a Hebrew window, because the row of key
            hints starts from the right and the two were drawn over each
            other. */
        void PaintSelector(Graphics g, string[] names, int current,
                           Rectangle[] spots, int anchor, int top,
                           bool fromLeft = false)
        {
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                        (Rtl ? TextFormatFlags.RightToLeft : TextFormatFlags.Default);
            using (var f = new Font("Segoe UI", 7.5f * scale))
            using (var chosen = new Font("Segoe UI", 7.5f * scale, FontStyle.Bold))
            {
                var widths = new int[names.Length];
                int total = 0, gap = S(13);
                for (int i = 0; i < names.Length; i++)
                {
                    widths[i] = TextRenderer.MeasureText(g, names[i],
                        i == current ? chosen : f,
                        new Size(int.MaxValue, int.MaxValue), flags).Width;
                    total += widths[i] + (i > 0 ? gap : 0);
                }

                /*  Forget where they were before working out where they go.
                    Returning early with last time's rectangles still in the
                    array leaves a strip of the cabinet that answers a click
                    while showing nothing at all. */
                for (int i = 0; i < spots.Length; i++) spots[i] = Rectangle.Empty;

                int x = fromLeft ? anchor : anchor - total;
                if (x < S(Pad) || x + total > ClientSize.Width - S(Pad)) return;

                /*  Drawn in the order the eye meets them.

                    TextFormatFlags.RightToLeft settles how one word reads and
                    nothing about the order of several, so the row ran left to
                    right in both languages: in Hebrew the first choice sat at
                    the far left and the last at the right, which is backwards.
                    The rectangles stay keyed to the logical index, so clicking
                    and the keys are untouched. */
                var mouse = PointToClient(MousePosition);
                for (int k = 0; k < names.Length; k++)
                {
                    int i = Rtl ? names.Length - 1 - k : k;
                    var box = new Rectangle(x, top, widths[i], S(15));
                    // The word is what is drawn; what answers the click is a
                    // little larger than the word, because 20 by 15 pixels is
                    // not a target.
                    spots[i] = Rectangle.Inflate(box, S(4), S(6));
                    bool on = i == current;
                    TextRenderer.DrawText(g, names[i], on ? chosen : f, box,
                        on ? palette.Bright
                           : (box.Contains(mouse) ? Engraved : Etched), flags);

                    /*  Which one is actually on.

                        It was a difference of colour and nothing else, and on
                        the museum's white page -- pure blue against a lilac
                        grey -- the three names read as one label. Whatever
                        was showing, the row began "מסוף", and that is what
                        people took the page to be called. Bold, with a rule
                        under it, the way a chosen tab is marked everywhere
                        else in this program. */
                    if (on)
                        using (var mark = new SolidBrush(palette.Bright))
                            g.FillRectangle(mark, box.X, box.Bottom + S(1),
                                            box.Width, Math.Max(1, S(2)));
                    x += widths[i];
                    if (k < names.Length - 1)
                    {
                        TextRenderer.DrawText(g, "·", f,
                            new Rectangle(x, top, gap, S(15)), Etched,
                            flags | TextFormatFlags.HorizontalCenter);
                        x += gap;
                    }
                }
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

        // ---- window behaviour ------------------------------------------

        const int WM_NCHITTEST = 0x0084;
        const int WM_NCLBUTTONDOWN = 0x00A1, WM_NCLBUTTONUP = 0x00A2;
        const int HTCLIENT = 1, HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12,
                  HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15,
                  HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17,
                  HTMINBUTTON = 8, HTCLOSE = 20;

        /*  A borderless form is given no system menu, so it gets none of the
            window management that comes with one. */
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= unchecked((int)0x00080000)     // WS_SYSMENU
                          | unchecked((int)0x00020000)     // WS_MINIMIZEBOX
                          | unchecked((int)0x00010000);    // WS_MAXIMIZEBOX
                return cp;
            }
        }

        int pressedButton;

        // What answers a click, which is larger than what is drawn: the glyph
        // belongs to a console of 1966 and eighteen pixels square is not a
        // target.
        Rectangle Reach(Rectangle drawn)
        {
            int want = S(30);
            return Rectangle.Inflate(drawn,
                Math.Max(0, (want - drawn.Width) / 2),
                Math.Max(0, (want - drawn.Height) / 2));
        }

        protected override void WndProc(ref Message m)
        {
            /*  The two window buttons, on the release and over the same
                button, the way the shell does it and the way every window on
                this machine does it. They fired on the way down, so a press
                that landed on Close and slid off still closed the
                conversation. */
            if (m.Msg == WM_NCLBUTTONDOWN)
            {
                int hit = (int)m.WParam;
                if (hit == HTCLOSE || hit == HTMINBUTTON)
                {
                    pressedButton = hit;
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == WM_NCLBUTTONUP)
            {
                int hit = (int)m.WParam;
                int was = pressedButton;
                pressedButton = 0;
                if (was != 0 && was == hit)
                {
                    if (hit == HTCLOSE) Close();
                    else WindowState = FormWindowState.Minimized;
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == WM_NCHITTEST)
            {
                var p = PointToClient(new Point(
                    (short)((long)m.LParam & 0xFFFF), (short)(((long)m.LParam >> 16) & 0xFFFF)));

                // The resize edges first, so a corner stays a corner.
                if (WindowState == FormWindowState.Normal)
                {
                    int g = S(Grip);
                    bool left = p.X <= g, right = p.X >= ClientSize.Width - g;
                    bool top = p.Y <= g, bottom = p.Y >= ClientSize.Height - g;

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

                if (Reach(closeButton).Contains(p)) { m.Result = (IntPtr)HTCLOSE; return; }
                if (Reach(minButton).Contains(p)) { m.Result = (IntPtr)HTMINBUTTON; return; }

                /*  And the rest of the top strip is the caption, which
                    Windows can have: with it come snapping to an edge, the
                    snap preview, dragging to the top to maximise, the shake,
                    Alt+Space and moving the window from the keyboard. None of
                    it looked missing until somebody tried.

                    Except the three words in that strip. The style selector
                    is drawn inside the title strip and answers clicks itself,
                    so handing Windows the whole strip would have made those
                    three words unclickable. */
                bool onWord = false;
                foreach (var spot in styleSpots)
                    if (!spot.IsEmpty && spot.Contains(p)) onWord = true;

                if (!onWord && p.Y >= 0 && p.Y < S(Pad) + S(TitleBar) &&
                    p.X >= 0 && p.X < ClientSize.Width)
                {
                    m.Result = (IntPtr)HTCAPTION;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        /*  Where a maximised borderless window may reach. Kept current here
            rather than only where a button asks for it, because dragging to
            the top, Win+Up and a double-click on the caption all maximise
            through Windows' own path. */
        protected override void OnMove(EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
            base.OnMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Only the left button commits anything. A right-click on a
            // control must never change it, and it used to change all of them.
            if (e.Button != MouseButtons.Left) return;
            for (int i = 0; i < styleSpots.Length; i++)
                if (styleSpots[i].Contains(e.Location)) { pressedWord = i + 1; return; }
            for (int i = 0; i < colourSpots.Length; i++)
                if (colourSpots[i].Contains(e.Location)) { pressedWord = -(i + 1); return; }
        }

        bool wasOverSelector;
        int pressedWord;               // +1..3 a style, -1..-3 a colour

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // Light a word as the pointer crosses it, and only repaint when
            // the answer actually changes.
            bool over = styleSpots.Concat(colourSpots).Any(r => r.Contains(e.Location));
            if (over != wasOverSelector) { wasOverSelector = over; Invalidate(); }
        }

        /*  On the release, and only over the word the press landed on. */
        protected override void OnMouseUp(MouseEventArgs e)
        {
            int was = pressedWord;
            pressedWord = 0;
            if (was == 0 || e.Button != MouseButtons.Left) return;
            if (was > 0)
            {
                if (styleSpots[was - 1].Contains(e.Location))
                    SetStyle((ScreenStyle)(was - 1));
            }
            else if (colourSpots[-was - 1].Contains(e.Location))
                SetColour(-was - 1);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            pressedWord = 0;
            base.OnMouseLeave(e);
        }

        /*  No hand-rolled double-click to maximise either: the caption is
            Windows' now and it brings that with it. */
    }
}
