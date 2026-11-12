using System;
using System.Collections.Generic;
using System.Drawing;

namespace TrackpadCanvas
{
    // A font size for PixelFont: Scale is in UI units (Theme.U device pixels each); Bold thickens strokes by one pixel.
    internal struct PFont
    {
        public readonly int Scale;
        public readonly bool Bold;
        public PFont(int scale, bool bold) { Scale = scale; Bold = bold; }
        // Scale 0 is a half-size font (one device pixel per font pixel at 200%), used by a preview variant.
        public int Px { get { return Scale > 0 ? Scale * Theme.U : Math.Max(1, Theme.U / 2); } }
    }

    // Built-in proportional bitmap font, drawn with filled squares so it stays crisp at any integer scale.
    // Glyphs are 7 rows above the baseline plus 2 descender rows.
    internal static class PixelFont
    {
        public const int Rows = 9, Advance = 11, Cap = 7;

        sealed class Glyph
        {
            public int W;
            public int[] Row, X, Len;
        }

        static readonly string[] Data =
        {
            "A .###.|#...#|#...#|#####|#...#|#...#|#...#",
            "B ####.|#...#|#...#|####.|#...#|#...#|####.",
            "C .###.|#...#|#....|#....|#....|#...#|.###.",
            "D ####.|#...#|#...#|#...#|#...#|#...#|####.",
            "E #####|#....|#....|####.|#....|#....|#####",
            "F #####|#....|#....|####.|#....|#....|#....",
            "G .###.|#...#|#....|#.###|#...#|#...#|.####",
            "H #...#|#...#|#...#|#####|#...#|#...#|#...#",
            "I ###|.#.|.#.|.#.|.#.|.#.|###",
            "J ..###|...#.|...#.|...#.|...#.|#..#.|.##..",
            "K #...#|#..#.|#.#..|##...|#.#..|#..#.|#...#",
            "L #....|#....|#....|#....|#....|#....|#####",
            "M #...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#",
            "N #...#|#...#|##..#|#.#.#|#..##|#...#|#...#",
            "O .###.|#...#|#...#|#...#|#...#|#...#|.###.",
            "P ####.|#...#|#...#|####.|#....|#....|#....",
            "Q .###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#",
            "R ####.|#...#|#...#|####.|#.#..|#..#.|#...#",
            "S .####|#....|#....|.###.|....#|....#|####.",
            "T #####|..#..|..#..|..#..|..#..|..#..|..#..",
            "U #...#|#...#|#...#|#...#|#...#|#...#|.###.",
            "V #...#|#...#|#...#|#...#|#...#|.#.#.|..#..",
            "W #...#|#...#|#...#|#.#.#|#.#.#|#.#.#|.#.#.",
            "X #...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
            "Y #...#|#...#|.#.#.|..#..|..#..|..#..|..#..",
            "Z #####|....#|...#.|..#..|.#...|#....|#####",
            "a .....|.....|.###.|....#|.####|#...#|.####",
            "b #....|#....|####.|#...#|#...#|#...#|####.",
            "c ....|....|.###|#...|#...|#...|.###",
            "d ....#|....#|.####|#...#|#...#|#...#|.####",
            "e .....|.....|.###.|#...#|#####|#....|.###.",
            "f ..##|.#..|###.|.#..|.#..|.#..|.#..",
            "g .....|.....|.####|#...#|#...#|#...#|.####|....#|.###.",
            "h #....|#....|####.|#...#|#...#|#...#|#...#",
            "i #|.|#|#|#|#|#",
            "j ..#|...|..#|..#|..#|..#|..#|#.#|.#.",
            "k #...|#...|#..#|#.#.|##..|#.#.|#..#",
            "l #.|#.|#.|#.|#.|#.|.#",
            "m .....|.....|##.#.|#.#.#|#.#.#|#.#.#|#.#.#",
            "n .....|.....|####.|#...#|#...#|#...#|#...#",
            "o .....|.....|.###.|#...#|#...#|#...#|.###.",
            "p .....|.....|####.|#...#|#...#|#...#|####.|#....|#....",
            "q .....|.....|.####|#...#|#...#|#...#|.####|....#|....#",
            "r ....|....|#.##|##..|#...|#...|#...",
            "s ....|....|.###|#...|.##.|...#|###.",
            "t ....|.#..|###.|.#..|.#..|.#..|..##",
            "u .....|.....|#...#|#...#|#...#|#...#|.####",
            "v .....|.....|#...#|#...#|#...#|.#.#.|..#..",
            "w .....|.....|#...#|#...#|#.#.#|#.#.#|.#.#.",
            "x .....|.....|#...#|.#.#.|..#..|.#.#.|#...#",
            "y .....|.....|#...#|#...#|#...#|#...#|.####|....#|.###.",
            "z .....|.....|#####|...#.|..#..|.#...|#####",
            "0 .###.|#...#|#..##|#.#.#|##..#|#...#|.###.",
            "1 ..#..|.##..|..#..|..#..|..#..|..#..|.###.",
            "2 .###.|#...#|....#|...#.|..#..|.#...|#####",
            "3 #####|...#.|..#..|...#.|....#|#...#|.###.",
            "4 ...#.|..##.|.#.#.|#..#.|#####|...#.|...#.",
            "5 #####|#....|####.|....#|....#|#...#|.###.",
            "6 ..##.|.#...|#....|####.|#...#|#...#|.###.",
            "7 #####|....#|...#.|..#..|.#...|.#...|.#...",
            "8 .###.|#...#|#...#|.###.|#...#|#...#|.###.",
            "9 .###.|#...#|#...#|.####|....#|...#.|.##..",
            "  ...|...|...|...|...|...|...",
            "! #|#|#|#|#|.|#",
            "\" #.#|#.#|...|...|...|...|...",
            "# .#.#.|#####|.#.#.|.#.#.|.#.#.|#####|.#.#.",
            "$ ..#..|.####|#.#..|.###.|..#.#|####.|..#..",
            "% ##...|##..#|...#.|..#..|.#...|#..##|...##",
            "& .##..|#..#.|#.#..|.#...|#.#.#|#..#.|.##.#",
            "' #|#|.|.|.|.|.",
            "( ..#|.#.|#..|#..|#..|.#.|..#",
            ") #..|.#.|..#|..#|..#|.#.|#..",
            "* .....|..#..|#.#.#|.###.|#.#.#|..#..|.....",
            "+ .....|..#..|..#..|#####|..#..|..#..|.....",
            ", ..|..|..|..|..|.#|.#|#.",
            "- ....|....|....|####|....|....|....",
            ". .|.|.|.|.|.|#",
            "/ ....#|....#|...#.|..#..|.#...|#....|#....",
            ": .|.|#|.|.|.|#",
            "; ..|..|.#|..|..|..|.#|#.",
            "< ...#|..#.|.#..|#...|.#..|..#.|...#",
            "= .....|.....|#####|.....|#####|.....|.....",
            "> #...|.#..|..#.|...#|..#.|.#..|#...",
            "? .###.|#...#|....#|...#.|..#..|.....|..#..",
            "@ .###.|#...#|#.###|#.#.#|#.###|#....|.###.",
            "[ ###|#..|#..|#..|#..|#..|###",
            "\\ #....|#....|.#...|..#..|...#.|....#|....#",
            "] ###|..#|..#|..#|..#|..#|###",
            "^ ..#..|.#.#.|#...#|.....|.....|.....|.....",
            "_ .....|.....|.....|.....|.....|.....|.....|#####",
            "` #.|.#|..|..|..|..|..",
            "{ ..##|.#..|.#..|#...|.#..|.#..|..##",
            "} ##..|..#.|..#.|...#|..#.|..#.|##..",
            "~ .....|.....|.#...|#.#.#|...#.|.....|.....",
            "· ..|..|..|##|##|..|..",
            "› ...|...|#..|.#.|..#|.#.|#..",
            "— .......|.......|.......|#######|.......|.......|.......",
            "… .....|.....|.....|.....|.....|.....|#.#.#",
        };

