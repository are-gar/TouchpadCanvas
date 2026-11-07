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

        static Bitmap Draw(int s)
        {
            var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                // Trackpad body
                float stroke = Math.Max(1f, s / 20f);
                float m = stroke / 2 + s * 0.03f;
                float w = s - 2 * m;
                float h = (float)Math.Round(w * 0.8f);
                var pad = new RectangleF(m, (s - h) / 2, w, h);
                using (var path = RoundRect(pad, s * 0.17f))
                using (var fill = new LinearGradientBrush(pad, Color.FromArgb(252, 252, 254), Color.FromArgb(206, 209, 218), 90f))
                using (var pen = new Pen(Color.FromArgb(88, 92, 105), stroke))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }

                // Two fingertips on a diagonal
                float cx = s / 2f, cy = s / 2f;
                float off = s * 0.15f;
                float d = s * (s <= 20 ? 0.26f : 0.22f);
                var blue = Color.FromArgb(37, 117, 240);
                using (var b = new SolidBrush(blue))
                {
                    g.FillEllipse(b, cx - off - d / 2, cy + off * 0.75f - d / 2, d, d);
                    g.FillEllipse(b, cx + off - d / 2, cy - off * 0.75f - d / 2, d, d);
                }

                // Rotation arcs at larger sizes
                if (s >= 32)
                {
                    float r = s * 0.29f;
                    using (var pen = new Pen(Color.FromArgb(170, blue), Math.Max(1.5f, s / 24f)))
                    {
                        pen.EndCap = LineCap.ArrowAnchor;
                        var arc = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
                        g.DrawArc(pen, arc, 195, 55);
                        g.DrawArc(pen, arc, 15, 55);
                    }
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
