// Museum.cs -- the psychotherapist's room, drawn again.
//
// The Hebrew ELIZA that Eran Hadas built for the Bloomfield Science Museum in
// 2010 was not a terminal. It was a white chat box with a four-pixel dotted
// blue border, sitting in a drawing of a psychotherapist's room: coloured
// pencil on white paper, filling the browser window. The drawing is most of
// why anybody who saw it remembers it.
//
// What is here is drawn from a description of that page, in the same medium
// and with the same furniture, and every line of it is this program's. The
// original artwork is not used, not traced and not copied; this was drawn
// from the list of what was in the room:
//
//   a wavy salmon plaque ringed with hibiscus, hand-lettered, at the top
//   a floor lamp with a blue and white striped shade
//   a tall potted plant, and a red and white striped flex to a wall socket
//   a grey-blue analyst's couch
//   three dreamcatchers hanging
//   the therapist herself: blue blouse printed with big orange flowers, an
//   orange bib collar, a brown skirt, blue hoop earrings and glasses, in an
//   armchair drawn in outline only, an orange clipboard on the armrest
//   a teal side table with a flowered tissue box and a clock
//
// Coloured pencil is not a line. It is several passes of a soft, uneven,
// half-transparent stroke that does not quite meet itself at the corners, so
// that is how everything here is drawn: Stroke() goes over a path more than
// once with a small offset, and nothing is closed exactly.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ElizaApp
{
    static class Museum
    {
        static readonly Color Pencil = Color.FromArgb(96, 88, 76);
        static readonly Color Salmon = Color.FromArgb(236, 132, 118);
        static readonly Color Petal = Color.FromArgb(232, 118, 140);
        static readonly Color Middle = Color.FromArgb(246, 196, 86);
        static readonly Color Leaf = Color.FromArgb(104, 152, 92);
        static readonly Color Deep = Color.FromArgb(74, 118, 70);
        static readonly Color Sky = Color.FromArgb(108, 150, 196);
        static readonly Color Couchy = Color.FromArgb(138, 158, 178);
        static readonly Color Teal = Color.FromArgb(96, 166, 162);
        static readonly Color Orange = Color.FromArgb(238, 146, 68);
        static readonly Color Brown = Color.FromArgb(150, 112, 82);
        static readonly Color Skin = Color.FromArgb(242, 208, 182);
        static readonly Color Hair = Color.FromArgb(92, 66, 52);
        static readonly Color Rust = Color.FromArgb(206, 78, 64);

        /*  One pass of a coloured pencil: soft, a little uneven, and never
            quite on top of where it went last time. */
        static void Stroke(Graphics g, Color c, float width, params PointF[] path)
        {
            if (path.Length < 2) return;
            for (int pass = 0; pass < 2; pass++)
            {
                float shift = pass == 0 ? 0f : 0.7f;
                using (var pen = new Pen(Color.FromArgb(pass == 0 ? 200 : 90, c), width))
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

        static void Ring(Graphics g, Color c, float width, RectangleF r)
        {
            for (int pass = 0; pass < 2; pass++)
                using (var pen = new Pen(Color.FromArgb(pass == 0 ? 200 : 90, c), width))
                    g.DrawEllipse(pen, r.X + pass * 0.7f, r.Y + pass * 0.7f, r.Width, r.Height);
        }

        // A wash of colour, the way a pencil fills: patchy, never solid.
        static void Wash(Graphics g, Color c, RectangleF r, int alpha)
        {
            using (var b = new SolidBrush(Color.FromArgb(alpha, c)))
                g.FillEllipse(b, r);
        }

        static void Fill(Graphics g, Color c, int alpha, params PointF[] path)
        {
            if (path.Length < 3) return;
            using (var b = new SolidBrush(Color.FromArgb(alpha, c)))
                g.FillPolygon(b, path);
        }

        static PointF P(float x, float y) { return new PointF(x, y); }

        // The first of these the machine actually has.
        static string Handwriting(string[] names)
        {
            using (var installed = new System.Drawing.Text.InstalledFontCollection())
            {
                var have = new HashSet<string>(
                    installed.Families.Select(f => f.Name),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var n in names) if (have.Contains(n)) return n;
            }
            return names[names.Length - 1];
        }

        // ---- the room -------------------------------------------------------

        /*  band  the strip along the foot of the window
            side  the column beside the chat box, on the side the reading runs
                  away from */
        /*  One unit for the whole room, taken from the band's height.

            It was measured two ways at once: the furniture's widths came from
            thirds of the band's width, which follows the window, and its
            heights from the band's height, which does not. So the proportions
            changed with the window and the figure came apart -- measured, at
            1366 pixels of client width the therapist had a six-pixel torso
            under a head thirteen times its height, and above about 1478 the
            shoulders dropped below the waist and the blouse became a bowtie.

            And nothing stood on the floor. The lamp and the plant were placed
            in the column beside the chat box, which ends where the box ends,
            so they floated a hundred and eighty pixels above the line they
            were drawn to stand on.

            Now: one unit, every piece sized from it, every piece standing on
            the floor line, and the group centred in whatever width there is. */
        public static void Room(Graphics g, Rectangle band, Rectangle side, float u)
        {
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            /*  Two measures, and they are not the same thing: u is how thick
                a pencil line is and follows the screen, unit is how big the
                furniture is and follows the band. */
            float unit = band.Height / 6f;
            float floor = band.Y + band.Height * 0.80f;

            Stroke(g, Pencil, 1.6f * u,
                P(band.X + 4 * u, floor),
                P(band.Right - 4 * u, floor + 0.8f * u));

            /*  What is drawn, in the order it stands along the floor. Each
                entry is a width in units; the whole row is centred, and
                anything that will not fit is left out from the middle rather
                than clipped at the edge. */
            float lampW = 2.0f * unit, plantW = 1.6f * unit;
            float couchW = 5.6f * unit, sheW = 2.6f * unit, tableW = 1.5f * unit;
            float gap = 0.5f * unit;

            float wanted = lampW + plantW + couchW + sheW + tableW + gap * 4;
            bool roomForAll = wanted <= band.Width;
            if (!roomForAll)
            {
                wanted = lampW + couchW + sheW + gap * 2;    // drop the small things
                if (wanted > band.Width)
                {
                    wanted = couchW + sheW + gap;            // and then the lamp
                    if (wanted > band.Width) { g.SmoothingMode = was; return; }
                }
            }

            // In a mirrored reading she sits at the end the eye starts from.
            float x = band.X + (band.Width - wanted) / 2f;

            if (roomForAll || wanted <= band.Width - lampW)
            {
                Lamp(g, new RectangleF(x, floor - 4.3f * unit, lampW, 4.3f * unit),u);
                x += lampW + gap;
            }
            if (roomForAll)
            {
                Plant(g, new RectangleF(x, floor - 2.6f * unit, plantW, 2.6f * unit),u);
                x += plantW + gap;
            }

            var couch = new RectangleF(x, floor - 2.3f * unit, couchW, 2.3f * unit);
            Couch(g, couch, u);
            Catchers(g, new RectangleF(couch.X + couchW * 0.15f, band.Y + 0.1f * unit,
                                       couchW * 0.7f, 1.5f * unit), u);
            x += couchW + gap;

            Therapist(g, new RectangleF(x, floor - 4.6f * unit, sheW, 4.6f * unit),u);
            x += sheW + gap;

            if (roomForAll)
                SideTable(g, new RectangleF(x, floor - 1.9f * unit, tableW, 1.9f * unit), u);

            g.SmoothingMode = was;
        }

        // ---- the plaque, which is also the title ---------------------------

        public static void Plaque(Graphics g, Rectangle box, string words, float u, bool rtl)
        {
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            /*  A wavy salmon shape, not a rectangle: it was hand-drawn and it
                wobbles, so it is built from a curve rather than a border. */
            var shape = new GraphicsPath();
            float w = box.Width, h = box.Height;
            float x = box.X, y = box.Y;
            var edge = new List<PointF>();
            int steps = 28;
            for (int i = 0; i <= steps; i++)          // along the top
            {
                float t = i / (float)steps;
                edge.Add(P(x + t * w, y + (float)Math.Sin(t * Math.PI * 3) * h * 0.10f + h * 0.16f));
            }
            for (int i = steps; i >= 0; i--)          // and back along the foot
            {
                float t = i / (float)steps;
                edge.Add(P(x + t * w, y + h - (float)Math.Sin(t * Math.PI * 2.4) * h * 0.10f - h * 0.12f));
            }
            shape.AddClosedCurve(edge.ToArray(), 0.4f);

            using (var b = new SolidBrush(Color.FromArgb(165, Salmon)))
                g.FillPath(b, shape);
            using (var pen = new Pen(Color.FromArgb(180, Rust), 1.3f * u))
                g.DrawPath(pen, shape);
            shape.Dispose();

            // Hibiscus around it, the way a border of flowers is drawn: not
            // evenly spaced, and each one a slightly different size.
            /*  Around it, and not across it. They alternated top and
                bottom all the way along, so three of the seven sat squarely
                in the band the lettering runs through and the words were
                read out of a flowerbed. Along the top, and one at each
                foot. */
            float r = h * 0.27f;
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                Hibiscus(g, x + r * 0.55f + t * (w - r * 1.1f), y + r * 0.02f,
                         r * (0.74f + (i % 3) * 0.13f), u);
            }
            Hibiscus(g, x + r * 0.30f, y + h - r * 0.30f, r * 0.80f, u);
            Hibiscus(g, x + w - r * 0.30f, y + h - r * 0.30f, r * 0.72f, u);

            /*  Hand lettering that fits on one line, whatever the words are:
                a plaque is a painted sign, and a painted sign does not wrap. */
            using (var fmt = new StringFormat
                   {
                       Alignment = StringAlignment.Center,
                       LineAlignment = StringAlignment.Center,
                       FormatFlags = (rtl ? StringFormatFlags.DirectionRightToLeft : 0)
                                     | StringFormatFlags.NoWrap
                   })
            using (var b = new SolidBrush(Color.FromArgb(225, 92, 46, 40)))
            {
                /*  Segoe Script contains no Hebrew at all -- not one of the
                    twenty-seven letters -- so GDI+ fell back silently and the
                    Hebrew plaque, the one piece of hand lettering in the
                    program, was set in a plain grotesque. The chain for Hebrew
                    must never end on it. */
                /*  And legible. Guttman Yad is a running hand: at plaque
                    size its letters run into one another and the line came
                    out as a scribble. Yad-Brush is the same hand painted
                    with a brush, which is what a painted sign is, and it
                    holds together. Levenim MT is the fallback that is
                    actually on a Windows machine without Office. */
                string face = rtl
                    ? Handwriting(new[] { "Guttman Yad-Brush", "Guttman Yad",
                                          "Levenim MT", "Narkisim", "David" })
                    : "Segoe Script";

                float points = 11f;
                Font f = null;
                try
                {
                    while (points > 5f)
                    {
                        f = new Font(face, points, FontStyle.Bold);
                        var size = g.MeasureString(words, f, int.MaxValue, fmt);
                        if (size.Width <= box.Width - r * 1.7f) break;
                        f.Dispose();
                        f = null;
                        points -= 0.5f;
                    }
                    if (f == null) f = new Font(face, 5f, FontStyle.Bold);
                    // Inside the flowers, not under them.
                    g.DrawString(words, f, b,
                        RectangleF.Inflate(box, -r * 0.85f, 0), fmt);
                }
                finally { if (f != null) f.Dispose(); }
            }

            g.SmoothingMode = was;
        }

        /*  One hibiscus. Public because the card on the models page borrows
            it as this room's mark -- the flower off the plaque is the one
            object from here that survives being drawn at forty pixels. */
        public static void Hibiscus(Graphics g, float cx, float cy, float r, float u)
        {
            for (int i = 0; i < 5; i++)
            {
                double a = i * Math.PI * 2 / 5 - Math.PI / 2;
                float px = cx + (float)Math.Cos(a) * r * 0.52f;
                float py = cy + (float)Math.Sin(a) * r * 0.52f;
                Wash(g, Petal, new RectangleF(px - r * 0.40f, py - r * 0.34f, r * 0.80f, r * 0.68f), 150);
                Ring(g, Rust, 0.8f * u,
                     new RectangleF(px - r * 0.40f, py - r * 0.34f, r * 0.80f, r * 0.68f));
            }
            Wash(g, Middle, new RectangleF(cx - r * 0.20f, cy - r * 0.20f, r * 0.40f, r * 0.40f), 220);
        }

        // ---- the furniture --------------------------------------------------

        static void Lamp(Graphics g, RectangleF r, float u)
        {
            float cx = r.X + r.Width / 2;
            float shadeH = r.Height * 0.26f, shadeW = r.Width * 0.96f;
            float top = r.Y;

            // A shade is a trapezium, wider at the foot.
            var shade = new[]
            {
                P(cx - shadeW * 0.32f, top),
                P(cx + shadeW * 0.32f, top),
                P(cx + shadeW * 0.50f, top + shadeH),
                P(cx - shadeW * 0.50f, top + shadeH),
                P(cx - shadeW * 0.32f, top)
            };
            Fill(g, Color.White, 210, shade);
            Stroke(g, Sky, 1.4f * u, shade);

            // Blue and white stripes, following the slope of the shade.
            for (int i = 1; i < 7; i++)
            {
                float t = i / 7f;
                float topX = cx - shadeW * 0.32f + t * shadeW * 0.64f;
                float botX = cx - shadeW * 0.50f + t * shadeW * 1.00f;
                Stroke(g, Sky, 1.6f * u, P(topX, top + 1.5f * u), P(botX, top + shadeH - 1.5f * u));
            }

            // Stem and foot.
            Stroke(g, Pencil, 1.5f * u, P(cx, top + shadeH), P(cx, r.Bottom - r.Height * 0.04f));
            Stroke(g, Pencil, 1.7f * u,
                   P(cx - r.Width * 0.30f, r.Bottom),
                   P(cx + r.Width * 0.30f, r.Bottom - 0.6f * u));

            /*  The flex, striped red and white, to a socket.

                It ran a length and a half of the lamp to the right and then
                stopped in mid-air. Since the room was rebuilt the lamp is the
                first thing along the floor, so that run crossed the plant and
                the couch on its way to nowhere. It keeps to the lamp's own
                width now, and it ends at a socket. */
            float fy = r.Bottom + 3 * u;
            float run = r.Width * 0.85f;
            var flex = new List<PointF>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                flex.Add(P(cx - t * run,
                           fy + (float)Math.Sin(t * Math.PI * 2) * 2.2f * u));
            }
            for (int i = 0; i + 1 < flex.Count; i += 2)
                Stroke(g, Rust, 1.5f * u, flex[i], flex[i + 1]);

            var socket = new RectangleF(cx - run - 2.4f * u, fy - 2.4f * u,
                                        4.8f * u, 4.8f * u);
            Stroke(g, Pencil, 1.1f * u,
                P(socket.Left, socket.Top), P(socket.Right, socket.Top),
                P(socket.Right, socket.Bottom), P(socket.Left, socket.Bottom),
                P(socket.Left, socket.Top));
        }

        static void Plant(Graphics g, RectangleF r, float u)
        {
            float cx = r.X + r.Width / 2;
            float potH = r.Height * 0.34f;
            float potTop = r.Bottom - potH;

            var pot = new[]
            {
                P(cx - r.Width * 0.34f, potTop),
                P(cx + r.Width * 0.34f, potTop),
                P(cx + r.Width * 0.24f, r.Bottom),
                P(cx - r.Width * 0.24f, r.Bottom),
                P(cx - r.Width * 0.34f, potTop)
            };
            Fill(g, Orange, 120, pot);
            Stroke(g, Brown, 1.4f * u, pot);
            Stroke(g, Brown, 1.2f * u,
                   P(cx - r.Width * 0.34f, potTop + potH * 0.22f),
                   P(cx + r.Width * 0.34f, potTop + potH * 0.22f));

            // Leaves: long blades springing from the middle of the pot.
            for (int i = 0; i < 7; i++)
            {
                float lean = (i - 3) / 3f;
                float tipX = cx + lean * r.Width * 0.62f;
                float tipY = potTop - r.Height * (0.36f + (3 - Math.Abs(i - 3)) * 0.14f);
                Stroke(g, i % 2 == 0 ? Leaf : Deep, 1.9f * u,
                       P(cx, potTop),
                       P(cx + lean * r.Width * 0.22f, (potTop + tipY) / 2),
                       P(tipX, tipY));
            }
        }

        static void Couch(Graphics g, RectangleF r, float u)
        {
            float seat = r.Y + r.Height * 0.52f;
            float backTop = r.Y + r.Height * 0.06f;

            /*  A back, a seat, and a roll at the head end. It was a wedge
                before, because it was drawn as one outline: a couch is two
                shapes that meet, and drawing it as one makes a ramp. */
            var back = new[]
            {
                P(r.X + r.Width * 0.10f, seat),
                P(r.X + r.Width * 0.10f, backTop),
                P(r.Right - r.Width * 0.04f, backTop + r.Height * 0.06f),
                P(r.Right - r.Width * 0.04f, seat),
                P(r.X + r.Width * 0.10f, seat)
            };
            Fill(g, Couchy, 120, back);
            Stroke(g, Couchy, 1.6f * u, back);

            // Meeting the back, not four per cent below it: a hairline of
            // wall used to run the whole length of the couch.
            var body = new[]
            {
                P(r.X, seat),
                P(r.Right, seat),
                P(r.Right, r.Bottom - r.Height * 0.10f),
                P(r.X, r.Bottom - r.Height * 0.10f),
                P(r.X, seat)
            };
            Fill(g, Couchy, 150, body);
            Stroke(g, Couchy, 1.6f * u, body);

            // The roll the head goes on, at the end the reading starts from.
            var roll = new RectangleF(r.X - r.Width * 0.02f, seat - r.Height * 0.16f,
                                      r.Width * 0.20f, r.Height * 0.22f);
            Wash(g, Couchy, roll, 170);
            Ring(g, Couchy, 1.4f * u, roll);

            // A cushion, and the buttoning across the back.
            var cushion = new RectangleF(r.X + r.Width * 0.22f, seat - r.Height * 0.20f,
                                         r.Width * 0.20f, r.Height * 0.22f);
            Wash(g, Orange, cushion, 120);
            Ring(g, Orange, 1.2f * u, cushion);
            for (int i = 1; i < 4; i++)
            {
                float bx = r.X + r.Width * (0.30f + i * 0.17f);
                Stroke(g, Color.FromArgb(140, Pencil), 0.8f * u,
                       P(bx, backTop + r.Height * 0.14f), P(bx, seat - r.Height * 0.04f));
            }

            // Legs, short and splayed.
            Stroke(g, Brown, 1.6f * u,
                   P(r.X + r.Width * 0.08f, r.Bottom - r.Height * 0.10f),
                   P(r.X + r.Width * 0.04f, r.Bottom));
            Stroke(g, Brown, 1.6f * u,
                   P(r.Right - r.Width * 0.08f, r.Bottom - r.Height * 0.10f),
                   P(r.Right - r.Width * 0.04f, r.Bottom));
        }

        static void Catchers(Graphics g, RectangleF r, float u)
        {
            for (int i = 0; i < 3; i++)
            {
                float cx = r.X + r.Width * (0.22f + i * 0.28f);
                float d = r.Height * (0.52f - i * 0.06f);
                float cy = r.Y + r.Height * 0.38f + i * 2.5f * u;

                Stroke(g, Pencil, 1.0f * u, P(cx, r.Y - 2 * u), P(cx, cy - d / 2));
                var hoop = new RectangleF(cx - d / 2, cy - d / 2, d, d);
                Ring(g, i == 1 ? Teal : Sky, 1.3f * u, hoop);

                // The web: a few chords across the hoop, not a real pattern.
                for (int k = 0; k < 5; k++)
                {
                    double a1 = k * Math.PI * 2 / 5, a2 = a1 + Math.PI * 4 / 5;
                    Stroke(g, Color.FromArgb(150, Pencil), 0.7f * u,
                        P(cx + (float)Math.Cos(a1) * d / 2, cy + (float)Math.Sin(a1) * d / 2),
                        P(cx + (float)Math.Cos(a2) * d / 2, cy + (float)Math.Sin(a2) * d / 2));
                }

                // Feathers below.
                for (int k = -1; k <= 1; k++)
                    Stroke(g, k == 0 ? Petal : Middle, 1.1f * u,
                           P(cx + k * d * 0.22f, cy + d / 2),
                           P(cx + k * d * 0.30f, cy + d * 0.92f));
            }
        }

        static void Therapist(Graphics g, RectangleF r, float u)
        {
            float cx = r.X + r.Width * 0.46f;

            // The armchair, in outline only, the way it was.
            var chair = new[]
            {
                P(r.X - r.Width * 0.20f, r.Bottom),
                P(r.X - r.Width * 0.20f, r.Y + r.Height * 0.50f),
                P(r.X - r.Width * 0.06f, r.Y + r.Height * 0.38f),
                P(r.Right - r.Width * 0.02f, r.Y + r.Height * 0.38f),
                P(r.Right + r.Width * 0.16f, r.Y + r.Height * 0.50f),
                P(r.Right + r.Width * 0.16f, r.Bottom)
            };
            Stroke(g, Pencil, 1.5f * u, chair);
            Stroke(g, Pencil, 1.4f * u,
                   P(r.X - r.Width * 0.20f, r.Y + r.Height * 0.74f),
                   P(r.Right + r.Width * 0.16f, r.Y + r.Height * 0.74f));

            /*  Head first, because everything else is measured from it. A head
                is about a seventh of a seated figure; drawn at a fifth it came
                out as a head in an armchair with a body somewhere behind it. */
            /*  A seated figure is about six heads tall, and every landmark
                below is measured from the head rather than from the box she
                is drawn in -- which is what kept her proportions honest at one
                window size and nowhere else. */
            /*  A seated figure is about six heads tall, and the box she is
                given is a fixed ratio now, so every landmark comes from its
                height. Measuring her width one way and her height another is
                what gave her a six-pixel torso on an ordinary laptop. */
            float headW = r.Height * 0.17f;
            var head = new RectangleF(cx - headW / 2, r.Y, headW, headW * 1.15f);

            float shoulder = head.Bottom + r.Height * 0.05f;
            float waist = shoulder + r.Height * 0.27f;
            float hem = r.Bottom;

            // Skirt.
            var skirt = new[]
            {
                P(cx - r.Width * 0.22f, waist),
                P(cx + r.Width * 0.22f, waist),
                P(cx + r.Width * 0.30f, hem),
                P(cx - r.Width * 0.30f, hem),
                P(cx - r.Width * 0.22f, waist)
            };
            Fill(g, Brown, 140, skirt);
            Stroke(g, Brown, 1.3f * u, skirt);

            // Blouse, blue, printed with big orange flowers.
            var blouse = new[]
            {
                P(cx - r.Width * 0.21f, shoulder),
                P(cx + r.Width * 0.21f, shoulder),
                P(cx + r.Width * 0.24f, waist),
                P(cx - r.Width * 0.24f, waist),
                P(cx - r.Width * 0.21f, shoulder)
            };
            Fill(g, Sky, 150, blouse);
            Stroke(g, Sky, 1.3f * u, blouse);
            for (int i = 0; i < 4; i++)
            {
                float fx = cx + ((i % 2) * 2 - 1) * r.Width * 0.12f;
                float fy = shoulder + (waist - shoulder) * (0.34f + (i / 2) * 0.36f);
                // Printed on the blue, not instead of it: at 0.13 of the
                // width four of them covered the whole torso and each other.
                Hibiscus(g, fx, fy, r.Width * 0.062f, u * 0.6f);
            }

            // The bib collar, orange, sitting on the shoulders.
            var collar = new[]
            {
                P(cx - r.Width * 0.14f, shoulder - r.Height * 0.01f),
                P(cx, shoulder + r.Height * 0.07f),
                P(cx + r.Width * 0.14f, shoulder - r.Height * 0.01f),
                P(cx + r.Width * 0.09f, shoulder - r.Height * 0.03f),
                P(cx - r.Width * 0.09f, shoulder - r.Height * 0.03f),
                P(cx - r.Width * 0.14f, shoulder - r.Height * 0.01f)
            };
            Fill(g, Orange, 190, collar);
            Stroke(g, Orange, 1.1f * u, collar);

            // Arms down to the rests, and the clipboard on one of them.
            Stroke(g, Sky, 2.6f * u,
                   P(cx - r.Width * 0.20f, shoulder + r.Height * 0.02f),
                   P(cx - r.Width * 0.30f, waist - r.Height * 0.02f));
            Stroke(g, Sky, 2.6f * u,
                   P(cx + r.Width * 0.20f, shoulder + r.Height * 0.02f),
                   P(cx + r.Width * 0.28f, waist - r.Height * 0.02f));
            Stroke(g, Skin, 2.2f * u,
                   P(cx - r.Width * 0.30f, waist - r.Height * 0.02f),
                   P(cx - r.Width * 0.32f, waist + r.Height * 0.06f));
            Stroke(g, Skin, 2.2f * u,
                   P(cx + r.Width * 0.28f, waist - r.Height * 0.02f),
                   P(cx + r.Width * 0.30f, waist + r.Height * 0.06f));

            var board = new RectangleF(cx + r.Width * 0.14f, waist + r.Height * 0.02f,
                                       r.Width * 0.28f, r.Height * 0.17f);
            using (var b = new SolidBrush(Color.FromArgb(180, Orange)))
                g.FillRectangle(b, board);
            using (var pen = new Pen(Color.FromArgb(200, Rust), 1.1f * u))
                g.DrawRectangle(pen, board.X, board.Y, board.Width, board.Height);
            for (int i = 1; i < 4; i++)
                Stroke(g, Color.White, 0.9f * u,
                       P(board.X + 2 * u, board.Y + board.Height * i / 4f),
                       P(board.Right - 2 * u, board.Y + board.Height * i / 4f));

            // Neck, then the head over it.
            Stroke(g, Skin, 2.6f * u,
                   P(cx, head.Bottom - 1 * u), P(cx, shoulder + 1 * u));

            Wash(g, Skin, head, 235);
            Ring(g, Brown, 1.1f * u, head);

            /*  Hair as a cap over the top of the head and down the sides, not
                a block across the face -- which is what a 0.62 height gave. */
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(head.X - 2 * u, head.Y - 3 * u,
                                head.Width + 4 * u, head.Height + 3 * u);
                // Saved rather than copied: g.Clip returns a Region that
                // has to be disposed, and this runs on every paint.
                var state = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                using (var b = new SolidBrush(Color.FromArgb(215, Hair)))
                    g.FillRectangle(b, head.X - 3 * u, head.Y - 4 * u,
                                    head.Width + 6 * u, head.Height * 0.40f);
                g.Restore(state);
            }
            Stroke(g, Hair, 1.8f * u,
                   P(head.X - 1 * u, head.Y + head.Height * 0.26f),
                   P(head.X - 2 * u, head.Y + head.Height * 0.70f));
            Stroke(g, Hair, 1.8f * u,
                   P(head.Right + 1 * u, head.Y + head.Height * 0.26f),
                   P(head.Right + 2 * u, head.Y + head.Height * 0.70f));

            // Glasses, eyes, earrings, and a mouth that is listening.
            float eyeY = head.Y + head.Height * 0.54f;
            float lens = head.Width * 0.26f;
            Ring(g, Pencil, 1.2f * u,
                 new RectangleF(cx - lens * 1.55f, eyeY - lens * 0.5f, lens, lens * 0.86f));
            Ring(g, Pencil, 1.2f * u,
                 new RectangleF(cx + lens * 0.55f, eyeY - lens * 0.5f, lens, lens * 0.86f));
            Stroke(g, Pencil, 1.0f * u,
                   P(cx - lens * 0.55f, eyeY - lens * 0.1f), P(cx + lens * 0.55f, eyeY - lens * 0.1f));
            using (var b = new SolidBrush(Color.FromArgb(200, Pencil)))
            {
                g.FillEllipse(b, cx - lens * 1.20f, eyeY - lens * 0.18f, lens * 0.30f, lens * 0.30f);
                g.FillEllipse(b, cx + lens * 0.90f, eyeY - lens * 0.18f, lens * 0.30f, lens * 0.30f);
            }

            Ring(g, Sky, 1.2f * u,
                 new RectangleF(head.X - 1.0f * u, eyeY + lens * 0.5f, 3.0f * u, 3.8f * u));
            Ring(g, Sky, 1.2f * u,
                 new RectangleF(head.Right - 2.0f * u, eyeY + lens * 0.5f, 3.0f * u, 3.8f * u));

            Stroke(g, Rust, 1.1f * u,
                   P(cx - head.Width * 0.16f, head.Bottom - head.Height * 0.20f),
                   P(cx, head.Bottom - head.Height * 0.14f),
                   P(cx + head.Width * 0.16f, head.Bottom - head.Height * 0.20f));
        }

        static void SideTable(Graphics g, RectangleF r, float u)
        {
            float topY = r.Y + r.Height * 0.34f;
            var top = new RectangleF(r.X, topY, r.Width, r.Height * 0.10f);
            using (var b = new SolidBrush(Color.FromArgb(150, Teal)))
                g.FillRectangle(b, top);
            Stroke(g, Teal, 1.3f * u,
                   P(top.X, top.Y), P(top.Right, top.Y), P(top.Right, top.Bottom),
                   P(top.X, top.Bottom), P(top.X, top.Y));

            Stroke(g, Teal, 1.4f * u, P(r.X + r.Width * 0.14f, top.Bottom), P(r.X + r.Width * 0.18f, r.Bottom));
            Stroke(g, Teal, 1.4f * u, P(r.Right - r.Width * 0.14f, top.Bottom), P(r.Right - r.Width * 0.18f, r.Bottom));

            // The tissue box, flowered, with one tissue standing up.
            var box = new RectangleF(r.X + r.Width * 0.10f, topY - r.Height * 0.26f,
                                     r.Width * 0.46f, r.Height * 0.26f);
            using (var b = new SolidBrush(Color.FromArgb(170, Color.White)))
                g.FillRectangle(b, box);
            using (var pen = new Pen(Color.FromArgb(190, Petal), 1.1f * u))
                g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            Hibiscus(g, box.X + box.Width * 0.5f, box.Y + box.Height * 0.62f, box.Width * 0.42f, u * 0.6f);
            Stroke(g, Pencil, 1.0f * u,
                   P(box.X + box.Width * 0.5f, box.Y),
                   P(box.X + box.Width * 0.42f, box.Y - r.Height * 0.10f),
                   P(box.X + box.Width * 0.62f, box.Y - r.Height * 0.06f));

            // And a small clock, because there was one.
            float cd = r.Height * 0.22f;
            var face = new RectangleF(r.Right - r.Width * 0.42f, topY - cd, cd, cd);
            Wash(g, Color.White, face, 220);
            Ring(g, Rust, 1.2f * u, face);
            Stroke(g, Pencil, 1.0f * u,
                   P(face.X + cd / 2, face.Y + cd / 2), P(face.X + cd / 2, face.Y + cd * 0.20f));
            Stroke(g, Pencil, 1.0f * u,
                   P(face.X + cd / 2, face.Y + cd / 2), P(face.X + cd * 0.78f, face.Y + cd / 2));
        }
    }
}