        static readonly Dictionary<char, Glyph> glyphs = Build();

        static Dictionary<char, Glyph> Build()
        {
            var d = new Dictionary<char, Glyph>();
            foreach (var entry in Data) d[entry[0]] = Parse(entry.Substring(2).Split('|'));
            var bar = new string[Rows];
            for (int i = 0; i < Rows; i++) bar[i] = "#";
            d['|'] = Parse(bar);
            return d;
        }

        static Glyph Parse(string[] rows)
        {
            var row = new List<int>();
            var x = new List<int>();
            var len = new List<int>();
            for (int r = 0; r < rows.Length; r++)
            {
                string s = rows[r];
                for (int c = 0; c < s.Length; c++)
                {
                    if (s[c] != '#') continue;
                    int start = c;
                    while (c < s.Length && s[c] == '#') c++;
                    row.Add(r); x.Add(start); len.Add(c - start);
                }
            }
            return new Glyph { W = rows[0].Length, Row = row.ToArray(), X = x.ToArray(), Len = len.ToArray() };
        }

        static Glyph Get(char c)
        {
            if (Theme.AllCaps) c = char.ToUpperInvariant(c);
            switch (c)
            {
                case '“': case '”': c = '"'; break;
                case '‘': case '’': c = '\''; break;
                case '\t': c = ' '; break;
            }
            Glyph g;
            return glyphs.TryGetValue(c, out g) ? g : glyphs['?'];
        }

