// Scenes.cs -- the room each script is talked to in.
//
// The conversation window has three styles. Two of them are about the
// machine: a terminal, and the console of 1966. The third was about one
// script -- the page the Bloomfield Science Museum showed in 2010, which is
// the Hebrew ELIZA and nothing else.
//
// Once there is more than one script with a character of its own, "the
// museum" stops being a setting anybody can choose. What the setting means
// is "in character", and which character is the script's business rather
// than the settings page's: a script says (SCENE ...) and gets its own
// colours, its own face, its own frame and its own room.
//
// A scene answers for everything on the screen that is not the words.
//
// Everything here is drawn by this program. Nothing is traced, and no image
// is loaded from anywhere -- there are no images in this program at all.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace ElizaApp
{
    abstract class Scene
    {
        public static Scene For(string id)
        {
            switch ((id ?? "").Trim().ToUpperInvariant())
            {
                case "TELETYPE": return new TeletypeScene();
                case "BEIS": return new BeisScene();
                case "MUSSAR": return new MussarScene();
                case "TAPES": return new TapesScene();
                case "PARLOUR": return new ParlourScene();
                case "COURT": return new CourtScene();
                case "CORNER": return new CornerScene();
                default: return new MuseumScene();
            }
        }

        // ---- what the words are set in --------------------------------
        public abstract Palette Colours { get; }

        // The two ends of the surround's gradient, and the two inks the
        // window's own chrome is written in.
        public abstract Color Surround1 { get; }
        public abstract Color Surround2 { get; }
        public abstract Color Engraved { get; }
        public abstract Color Etched { get; }

        // The face the conversation is set in, best first.
        public abstract string[] Faces(bool hebrew);

        /*  How much of the window the scenery is allowed to take, as a
            proportion of the height. Zero means the chat box fills the
            window and only the frame belongs to the scene. */
        public virtual float Band { get { return 0.24f; } }
        public virtual float Top { get { return 0.08f; } }

        /*  How strongly the white grain shows on the surround. A sheet of
            paper has a tooth you can see; a dark room does not. */
        public virtual int Grain { get { return 9; } }

        // ---- and what is drawn ----------------------------------------

        // The edge of the chat box.
        public abstract void Frame(Graphics g, Rectangle well, float u);

        // The strip along the foot of the window.
        public abstract void Floor(Graphics g, Rectangle band, float u, bool rtl);

        // The sign above it. May draw nothing.
        public abstract void Sign(Graphics g, Rectangle box, float u, bool rtl);

        /*  A mark for the room, small enough to sit on a card.

            The opening screen has to say which of these you are choosing
            before you have been in any of them, and a name and a sentence do
            not do it. One object out of the room, in the room's own colours. */
        public virtual void Emblem(Graphics g, RectangleF r, float u) { }

        public virtual Color Mark { get { return Surround1; } }

        /*  What the room is called.

            The settings page offers "in character", because there it is a
            choice you make before you know which character. In the window
            itself you are already in the room, and "in character" is a word
            about the program rather than about where you are -- so the strip
            along the top says the room. */
        public abstract string Called(bool hebrew);

        // ---- drawing, shared ------------------------------------------

        protected static PointF P(float x, float y) { return new PointF(x, y); }

        /*  A line drawn more than once with a small offset, which is what a
            pencil, a brush and a worn ribbon all have in common: nothing is
            exactly on top of where it went last time. */
        protected static void Stroke(Graphics g, Color c, float width, params PointF[] path)
        {
            if (path.Length < 2) return;
            for (int pass = 0; pass < 2; pass++)
            {
                float shift = pass == 0 ? 0f : 0.6f;
                using (var pen = new Pen(Color.FromArgb(pass == 0 ? 205 : 85, c), width))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    var moved = new PointF[path.Length];
                    for (int i = 0; i < path.Length; i++)
                        moved[i] = new PointF(path[i].X + shift, path[i].Y + shift);
                    g.DrawLines(pen, moved);
                }
            }
        }

        protected static void Fill(Graphics g, Color c, int alpha, params PointF[] path)
        {
            if (path.Length < 3) return;
            using (var b = new SolidBrush(Color.FromArgb(alpha, c)))
                g.FillPolygon(b, path);
        }

        protected static void Box(Graphics g, Color line, Color fill, int alpha,
                                  float width, RectangleF r)
        {
            if (alpha > 0)
                using (var b = new SolidBrush(Color.FromArgb(alpha, fill)))
                    g.FillRectangle(b, r);
            Stroke(g, line, width,
                P(r.Left, r.Top), P(r.Right, r.Top),
                P(r.Right, r.Bottom), P(r.Left, r.Bottom), P(r.Left, r.Top));
        }

        // The first of these the machine actually has.
        protected static string Installed(string[] names)
        {
            using (var have = new System.Drawing.Text.InstalledFontCollection())
            {
                var got = new HashSet<string>(have.Families.Select(f => f.Name),
                                              StringComparer.OrdinalIgnoreCase);
                foreach (var n in names) if (got.Contains(n)) return n;
            }
            return names[names.Length - 1];
        }

        /*  A sign only as wide as what is written on it.

            The strip a sign is given is as wide as a sign COULD be; a plate
            stretched across all of it around three short words reads as a
            banner with a gap in the middle. */
        protected static RectangleF Plate(Graphics g, Rectangle box, string words,
                                          string face, float u, float points, float pad)
        {
            float wide;
            using (var f = new Font(face, points * u, FontStyle.Bold))
                wide = g.MeasureString(words, f).Width + pad * u;
            wide = Math.Min(box.Width, Math.Max(box.Width * 0.28f, wide));
            return new RectangleF(box.X + (box.Width - wide) / 2f, box.Y,
                                  wide, box.Height);
        }

        /*  A line of lettering that fits on one line, whatever the words are.
            A painted sign does not wrap. */
        protected static void Lettering(Graphics g, RectangleF box, string words,
                                        string face, Color ink, float u, bool rtl,
                                        float points)
        {
            using (var fmt = new StringFormat
                   {
                       Alignment = StringAlignment.Center,
                       LineAlignment = StringAlignment.Center,
                       FormatFlags = (rtl ? StringFormatFlags.DirectionRightToLeft : 0)
                                     | StringFormatFlags.NoWrap
                   })
            using (var b = new SolidBrush(ink))
            {
                float size = points * u;
                Font f = null;
                try
                {
                    while (size > 5f * u)
                    {
                        f = new Font(face, size, FontStyle.Bold);
                        if (g.MeasureString(words, f, int.MaxValue, fmt).Width
                            <= box.Width - 8 * u) break;
                        f.Dispose();
                        f = null;
                        size -= 0.5f * u;
                    }
                    if (f == null) f = new Font(face, 5f * u, FontStyle.Bold);
                    g.DrawString(words, f, b, box, fmt);
                }
                finally { if (f != null) f.Dispose(); }
            }
        }
    }

    // ==================================================================
    //  The Science Museum's page, 2010 -- ELIZA in Hebrew
    // ==================================================================
    class MuseumScene : Scene
    {
        public override string Called(bool hebrew)
        {
            return hebrew ? "מוזיאון המדע" : "The Science Museum";
        }

        public override Palette Colours { get { return Palette.For(Phosphor.Paper); } }
        public override Color Surround1 { get { return Color.FromArgb(252, 250, 245); } }
        public override Color Surround2 { get { return Color.FromArgb(232, 226, 214); } }
        public override Color Engraved { get { return Color.FromArgb(0, 0, 190); } }
        public override Color Etched { get { return Color.FromArgb(95, 95, 160); } }

        /*  Courier, whichever language: the page asked for 'Courier New',
            Courier, monospace and got it, and the letter shapes are half of
            why it looked the way it did. */
        public override string[] Faces(bool hebrew)
        {
            return new[] { "Courier New", "Miriam Fixed", "Consolas" };
        }

        public override int Grain { get { return 18; } }

        /*  Not a bezel. The museum's page was a plain white box on a drawing,
            and what framed it was four pixels of dotted blue -- the CSS
            default, square corners, no depth at all. It is the detail anyone
            who stood in front of it would recognise, and the one thing a
            picture of a room could never carry. */
        public override void Frame(Graphics g, Rectangle well, float u)
        {
            using (var sheet = new SolidBrush(Color.White))
                g.FillRectangle(sheet, well);
            using (var dotted = new Pen(Color.FromArgb(0, 0, 255), 4f * u))
            {
                dotted.DashStyle = DashStyle.Dot;
                dotted.DashCap = DashCap.Round;
                g.DrawRectangle(dotted, well);
            }
        }

        public override void Floor(Graphics g, Rectangle band, float u, bool rtl)
        {
            Museum.Room(g, band, Rectangle.Empty, u);
        }

        public override Color Mark { get { return Color.FromArgb(236, 132, 118); } }

        // The flower off the plaque.
        public override void Emblem(Graphics g, RectangleF r, float u)
        {
            Museum.Hibiscus(g, r.X + r.Width / 2f, r.Y + r.Height / 2f,
                            Math.Min(r.Width, r.Height) * 0.46f, u);
        }

        public override void Sign(Graphics g, Rectangle box, float u, bool rtl)
        {
            Museum.Plaque(g, box,
                rtl ? "אלייזה – הפסיכולוגית הווירטואלית"
                    : "ELIZA – the virtual psychologist",
                u, rtl);
        }
    }

    // ==================================================================
    //  A Model 33 teletype -- ELIZA in English, 1966
    // ==================================================================
    /*  Nobody in 1966 saw ELIZA on a screen. She came out of a teleprinter,
        struck one character at a time onto a roll of paper, and what people
        kept afterwards was the roll. Weizenbaum's paper prints a conversation
        as a printout because that is what a conversation was.

        So the English script's own scene is the paper: the chat box is the
        sheet, the sprocket strips run down both edges of it, and the machine
        that is printing it stands along the foot of the window. */
    class TeletypeScene : Scene
    {
        static readonly Color Ink = Color.FromArgb(38, 34, 28);
        static readonly Color Steel = Color.FromArgb(108, 110, 104);
        static readonly Color Dark = Color.FromArgb(62, 64, 60);

        public override Palette Colours
        {
            get
            {
                return new Palette
                {
                    Screen = Color.FromArgb(250, 246, 236),
                    Normal = Color.FromArgb(44, 40, 34),
                    Bright = Color.FromArgb(12, 10, 8),
                    Dim = Color.FromArgb(122, 116, 102),
                    User = Color.FromArgb(88, 80, 66)
                };
            }
        }

        public override Color Surround1 { get { return Color.FromArgb(154, 152, 142); } }
        public override Color Surround2 { get { return Color.FromArgb(104, 103, 96); } }
        public override Color Engraved { get { return Color.FromArgb(28, 28, 26); } }
        public override Color Etched { get { return Color.FromArgb(66, 66, 62); } }

        public override string[] Faces(bool hebrew)
        {
            return hebrew
                ? new[] { "Miriam Fixed", "Courier New", "Consolas" }
                : new[] { "Courier New", "Consolas", "Lucida Console" };
        }

        public override float Band { get { return 0.22f; } }

        /*  No sign. The roll's own header is the first thing printed on it,
            and it is printed on it: the banner inside the chat box already
            says MASSACHUSETTS INSTITUTE OF TECHNOLOGY, PROJECT MAC, 1966. A
            second one hanging over the paper would be a caption on a
            photograph of a caption. */
        public override float Top { get { return 0f; } }
        public override int Grain { get { return 14; } }

        /*  The sheet, with the sprocket strip down each side: a column of
            square holes on a narrow margin, and a perforation line between
            the margin and the printed part. */
        public override void Frame(Graphics g, Rectangle well, float u)
        {
            using (var paper = new SolidBrush(Color.FromArgb(250, 246, 236)))
                g.FillRectangle(paper, well);

            float strip = Math.Max(14f * u, well.Width * 0.030f);
            float hole = strip * 0.42f;
            float step = hole * 2.6f;

            using (var edge = new Pen(Color.FromArgb(90, Ink), 1f))
            using (var punched = new SolidBrush(Color.FromArgb(70, 116, 112, 98)))
            {
                foreach (float x in new[] { well.Left + strip * 0.5f, well.Right - strip * 0.5f })
                    for (float y = well.Top + step * 0.7f; y < well.Bottom - step * 0.3f; y += step)
                    {
                        var h = new RectangleF(x - hole / 2f, y - hole / 2f, hole, hole);
                        g.FillRectangle(punched, h);
                        g.DrawRectangle(edge, h.X, h.Y, h.Width, h.Height);
                    }

                // The perforation: a dotted line, not a rule.
                using (var perf = new Pen(Color.FromArgb(58, Ink), 1f))
                {
                    perf.DashStyle = DashStyle.Dot;
                    g.DrawLine(perf, well.Left + strip, well.Top, well.Left + strip, well.Bottom);
                    g.DrawLine(perf, well.Right - strip, well.Top, well.Right - strip, well.Bottom);
                }
            }

            // And the shadow the sheet casts on the desk.
            using (var under = new SolidBrush(Color.FromArgb(46, 0, 0, 0)))
                g.FillRectangle(under, well.Left + 2 * u, well.Bottom, well.Width, 3f * u);
        }

        /*  The machine: a grey box with a platen across the top, the paper
            coming up out of it, a keyboard sloping towards you and a paper
            tape reader on one side. Drawn small -- it is furniture, not the
            subject. */
        public override void Floor(Graphics g, Rectangle band, float u, bool rtl)
        {
            if (band.Height < 40 * u) return;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float unit = band.Height / 5f;
            float floor = band.Y + band.Height * 0.88f;
            float wide = Math.Min(band.Width * 0.62f, unit * 11f);
            float cx = band.X + band.Width / 2f;
            float left = cx - wide / 2f;

            // the desk it stands on
            Stroke(g, Dark, 1.4f * u, P(band.X + 4 * u, floor), P(band.Right - 4 * u, floor));

            // the body
            var body = new RectangleF(left, floor - unit * 2.3f, wide, unit * 2.3f);
            Box(g, Dark, Steel, 150, 1.6f * u, body);

            // the platen across the top, and the paper coming out of it
            var platen = new RectangleF(left + wide * 0.10f, body.Top - unit * 0.42f,
                                        wide * 0.80f, unit * 0.52f);
            using (var b = new SolidBrush(Color.FromArgb(190, Dark)))
            using (var path = new GraphicsPath())
            {
                path.AddArc(platen.X, platen.Y, platen.Height, platen.Height, 90, 180);
                path.AddArc(platen.Right - platen.Height, platen.Y,
                            platen.Height, platen.Height, 270, 180);
                path.CloseFigure();
                g.FillPath(b, path);
            }
            /*  The paper comes out of the machine and goes up into the
                sheet above. The band starts a window-margin below the foot of
                the chat box, so it is drawn that much higher than the band --
                otherwise the roll reads as a second, separate sheet lying on
                the desk. */
            float paperL = platen.X + platen.Width * 0.08f;
            float paperR = platen.Right - platen.Width * 0.08f;
            float paperTop = band.Y - 30f * u;
            using (var paper = new SolidBrush(Color.FromArgb(240, 250, 246, 236)))
                g.FillRectangle(paper, paperL, paperTop,
                                paperR - paperL, platen.Top - paperTop + 1);
            Stroke(g, Color.FromArgb(110, Ink), 1f * u,
                P(paperL, paperTop), P(paperL, platen.Top));
            Stroke(g, Color.FromArgb(110, Ink), 1f * u,
                P(paperR, paperTop), P(paperR, platen.Top));
            // and the last line it printed, still in the machine
            using (var pen = new Pen(Color.FromArgb(70, Ink), 1f * u))
                g.DrawLine(pen, paperL + unit * 0.25f, platen.Top - unit * 0.22f,
                                paperR - unit * 0.25f, platen.Top - unit * 0.22f);

            // the keyboard, three rows sloping away
            using (var key = new Pen(Color.FromArgb(150, Dark), 1f * u))
                for (int row = 0; row < 3; row++)
                {
                    float y = body.Top + unit * (0.85f + row * 0.42f);
                    float inset = wide * (0.16f + row * 0.012f);
                    for (float x = left + inset; x < body.Right - inset; x += unit * 0.42f)
                        g.DrawEllipse(key, x, y, unit * 0.26f, unit * 0.20f);
                }

            /*  The paper-tape reader, bolted to the side of the machine the
                reading runs away from: a stand, a reel of tape on it, and the
                tape going down into the body. */
            float side = rtl ? left : body.Right;
            float away = rtl ? -1f : 1f;
            float rx = side + away * unit * 1.15f;
            float rr = unit * 0.78f;
            if (rx - rr > band.X + 2 * u && rx + rr < band.Right - 2 * u)
            {
                Stroke(g, Dark, 1.4f * u,
                    P(side, floor - unit * 1.1f), P(rx, floor - unit * 1.1f));
                Stroke(g, Dark, 1.4f * u,
                    P(rx, floor - unit * 1.1f), P(rx, floor - unit * 1.55f + rr));
                using (var pen = new Pen(Color.FromArgb(190, Dark), 1.5f * u))
                    g.DrawEllipse(pen, rx - rr, floor - unit * 1.55f - rr, rr * 2, rr * 2);
                using (var b = new SolidBrush(Color.FromArgb(120, Steel)))
                    g.FillEllipse(b, rx - rr * 0.92f, floor - unit * 1.55f - rr * 0.92f,
                                  rr * 1.84f, rr * 1.84f);
                using (var pen = new Pen(Color.FromArgb(170, Dark), 1.2f * u))
                    g.DrawEllipse(pen, rx - rr * 0.26f, floor - unit * 1.55f - rr * 0.26f,
                                  rr * 0.52f, rr * 0.52f);
            }

            g.SmoothingMode = was;
        }

        /*  Typed, not painted: the header of the roll, struck by the same
            machine, in the same ribbon, a little above the sheet. */
        public override Color Mark { get { return Color.FromArgb(206, 202, 190); } }

        // A sheet with the sprocket strip down one side, and a line of type.
        public override void Emblem(Graphics g, RectangleF r, float u)
        {
            float w = r.Width * 0.62f, h = r.Height * 0.78f;
            var sheet = new RectangleF(r.X + (r.Width - w) / 2f,
                                       r.Y + (r.Height - h) / 2f, w, h);
            using (var b = new SolidBrush(Color.FromArgb(250, 246, 236)))
                g.FillRectangle(b, sheet);
            using (var pen = new Pen(Color.FromArgb(150, Ink), Math.Max(1f, 0.9f * u)))
                g.DrawRectangle(pen, sheet.X, sheet.Y, sheet.Width, sheet.Height);
            float hole = w * 0.13f;
            using (var b = new SolidBrush(Color.FromArgb(120, Dark)))
                for (float y = sheet.Top + hole; y < sheet.Bottom - hole * 0.6f; y += hole * 2.1f)
                    g.FillRectangle(b, sheet.X + hole * 0.5f, y - hole / 2f, hole, hole);
            using (var pen = new Pen(Color.FromArgb(170, Ink), Math.Max(1f, 0.9f * u)))
                for (int i = 0; i < 3; i++)
                    g.DrawLine(pen, sheet.X + hole * 2.2f,
                        sheet.Top + h * (0.28f + i * 0.2f),
                        sheet.Right - hole * 0.7f, sheet.Top + h * (0.28f + i * 0.2f));
        }

        public override void Sign(Graphics g, Rectangle box, float u, bool rtl) { }

        public override string Called(bool hebrew)
        {
            return hebrew ? "טלטייפ" : "The teletype";
        }
    }

    // ==================================================================
    //  A beis medrash -- the chikaber
    // ==================================================================
    /*  A study hall, from the seat of somebody who has been sitting in one
        since the morning: a lectern with an open volume on it, a bench, a
        case of books along one wall, and an arched window with the light
        going. Wood and paper, and nothing that was made after 1900. */
    class BeisScene : Scene
    {
        static readonly Color Wood = Color.FromArgb(124, 84, 48);
        static readonly Color Deep = Color.FromArgb(74, 48, 26);
        static readonly Color Page = Color.FromArgb(246, 236, 214);
        static readonly Color Ink = Color.FromArgb(48, 36, 24);
        static readonly Color Gold = Color.FromArgb(176, 138, 68);
        static readonly Color Dusk = Color.FromArgb(158, 146, 116);

        public override Palette Colours
        {
            get
            {
                return new Palette
                {
                    Screen = Color.FromArgb(248, 240, 222),
                    Normal = Color.FromArgb(46, 34, 22),
                    Bright = Color.FromArgb(18, 12, 6),
                    Dim = Color.FromArgb(126, 106, 78),
                    User = Color.FromArgb(40, 62, 108)
                };
            }
        }

        public override Color Surround1 { get { return Color.FromArgb(120, 86, 54); } }
        public override Color Surround2 { get { return Color.FromArgb(64, 44, 26); } }
        public override Color Engraved { get { return Color.FromArgb(246, 232, 204); } }
        public override Color Etched { get { return Color.FromArgb(206, 186, 150); } }

        /*  A face with a Hebrew book in it. Frank Ruehl is the type a
            volume of the Talmud is set in and it ships with Windows; the
            engine needs a fixed pitch, so the fixed Hebrew face comes
            first and Frank Ruehl is what a machine without it falls to. */
        public override string[] Faces(bool hebrew)
        {
            return hebrew
                ? new[] { "Miriam Fixed", "FrankRuehl", "David", "Courier New" }
                : new[] { "Courier New", "Consolas" };
        }

        public override float Band { get { return 0.25f; } }
        public override float Top { get { return 0.075f; } }

        // A page, held in a wooden frame with a gold rule inside it.
        public override void Frame(Graphics g, Rectangle well, float u)
        {
            using (var page = new SolidBrush(Color.FromArgb(248, 240, 222)))
                g.FillRectangle(page, well);
            using (var pen = new Pen(Deep, 3.2f * u))
                g.DrawRectangle(pen, well);
            using (var pen = new Pen(Color.FromArgb(190, Gold), 1.1f * u))
                g.DrawRectangle(pen, well.X + (int)(3.4f * u), well.Y + (int)(3.4f * u),
                                well.Width - (int)(6.8f * u), well.Height - (int)(6.8f * u));
        }

        public override void Floor(Graphics g, Rectangle band, float u, bool rtl)
        {
            if (band.Height < 40 * u) return;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float unit = band.Height / 5f;
            float floor = band.Y + band.Height * 0.86f;

            // the floor
            Stroke(g, Deep, 1.6f * u, P(band.X + 3 * u, floor), P(band.Right - 3 * u, floor));

            float shelfW = unit * 3.4f, standW = unit * 2.5f, benchW = unit * 3.2f;
            float gap = unit * 0.7f;
            float total = shelfW + standW + benchW + gap * 2;
            float x = band.X + (band.Width - total) / 2f;
            if (total > band.Width) { x = band.X + gap; }

            float shelfX = rtl ? x + benchW + gap * 2 + standW : x;
            float standX = x + (rtl ? benchW + gap : shelfW + gap);
            float benchX = rtl ? x : x + shelfW + gap * 2 + standW;

            Bookcase(g, new RectangleF(shelfX, floor - unit * 3.3f, shelfW, unit * 3.3f), u);
            Bench(g, new RectangleF(benchX, floor - unit * 1.25f, benchW, unit * 1.25f), u);
            Shtender(g, new RectangleF(standX, floor - unit * 2.6f, standW, unit * 2.6f), u, rtl);

            g.SmoothingMode = was;
        }

        // A case of volumes, spines out, not quite upright.
        static void Bookcase(Graphics g, RectangleF r, float u)
        {
            Box(g, Deep, Wood, 60, 1.6f * u, r);
            int shelves = 3;
            for (int s = 0; s < shelves; s++)
            {
                float top = r.Top + r.Height * (s / (float)shelves);
                float bottom = r.Top + r.Height * ((s + 1) / (float)shelves);
                Stroke(g, Deep, 1.3f * u, P(r.Left, bottom), P(r.Right, bottom));

                float x = r.Left + 2f * u;
                int i = 0;
                while (x < r.Right - 3f * u)
                {
                    // Wide enough to read as a volume. At two pixels a shelf
                    // was a row of hatching.
                    float w = (bottom - top) * (0.17f + (i % 3) * 0.05f);
                    float lean = (i % 5 == 4) ? 1.6f * u : 0f;
                    float h = (bottom - top) * (0.80f + (i % 2) * 0.10f);
                    Fill(g, i % 2 == 0 ? Deep : Wood, 165,
                        P(x, bottom - 1), P(x + lean, bottom - h),
                        P(x + w + lean, bottom - h), P(x + w, bottom - 1));
                    Stroke(g, Ink, 0.8f * u,
                        P(x, bottom - 1), P(x + lean, bottom - h));
                    // the gold band across the spine
                    using (var b = new SolidBrush(Color.FromArgb(150, Gold)))
                        g.FillRectangle(b, x + lean * 0.6f, bottom - h * 0.72f, w, 1.1f * u);
                    x += w + 1.1f * u;
                    i++;
                }
            }
        }

        // A lectern: a sloping top on a post, with an open volume on it.
        static void Shtender(Graphics g, RectangleF r, float u, bool rtl)
        {
            float midX = r.Left + r.Width / 2f;

            // the post and the foot
            Fill(g, Wood, 150,
                P(midX - r.Width * 0.09f, r.Bottom - 1),
                P(midX - r.Width * 0.07f, r.Top + r.Height * 0.42f),
                P(midX + r.Width * 0.07f, r.Top + r.Height * 0.42f),
                P(midX + r.Width * 0.09f, r.Bottom - 1));
            Stroke(g, Deep, 1.4f * u,
                P(midX - r.Width * 0.30f, r.Bottom - 1),
                P(midX + r.Width * 0.30f, r.Bottom - 1));

            // the sloping top
            var top = new PointF[]
            {
                P(r.Left, r.Top + r.Height * 0.30f),
                P(r.Right, r.Top + r.Height * 0.16f),
                P(r.Right, r.Top + r.Height * 0.26f),
                P(r.Left, r.Top + r.Height * 0.40f),
                P(r.Left, r.Top + r.Height * 0.30f)
            };
            Fill(g, Wood, 175, top);
            Stroke(g, Deep, 1.5f * u, top);

            // the open volume, two leaves meeting in the middle
            float bookH = r.Height * 0.26f;
            float bookY = r.Top + r.Height * 0.06f;
            var leafL = new PointF[]
            {
                P(r.Left + r.Width * 0.06f, bookY + bookH * 0.55f),
                P(midX, bookY),
                P(midX, bookY + bookH * 0.72f),
                P(r.Left + r.Width * 0.06f, bookY + bookH * 1.10f)
            };
            var leafR = new PointF[]
            {
                P(r.Right - r.Width * 0.06f, bookY + bookH * 0.40f),
                P(midX, bookY),
                P(midX, bookY + bookH * 0.72f),
                P(r.Right - r.Width * 0.06f, bookY + bookH * 0.96f)
            };
            Fill(g, Page, 230, leafL);
            Fill(g, Page, 230, leafR);
            Stroke(g, Ink, 1.1f * u, leafL);
            Stroke(g, Ink, 1.1f * u, leafR);

            /*  The type on the page: a block of small lines in the middle
                with wider margins either side of it, which is what a page of
                the Talmud looks like from across a room and the only thing
                about it that carries at this size. */
            using (var pen = new Pen(Color.FromArgb(120, Ink), 0.7f * u))
                for (int i = 0; i < 5; i++)
                {
                    float y = bookY + bookH * (0.28f + i * 0.13f);
                    float inset = i < 1 || i > 3 ? r.Width * 0.17f : r.Width * 0.11f;
                    g.DrawLine(pen, r.Left + inset, y + bookH * 0.10f, midX - r.Width * 0.03f, y);
                    g.DrawLine(pen, midX + r.Width * 0.03f, y, r.Right - inset, y + bookH * 0.06f);
                }
        }

        // A bench, worn, with a coat over one end of it.
        static void Bench(Graphics g, RectangleF r, float u)
        {
            Fill(g, Wood, 150,
                P(r.Left, r.Top), P(r.Right, r.Top - r.Height * 0.06f),
                P(r.Right, r.Top + r.Height * 0.18f), P(r.Left, r.Top + r.Height * 0.24f));
            Stroke(g, Deep, 1.4f * u,
                P(r.Left, r.Top), P(r.Right, r.Top - r.Height * 0.06f),
                P(r.Right, r.Top + r.Height * 0.18f), P(r.Left, r.Top + r.Height * 0.24f),
                P(r.Left, r.Top));
            Stroke(g, Deep, 1.3f * u,
                P(r.Left + r.Width * 0.10f, r.Top + r.Height * 0.22f),
                P(r.Left + r.Width * 0.12f, r.Bottom - 1));
            Stroke(g, Deep, 1.3f * u,
                P(r.Right - r.Width * 0.10f, r.Top + r.Height * 0.16f),
                P(r.Right - r.Width * 0.12f, r.Bottom - 1));
        }

        public override Color Mark { get { return Color.FromArgb(196, 154, 98); } }

        /*  An open volume, seen from across the room: two leaves meeting in
            the middle and a block of type on each. */
        public override void Emblem(Graphics g, RectangleF r, float u)
        {
            float w = r.Width * 0.84f, h = r.Height * 0.56f;
            float cx = r.X + r.Width / 2f, top = r.Y + (r.Height - h) / 2f;
            var leafL = new[]
            {
                P(cx - w / 2f, top + h * 0.16f), P(cx, top),
                P(cx, top + h * 0.84f), P(cx - w / 2f, top + h)
            };
            var leafR = new[]
            {
                P(cx + w / 2f, top + h * 0.16f), P(cx, top),
                P(cx, top + h * 0.84f), P(cx + w / 2f, top + h)
            };
            Fill(g, Page, 245, leafL);
            Fill(g, Page, 245, leafR);
            Stroke(g, Ink, Math.Max(1f, 0.9f * u), leafL);
            Stroke(g, Ink, Math.Max(1f, 0.9f * u), leafR);
            using (var pen = new Pen(Color.FromArgb(130, Ink), Math.Max(1f, 0.7f * u)))
                for (int i = 0; i < 4; i++)
                {
                    float y = top + h * (0.26f + i * 0.16f);
                    float inset = (i == 0 || i == 3) ? w * 0.18f : w * 0.10f;
                    g.DrawLine(pen, cx - w / 2f + inset, y + h * 0.08f, cx - w * 0.05f, y);
                    g.DrawLine(pen, cx + w * 0.05f, y, cx + w / 2f - inset, y + h * 0.06f);
                }
        }

        public override string Called(bool hebrew)
        {
            return hebrew ? "בית המדרש" : "The study hall";
        }

        public override void Sign(Graphics g, Rectangle box, float u, bool rtl)
        {
            // A board over the door, carved and painted.
            string face = Installed(new[] { "FrankRuehl", "David", "Narkisim", "Segoe UI" });
            string words = Called(rtl);
            var plate = Plate(g, new Rectangle(box.X, box.Y, box.Width,
                                               (int)(box.Height * 0.92f)),
                              words, face, u, 11f, 56f);
            using (var b = new SolidBrush(Color.FromArgb(225, Deep)))
            using (var path = new GraphicsPath())
            {
                float d = Math.Min(plate.Height, plate.Width) * 0.45f;
                path.AddArc(plate.X, plate.Y, d, d, 180, 90);
                path.AddArc(plate.Right - d, plate.Y, d, d, 270, 90);
                path.AddArc(plate.Right - d, plate.Bottom - d, d, d, 0, 90);
                path.AddArc(plate.X, plate.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(190, Gold), 1.2f * u))
                    g.DrawPath(pen, path);
            }
            Lettering(g, plate, words, face,
                Color.FromArgb(240, 238, 222, 186), u, rtl, 11f);
        }
    }

    // ==================================================================
    //  The hall at night -- the mashgiach
    // ==================================================================
    /*  The same hall, hours later. One bulb on a flex, a lectern under it, a
        row of benches nobody is sitting on, and everything else out of the
        light. The mashgiach's talk is given at night, and that is the whole
        of the reason the room is drawn this way. */
    class MussarScene : Scene
    {
        static readonly Color Wall = Color.FromArgb(24, 27, 30);
        static readonly Color Wood = Color.FromArgb(58, 44, 32);
        static readonly Color Lamp = Color.FromArgb(226, 190, 118);
        static readonly Color Cold = Color.FromArgb(86, 96, 106);

        public override Palette Colours
        {
            get
            {
                return new Palette
                {
                    Screen = Color.FromArgb(18, 20, 23),
                    Normal = Color.FromArgb(214, 208, 196),
                    Bright = Color.FromArgb(250, 246, 236),
                    Dim = Color.FromArgb(132, 140, 148),
                    User = Color.FromArgb(214, 172, 96)
                };
            }
        }

        public override Color Surround1 { get { return Color.FromArgb(20, 23, 26); } }
        public override Color Surround2 { get { return Color.FromArgb(8, 10, 12); } }
        public override Color Engraved { get { return Color.FromArgb(212, 206, 194); } }
        public override Color Etched { get { return Color.FromArgb(138, 142, 148); } }

        public override string[] Faces(bool hebrew)
        {
            return hebrew
                ? new[] { "Miriam Fixed", "FrankRuehl", "David", "Courier New" }
                : new[] { "Consolas", "Courier New" };
        }

        public override float Band { get { return 0.26f; } }
        public override float Top { get { return 0.07f; } }

        // A thin brass rule, and nothing else. The room is dark; the page is
        // the only thing with light on it.
        public override void Frame(Graphics g, Rectangle well, float u)
        {
            using (var b = new SolidBrush(Color.FromArgb(18, 20, 23)))
                g.FillRectangle(b, well);
            using (var pen = new Pen(Color.FromArgb(150, Lamp), 1.4f * u))
                g.DrawRectangle(pen, well);
            using (var glow = new SolidBrush(Color.FromArgb(16, Lamp)))
                g.FillRectangle(glow, well.X, well.Y, well.Width, (int)(10 * u));
        }

        public override void Floor(Graphics g, Rectangle band, float u, bool rtl)
        {
            if (band.Height < 40 * u) return;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float unit = band.Height / 5f;
            float floor = band.Y + band.Height * 0.88f;
            float cx = band.X + band.Width * (rtl ? 0.60f : 0.40f);

            // the cone of light, and the pool of it on the floor
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[]
                {
                    P(cx - unit * 0.55f, band.Y + unit * 0.55f),
                    P(cx + unit * 0.55f, band.Y + unit * 0.55f),
                    P(cx + unit * 2.6f, floor),
                    P(cx - unit * 2.6f, floor)
                });
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = P(cx, band.Y + unit * 0.7f);
                    brush.CenterColor = Color.FromArgb(54, Lamp);
                    brush.SurroundColors = new[] { Color.FromArgb(0, Lamp) };
                    g.FillPath(brush, path);
                }
            }
            using (var b = new SolidBrush(Color.FromArgb(26, Lamp)))
                g.FillEllipse(b, cx - unit * 2.4f, floor - unit * 0.34f,
                              unit * 4.8f, unit * 0.68f);

            // the flex and the bulb
            Stroke(g, Cold, 1.1f * u, P(cx, band.Y), P(cx, band.Y + unit * 0.5f));
            using (var b = new SolidBrush(Color.FromArgb(235, Lamp)))
                g.FillEllipse(b, cx - unit * 0.20f, band.Y + unit * 0.48f,
                              unit * 0.40f, unit * 0.52f);
            using (var b = new SolidBrush(Color.FromArgb(60, Lamp)))
                g.FillEllipse(b, cx - unit * 0.55f, band.Y + unit * 0.18f,
                              unit * 1.10f, unit * 1.15f);

            // the floor line
            Stroke(g, Cold, 1.3f * u, P(band.X + 3 * u, floor), P(band.Right - 3 * u, floor));

            // the lectern, under the light
            float sw = unit * 2.0f;
            var stand = new RectangleF(cx - sw / 2f, floor - unit * 2.2f, sw, unit * 2.2f);
            Fill(g, Wood, 200,
                P(stand.Left + sw * 0.40f, stand.Bottom - 1),
                P(stand.Left + sw * 0.44f, stand.Top + stand.Height * 0.44f),
                P(stand.Left + sw * 0.56f, stand.Top + stand.Height * 0.44f),
                P(stand.Left + sw * 0.60f, stand.Bottom - 1));
            var top = new PointF[]
            {
                P(stand.Left, stand.Top + stand.Height * 0.34f),
                P(stand.Right, stand.Top + stand.Height * 0.18f),
                P(stand.Right, stand.Top + stand.Height * 0.30f),
                P(stand.Left, stand.Top + stand.Height * 0.46f),
                P(stand.Left, stand.Top + stand.Height * 0.34f)
            };
            Fill(g, Wood, 215, top);
            Stroke(g, Color.FromArgb(150, Lamp), 1.2f * u, top);
            Stroke(g, Cold, 1.2f * u,
                P(stand.Left + sw * 0.20f, stand.Bottom - 1),
                P(stand.Right - sw * 0.20f, stand.Bottom - 1));

            // and the benches, out of the light, in both directions
            for (int side = 0; side < 2; side++)
                for (int i = 1; i <= 2; i++)
                {
                    float bx = cx + (side == 0 ? -1 : 1) * unit * (2.4f + i * 1.9f);
                    float bw = unit * 1.5f;
                    if (bx - bw / 2 < band.X + unit * 0.3f) continue;
                    if (bx + bw / 2 > band.Right - unit * 0.3f) continue;
                    /*  A plank on two short legs, not a box: the legs used to
                        run all the way to the floor line, which closed the
                        shape and made every bench a rectangle. */
                    int fade = 110 - i * 30;
                    float seat = floor - unit * 0.62f;
                    Stroke(g, Color.FromArgb(fade, Cold), 1.6f * u,
                        P(bx - bw / 2, seat), P(bx + bw / 2, seat - unit * 0.04f));
                    Stroke(g, Color.FromArgb(fade, Cold), 1.0f * u,
                        P(bx - bw / 2 + 3 * u, seat), P(bx - bw / 2 + 3.4f * u, floor));
                    Stroke(g, Color.FromArgb(fade, Cold), 1.0f * u,
                        P(bx + bw / 2 - 3 * u, seat - unit * 0.04f),
                        P(bx + bw / 2 - 3.4f * u, floor));
                }

            g.SmoothingMode = was;
        }

        public override Color Mark { get { return Color.FromArgb(30, 33, 37); } }

        // The bulb on its flex, and the cone under it.
        public override void Emblem(Graphics g, RectangleF r, float u)
        {
            float cx = r.X + r.Width / 2f;
            float top = r.Y + r.Height * 0.16f;
            float bulb = r.Height * 0.15f;

            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[]
                {
                    P(cx - bulb, top + bulb), P(cx + bulb, top + bulb),
                    P(cx + r.Width * 0.34f, r.Bottom - r.Height * 0.16f),
                    P(cx - r.Width * 0.34f, r.Bottom - r.Height * 0.16f)
                });
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = P(cx, top + bulb);
                    brush.CenterColor = Color.FromArgb(150, Lamp);
                    brush.SurroundColors = new[] { Color.FromArgb(0, Lamp) };
                    g.FillPath(brush, path);
                }
            }
            Stroke(g, Cold, Math.Max(1f, 0.9f * u), P(cx, r.Top), P(cx, top));
            using (var b = new SolidBrush(Color.FromArgb(60, Lamp)))
                g.FillEllipse(b, cx - bulb * 2.2f, top - bulb * 1.2f,
                              bulb * 4.4f, bulb * 4.4f);
            using (var b = new SolidBrush(Lamp))
                g.FillEllipse(b, cx - bulb, top, bulb * 2, bulb * 2.3f);
        }

        public override string Called(bool hebrew)
        {
            return hebrew ? "שיחת מוסר" : "The mussar talk";
        }

        public override void Sign(Graphics g, Rectangle box, float u, bool rtl)
        {
            // A small brass plate, the kind screwed to a door.
            string face = Installed(new[] { "FrankRuehl", "David", "Narkisim", "Segoe UI" });
            string words = Called(rtl);
            var plate = Plate(g, new Rectangle(box.X, box.Y + (int)(box.Height * 0.14f),
                                               box.Width, (int)(box.Height * 0.66f)),
                              words, face, u, 9.5f, 46f);
            using (var b = new LinearGradientBrush(plate,
                       Color.FromArgb(210, 150, 126, 74),
                       Color.FromArgb(210, 96, 80, 46), 70f))
                g.FillRectangle(b, plate);
            using (var pen = new Pen(Color.FromArgb(180, Lamp), 1f * u))
                g.DrawRectangle(pen, plate.X, plate.Y, plate.Width, plate.Height);
            Lettering(g, plate, words, face,
                Color.FromArgb(240, 244, 232, 206), u, rtl, 9.5f);
        }
    }

    // ==================================================================
    //  The machine room -- ELIZA taken as far as 1966 could take her
    // ==================================================================
    /*  What the screen would have been attached to. The 7094 was not a box on
        a desk: it was a room of cabinets with tape drives in them, and the
        reels turning was how you knew anything was happening at all.
        This scene is for the model that asks what she could have been if
        somebody had kept writing the script for another ten years. */
    class TapesScene : Scene
    {
        static readonly Color Steel = Color.FromArgb(92, 100, 110);
        static readonly Color Dark = Color.FromArgb(38, 44, 52);
        static readonly Color Glass = Color.FromArgb(168, 186, 204);
        static readonly Color Lit = Color.FromArgb(120, 226, 150);

        public override Palette Colours
        {
            get
            {
                return new Palette
                {
                    Screen = Color.FromArgb(5, 18, 11),
                    Normal = Color.FromArgb(72, 226, 126),
                    Bright = Color.FromArgb(190, 255, 206),
                    Dim = Color.FromArgb(62, 158, 96),
                    User = Color.FromArgb(226, 255, 230)
                };
            }
        }

        public override Color Surround1 { get { return Color.FromArgb(78, 86, 96); } }
        public override Color Surround2 { get { return Color.FromArgb(30, 35, 41); } }
        public override Color Engraved { get { return Color.FromArgb(206, 214, 222); } }
        public override Color Etched { get { return Color.FromArgb(150, 158, 168); } }

        public override string[] Faces(bool hebrew)
        {
            return hebrew
                ? new[] { "Miriam Fixed", "Courier New", "Consolas" }
                : new[] { "Consolas", "Lucida Console", "Courier New" };
        }

        public override float Band { get { return 0.24f; } }
        public override float Top { get { return 0.065f; } }

        // A heavy bezel, the way a console screen was set into a cabinet.
        public override void Frame(Graphics g, Rectangle well, float u)
        {
            using (var b = new LinearGradientBrush(well,
                       Color.FromArgb(16, 18, 22), Color.FromArgb(58, 64, 72), 64f))
                g.FillRectangle(b, well);
            using (var pen = new Pen(Color.FromArgb(210, Dark), 3f * u))
                g.DrawRectangle(pen, well);
            using (var pen = new Pen(Color.FromArgb(120, Steel), 1f * u))
                g.DrawRectangle(pen, well.X - (int)(2 * u), well.Y - (int)(2 * u),
                                well.Width + (int)(4 * u), well.Height + (int)(4 * u));
        }

        public override void Floor(Graphics g, Rectangle band, float u, bool rtl)
        {
            if (band.Height < 40 * u) return;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float unit = band.Height / 5f;
            float floor = band.Y + band.Height * 0.90f;
            Stroke(g, Dark, 1.4f * u, P(band.X + 3 * u, floor), P(band.Right - 3 * u, floor));

            float cabW = unit * 2.5f, gap = unit * 0.34f;
            int most = Math.Max(1, (int)((band.Width - gap) / (cabW + gap)));
            int count = Math.Min(5, most);
            float total = count * cabW + (count - 1) * gap;
            float x = band.X + (band.Width - total) / 2f;

            for (int i = 0; i < count; i++)
            {
                Cabinet(g, new RectangleF(x, floor - unit * 3.2f, cabW, unit * 3.2f), u, i);
                x += cabW + gap;
            }

            g.SmoothingMode = was;
        }

        static void Cabinet(Graphics g, RectangleF r, float u, int which)
        {
            using (var b = new LinearGradientBrush(r,
                       Color.FromArgb(200, Steel), Color.FromArgb(220, Dark), 20f))
                g.FillRectangle(b, r);
            Stroke(g, Dark, 1.4f * u,
                P(r.Left, r.Top), P(r.Right, r.Top),
                P(r.Right, r.Bottom), P(r.Left, r.Bottom), P(r.Left, r.Top));

            // the glass door, with two reels behind it
            var door = new RectangleF(r.X + r.Width * 0.12f, r.Y + r.Height * 0.10f,
                                      r.Width * 0.76f, r.Height * 0.44f);
            using (var b = new SolidBrush(Color.FromArgb(46, Glass)))
                g.FillRectangle(b, door);
            using (var pen = new Pen(Color.FromArgb(150, Dark), 1f * u))
                g.DrawRectangle(pen, door.X, door.Y, door.Width, door.Height);

            float rr = door.Height * 0.30f;
            foreach (float rx in new[] { door.X + door.Width * 0.30f,
                                         door.X + door.Width * 0.70f })
            {
                float ry = door.Y + door.Height * 0.44f;
                using (var pen = new Pen(Color.FromArgb(210, Glass), 1.3f * u))
                    g.DrawEllipse(pen, rx - rr, ry - rr, rr * 2, rr * 2);
                using (var b = new SolidBrush(Color.FromArgb(140, Dark)))
                    g.FillEllipse(b, rx - rr * 0.62f, ry - rr * 0.62f, rr * 1.24f, rr * 1.24f);
                using (var pen = new Pen(Color.FromArgb(160, Glass), 1f * u))
                {
                    // three spokes, at a different angle on each cabinet, so
                    // the row does not read as one thing repeated.
                    for (int s = 0; s < 3; s++)
                    {
                        double a = (which * 0.5 + s * Math.PI * 2 / 3);
                        g.DrawLine(pen, rx, ry,
                            rx + (float)Math.Cos(a) * rr * 0.9f,
                            ry + (float)Math.Sin(a) * rr * 0.9f);
                    }
                }
            }
            // the tape between them
            Stroke(g, Color.FromArgb(150, Dark), 1f * u,
                P(door.X + door.Width * 0.30f, door.Y + door.Height * 0.44f + rr),
                P(door.X + door.Width * 0.50f, door.Bottom - 2 * u),
                P(door.X + door.Width * 0.70f, door.Y + door.Height * 0.44f + rr));

            // the panel of lights below it
            float ly = r.Y + r.Height * 0.64f;
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 6; i++)
                {
                    float lx = r.X + r.Width * (0.16f + i * 0.135f);
                    bool on = ((which * 7 + row * 11 + i * 5) % 3) == 0;
                    using (var b = new SolidBrush(on ? Color.FromArgb(220, Lit)
                                                     : Color.FromArgb(120, Dark)))
                        g.FillEllipse(b, lx, ly + row * r.Height * 0.10f,
                                      r.Width * 0.06f, r.Width * 0.06f);
                }
        }

        // Light enough for the reel drawn on it to read at forty pixels: at
        // the machine room's own near-black it was a dark shape on a dark
        // disc.
        public override Color Mark { get { return Color.FromArgb(58, 66, 76); } }

        // One tape reel, turning.
        public override void Emblem(Graphics g, RectangleF r, float u)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float rr = Math.Min(r.Width, r.Height) * 0.40f;
            using (var b = new SolidBrush(Color.FromArgb(50, Glass)))
                g.FillEllipse(b, cx - rr, cy - rr, rr * 2, rr * 2);
            using (var pen = new Pen(Color.FromArgb(225, Glass), Math.Max(1f, 1.2f * u)))
                g.DrawEllipse(pen, cx - rr, cy - rr, rr * 2, rr * 2);
            using (var b = new SolidBrush(Color.FromArgb(200, Dark)))
                g.FillEllipse(b, cx - rr * 0.34f, cy - rr * 0.34f, rr * 0.68f, rr * 0.68f);
            using (var pen = new Pen(Color.FromArgb(180, Glass), Math.Max(1f, 1f * u)))
                for (int s = 0; s < 3; s++)
                {
                    double a = 0.5 + s * Math.PI * 2 / 3;
                    g.DrawLine(pen,
                        cx + (float)Math.Cos(a) * rr * 0.36f,
                        cy + (float)Math.Sin(a) * rr * 0.36f,
                        cx + (float)Math.Cos(a) * rr * 0.92f,
                        cy + (float)Math.Sin(a) * rr * 0.92f);
                }
            using (var b = new SolidBrush(Color.FromArgb(230, Lit)))
                g.FillEllipse(b, cx + rr * 0.78f, cy + rr * 0.78f, rr * 0.3f, rr * 0.3f);
        }

        public override string Called(bool hebrew)
        {
            return hebrew ? "חדר המכונות" : "The machine room";
        }

        public override void Sign(Graphics g, Rectangle box, float u, bool rtl)
        {
            /*  The cabinet's own plate. In Latin in both languages: a line
                that mixes "IBM 7094" with Hebrew reorders itself around the
                numbers and comes out as a puzzle, and this is a serial plate
                screwed to a machine, which is not translated anyway. */
            string face = Installed(new[] { "Consolas", "Courier New", "Segoe UI" });
            const string words = "IBM 7094 · PROJECT MAC";
            var plate = Plate(g, new Rectangle(box.X, box.Y + (int)(box.Height * 0.16f),
                                               box.Width, (int)(box.Height * 0.62f)),
                              words, face, u, 9f, 34f);
            using (var b = new SolidBrush(Color.FromArgb(190, Dark)))
                g.FillRectangle(b, plate);
            using (var pen = new Pen(Color.FromArgb(150, Steel), 1f * u))
                g.DrawRectangle(pen, plate.X, plate.Y, plate.Width, plate.Height);
            Lettering(g, plate, words, face,
                Color.FromArgb(235, 196, 206, 214), u, false, 9f);
        }
    }

    // ==================================================================
    //  A front room -- the shadchan
    // ==================================================================
    /*  Somebody's dining room, in the hour it is a place of business: a
        cloth over the table, a glass of tea going cold in its holder, an
        open notebook with a column of names down one side of the page, and
        the telephone where it can be reached without standing up. Two
        chairs facing each other across the table, because that is the
        whole arrangement -- one asks and one answers. */
    class ParlourScene : Scene
    {
        static readonly Color Wood = Color.FromArgb(118, 76, 44);
        static readonly Color Deep = Color.FromArgb(72, 44, 24);
        static readonly Color Cloth = Color.FromArgb(148, 62, 58);
        static readonly Color Page = Color.FromArgb(250, 243, 226);
        static readonly Color Ink = Color.FromArgb(46, 36, 28);
        static readonly Color Tea = Color.FromArgb(176, 96, 36);
        static readonly Color Brass = Color.FromArgb(186, 152, 82);

        public override Palette Colours
        {
            get
            {
                return new Palette
                {
                    Screen = Color.FromArgb(250, 244, 230),
                    Normal = Color.FromArgb(52, 40, 30),
                    Bright = Color.FromArgb(22, 16, 10),
                    Dim = Color.FromArgb(138, 118, 94),
                    User = Color.FromArgb(122, 46, 44)
                };
            }
        }

        public override Color Surround1 { get { return Color.FromArgb(206, 172, 126); } }
        public override Color Surround2 { get { return Color.FromArgb(150, 112, 72); } }
        public override Color Engraved { get { return Color.FromArgb(58, 34, 20); } }
        public override Color Etched { get { return Color.FromArgb(104, 70, 44); } }

        public override string[] Faces(bool hebrew)
        {
            return hebrew
                ? new[] { "Miriam Fixed", "FrankRuehl", "David", "Courier New" }
                : new[] { "Courier New", "Consolas" };
        }

        public override float Band { get { return 0.26f; } }
        public override float Top { get { return 0.075f; } }
        public override int Grain { get { return 13; } }

        // A page of the notebook, held down by a wooden edge.
        public override void Frame(Graphics g, Rectangle well, float u)
        {
            using (var page = new SolidBrush(Color.FromArgb(250, 244, 230)))
                g.FillRectangle(page, well);
            using (var pen = new Pen(Deep, 3.0f * u))
                g.DrawRectangle(pen, well);
            using (var pen = new Pen(Color.FromArgb(150, Cloth), 1.1f * u))
                g.DrawRectangle(pen, well.X + (int)(3.2f * u), well.Y + (int)(3.2f * u),
                                well.Width - (int)(6.4f * u), well.Height - (int)(6.4f * u));
        }

        public override void Floor(Graphics g, Rectangle band, float u, bool rtl)
        {
            if (band.Height < 40 * u) return;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float unit = band.Height / 5f;
            float floor = band.Y + band.Height * 0.88f;
            Stroke(g, Deep, 1.6f * u, P(band.X + 3 * u, floor), P(band.Right - 3 * u, floor));

            float tableW = unit * 5.2f, chairW = unit * 1.5f, gap = unit * 0.5f;
            float total = tableW + chairW * 2 + gap * 2;
            float x = band.X + (band.Width - total) / 2f;
            if (total > band.Width) x = band.X + gap;

            Chair(g, new RectangleF(x, floor - unit * 2.5f, chairW, unit * 2.5f), u, false);
            Table(g, new RectangleF(x + chairW + gap, floor - unit * 2.3f, tableW, unit * 2.3f),
                  u, rtl);
            Chair(g, new RectangleF(x + chairW + gap * 2 + tableW, floor - unit * 2.5f,
                                    chairW, unit * 2.5f), u, true);

            g.SmoothingMode = was;
        }

        // A table with a cloth over it, and what is standing on the cloth.
        static void Table(Graphics g, RectangleF r, float u, bool rtl)
        {
            float top = r.Top + r.Height * 0.46f;

            // the cloth, hanging a little unevenly
            var drape = new[]
            {
                P(r.Left, top), P(r.Right, top),
                P(r.Right - r.Width * 0.03f, r.Top + r.Height * 0.86f),
                P(r.Left + r.Width * 0.02f, r.Top + r.Height * 0.90f)
            };
            Fill(g, Cloth, 150, drape);
            Stroke(g, Deep, 1.4f * u, drape);
            Stroke(g, Deep, 1.5f * u, P(r.Left, top), P(r.Right, top));

            // the legs, only as much of them as the cloth leaves
            Stroke(g, Deep, 1.5f * u,
                P(r.Left + r.Width * 0.08f, r.Top + r.Height * 0.90f),
                P(r.Left + r.Width * 0.08f, r.Bottom - 1));
            Stroke(g, Deep, 1.5f * u,
                P(r.Right - r.Width * 0.08f, r.Top + r.Height * 0.86f),
                P(r.Right - r.Width * 0.08f, r.Bottom - 1));

            // the open notebook, flat on the table
            float bookW = r.Width * 0.34f, bookH = r.Height * 0.20f;
            float bookX = rtl ? r.Right - r.Width * 0.10f - bookW : r.Left + r.Width * 0.10f;
            Notebook(g, new RectangleF(bookX, top - bookH, bookW, bookH), u);

            // the glass of tea in its holder
            float glassW = r.Width * 0.08f;
            float glassX = rtl ? r.Left + r.Width * 0.16f : r.Right - r.Width * 0.16f - glassW;
            Glass(g, new RectangleF(glassX, top - r.Height * 0.30f, glassW, r.Height * 0.30f), u);

            // and the telephone, within reach
            float phoneW = r.Width * 0.15f;
            float phoneX = rtl ? r.Left + r.Width * 0.30f : r.Right - r.Width * 0.30f - phoneW;
            Telephone(g, new RectangleF(phoneX, top - r.Height * 0.22f,
                                        phoneW, r.Height * 0.22f), u);
        }

        // An exercise book, open, with a column of short entries on it.
        static void Notebook(Graphics g, RectangleF r, float u)
        {
            float mid = r.Left + r.Width / 2f;
            var left = new[]
            {
                P(r.Left, r.Top + r.Height * 0.22f), P(mid, r.Top),
                P(mid, r.Bottom - r.Height * 0.06f), P(r.Left, r.Bottom)
            };
            var right = new[]
            {
                P(r.Right, r.Top + r.Height * 0.22f), P(mid, r.Top),
                P(mid, r.Bottom - r.Height * 0.06f), P(r.Right, r.Bottom)
            };
            Fill(g, Page, 240, left);
            Fill(g, Page, 240, right);
            Stroke(g, Ink, 1.0f * u, left);
            Stroke(g, Ink, 1.0f * u, right);
            using (var pen = new Pen(Color.FromArgb(120, Ink), 0.7f * u))
                for (int i = 0; i < 4; i++)
                {
                    float y = r.Top + r.Height * (0.36f + i * 0.17f);
                    g.DrawLine(pen, r.Left + r.Width * 0.08f, y + r.Height * 0.08f,
                               mid - r.Width * 0.04f, y);
                    // the right-hand leaf is where the names are: shorter lines
                    g.DrawLine(pen, mid + r.Width * 0.04f, y,
                               mid + r.Width * (0.18f + (i % 2) * 0.12f), y);
                }
        }

        // A glass in a metal holder, with a spoon standing in it.
        static void Glass(Graphics g, RectangleF r, float u)
        {
            float lip = r.Top + r.Height * 0.34f;
            var body = new[]
            {
                P(r.Left, lip), P(r.Right, lip),
                P(r.Right - r.Width * 0.14f, r.Bottom - r.Height * 0.10f),
                P(r.Left + r.Width * 0.14f, r.Bottom - r.Height * 0.10f)
            };
            Fill(g, Tea, 130, body);
            Stroke(g, Deep, 1.2f * u, body);
            // the holder: a band round the glass and a handle off one side
            Stroke(g, Brass, 1.6f * u,
                P(r.Left + r.Width * 0.04f, lip + r.Height * 0.16f),
                P(r.Right - r.Width * 0.04f, lip + r.Height * 0.16f));
            Stroke(g, Brass, 1.4f * u,
                P(r.Right - r.Width * 0.04f, lip + r.Height * 0.08f),
                P(r.Right + r.Width * 0.26f, lip + r.Height * 0.26f),
                P(r.Right - r.Width * 0.08f, lip + r.Height * 0.42f));
            // the saucer
            Stroke(g, Deep, 1.3f * u,
                P(r.Left - r.Width * 0.20f, r.Bottom - 1),
                P(r.Right + r.Width * 0.20f, r.Bottom - 1));
            // and the spoon
            Stroke(g, Brass, 1.2f * u,
                P(r.Left + r.Width * 0.30f, lip - r.Height * 0.26f),
                P(r.Left + r.Width * 0.62f, lip + r.Height * 0.20f));
        }

        // The telephone, seen end on: a body, a dial and the handset across it.
        static void Telephone(Graphics g, RectangleF r, float u)
        {
            var body = new[]
            {
                P(r.Left, r.Bottom - 1), P(r.Left + r.Width * 0.10f, r.Top + r.Height * 0.40f),
                P(r.Right - r.Width * 0.10f, r.Top + r.Height * 0.40f), P(r.Right, r.Bottom - 1)
            };
            Fill(g, Deep, 165, body);
            Stroke(g, Ink, 1.2f * u, body);

            using (var pen = new Pen(Color.FromArgb(180, Brass), 1.1f * u))
                g.DrawEllipse(pen, r.Left + r.Width * 0.30f, r.Top + r.Height * 0.52f,
                              r.Width * 0.40f, r.Height * 0.34f);

            // the handset, lying across the top
            var rest = new[]
            {
                P(r.Left + r.Width * 0.06f, r.Top + r.Height * 0.26f),
                P(r.Right - r.Width * 0.06f, r.Top + r.Height * 0.18f)
            };
            Stroke(g, Ink, 2.4f * u, rest);
            Stroke(g, Ink, 1.2f * u,
                P(r.Left + r.Width * 0.06f, r.Top + r.Height * 0.12f),
                P(r.Left + r.Width * 0.06f, r.Top + r.Height * 0.30f));
            Stroke(g, Ink, 1.2f * u,
                P(r.Right - r.Width * 0.06f, r.Top + r.Height * 0.04f),
                P(r.Right - r.Width * 0.06f, r.Top + r.Height * 0.22f));
        }

        /*  A chair, face on.

            It was drawn from the side, and from the side a chair is a post
            with rails coming off it -- which is an easel, not a chair. Face
            on it is a back, a seat and two legs, and nothing else is needed
            for anybody to know what it is. */
        static void Chair(Graphics g, RectangleF r, float u, bool facingLeft)
        {
            float seat = r.Top + r.Height * 0.60f;
            float backW = r.Width * 0.80f;
            float bx = r.Left + (r.Width - backW) / 2f;

            // the back: two uprights with a rail over them
            Stroke(g, Deep, 1.5f * u, P(bx, seat), P(bx, r.Top + r.Height * 0.06f));
            Stroke(g, Deep, 1.5f * u,
                P(bx + backW, seat), P(bx + backW, r.Top + r.Height * 0.06f));
            Stroke(g, Deep, 1.6f * u,
                P(bx, r.Top + r.Height * 0.06f),
                P(bx + backW * 0.5f, r.Top),
                P(bx + backW, r.Top + r.Height * 0.06f));
            for (int i = 0; i < 2; i++)
            {
                float y = r.Top + r.Height * (0.22f + i * 0.16f);
                Stroke(g, Wood, 1.2f * u, P(bx, y), P(bx + backW, y));
            }

            // the seat, a shade wider than the back
            Fill(g, Wood, 130,
                P(r.Left, seat), P(r.Right, seat),
                P(r.Right, seat + r.Height * 0.09f), P(r.Left, seat + r.Height * 0.09f));
            Stroke(g, Deep, 1.5f * u,
                P(r.Left, seat), P(r.Right, seat),
                P(r.Right, seat + r.Height * 0.09f), P(r.Left, seat + r.Height * 0.09f),
                P(r.Left, seat));

            // and two legs, splayed the least a chair is allowed to splay
            Stroke(g, Deep, 1.4f * u,
                P(r.Left + r.Width * 0.10f, seat + r.Height * 0.09f),
                P(r.Left + r.Width * 0.06f, r.Bottom - 1));
            Stroke(g, Deep, 1.4f * u,
                P(r.Right - r.Width * 0.10f, seat + r.Height * 0.09f),
                P(r.Right - r.Width * 0.06f, r.Bottom - 1));
        }

        public override Color Mark { get { return Color.FromArgb(170, 118, 70); } }

        // The glass of tea, which is the first thing put in front of you.
        public override void Emblem(Graphics g, RectangleF r, float u)
        {
            float w = r.Width * 0.46f, h = r.Height * 0.62f;
            float cx = r.X + r.Width * 0.46f, top = r.Y + (r.Height - h) / 2f;
            var body = new[]
            {
                P(cx - w / 2f, top), P(cx + w / 2f, top),
                P(cx + w * 0.32f, top + h), P(cx - w * 0.32f, top + h)
            };
            Fill(g, Tea, 190, body);
            Stroke(g, Page, Math.Max(1f, 1.0f * u), body);
            using (var pen = new Pen(Color.FromArgb(230, Brass), Math.Max(1f, 1.2f * u)))
            {
                g.DrawLine(pen, cx - w * 0.46f, top + h * 0.30f, cx + w * 0.46f, top + h * 0.30f);
                g.DrawLine(pen, cx - w * 0.42f, top + h * 0.72f, cx + w * 0.42f, top + h * 0.72f);
            }
            // the handle
            Stroke(g, Brass, Math.Max(1f, 1.1f * u),
                P(cx + w * 0.46f, top + h * 0.24f),
                P(cx + w * 0.92f, top + h * 0.50f),
                P(cx + w * 0.44f, top + h * 0.76f));
            // and the steam
            Stroke(g, Page, Math.Max(1f, 0.9f * u),
                P(cx - w * 0.12f, top - h * 0.30f), P(cx + w * 0.04f, top - h * 0.18f),
                P(cx - w * 0.08f, top - h * 0.06f));
        }

        public override string Called(bool hebrew)
        {
            return hebrew ? "אצל השדכן" : "The shadchan's front room";
        }

        public override void Sign(Graphics g, Rectangle box, float u, bool rtl)
        {
            // A card in the window, hand lettered, propped on the sill.
            string face = Installed(new[] { "FrankRuehl", "David", "Narkisim", "Segoe UI" });
            string words = rtl ? "שידוכים" : "Matches made";
            // The card in the window says the trade; the room is somebody's.
            var plate = Plate(g, new Rectangle(box.X, box.Y, box.Width,
                                               (int)(box.Height * 0.92f)),
                              words, face, u, 11f, 56f);
            using (var b = new SolidBrush(Color.FromArgb(238, Page)))
                g.FillRectangle(b, plate);
            using (var pen = new Pen(Color.FromArgb(210, Deep), 1.4f * u))
                g.DrawRectangle(pen, plate.X, plate.Y, plate.Width, plate.Height);
            using (var pen = new Pen(Color.FromArgb(150, Cloth), 1.0f * u))
                g.DrawRectangle(pen, plate.X + 3f * u, plate.Y + 3f * u,
                                plate.Width - 6f * u, plate.Height - 6f * u);
            Lettering(g, plate, words, face, Color.FromArgb(240, 96, 40, 36), u, rtl, 11f);
        }
    }

    // ==================================================================
    //  A room where a case is heard -- the dayan
    // ==================================================================
    /*  Not a courtroom. A long table with a cloth over it, three chairs
        behind it and one in front, a stack of volumes at the dayan's elbow
        and a lamp that is on because the shutters are closed. Everything in
        the room says the same thing: what is said here is weighed before it
        is answered. */
    class CourtScene : Scene
    {
        static readonly Color Panel = Color.FromArgb(62, 52, 42);
        static readonly Color Deep = Color.FromArgb(38, 32, 26);
        static readonly Color Baize = Color.FromArgb(42, 74, 58);
        static readonly Color Page = Color.FromArgb(240, 234, 220);
        static readonly Color Ink = Color.FromArgb(28, 26, 24);
        static readonly Color Lamp = Color.FromArgb(224, 200, 148);

        public override Palette Colours
        {
            get
            {
                return new Palette
                {
                    Screen = Color.FromArgb(242, 238, 228),
                    Normal = Color.FromArgb(38, 36, 32),
                    Bright = Color.FromArgb(12, 12, 10),
                    Dim = Color.FromArgb(120, 118, 110),
                    User = Color.FromArgb(34, 72, 60)
                };
            }
        }

        public override Color Surround1 { get { return Color.FromArgb(78, 66, 54); } }
        public override Color Surround2 { get { return Color.FromArgb(40, 34, 28); } }
        public override Color Engraved { get { return Color.FromArgb(236, 230, 216); } }
        public override Color Etched { get { return Color.FromArgb(178, 170, 154); } }

        public override string[] Faces(bool hebrew)
        {
            return hebrew
                ? new[] { "Miriam Fixed", "FrankRuehl", "David", "Courier New" }
                : new[] { "Courier New", "Consolas" };
        }

        public override float Band { get { return 0.25f; } }
        public override float Top { get { return 0.08f; } }
        public override int Grain { get { return 7; } }

        // A sheet of paper on the table, squared off by a dark rule.
        public override void Frame(Graphics g, Rectangle well, float u)
        {
            using (var page = new SolidBrush(Color.FromArgb(242, 238, 228)))
                g.FillRectangle(page, well);
            using (var pen = new Pen(Deep, 3.4f * u))
                g.DrawRectangle(pen, well);
            using (var pen = new Pen(Color.FromArgb(170, Baize), 1.2f * u))
                g.DrawRectangle(pen, well.X + (int)(3.6f * u), well.Y + (int)(3.6f * u),
                                well.Width - (int)(7.2f * u), well.Height - (int)(7.2f * u));
        }

        public override void Floor(Graphics g, Rectangle band, float u, bool rtl)
        {
            if (band.Height < 40 * u) return;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float unit = band.Height / 5f;
            float floor = band.Y + band.Height * 0.88f;

            /*  The panelling, behind the table and no further.

                Drawn right across the band it was a picket fence: a
                repeated vertical reads as a fence unless something stops
                it, so it stops where the room does. */
            float wall = Math.Min(band.Width * 0.74f, unit * 9f);
            float wallX = band.X + (band.Width - wall) / 2f;
            using (var pen = new Pen(Color.FromArgb(34, Page), 1.0f * u))
            {
                for (float x = wallX + unit * 0.8f; x < wallX + wall; x += unit * 1.6f)
                    g.DrawLine(pen, x, band.Y + band.Height * 0.14f, x, floor - unit * 0.2f);
                g.DrawLine(pen, wallX, band.Y + band.Height * 0.14f,
                           wallX + wall, band.Y + band.Height * 0.14f);
            }

            using (var pen = new Pen(Color.FromArgb(120, Page), 1.3f * u))
                g.DrawLine(pen, band.X + 3 * u, floor, band.Right - 3 * u, floor);

            float tableW = Math.Min(band.Width * 0.62f, unit * 7.5f);
            float x0 = band.X + (band.Width - tableW) / 2f;
            var table = new RectangleF(x0, floor - unit * 2.4f, tableW, unit * 2.4f);

            /*  Three chairs behind it, only their backs showing.

                A plain rounded slab at this size is a headstone. Two
                uprights with a rail over them and a slat across is a chair
                back, which is what the room needs it to be. */
            float backW = tableW * 0.15f;
            for (int i = 0; i < 3; i++)
            {
                float cx = table.Left + tableW * (0.22f + i * 0.28f);
                var back = new RectangleF(cx - backW / 2f, table.Top - unit * 1.35f,
                                          backW, unit * 1.35f);
                float shoulder = back.Top + back.Height * 0.16f;
                Fill(g, Panel, 120,
                     P(back.Left, back.Bottom), P(back.Left, shoulder),
                     P(back.Left + back.Width / 2f, back.Top),
                     P(back.Right, shoulder), P(back.Right, back.Bottom));
                Stroke(g, Page, 1.1f * u,
                    P(back.Left, back.Bottom), P(back.Left, shoulder),
                    P(back.Left + back.Width / 2f, back.Top),
                    P(back.Right, shoulder), P(back.Right, back.Bottom));
                Stroke(g, Page, 0.9f * u,
                    P(back.Left + back.Width * 0.12f, back.Top + back.Height * 0.44f),
                    P(back.Right - back.Width * 0.12f, back.Top + back.Height * 0.44f));
            }

            Bench(g, table, u, rtl);
            g.SmoothingMode = was;
        }

        // The table: a green cloth, a stack of volumes and a lamp.
        static void Bench(Graphics g, RectangleF r, float u, bool rtl)
        {
            float top = r.Top + r.Height * 0.40f;
            var drape = new[]
            {
                P(r.Left, top), P(r.Right, top),
                P(r.Right, r.Bottom - r.Height * 0.06f), P(r.Left, r.Bottom - r.Height * 0.04f)
            };
            Fill(g, Baize, 175, drape);
            Stroke(g, Page, 1.3f * u, drape);

            // a rule of light along the front edge of the cloth
            using (var pen = new Pen(Color.FromArgb(90, Lamp), 1.0f * u))
                g.DrawLine(pen, r.Left + r.Width * 0.02f, top + 1.6f * u,
                           r.Right - r.Width * 0.02f, top + 1.6f * u);

            float stackW = r.Width * 0.13f;
            float stackX = rtl ? r.Left + r.Width * 0.09f : r.Right - r.Width * 0.09f - stackW;
            Volumes(g, new RectangleF(stackX, top - r.Height * 0.30f,
                                      stackW, r.Height * 0.30f), u);

            float lampW = r.Width * 0.11f;
            float lampX = rtl ? r.Right - r.Width * 0.11f - lampW : r.Left + r.Width * 0.11f;
            Lamplight(g, new RectangleF(lampX, top - r.Height * 0.46f,
                                        lampW, r.Height * 0.46f), u);

            // and one sheet of paper in the middle, squared to the table
            var sheet = new RectangleF(r.Left + r.Width * 0.40f, top - r.Height * 0.13f,
                                       r.Width * 0.20f, r.Height * 0.13f);
            Fill(g, Page, 225, P(sheet.Left, sheet.Bottom), P(sheet.Left + sheet.Width * 0.06f,
                 sheet.Top), P(sheet.Right, sheet.Top), P(sheet.Right - sheet.Width * 0.06f,
                 sheet.Bottom));
            using (var pen = new Pen(Color.FromArgb(110, Ink), 0.7f * u))
                for (int i = 0; i < 3; i++)
                {
                    float y = sheet.Top + sheet.Height * (0.28f + i * 0.24f);
                    g.DrawLine(pen, sheet.Left + sheet.Width * 0.14f, y,
                               sheet.Right - sheet.Width * 0.14f, y);
                }
        }

        // Three volumes, lying one on the other.
        static void Volumes(Graphics g, RectangleF r, float u)
        {
            for (int i = 0; i < 3; i++)
            {
                float h = r.Height / 3f;
                float y = r.Bottom - h * (i + 1);
                float inset = r.Width * 0.04f * i;
                var box = new RectangleF(r.Left + inset, y, r.Width - inset * 2f, h * 0.86f);
                Fill(g, i == 1 ? Deep : Panel, 180,
                     P(box.Left, box.Bottom), P(box.Left, box.Top),
                     P(box.Right, box.Top), P(box.Right, box.Bottom));
                Stroke(g, Page, 0.9f * u,
                    P(box.Left, box.Bottom), P(box.Left, box.Top),
                    P(box.Right, box.Top), P(box.Right, box.Bottom), P(box.Left, box.Bottom));
                using (var b = new SolidBrush(Color.FromArgb(150, Lamp)))
                    g.FillRectangle(b, box.Left + box.Width * 0.12f,
                                    box.Top + box.Height * 0.34f,
                                    box.Width * 0.42f, 1.0f * u);
            }
        }

        // A lamp with a shade, and the light it throws on the cloth.
        static void Lamplight(Graphics g, RectangleF r, float u)
        {
            float cx = r.Left + r.Width / 2f;
            var shade = new[]
            {
                P(cx - r.Width * 0.50f, r.Top + r.Height * 0.34f),
                P(cx - r.Width * 0.26f, r.Top),
                P(cx + r.Width * 0.26f, r.Top),
                P(cx + r.Width * 0.50f, r.Top + r.Height * 0.34f)
            };
            Fill(g, Lamp, 120, shade);
            Stroke(g, Page, 1.1f * u, shade);
            Stroke(g, Page, 1.2f * u, P(cx, r.Top + r.Height * 0.34f), P(cx, r.Bottom - 1));
            Stroke(g, Page, 1.3f * u,
                P(cx - r.Width * 0.34f, r.Bottom - 1), P(cx + r.Width * 0.34f, r.Bottom - 1));

            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[]
                {
                    P(cx - r.Width * 0.50f, r.Top + r.Height * 0.36f),
                    P(cx + r.Width * 0.50f, r.Top + r.Height * 0.36f),
                    P(cx + r.Width * 1.30f, r.Bottom),
                    P(cx - r.Width * 1.30f, r.Bottom)
                });
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = P(cx, r.Top + r.Height * 0.36f);
                    brush.CenterColor = Color.FromArgb(70, Lamp);
                    brush.SurroundColors = new[] { Color.FromArgb(0, Lamp) };
                    g.FillPath(brush, path);
                }
            }
        }

        public override Color Mark { get { return Color.FromArgb(44, 62, 54); } }

        /*  A balance. Not because a beis din owns one, but because it is the
            one object that says "weighed before it is answered" at the size
            of a coin. */
        public override void Emblem(Graphics g, RectangleF r, float u)
        {
            float cx = r.X + r.Width / 2f;
            float top = r.Y + r.Height * 0.16f;
            float beam = r.Width * 0.78f;
            float arm = top + r.Height * 0.22f;
            float thin = Math.Max(1f, 1.1f * u);

            // the post and the foot
            Stroke(g, Page, thin, P(cx, arm), P(cx, r.Bottom - r.Height * 0.14f));
            Stroke(g, Page, thin,
                P(cx - r.Width * 0.22f, r.Bottom - r.Height * 0.14f),
                P(cx + r.Width * 0.22f, r.Bottom - r.Height * 0.14f));

            // the beam, hanging a little off true
            var left = P(cx - beam / 2f, arm + r.Height * 0.05f);
            var right = P(cx + beam / 2f, arm - r.Height * 0.05f);
            Stroke(g, Page, thin, left, right);

            // and the two pans
            Pan(g, left, r.Width * 0.26f, r.Height * 0.22f, thin);
            Pan(g, right, r.Width * 0.26f, r.Height * 0.22f, thin);

            using (var b = new SolidBrush(Color.FromArgb(230, Lamp)))
                g.FillEllipse(b, cx - thin * 1.6f, arm - thin * 1.6f, thin * 3.2f, thin * 3.2f);
        }

        static void Pan(Graphics g, PointF from, float wide, float drop, float thin)
        {
            float y = from.Y + drop;
            Stroke(g, Page, thin, from, P(from.X - wide / 2f, y));
            Stroke(g, Page, thin, from, P(from.X + wide / 2f, y));
            Stroke(g, Page, thin,
                P(from.X - wide / 2f, y), P(from.X, y + drop * 0.34f),
                P(from.X + wide / 2f, y));
        }

        public override string Called(bool hebrew)
        {
            return hebrew ? "בית דין" : "The beis din";
        }

        public override void Sign(Graphics g, Rectangle box, float u, bool rtl)
        {
            // A board on the door, painted, with the hours under the name.
            string face = Installed(new[] { "FrankRuehl", "David", "Narkisim", "Segoe UI" });
            string words = Called(rtl);
            var plate = Plate(g, new Rectangle(box.X, box.Y, box.Width,
                                               (int)(box.Height * 0.92f)),
                              words, face, u, 11f, 56f);
            // Panel, not Deep: against this surround Deep reads as a hole.
            using (var b = new SolidBrush(Color.FromArgb(235, Panel)))
                g.FillRectangle(b, plate);
            using (var pen = new Pen(Color.FromArgb(190, Page), 1.2f * u))
                g.DrawRectangle(pen, plate.X, plate.Y, plate.Width, plate.Height);
            using (var pen = new Pen(Color.FromArgb(120, Baize), 1.0f * u))
                g.DrawRectangle(pen, plate.X + 3f * u, plate.Y + 3f * u,
                                plate.Width - 6f * u, plate.Height - 6f * u);
            Lettering(g, plate, words, face, Color.FromArgb(240, 236, 232, 218), u, rtl, 11f);
        }
    }

    // ==================================================================
    //  The corner by the notice board -- the tzul
    // ==================================================================
    /*  Not the hall itself: the corner of it by the door, where the notice
        board is and where a person on the way out gets stopped. A board
        three deep in small notices, a lectern turned across the gangway so
        there is no way past it, an urn of hot water, and a strip light that
        is on because the evening seder is on.

        Everything here is pale, flat and institutional, which is exactly
        the point: the study hall is warm and the mussar talk is dark, and
        this is the third light in the same building -- the one you get
        cornered under. */
    class CornerScene : Scene
    {
        static readonly Color Wall = Color.FromArgb(196, 206, 190);
        static readonly Color Deep = Color.FromArgb(78, 88, 76);
        static readonly Color Board = Color.FromArgb(122, 96, 62);
        static readonly Color Paper = Color.FromArgb(250, 249, 242);
        static readonly Color Ink = Color.FromArgb(44, 48, 44);
        static readonly Color Tube = Color.FromArgb(226, 238, 214);

        public override Palette Colours
        {
            get
            {
                return new Palette
                {
                    Screen = Color.FromArgb(248, 250, 243),
                    Normal = Color.FromArgb(42, 48, 42),
                    Bright = Color.FromArgb(14, 18, 14),
                    Dim = Color.FromArgb(126, 136, 122),
                    User = Color.FromArgb(38, 84, 64)
                };
            }
        }

        public override Color Surround1 { get { return Color.FromArgb(186, 198, 180); } }
        public override Color Surround2 { get { return Color.FromArgb(140, 154, 136); } }
        public override Color Engraved { get { return Color.FromArgb(40, 48, 40); } }
        public override Color Etched { get { return Color.FromArgb(88, 100, 86); } }

        public override string[] Faces(bool hebrew)
        {
            return hebrew
                ? new[] { "Miriam Fixed", "FrankRuehl", "David", "Courier New" }
                : new[] { "Courier New", "Consolas" };
        }

        public override float Band { get { return 0.26f; } }
        public override float Top { get { return 0.075f; } }
        public override int Grain { get { return 11; } }

        public override string Called(bool hebrew)
        {
            return hebrew ? "סדר ערב" : "The evening seder";
        }

        // A notice, pinned flat: paper with a hard grey rule round it.
        public override void Frame(Graphics g, Rectangle well, float u)
        {
            using (var page = new SolidBrush(Color.FromArgb(248, 250, 243)))
                g.FillRectangle(page, well);
            using (var pen = new Pen(Deep, 2.6f * u))
                g.DrawRectangle(pen, well);
            using (var pen = new Pen(Color.FromArgb(120, Board), 1.1f * u))
                g.DrawRectangle(pen, well.X + (int)(3.2f * u), well.Y + (int)(3.2f * u),
                                well.Width - (int)(6.4f * u), well.Height - (int)(6.4f * u));
        }

        public override void Floor(Graphics g, Rectangle band, float u, bool rtl)
        {
            if (band.Height < 40 * u) return;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float unit = band.Height / 5f;
            float floor = band.Y + band.Height * 0.88f;

            // the strip light, and what it throws down the wall
            Strip(g, band, u);

            using (var pen = new Pen(Color.FromArgb(150, Deep), 1.4f * u))
                g.DrawLine(pen, band.X + 3 * u, floor, band.Right - 3 * u, floor);

            float boardW = unit * 4.4f, standW = unit * 2.3f, urnW = unit * 1.3f;
            float gap = unit * 0.8f;
            float total = boardW + standW + urnW + gap * 2;
            float x = band.X + (band.Width - total) / 2f;
            if (total > band.Width) x = band.X + gap;

            float boardX = rtl ? x + urnW + gap * 2 + standW : x;
            float standX = x + (rtl ? urnW + gap : boardW + gap);
            float urnX = rtl ? x : x + boardW + gap * 2 + standW;

            Notices(g, new RectangleF(boardX, floor - unit * 3.5f, boardW, unit * 2.4f), u);
            Barricade(g, new RectangleF(standX, floor - unit * 2.4f, standW, unit * 2.4f), u, rtl);
            Urn(g, new RectangleF(urnX, floor - unit * 1.8f, urnW, unit * 1.8f), u);

            g.SmoothingMode = was;
        }

        // A fluorescent tube, and the pale wash under it.
        static void Strip(Graphics g, Rectangle band, float u)
        {
            float y = band.Y + band.Height * 0.10f;
            float wide = band.Width * 0.40f;
            float x = band.X + (band.Width - wide) / 2f;
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(new[]
                {
                    P(x, y), P(x + wide, y),
                    P(x + wide * 1.35f, band.Bottom), P(x - wide * 0.35f, band.Bottom)
                });
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = P(x + wide / 2f, y);
                    brush.CenterColor = Color.FromArgb(70, Tube);
                    brush.SurroundColors = new[] { Color.FromArgb(0, Tube) };
                    g.FillPath(brush, path);
                }
            }
            using (var b = new SolidBrush(Color.FromArgb(220, Tube)))
                g.FillRectangle(b, x, y - 1.2f * u, wide, 2.4f * u);
            using (var pen = new Pen(Color.FromArgb(150, Deep), 1f * u))
                g.DrawRectangle(pen, x, y - 1.2f * u, wide, 2.4f * u);
        }

        /*  The board: small notices at small angles, three deep, because one
            notice is a sign and thirty are a notice board. */
        static void Notices(Graphics g, RectangleF r, float u)
        {
            Box(g, Deep, Board, 90, 1.6f * u, r);
            int i = 0;
            for (float y = r.Top + r.Height * 0.08f; y < r.Bottom - r.Height * 0.22f;
                 y += r.Height * 0.30f)
            {
                for (float x = r.Left + r.Width * 0.05f; x < r.Right - r.Width * 0.18f;
                     x += r.Width * 0.22f)
                {
                    float w = r.Width * (0.15f + (i % 3) * 0.022f);
                    float h = r.Height * (0.20f + (i % 2) * 0.04f);
                    float tilt = ((i % 5) - 2) * 0.9f * u;
                    var sheet = new[]
                    {
                        P(x, y + tilt), P(x + w, y - tilt),
                        P(x + w, y + h - tilt), P(x, y + h + tilt)
                    };
                    Fill(g, Paper, 235, sheet);
                    Stroke(g, Ink, 0.7f * u, sheet);
                    using (var pen = new Pen(Color.FromArgb(90, Ink), 0.6f * u))
                        for (int k = 0; k < 2; k++)
                        {
                            float ly = y + h * (0.38f + k * 0.28f);
                            g.DrawLine(pen, x + w * 0.14f, ly, x + w * 0.86f, ly);
                        }
                    using (var b = new SolidBrush(Color.FromArgb(200, Deep)))
                        g.FillEllipse(b, x + w / 2f - 0.9f * u, y - 0.2f * u,
                                      1.8f * u, 1.8f * u);
                    i++;
                }
            }
        }

        // A lectern turned across the gangway, which is how you get stopped.
        static void Barricade(Graphics g, RectangleF r, float u, bool rtl)
        {
            float midX = r.Left + r.Width / 2f;
            Fill(g, Deep, 110,
                P(midX - r.Width * 0.09f, r.Bottom - 1),
                P(midX - r.Width * 0.07f, r.Top + r.Height * 0.46f),
                P(midX + r.Width * 0.07f, r.Top + r.Height * 0.46f),
                P(midX + r.Width * 0.09f, r.Bottom - 1));
            Stroke(g, Deep, 1.4f * u,
                P(midX - r.Width * 0.32f, r.Bottom - 1),
                P(midX + r.Width * 0.32f, r.Bottom - 1));

            // the top, across rather than along
            float lean = rtl ? -1f : 1f;
            var top = new[]
            {
                P(midX - r.Width * 0.44f, r.Top + r.Height * (0.34f + 0.06f * lean)),
                P(midX + r.Width * 0.44f, r.Top + r.Height * (0.34f - 0.06f * lean)),
                P(midX + r.Width * 0.44f, r.Top + r.Height * (0.46f - 0.06f * lean)),
                P(midX - r.Width * 0.44f, r.Top + r.Height * (0.46f + 0.06f * lean))
            };
            Fill(g, Board, 130, top);
            Stroke(g, Deep, 1.5f * u, top);

            // and a pile of pamphlets on it, not a volume
            for (int k = 0; k < 3; k++)
            {
                float y = r.Top + r.Height * (0.30f - k * 0.05f);
                var sheet = new RectangleF(midX - r.Width * 0.24f + k * 1.2f * u, y,
                                           r.Width * 0.48f, r.Height * 0.05f);
                Fill(g, Paper, 230,
                     P(sheet.Left, sheet.Bottom), P(sheet.Left, sheet.Top),
                     P(sheet.Right, sheet.Top), P(sheet.Right, sheet.Bottom));
                Stroke(g, Ink, 0.7f * u,
                    P(sheet.Left, sheet.Bottom), P(sheet.Left, sheet.Top),
                    P(sheet.Right, sheet.Top), P(sheet.Right, sheet.Bottom),
                    P(sheet.Left, sheet.Bottom));
            }
        }

        // The urn, which is why anybody stands in this corner at all.
        static void Urn(Graphics g, RectangleF r, float u)
        {
            var body = new[]
            {
                P(r.Left + r.Width * 0.10f, r.Top + r.Height * 0.18f),
                P(r.Right - r.Width * 0.10f, r.Top + r.Height * 0.18f),
                P(r.Right - r.Width * 0.04f, r.Bottom - r.Height * 0.08f),
                P(r.Left + r.Width * 0.04f, r.Bottom - r.Height * 0.08f)
            };
            Fill(g, Wall, 160, body);
            Stroke(g, Deep, 1.3f * u, body);
            Stroke(g, Deep, 1.3f * u,
                P(r.Left, r.Top + r.Height * 0.18f), P(r.Right, r.Top + r.Height * 0.18f));
            Stroke(g, Deep, 1.2f * u,
                P(r.Left + r.Width * 0.34f, r.Top + r.Height * 0.06f),
                P(r.Right - r.Width * 0.34f, r.Top + r.Height * 0.06f));
            // the tap
            Stroke(g, Deep, 1.4f * u,
                P(r.Right - r.Width * 0.08f, r.Top + r.Height * 0.56f),
                P(r.Right + r.Width * 0.16f, r.Top + r.Height * 0.56f),
                P(r.Right + r.Width * 0.16f, r.Top + r.Height * 0.70f));
            Stroke(g, Deep, 1.2f * u,
                P(r.Left - r.Width * 0.10f, r.Bottom - 1),
                P(r.Right + r.Width * 0.10f, r.Bottom - 1));
        }

        public override Color Mark { get { return Color.FromArgb(150, 164, 144); } }

        /*  A glass held over a line of writing. The character's whole move is
            to stop on a word you thought nobody would look at twice. */
        public override void Emblem(Graphics g, RectangleF r, float u)
        {
            float thin = Math.Max(1f, 1.1f * u);
            var sheet = new RectangleF(r.X + r.Width * 0.10f, r.Y + r.Height * 0.18f,
                                       r.Width * 0.66f, r.Height * 0.64f);
            Fill(g, Paper, 235,
                 P(sheet.Left, sheet.Bottom), P(sheet.Left, sheet.Top),
                 P(sheet.Right, sheet.Top), P(sheet.Right, sheet.Bottom));
            Stroke(g, Ink, thin,
                P(sheet.Left, sheet.Bottom), P(sheet.Left, sheet.Top),
                P(sheet.Right, sheet.Top), P(sheet.Right, sheet.Bottom),
                P(sheet.Left, sheet.Bottom));
            using (var pen = new Pen(Color.FromArgb(130, Ink), Math.Max(1f, 0.8f * u)))
                for (int k = 0; k < 3; k++)
                {
                    float y = sheet.Top + sheet.Height * (0.24f + k * 0.24f);
                    g.DrawLine(pen, sheet.Left + sheet.Width * 0.12f, y,
                               sheet.Right - sheet.Width * (k == 1 ? 0.34f : 0.12f), y);
                }

            float rr = Math.Min(r.Width, r.Height) * 0.26f;
            float cx = r.X + r.Width * 0.62f, cy = r.Y + r.Height * 0.52f;
            using (var b = new SolidBrush(Color.FromArgb(70, Tube)))
                g.FillEllipse(b, cx - rr, cy - rr, rr * 2, rr * 2);
            using (var pen = new Pen(Color.FromArgb(240, Ink), Math.Max(1.4f, 1.6f * u)))
            {
                g.DrawEllipse(pen, cx - rr, cy - rr, rr * 2, rr * 2);
                g.DrawLine(pen, cx + rr * 0.72f, cy + rr * 0.72f,
                           cx + rr * 1.55f, cy + rr * 1.55f);
            }
        }

        public override void Sign(Graphics g, Rectangle box, float u, bool rtl)
        {
            // One more notice, pinned where the others could not fit.
            string face = Installed(new[] { "FrankRuehl", "David", "Narkisim", "Segoe UI" });
            string words = Called(rtl);
            var plate = Plate(g, new Rectangle(box.X, box.Y, box.Width,
                                               (int)(box.Height * 0.92f)),
                              words, face, u, 11f, 56f);
            using (var b = new SolidBrush(Color.FromArgb(240, Paper)))
                g.FillRectangle(b, plate);
            using (var pen = new Pen(Color.FromArgb(210, Deep), 1.3f * u))
                g.DrawRectangle(pen, plate.X, plate.Y, plate.Width, plate.Height);
            using (var b = new SolidBrush(Color.FromArgb(210, Deep)))
                g.FillEllipse(b, plate.X + plate.Width / 2f - 2f * u, plate.Y + 2f * u,
                              4f * u, 4f * u);
            Lettering(g, plate, words, face, Color.FromArgb(240, 40, 48, 40), u, rtl, 11f);
        }
    }
}
