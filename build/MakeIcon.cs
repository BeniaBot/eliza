// Draws the application icon and writes a multi-size .ico.
//
// Every frame is a 32-bit BMP (DIB), not a PNG, because PNG-framed icons trip
// up older readers. The AND mask is present but empty: the alpha channel does
// the work.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

static class MakeIcon
{
    /*  Ten frames, and the two that were missing are the two that a scaled
        desktop actually asks for: Windows wants 40 at 125 per cent and 96 at
        150, and when the frame is not there it takes the next one up and
        shrinks it. A drawing made of hairlines does not survive being
        shrunk by a quarter -- which is exactly the taskbar and the alt-tab
        card on a laptop, where most people were going to see it. */
    static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };

    static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float s = size / 16f;
            var screen = new RectangleF(s * 0.7f, s * 1.7f, s * 14.6f, s * 12.6f);

            // cabinet
            using (var body = new LinearGradientBrush(
                new RectangleF(0, 0, size, size),
                Color.FromArgb(255, 74, 74, 80), Color.FromArgb(255, 30, 30, 34), 64f))
            using (var path = Rounded(new RectangleF(0, s * 1f, size, s * 14f), s * 2f))
                g.FillPath(body, path);

            // tube
            using (var dark = new SolidBrush(Color.FromArgb(255, 6, 18, 9)))
            using (var path = Rounded(screen, s * 1.4f))
                g.FillPath(dark, path);

            // a line of text and the cursor after it
            var green = Color.FromArgb(255, 64, 240, 110);
            using (var b = new SolidBrush(green))
            {
                if (size >= 32)
                {
                    g.FillRectangle(b, screen.X + s * 1.6f, screen.Y + s * 3.2f, s * 7.5f, s * 1.1f);
                    g.FillRectangle(b, screen.X + s * 1.6f, screen.Y + s * 5.6f, s * 10.0f, s * 1.1f);
                    g.FillRectangle(b, screen.X + s * 1.6f, screen.Y + s * 8.0f, s * 4.2f, s * 1.1f);
                    g.FillRectangle(b, screen.X + s * 6.6f, screen.Y + s * 8.0f, s * 1.9f, s * 1.1f);
                }
                else
                {
                    g.FillRectangle(b, screen.X + s * 1.6f, screen.Y + s * 3.6f, s * 8.4f, s * 1.6f);
                    g.FillRectangle(b, screen.X + s * 1.6f, screen.Y + s * 7.4f, s * 4.2f, s * 1.6f);
                    g.FillRectangle(b, screen.X + s * 7.0f, screen.Y + s * 7.4f, s * 2.4f, s * 1.6f);
                }
            }

            if (size >= 48)
                using (var glow = new SolidBrush(Color.FromArgb(46, green)))
                using (var path = Rounded(RectangleF.Inflate(screen, -s, -s), s))
                    g.FillPath(glow, path);
        }
        return bmp;
    }

    static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = Math.Max(1f, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static byte[] Dib(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int pixels = w * h * 4;
        int maskStride = ((w + 31) / 32) * 4;
        int mask = maskStride * h;

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(40);              // BITMAPINFOHEADER
            bw.Write(w);
            bw.Write(h * 2);           // height counts colour plus mask
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(0);               // BI_RGB
            bw.Write(pixels + mask);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

            for (int y = h - 1; y >= 0; y--)          // bottom-up
                for (int x = 0; x < w; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }

            bw.Write(new byte[mask]);
            return ms.ToArray();
        }
    }

    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "eliza.ico";
        var frames = new byte[Sizes.Length][];
        for (int i = 0; i < Sizes.Length; i++)
            using (var bmp = Draw(Sizes[i]))
                frames[i] = Dib(bmp);

        using (var fs = File.Create(path))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((short)0);
            bw.Write((short)1);                       // type: icon
            bw.Write((short)Sizes.Length);

            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                bw.Write((byte)(Sizes[i] >= 256 ? 0 : Sizes[i]));
                bw.Write((byte)(Sizes[i] >= 256 ? 0 : Sizes[i]));
                bw.Write((byte)0);                    // palette
                bw.Write((byte)0);                    // reserved
                bw.Write((short)1);                   // planes
                bw.Write((short)32);                  // bits per pixel
                bw.Write(frames[i].Length);
                bw.Write(offset);
                offset += frames[i].Length;
            }
            foreach (var f in frames) bw.Write(f);
        }

        Console.WriteLine("wrote " + path + " with " + Sizes.Length + " sizes");
        return 0;
    }
}