        public static int Width(string s, PFont f)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int extra = f.Bold ? 1 : 0, w = 0;
            foreach (char c in s) w += Get(c).W + 1 + extra;
            return (w - 1) * f.Px;
        }

        public static int Height(PFont f) { return Rows * f.Px; }
        public static int CapHeight(PFont f) { return Cap * f.Px; }
        public static int LineHeight(PFont f) { return Advance * f.Px; }

        public static void Draw(Graphics g, string s, int x, int y, PFont f, Color color)
        {
            if (string.IsNullOrEmpty(s)) return;
            int px = f.Px, extra = f.Bold ? 1 : 0;
            var rects = new List<Rectangle>(s.Length * 10);
            foreach (char c in s)
            {
                var gl = Get(c);
                for (int i = 0; i < gl.Row.Length; i++)
                    rects.Add(new Rectangle(x + gl.X[i] * px, y + gl.Row[i] * px, (gl.Len[i] + extra) * px, px));
                x += (gl.W + 1 + extra) * px;
            }
            if (rects.Count > 0)
                using (var b = new SolidBrush(color)) g.FillRectangles(b, rects.ToArray());
        }

        // Cuts the text to fit width, ending with an ellipsis.
        public static string Fit(string s, PFont f, int width)
        {
            if (string.IsNullOrEmpty(s) || Width(s, f) <= width) return s;
            const string dots = "…";
            int n = s.Length;
            while (n > 0 && Width(s.Substring(0, n).TrimEnd() + dots, f) > width) n--;
            return n == 0 ? "" : s.Substring(0, n).TrimEnd() + dots;
        }

        // One line inside r, vertically centred on the capital letters.
        public static void DrawIn(Graphics g, string s, Rectangle r, PFont f, Color color, bool center)
        {
            s = Fit(s, f, r.Width);
            int w = Width(s, f);
            int x = center ? r.X + (r.Width - w) / 2 : r.X;
            Draw(g, s, x, r.Y + (r.Height - CapHeight(f)) / 2, f, color);
        }

        public static List<string> Wrap(string text, PFont f, int width)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (var para in text.Replace("\r", "").Split('\n'))
            {
                string line = "";
                foreach (var word in para.Split(' '))
                {
                    string w = word;
                    string cand = line.Length == 0 ? w : line + " " + w;
                    if (Width(cand, f) <= width) { line = cand; continue; }
                    if (line.Length > 0) lines.Add(line);
                    while (w.Length > 1 && Width(w, f) > width)
                    {
                        int n = w.Length - 1;
                        while (n > 1 && Width(w.Substring(0, n), f) > width) n--;
                        lines.Add(w.Substring(0, n));
                        w = w.Substring(n);
                    }
                    line = w;
                }
                lines.Add(line);
            }
            return lines;
        }

        public static int BlockHeight(int lines, PFont f)
        {
            return lines <= 0 ? 0 : (lines * Advance - (Advance - Rows)) * f.Px;
        }

        public static int BlockHeight(string text, PFont f, int width)
        {
            return BlockHeight(Wrap(text, f, width).Count, f);
        }

        public static void DrawBlock(Graphics g, string text, PFont f, Rectangle r, Color color)
        {
            var lines = Wrap(text, f, r.Width);
            for (int i = 0; i < lines.Count; i++) Draw(g, lines[i], r.X, r.Y + i * LineHeight(f), f, color);
        }
    }
}
