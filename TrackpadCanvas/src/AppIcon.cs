using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TrackpadCanvas
{
    // Draws the app icon (a trackpad with two fingertips) and packs it as a multi-size .ico.
    internal static class AppIcon
    {
        static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128 };
        static byte[] cached;

        public static Icon Load()
        {
            return new Icon(new MemoryStream(IcoBytes()));
        }

        public static Icon Load(Size size)
        {
            return new Icon(new MemoryStream(IcoBytes()), size);
        }

        public static void Write(string path)
        {
            File.WriteAllBytes(path, IcoBytes());
        }

        static byte[] IcoBytes()
        {
            if (cached != null) return cached;
            var images = new byte[Sizes.Length][];
            for (int i = 0; i < Sizes.Length; i++)
                using (var bmp = Draw(Sizes[i]))
                    images[i] = ToDib(bmp);

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((ushort)0);            // reserved
                w.Write((ushort)1);            // type: icon
                w.Write((ushort)Sizes.Length);
                int offset = 6 + 16 * Sizes.Length;
                for (int i = 0; i < Sizes.Length; i++)
                {
                    int s = Sizes[i];
                    w.Write((byte)(s >= 256 ? 0 : s));
                    w.Write((byte)(s >= 256 ? 0 : s));
                    w.Write((byte)0);          // palette colours
                    w.Write((byte)0);          // reserved
                    w.Write((ushort)1);        // planes
                    w.Write((ushort)32);       // bits per pixel
                    w.Write(images[i].Length);
                    w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var img in images) w.Write(img);
                cached = ms.ToArray();
            }
            return cached;
        }

        // 16x16 pixel-art trackpad with two fingertips. K ink, w highlight, c body, d body shade,
        // B blue finger, P pink finger, S translucent shadow, '.' transparent.
        static readonly string[] Pixels =
        {
            "................",
            "................",
            "..KKKKKKKKKKKK..",
            ".KwwwwwwwwwwwwK.",
            ".KccccccccKKccKS",
            ".KcccccccKBBKcKS",
            ".KcccccccKBBKcKS",
            ".KccccccccKKccKS",
            ".KccKKccccccccKS",
            ".KcKPPKcccccccKS",
            ".KcKPPKcccccccKS",
            ".KccKKccccccccKS",
            ".KddddddddddddKS",
            "..KKKKKKKKKKKKSS",
            "...SSSSSSSSSSSS.",
            "................",
        };

        static Color PixelColor(char c)
        {
            switch (c)
            {
                case 'K': return Color.FromArgb(22, 30, 72);
                case 'w': return Color.FromArgb(222, 242, 228);
                case 'c': return Color.FromArgb(178, 220, 192);
                case 'd': return Color.FromArgb(136, 188, 154);
                case 'B': return Color.FromArgb(72, 96, 224);
                case 'P': return Color.FromArgb(226, 6, 90);
                case 'S': return Color.FromArgb(90, 20, 20, 60);
            }
            return Color.Transparent;
        }

        // Draws the pixel icon with its top-left at (x, y), each icon pixel px device pixels wide.
        public static void DrawPixels(Graphics g, int x, int y, int px)
        {
            for (int row = 0; row < 16; row++)
                for (int col = 0; col < 16; col++)
                {
                    var c = PixelColor(Pixels[row][col]);
                    if (c.A == 0) continue;
                    using (var b = new SolidBrush(c)) g.FillRectangle(b, x + col * px, y + row * px, px, px);
                }
        }

        static Bitmap Draw(int s)
        {
            var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.None;
                // Nearest-neighbour cells; exact for multiples of 16, slightly uneven otherwise.
                for (int row = 0; row < 16; row++)
                    for (int col = 0; col < 16; col++)
                    {
                        var c = PixelColor(Pixels[row][col]);
                        if (c.A == 0) continue;
                        int x0 = col * s / 16, x1 = (col + 1) * s / 16, y0 = row * s / 16, y1 = (row + 1) * s / 16;
                        using (var b = new SolidBrush(c)) g.FillRectangle(b, x0, y0, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0));
                    }
            }
            return bmp;
        }

        static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 32-bit DIB icon image: BITMAPINFOHEADER, bottom-up BGRA pixels, then the 1-bit AND mask.
        static byte[] ToDib(Bitmap bmp)
        {
            int s = bmp.Width;
            var data = bmp.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[s * s * 4];
            for (int y = 0; y < s; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * s * 4, s * 4);
            bmp.UnlockBits(data);

            int maskStride = ((s + 31) / 32) * 4;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(40); w.Write(s); w.Write(s * 2);
                w.Write((ushort)1); w.Write((ushort)32);
                w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                for (int y = s - 1; y >= 0; y--)
                    w.Write(pixels, y * s * 4, s * 4);
                for (int y = s - 1; y >= 0; y--)
                {
                    var row = new byte[maskStride];
                    for (int x = 0; x < s; x++)
                        if (pixels[(y * s + x) * 4 + 3] == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
                    w.Write(row);
                }
                return ms.ToArray();
            }
        }
    }
}
