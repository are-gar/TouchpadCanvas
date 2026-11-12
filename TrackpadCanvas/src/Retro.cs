using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TrackpadCanvas
{
    // Pastel pixel-art look: everything is drawn on a grid of "units" (Theme.U device pixels each, so it stays
    // crisp at any display scale) with stair-stepped corners, 1-unit ink outlines, hard shadows and the built-in
    // bitmap font. All sizes below are in units.
    internal static partial class Theme
    {
        // Grayscale Windows 95 palette: grey desktop, silver bezels with white/grey bevels, white fields and
        // read-outs, black text and dark grey title strips. (Formerly preview variant 10.)
        public static Color Paper = Color.FromArgb(128, 128, 128);
        public static Color PaperAlt = Color.FromArgb(112, 112, 112);
        public static Color PaperDark = Color.FromArgb(164, 164, 164);
        public static Color Dot = Color.FromArgb(192, 192, 192);
        public static Color Card = Color.FromArgb(192, 192, 192);
        public static Color Field = Color.White;
        public static Color Ink = Color.Black;
        public static Color InkSoft = Color.FromArgb(64, 64, 64);
        public static Color Disabled = Color.FromArgb(121, 121, 121);
        public static Color DisabledFill = Color.FromArgb(177, 177, 177);
        public static Color Pink = Color.FromArgb(228, 228, 228);
        public static Color Blue = Color.FromArgb(212, 212, 212);
        public static Color Yellow = Color.FromArgb(236, 236, 236);
        public static Color Mint = Color.FromArgb(220, 220, 220);
        public static Color Lavender = Color.FromArgb(216, 216, 216);
        public static Color Peach = Color.FromArgb(224, 224, 224);
        public static Color Hover = Color.FromArgb(224, 224, 224);
        public static Color Link = Color.FromArgb(128, 128, 128);
        public static Color Selection = Color.FromArgb(223, 223, 223);
        public static Color Lcd = Color.White;
        public static Color LcdText = Color.Black;
        public static Color LcdDim = Color.FromArgb(204, 204, 204);
        public static Color Bezel = Color.FromArgb(192, 192, 192);
        public static Color BezelLight = Color.White;
        public static Color BezelDark = Color.FromArgb(128, 128, 128);
        public static Color Shadow = Color.FromArgb(80, 20, 20, 60);

        public static PFont Text = new PFont(1, false);
        public static PFont Bold = new PFont(1, true);
        public static PFont Title = new PFont(2, true);

        // Corner shapes: how many units each row is inset from the edge, starting at the top row.
        public static readonly int[] C1 = { 1 }, C2 = { 2, 1 }, C3 = { 3, 1, 1 }, C4 = { 4, 2, 1, 1 };

        // ---- Style switches. The defaults are the normal look; Theme.ApplyVariant (Variants.cs) changes them
        // for the hidden --variant N preview mode.
        public static int Variant;
        public static int BgStyle;               // 0 solid, 1 checker, 2 small checker, 3 stripes, 4 dot grid, 5 scanlines
        public static int CardStyle;             // 0 bezel + LCD body, 1 flat, 2 Win95 bevel
        public static int BezelU = 3;
        public static int[] CardCorners = C3, BodyCorners = C2;
        public static bool DoubleOutline, AccentBezel;
        public static int TitleAlign;            // 0 left strip, 1 centred strip, 2 tab on top of the card
        public static Color TitleFill = Color.FromArgb(64, 64, 64), TitleText = Color.White;
        public static int Space = 100;           // percent applied to padding and gaps
        public static int PageMarginU = 10;
        public static int WindowWU = 470, WindowHU = 470;
        public static bool TwoColumns;
        public static int TabsAt;                // 0 top, 1 left, 2 bottom
        public static int HeaderMode;            // 0 full, 1 slim bar, 2 none (switch moves into the tabs bar)
        public static int HeaderU = 52;
        public static bool StatusBar, AllCaps;
        public static int HintMode;              // 0 normal, 1 tooltip only, 2 small and dim
        public static PFont Hint = Text;
        public static bool CheckSwitches, FlatKeys, LcdReadouts, SegmentedTrack;
        public static int KeyScale = 1;
        static ToolTip hintTip;

        public static Color HintInk { get { return HintMode == 2 ? Mix(InkSoft, Card, 0.15f) : InkSoft; } }

        // Spacing in units scaled by Space, then converted to device pixels.
        public static int Sp(int units) { return Px((int)Math.Round(units * Space / 100.0)); }

        // In tooltip-hint mode, moves a control's hint into a tooltip and returns null; otherwise returns the hint.
        public static string TakeHint(Control c, string hint)
        {
            if (HintMode != 1 || string.IsNullOrEmpty(hint)) return hint;
            if (hintTip == null) hintTip = PixelToolTip();
            hintTip.SetToolTip(c, hint);
            return null;
        }

        public static readonly int U = ComputeUnit();

        static int ComputeUnit()
        {
            double scale = Program.Px(1000) / 1000.0;
            return Math.Max(1, (int)Math.Round(scale, MidpointRounding.AwayFromZero));
        }

        public static int Px(int units) { return units * U; }
        public static Size Sz(int w, int h) { return new Size(Px(w), Px(h)); }

        public static int W(Size proposed, int fallback)
        {
            return proposed.Width > 0 && proposed.Width < 100000 ? proposed.Width : fallback;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(a.R + (int)((b.R - a.R) * t), a.G + (int)((b.G - a.G) * t), a.B + (int)((b.B - a.B) * t));
        }

        public static void Fill(Graphics g, Color c, Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var b = new SolidBrush(c)) g.FillRectangle(b, r);
        }

        // Corner insets for the shape left after removing a 1-unit outline.
        public static int[] Inner(int[] ins)
        {
            var l = new List<int>();
            for (int j = 0; j < ins.Length; j++)
            {
                int below = j + 1 < ins.Length ? ins[j + 1] : 0;
                l.Add(Math.Max(below + 1, ins[j]) - 1);
            }
            while (l.Count > 0 && l[l.Count - 1] <= 0) l.RemoveAt(l.Count - 1);
            return l.ToArray();
        }

        public static int[] RoundInsets(int r)
        {
            var l = new List<int>();
            for (int i = 0; i < r; i++)
            {
                double dy = r - i - 0.5;
                int ins = (int)Math.Round(r - Math.Sqrt(Math.Max(0, r * r - dy * dy)));
                if (ins <= 0) break;
                l.Add(ins);
            }
            return l.ToArray();
        }

        // A rectangle with stair-stepped corners.
        public static void Stepped(Graphics g, Color c, Rectangle r, int[] ins, bool top = true, bool bottom = true)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            int u = U, n = Math.Min(ins.Length, r.Height / (2 * u));
            using (var b = new SolidBrush(c))
            {
                int y0 = r.Y + (top ? n * u : 0), y1 = r.Bottom - (bottom ? n * u : 0);
                if (y1 > y0) g.FillRectangle(b, r.X, y0, r.Width, y1 - y0);
                for (int i = 0; i < n; i++)
                {
                    int x = r.X + ins[i] * u, w = r.Width - 2 * ins[i] * u;
                    if (w <= 0) continue;
                    if (top) g.FillRectangle(b, x, r.Y + i * u, w, u);
                    if (bottom) g.FillRectangle(b, x, r.Bottom - (i + 1) * u, w, u);
                }
            }
        }

        // Ink-outlined stepped box, optionally with a hard shadow `shadow` units to the bottom right.
        public static void Frame(Graphics g, Rectangle r, Color fill, Color border, int[] corners, int shadow)
        {
            if (shadow > 0)
            {
                var s = r;
                s.Offset(Px(shadow), Px(shadow));
                Stepped(g, Shadow, s, corners);
            }
            Stepped(g, border, r, corners);
            Stepped(g, fill, Rectangle.Inflate(r, -U, -U), Inner(corners));
        }

        // Light top/left and dark bottom/right edge just inside an outline drawn with Frame.
        public static void Bevel(Graphics g, Rectangle r, int[] corners, Color hi, Color lo)
        {
            var inr = Rectangle.Inflate(r, -U, -U);
            var ic = Inner(corners);
            int n = ic.Length, i0 = n > 0 ? ic[0] : 0;
            Fill(g, hi, new Rectangle(inr.X + Px(i0), inr.Y, inr.Width - 2 * Px(i0), U));
            Fill(g, hi, new Rectangle(inr.X, inr.Y + Px(n), U, inr.Height - 2 * Px(n)));
            Fill(g, lo, new Rectangle(inr.X + Px(i0), inr.Bottom - U, inr.Width - 2 * Px(i0), U));
            Fill(g, lo, new Rectangle(inr.Right - U, inr.Y + Px(n), U, inr.Height - 2 * Px(n)));
        }

        // Checkerboard dither of `cell`-unit squares.
        public static void Dither(Graphics g, Rectangle r, Color c, int cell)
        {
            int s = Px(cell);
            var rects = new List<Rectangle>();
            for (int y = r.Y, row = 0; y < r.Bottom; y += s, row++)
                for (int x = r.X + (row & 1) * s; x < r.Right; x += 2 * s)
                {
                    var q = Rectangle.Intersect(new Rectangle(x, y, s, s), r);
                    if (q.Width > 0 && q.Height > 0) rects.Add(q);
                }
            if (rects.Count > 0) using (var b = new SolidBrush(c)) g.FillRectangles(b, rects.ToArray());
        }

        public static void PixelCircle(Graphics g, Color c, int x, int y, int d)
        {
            double r = d / 2.0;
            int u = U;
            using (var b = new SolidBrush(c))
                for (int i = 0; i < d; i++)
                {
                    double dy = i + 0.5 - r;
                    double half = Math.Sqrt(Math.Max(0, r * r - dy * dy));
                    int x0 = (int)Math.Round(r - half), x1 = (int)Math.Round(r + half);
                    if (x1 > x0) g.FillRectangle(b, x + x0 * u, y + i * u, (x1 - x0) * u, u);
                }
        }

        // Draws the '#' cells of a small bitmap.
        public static void Pattern(Graphics g, string[] rows, int x, int y, char ch, Color c)
        {
            using (var b = new SolidBrush(c))
                for (int r = 0; r < rows.Length; r++)
                    for (int k = 0; k < rows[r].Length; k++)
                        if (rows[r][k] == ch) g.FillRectangle(b, x + k * U, y + r * U, U, U);
        }

        // Dotted pixel outline used as the keyboard focus cue.
        public static void FocusDots(Graphics g, Rectangle r)
        {
            int u = U;
            using (var b = new SolidBrush(Ink))
            {
                for (int x = r.X; x < r.Right; x += 2 * u) { g.FillRectangle(b, x, r.Y, u, u); g.FillRectangle(b, x, r.Bottom - u, u, u); }
                for (int y = r.Y; y < r.Bottom; y += 2 * u) { g.FillRectangle(b, r.X, y, u, u); g.FillRectangle(b, r.Right - u, y, u, u); }
            }
        }

        static int FloorDiv(int a, int b) { return a >= 0 ? a / b : -((-a + b - 1) / b); }

        // Page background: plain Paper by default; BgStyle adds a PaperAlt pattern aligned to `origin`
        // (the area's top-left in the page's coordinate space).
        public static void Checker(Graphics g, Rectangle area, Point origin)
        {
            Fill(g, Paper, area);
            if (BgStyle == 0) return;
            var rects = new List<Rectangle>();
            int u = U;
            if (BgStyle == 1 || BgStyle == 2)
            {
                int s = Px(BgStyle == 1 ? 14 : 4);
                for (int sy = FloorDiv(origin.Y, s); sy * s < origin.Y + area.Height; sy++)
                    for (int sx = FloorDiv(origin.X, s); sx * s < origin.X + area.Width; sx++)
                        if (((sx + sy) & 1) != 0) rects.Add(new Rectangle(area.X + sx * s - origin.X, area.Y + sy * s - origin.Y, s, s));
            }
            else if (BgStyle == 3) // diagonal stripes, 4 units on / 4 off, stepped one unit per row
            {
                int period = Px(8);
                for (int y = FloorDiv(origin.Y, u) * u; y < origin.Y + area.Height; y += u)
                {
                    int shift = ((y / u) % 8 + 8) % 8 * u;
                    for (int x = FloorDiv(origin.X - shift, period) * period + shift; x < origin.X + area.Width; x += period)
                        rects.Add(new Rectangle(area.X + x - origin.X, area.Y + y - origin.Y, Px(4), u));
                }
            }
            else if (BgStyle == 4) // dot grid
            {
                int s = Px(8);
                for (int y = FloorDiv(origin.Y, s) * s; y < origin.Y + area.Height; y += s)
                    for (int x = FloorDiv(origin.X, s) * s; x < origin.X + area.Width; x += s)
                        rects.Add(new Rectangle(area.X + x - origin.X, area.Y + y - origin.Y, Px(2), Px(2)));
            }
            else if (BgStyle == 5) // scanlines
            {
                int s = Px(3);
                for (int y = FloorDiv(origin.Y, s) * s; y < origin.Y + area.Height; y += s)
                    rects.Add(new Rectangle(area.X, area.Y + y - origin.Y, area.Width, u));
            }
            var clipped = new List<Rectangle>(rects.Count);
            foreach (var r in rects)
            {
                var q = Rectangle.Intersect(r, area);
                if (q.Width > 0 && q.Height > 0) clipped.Add(q);
            }
            if (clipped.Count > 0) using (var b = new SolidBrush(PaperAlt)) g.FillRectangles(b, clipped.ToArray());
        }

        // Paints what is "behind" a control: the page checkerboard, or its parent's colour.
        public static void Backdrop(Graphics g, Control c)
        {
            var vs = c.Parent as VStack;
            if (vs != null && vs.Checker)
            {
                var root = vs.CheckerRoot;
                Checker(g, c.ClientRectangle, root.PointToClient(c.PointToScreen(Point.Empty)));
            }
            else Fill(g, c.Parent != null ? c.Parent.BackColor : c.BackColor, c.ClientRectangle);
        }

        // A small outlined label; returns the area it covers including its shadow.
        public static Rectangle Tag(Graphics g, int x, int y, string text, PFont f, Color fill, bool enabled)
        {
            var r = new Rectangle(x, y, PixelFont.Width(text, f) + Px(8), TagHeight);
            Frame(g, r, enabled ? fill : DisabledFill, enabled ? Ink : Disabled, C1, 1);
            PixelFont.DrawIn(g, text, r, f, enabled ? Ink : Disabled, true);
            return new Rectangle(r.X, r.Y, r.Width + U, r.Height + U);
        }

        public static int TagHeight { get { return Px(PixelFont.Rows + 4); } }

        // A keyboard-key style button face. Returns the rectangle to centre the label in.
        public static Rectangle Keycap(Graphics g, Rectangle r, Color face, bool pressed, bool enabled, bool hover)
        {
            int lip = Px(2 * KeyScale);
            Color ink = enabled ? Ink : Disabled;
            Color f = !enabled ? DisabledFill : hover && !pressed ? Mix(face, Color.White, 0.4f) : face;
            if (FlatKeys)
            {
                Frame(g, r, pressed ? Mix(f, Ink, 0.15f) : f, ink, C1, 0);
                return r;
            }
            if (pressed)
            {
                var pr = new Rectangle(r.X, r.Y + lip, r.Width, r.Height - lip);
                Frame(g, pr, f, ink, C2, 0);
                return pr;
            }
            Frame(g, r, Mix(f, Ink, 0.25f), ink, C2, 0);
            var inr = Rectangle.Inflate(r, -U, -U);
            var faceR = new Rectangle(inr.X, inr.Y, inr.Width, inr.Height - lip);
            Stepped(g, f, faceR, Inner(C2));
            Fill(g, Mix(f, Color.White, 0.6f), new Rectangle(faceR.X + U, faceR.Y, faceR.Width - 2 * U, U));
            return new Rectangle(r.X, r.Y, r.Width, r.Height - lip);
        }

        // Re-measures every container above a control whose preferred height just changed.
        public static void Relayout(Control c)
        {
            var chain = new List<Control>();
            for (Control p = c.Parent; p != null; p = p.Parent) chain.Add(p);
            for (int i = chain.Count - 1; i >= 0; i--) chain[i].PerformLayout();
        }

        public static void StyleMenu(ToolStrip menu)
        {
            menu.Font = new Font("Segoe UI", 9f);
            menu.Renderer = new RetroMenuRenderer();
        }

        // Tooltips drawn with the pixel font.
        public static ToolTip PixelToolTip()
        {
            var tip = new ToolTip { OwnerDraw = true };
            tip.Popup += (s, e) =>
            {
                string text = tip.GetToolTip(e.AssociatedControl);
                var lines = PixelFont.Wrap(text, Text, Px(220));
                int w = 0;
                foreach (var l in lines) w = Math.Max(w, PixelFont.Width(l, Text));
                e.ToolTipSize = new Size(w + Px(10), PixelFont.BlockHeight(lines.Count, Text) + Px(10));
            };
            tip.Draw += (s, e) =>
            {
                Fill(e.Graphics, Card, e.Bounds);
                var b = e.Bounds;
                Fill(e.Graphics, Ink, new Rectangle(b.X, b.Y, b.Width, U));
                Fill(e.Graphics, Ink, new Rectangle(b.X, b.Bottom - U, b.Width, U));
                Fill(e.Graphics, Ink, new Rectangle(b.X, b.Y, U, b.Height));
                Fill(e.Graphics, Ink, new Rectangle(b.Right - U, b.Y, U, b.Height));
                PixelFont.DrawBlock(e.Graphics, e.ToolTipText, Text, new Rectangle(b.X + Px(5), b.Y + Px(5), b.Width - Px(10), b.Height), Ink);
            };
            return tip;
        }
    }

    internal sealed class RetroMenuColors : ProfessionalColorTable
    {
        public override Color MenuBorder { get { return Theme.Ink; } }
        public override Color MenuItemBorder { get { return Theme.Ink; } }
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Yellow; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Yellow; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.PaperDark; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.PaperDark; } }
        public override Color ImageMarginGradientEnd { get { return Theme.PaperDark; } }
        public override Color SeparatorDark { get { return Theme.InkSoft; } }
        public override Color SeparatorLight { get { return Theme.Card; } }
        public override Color CheckBackground { get { return Theme.Mint; } }
        public override Color CheckSelectedBackground { get { return Theme.Mint; } }
        public override Color CheckPressedBackground { get { return Theme.Mint; } }
    }

    internal sealed class RetroMenuRenderer : ToolStripProfessionalRenderer
    {
        public RetroMenuRenderer() : base(new RetroMenuColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var r = e.TextRectangle;
            r.Width += Theme.Px(40); // the pixel font is a little wider than the measuring font
            PixelFont.DrawIn(e.Graphics, e.Text, r, Theme.Text, e.Item.Enabled ? Theme.Ink : Theme.Disabled, false);
        }
    }

    // Lays its children out top to bottom at full width, each at its preferred height for that width.
    internal class VStack : Panel
    {
        public int Gap;
        public bool Checker;

        public VStack()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }

        // The outermost checkered stack; nested ones align their pattern to it.
        public Control CheckerRoot
        {
            get
            {
                Control root = this;
                while (root.Parent is VStack && ((VStack)root.Parent).Checker) root = root.Parent;
                return root;
            }
        }

        static int ChildHeight(Control c, int width)
        {
            return c.GetPreferredSize(new Size(Math.Max(1, width), 0)).Height;
        }

        public override Size GetPreferredSize(Size proposed)
        {
            int w = Theme.W(proposed, Width);
            if (Columns == 2) return new Size(w, ArrangeColumns(w, false));
            int inner = w - Padding.Horizontal;
            int h = Padding.Vertical, n = 0;
            foreach (Control c in Controls)
            {
                if (n++ > 0) h += Gap;
                h += c.Margin.Vertical + ChildHeight(c, inner - c.Margin.Horizontal);
            }
            return new Size(w, h);
        }

        // Two-column mode: cards fill whichever column is shorter; anything else spans both columns.
        public int Columns = 1;

        int ArrangeColumns(int width, bool apply)
        {
            int inner = width - Padding.Horizontal;
            int colW = (inner - Gap) / 2;
            int y0 = Padding.Top, left = y0, right = y0;
            bool any = false;
            foreach (Control c in Controls)
            {
                if (c is Card)
                {
                    bool useLeft = left <= right;
                    int y = useLeft ? left : right;
                    int h = ChildHeight(c, colW);
                    if (apply) c.SetBounds(Padding.Left + (useLeft ? 0 : colW + Gap), y, colW, h);
                    if (useLeft) left = y + h + Gap; else right = y + h + Gap;
                }
                else
                {
                    int y = Math.Max(left, right);
                    int w = Math.Max(1, inner - c.Margin.Horizontal);
                    int h = ChildHeight(c, w);
                    if (apply) c.SetBounds(Padding.Left + c.Margin.Left, y + c.Margin.Top, w, h);
                    left = right = y + c.Margin.Vertical + h + Gap;
                }
                any = true;
            }
            int bottom = Math.Max(left, right) - (any ? Gap : 0);
            return bottom + Padding.Bottom;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (Columns == 2) { ArrangeColumns(ClientSize.Width, true); return; }
            int inner = ClientSize.Width - Padding.Horizontal;
            int y = Padding.Top, n = 0;
            foreach (Control c in Controls)
            {
                if (n++ > 0) y += Gap;
                int w = Math.Max(1, inner - c.Margin.Horizontal);
                int h = ChildHeight(c, w);
                c.SetBounds(Padding.Left + c.Margin.Left, y + c.Margin.Top, w, h);
                y += c.Margin.Vertical + h;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (!Checker) { Theme.Fill(e.Graphics, BackColor, ClientRectangle); return; }
            var root = CheckerRoot;
            Point o = root == this ? Point.Empty : root.PointToClient(PointToScreen(Point.Empty));
            Theme.Checker(e.Graphics, ClientRectangle, o);
        }
    }

    // A scrolling page with the checkerboard background, holding a VStack.
    internal sealed class ScrollPage : Panel
    {
        public readonly VStack Stack;

        public ScrollPage()
        {
            AutoScroll = true;
            BackColor = Theme.Paper;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            Stack = new VStack
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Checker = true,
                Gap = Theme.Sp(9), Columns = Theme.TwoColumns ? 2 : 1,
                Padding = new Padding(Theme.Sp(Theme.PageMarginU), Theme.Sp(Theme.PageMarginU == 0 ? 0 : 4),
                                      Theme.Sp(Theme.PageMarginU), Theme.Sp(Theme.PageMarginU == 0 ? 0 : 12)),
            };
            Controls.Add(Stack);
        }
    }

    // A pixel window. Default: blue bezel with an LCD title strip and a recessed LCD body. Theme.CardStyle,
    // TitleAlign, BezelU, CardCorners, DoubleOutline and AccentBezel give the preview variants.
    internal sealed class Card : VStack
    {
        readonly string title;
        public Color Accent;
        const int ShadowU = 3, TitleU = 15;

        public Card(string title, Color accent)
        {
            this.title = title;
            Accent = accent;
            BackColor = Theme.Card;
            int side, top;
            Insets(out side, out top);
            Padding = new Padding(side, top, side + Theme.Px(ShadowU), side + Theme.Px(ShadowU));
            Gap = Theme.Sp(7);
        }

        static int TabH { get { return Theme.TitleAlign == 2 ? Theme.Px(TitleU) : 0; } }
        static int Rim { get { return Theme.Px(1 + Theme.BezelU); } }

        static void Insets(out int side, out int top)
        {
            int tabH = TabH;
            switch (Theme.CardStyle)
            {
                case 1:
                    side = Theme.Px(1) + Theme.Sp(8);
                    top = tabH > 0 ? tabH + Theme.Px(1) + Theme.Sp(7) : Theme.Px(1 + TitleU + 1) + Theme.Sp(7);
                    break;
                case 2:
                    side = Theme.Px(3) + Theme.Sp(6);
                    top = tabH > 0 ? tabH + Theme.Px(3) + Theme.Sp(6) : Theme.Px(3 + 13) + Theme.Sp(7);
                    break;
                default:
                    side = Rim + Theme.Sp(5);
                    top = (tabH > 0 ? tabH + Rim : Theme.Px(1 + TitleU + Theme.BezelU - 1)) + Theme.Sp(6);
                    break;
            }
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        void DrawLed(Graphics g, Rectangle plate)
        {
            int u = Theme.U, led = 6, ly = plate.Y + (plate.Height - Theme.Px(led)) / 2;
            ly -= ly % u;
            Theme.Frame(g, new Rectangle(plate.Right - Theme.Px(5 + led), ly, Theme.Px(led), Theme.Px(led)),
                Enabled ? Accent : Theme.DisabledFill, Theme.Ink, Theme.C1, 0);
        }

        void DrawTitle(Graphics g, Rectangle r, Color color, bool led)
        {
            bool centre = Theme.TitleAlign != 0;
            var text = centre ? new Rectangle(r.X + Theme.Px(4), r.Y, r.Width - Theme.Px(8), r.Height)
                              : new Rectangle(r.X + Theme.Px(5), r.Y, r.Width - Theme.Px(led ? 20 : 8), r.Height);
            PixelFont.DrawIn(g, title, text, Theme.Bold, color, centre);
            if (led && !centre) DrawLed(g, r);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            int u = Theme.U, sh = Theme.Px(ShadowU), tabH = TabH;
            bool en = Enabled;
            Theme.Backdrop(g, this);
            var box = new Rectangle(0, tabH, Width - sh, Height - sh - tabH);
            var cc = Theme.CardCorners;
            Color ink = en ? Theme.Ink : Theme.Disabled;
            Color titleFill = en ? Theme.TitleFill : Theme.DisabledFill, titleText = en ? Theme.TitleText : Theme.Disabled;
            Color tabFill;

            if (Theme.CardStyle == 1) // flat: outline, title text and a rule
            {
                Theme.Frame(g, box, Theme.Card, ink, cc, ShadowU);
                tabFill = Theme.Card;
                if (tabH == 0)
                {
                    DrawTitle(g, new Rectangle(u, u, box.Width - 2 * u, Theme.Px(TitleU)), ink, true);
                    Theme.Fill(g, ink, new Rectangle(u, Theme.Px(1 + TitleU), box.Width - 2 * u, u));
                }
            }
            else if (Theme.CardStyle == 2) // Win95: raised bevel and a solid title bar
            {
                Theme.Frame(g, box, Theme.Card, ink, cc, ShadowU);
                Theme.Bevel(g, box, cc, Color.White, Theme.Mix(Theme.Card, Theme.Ink, 0.35f));
                tabFill = titleFill;
                if (tabH == 0)
                {
                    var bar = new Rectangle(Theme.Px(3), Theme.Px(3), box.Width - Theme.Px(6), Theme.Px(13));
                    Theme.Fill(g, titleFill, bar);
                    DrawTitle(g, bar, titleText, false);
                }
            }
            else // bezel + LCD body
            {
                Color bez = Theme.AccentBezel ? Accent : Theme.Bezel;
                if (!en) bez = Theme.Mix(bez, Theme.DisabledFill, 0.6f);
                Color hi = Theme.AccentBezel || !en ? Theme.Mix(bez, Color.White, 0.4f) : Theme.BezelLight;
                Color lo = Theme.AccentBezel || !en ? Theme.Mix(bez, Theme.Ink, 0.3f) : Theme.BezelDark;
                Theme.Frame(g, box, bez, Theme.Ink, cc, ShadowU);
                Theme.Bevel(g, box, cc, hi, lo);
                if (Theme.DoubleOutline)
                {
                    var r2 = Rectangle.Inflate(box, -Theme.Px(2), -Theme.Px(2));
                    var c2 = Theme.Inner(Theme.Inner(cc));
                    Theme.Stepped(g, Theme.Ink, r2, c2);
                    Theme.Stepped(g, bez, Rectangle.Inflate(r2, -u, -u), Theme.Inner(c2));
                }
                tabFill = bez;
                if (tabH == 0)
                {
                    var plate = new Rectangle(Rim, Theme.Px(3), box.Width - 2 * Rim, Theme.Px(TitleU - 1));
                    Theme.Frame(g, plate, titleFill, lo, Theme.C1, 0);
                    DrawTitle(g, plate, titleText, true);
                }
                int by = tabH > 0 ? box.Y + Rim : Theme.Px(1 + TitleU + Theme.BezelU - 1);
                var body = new Rectangle(Rim, by, box.Width - 2 * Rim, box.Bottom - Rim - by);
                Theme.Frame(g, body, Theme.Card, lo, Theme.BodyCorners, 0);
                Theme.Bevel(g, body, Theme.BodyCorners, Theme.Mix(Theme.Card, Theme.TitleText, 0.15f), Theme.Mix(Theme.Card, Color.White, 0.35f));
            }

            if (tabH > 0) // title as a tab sticking out of the top edge
            {
                int tw = PixelFont.Width(title, Theme.Bold);
                int depth = Theme.CardStyle == 0 ? Rim : Theme.CardStyle == 2 ? Theme.Px(2) : u;
                var tab = new Rectangle(Theme.Px(6), 0, tw + Theme.Px(16), tabH + depth);
                Theme.Stepped(g, ink, new Rectangle(tab.X, 0, tab.Width, tabH + u), Theme.C2, true, false);
                Theme.Stepped(g, tabFill, new Rectangle(tab.X + u, u, tab.Width - 2 * u, tab.Height - u), Theme.Inner(Theme.C2), true, false);
                if (Theme.CardStyle == 0)
                {
                    var plate = new Rectangle(tab.X + Theme.Px(3), Theme.Px(3), tab.Width - Theme.Px(6), tabH - Theme.Px(3));
                    Theme.Frame(g, plate, titleFill, Theme.Mix(tabFill, Theme.Ink, 0.3f), Theme.C1, 0);
                    PixelFont.DrawIn(g, title, plate, Theme.Bold, titleText, true);
                }
                else
                    PixelFont.DrawIn(g, title, new Rectangle(tab.X, u, tab.Width, tabH), Theme.Bold, Theme.CardStyle == 2 ? titleText : ink, true);
            }
        }
    }

    // Lays children out left to right, vertically centred; RightControl is pinned to the right edge.
    internal sealed class HRow : Panel
    {
        public int Gap = Theme.Px(6);
        public Control RightControl;
        public bool TopRule;

        public HRow()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }

        public override Size GetPreferredSize(Size proposed)
        {
            int w = 0, h = 0, n = 0;
            foreach (Control c in Controls)
            {
                var s = c.GetPreferredSize(Size.Empty);
                if (c != RightControl) { if (n++ > 0) w += Gap; w += s.Width; }
                else w += Gap + s.Width;
                h = Math.Max(h, s.Height);
            }
            return new Size(Theme.W(proposed, w + Padding.Horizontal), h + Padding.Vertical + (TopRule ? Theme.U : 0));
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int top = Padding.Top + (TopRule ? Theme.U : 0);
            int ih = ClientSize.Height - top - Padding.Bottom;
            int x = Padding.Left;
            foreach (Control c in Controls)
            {
                var s = c.GetPreferredSize(Size.Empty);
                int y = top + (ih - s.Height) / 2;
                if (c == RightControl) c.SetBounds(ClientSize.Width - Padding.Right - s.Width, y, s.Width, s.Height);
                else { c.SetBounds(x, y, s.Width, s.Height); x += s.Width + Gap; }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Theme.Fill(e.Graphics, BackColor, ClientRectangle);
            if (TopRule) Theme.Fill(e.Graphics, Theme.Ink, new Rectangle(0, 0, Width, Theme.U));
        }
    }

    // Word-wrapped pixel text.
    internal sealed class RetroLabel : Control
    {
        readonly PFont font;

        public RetroLabel(string text, PFont font, Color color)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            this.font = font;
            Text = text;
            ForeColor = color;
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }

        public override Size GetPreferredSize(Size proposed)
        {
            int w = Theme.W(proposed, Width);
            return new Size(w, Math.Max(PixelFont.Height(font), PixelFont.BlockHeight(Text, font, w)));
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (Width > 0 && GetPreferredSize(new Size(Width, 0)).Height != Height) Theme.Relayout(this);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Backdrop(e.Graphics, this);
            PixelFont.DrawBlock(e.Graphics, Text, font, ClientRectangle, Enabled ? ForeColor : Theme.Disabled);
        }
    }

    // Shared drawing for the check box and radio button: pixel indicator, bold label and an optional muted hint.
    internal static class ToggleArt
    {
        const int Ind = 11, TextTopU = 3, BoxTopU = 1, SwW = 20, SwH = 11;
        public static int TextX { get { return Theme.Px((Theme.CheckSwitches ? SwW : Ind) + 7); } }

        static readonly string[] Check =
        {
            "......#",
            ".....##",
            "#...##.",
            "##.##..",
            ".###...",
            "..#....",
        };

        public static Size Measure(string text, string hint, int width)
        {
            int tw = Math.Max(Theme.Px(30), width - TextX);
            int h = Theme.Px(TextTopU) + PixelFont.BlockHeight(text, Theme.Bold, tw);
            if (!string.IsNullOrEmpty(hint)) h += Theme.Px(3) + PixelFont.BlockHeight(hint, Theme.Hint, tw);
            return new Size(width, Math.Max(h, Theme.Px(BoxTopU + Ind + 1)) + Theme.Px(2));
        }

        public static void Paint(Graphics g, ButtonBase c, bool radio, bool on, bool hover, string hint, Color accent, bool focus)
        {
            bool en = c.Enabled;
            int u = Theme.U;
            Color ink = en ? Theme.Ink : Theme.Disabled;
            Theme.Backdrop(g, c);
            int tw = Math.Max(Theme.Px(30), c.Width - TextX);
            var r = new Rectangle(0, Theme.Px(BoxTopU), Theme.Px(Ind), Theme.Px(Ind));
            Color fill = !en ? Theme.DisabledFill : on ? accent : hover ? Theme.Hover : Theme.Field;
            if (radio)
            {
                Theme.PixelCircle(g, Theme.Shadow, r.X + u, r.Y + u, Ind);
                Theme.PixelCircle(g, ink, r.X, r.Y, Ind);
                Theme.PixelCircle(g, fill, r.X + u, r.Y + u, Ind - 2);
                if (on) Theme.PixelCircle(g, ink, r.X + 3 * u, r.Y + 3 * u, Ind - 6);
            }
            else if (Theme.CheckSwitches) // small pill switch instead of a box
            {
                var sr = new Rectangle(0, Theme.Px(BoxTopU), Theme.Px(SwW), Theme.Px(SwH));
                var ins = Theme.RoundInsets(SwH / 2);
                var s = sr; s.Offset(u, u);
                Theme.Stepped(g, Theme.Shadow, s, ins);
                Theme.Stepped(g, ink, sr, ins);
                Theme.Stepped(g, fill, Rectangle.Inflate(sr, -u, -u), Theme.Inner(ins));
                int kd = SwH - 4, kx = on ? sr.Right - Theme.Px(kd + 2) : sr.X + Theme.Px(2);
                Theme.PixelCircle(g, ink, kx, sr.Y + Theme.Px(2), kd);
                Theme.PixelCircle(g, en ? Theme.Field : Theme.DisabledFill, kx + u, sr.Y + Theme.Px(3), kd - 2);
            }
            else
            {
                Theme.Frame(g, r, fill, ink, Theme.C1, 1);
                if (on) Theme.Pattern(g, Check, r.X + 2 * u, r.Y + 3 * u, '#', ink);
            }
            int ty = Theme.Px(TextTopU);
            int th = PixelFont.BlockHeight(c.Text, Theme.Bold, tw);
            PixelFont.DrawBlock(g, c.Text, Theme.Bold, new Rectangle(TextX, ty, tw, th), ink);
            if (!string.IsNullOrEmpty(hint))
                PixelFont.DrawBlock(g, hint, Theme.Hint, new Rectangle(TextX, ty + th + Theme.Px(3), tw, c.Height), en ? Theme.HintInk : Theme.Disabled);
            if (focus)
                Theme.FocusDots(g, new Rectangle(TextX - Theme.Px(3), ty - Theme.Px(3),
                    Math.Min(tw, PixelFont.Width(c.Text, Theme.Bold)) + Theme.Px(6), th + Theme.Px(5)));
        }
    }

    internal class RetroCheck : CheckBox
    {
        bool hover;
        readonly string hint;
        public Color Accent = Theme.Mint;

        public RetroCheck(string text, string hint)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Text = text;
            this.hint = Theme.TakeHint(this, hint);
            AccessibleDescription = hint;
            Cursor = Cursors.Hand;
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }
        public override Size GetPreferredSize(Size proposed) { return ToggleArt.Measure(Text, hint, Theme.W(proposed, Width)); }
        protected override void OnPaint(PaintEventArgs e) { ToggleArt.Paint(e.Graphics, this, false, Checked, hover, hint, Accent, Focused && ShowFocusCues); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    internal class RetroRadio : RadioButton
    {
        bool hover;
        readonly string hint;
        public Color Accent = Theme.Yellow;

        public RetroRadio(string text, string hint)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Text = text;
            this.hint = Theme.TakeHint(this, hint);
            AccessibleDescription = hint;
            Cursor = Cursors.Hand;
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }
        public override Size GetPreferredSize(Size proposed) { return ToggleArt.Measure(Text, hint, Theme.W(proposed, Width)); }
        protected override void OnPaint(PaintEventArgs e) { ToggleArt.Paint(e.Graphics, this, true, Checked, hover, hint, Accent, Focused && ShowFocusCues); }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // ON/OFF pixel pill switch.
    internal sealed class RetroSwitch : CheckBox
    {
        bool hover;
        public Color PlateColor = Theme.Card;
        public Color OnColor = Color.FromArgb(76, 196, 96);   // green
        public Color OffColor = Color.FromArgb(232, 76, 76);  // red
        const int WU = 38, HU = 14;

        public RetroSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Text = "";
            Cursor = Cursors.Hand;
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }
        public override Size GetPreferredSize(Size proposed) { return Theme.Sz(WU + 1, HU + 1); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int u = Theme.U;
            Theme.Fill(g, PlateColor, ClientRectangle);
            bool en = Enabled, on = Checked;
            Color ink = en ? Theme.Ink : Theme.Disabled;
            Color fill = !en ? Theme.DisabledFill : on ? OnColor : OffColor;
            if (hover && en) fill = Theme.Mix(fill, Color.White, 0.3f);
            var r = new Rectangle(0, 0, Theme.Px(WU), Theme.Px(HU));
            var ins = Theme.RoundInsets(HU / 2);
            var s = r; s.Offset(u, u);
            Theme.Stepped(g, Theme.Shadow, s, ins);
            Theme.Stepped(g, ink, r, ins);
            Theme.Stepped(g, fill, Rectangle.Inflate(r, -u, -u), Theme.Inner(ins));

            const int kd = HU - 4;
            int kx = on ? r.Right - Theme.Px(kd + 2) : r.X + Theme.Px(2), ky = r.Y + Theme.Px(2);
            Theme.PixelCircle(g, ink, kx, ky, kd);
            Theme.PixelCircle(g, en ? Theme.Field : Theme.DisabledFill, kx + u, ky + u, kd - 2);
            var tr = on ? new Rectangle(r.X + u, r.Y, kx - r.X - u, r.Height)
                        : new Rectangle(kx + Theme.Px(kd), r.Y, r.Right - u - kx - Theme.Px(kd), r.Height);
            PixelFont.DrawIn(g, on ? "ON" : "OFF", tr, Theme.Bold, ink, true);
            if (Focused && ShowFocusCues) Theme.FocusDots(g, Rectangle.Inflate(r, -Theme.Px(2), -Theme.Px(2)));
        }

        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // Keycap push button that sinks when pressed.
    internal sealed class RetroButton : Button
    {
        bool hover, down;
        public Color Accent;
        bool shield;

        static readonly string[] ShieldArt =
        {
            "KKKKKKK",
            "KbbbyyK",
            "KbbbyyK",
            "KbbbyyK",
            "KyyybbK",
            ".KyybK.",
            "..KbK..",
            "...K...",
        };

        public RetroButton(string text, Color accent)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            Accent = accent;
            Cursor = Cursors.Hand;
            AutoSize = false;
        }

        public bool ShowShield { get { return shield; } set { shield = value; if (Parent != null) Parent.PerformLayout(); Invalidate(); } }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }

        public override Size GetPreferredSize(Size proposed)
        {
            int k = Theme.KeyScale;
            int w = PixelFont.Width(Text, Theme.Bold) + Theme.Px(16 * k) + (shield ? Theme.Px(11) : 0);
            return new Size(w, Theme.Px(PixelFont.Rows + 11 + 8 * (k - 1) - (Theme.FlatKeys ? 2 : 0)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Backdrop(g, this);
            bool en = Enabled;
            var face = Theme.Keycap(g, ClientRectangle, Accent, down && en, en, hover);
            Color ink = en ? Theme.Ink : Theme.Disabled;
            int tw = PixelFont.Width(Text, Theme.Bold);
            int total = tw + (shield ? Theme.Px(11) : 0);
            int x = face.X + (face.Width - total) / 2;
            if (shield)
            {
                int sy = face.Y + (face.Height - Theme.Px(8)) / 2;
                Theme.Pattern(g, ShieldArt, x, sy, 'K', ink);
                Theme.Pattern(g, ShieldArt, x, sy, 'b', en ? Color.FromArgb(90, 150, 220) : Theme.Disabled);
                Theme.Pattern(g, ShieldArt, x, sy, 'y', en ? Color.FromArgb(240, 200, 80) : Theme.DisabledFill);
                x += Theme.Px(11);
            }
            PixelFont.DrawIn(g, Text, new Rectangle(x, face.Y, tw + Theme.U, face.Height), Theme.Bold, ink, false);
            if (Focused && ShowFocusCues) Theme.FocusDots(g, Rectangle.Inflate(face, -Theme.Px(3), -Theme.Px(3)));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // Labelled slider with a value tag; drag, click, or use the arrow keys / Page Up / Page Down / Home / End.
    internal sealed class RetroSlider : Control
    {
        readonly double min, max, step;
        double val;
        readonly string label, hint;
        public string[] TickLabels;
        public Func<double, string> Format;
        public Color Accent = Theme.Pink;
        public event EventHandler ValueChanged;
        bool dragging, hover;
        const int KnobWU = 8, KnobHU = 14, TrackHU = 6;

        public RetroSlider(string label, string hint, double min, double max, double step, double value)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            this.label = label;
            this.hint = Theme.TakeHint(this, hint);
            this.min = min;
            this.max = max;
            this.step = step;
            val = Snap(value);
            AccessibleRole = AccessibleRole.Slider;
            AccessibleName = label;
            AccessibleDescription = hint;
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }

        public double Value
        {
            get { return val; }
            set
            {
                double v = Snap(value);
                if (v == val) return;
                val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        double Snap(double v)
        {
            v = Math.Max(min, Math.Min(max, v));
            if (step > 0) v = min + Math.Round((v - min) / step) * step;
            return Math.Max(min, Math.Min(max, Math.Round(v, 6)));
        }

        int HeaderH { get { return Theme.TagHeight + Theme.U; } }
        int HintH(int w) { return string.IsNullOrEmpty(hint) ? 0 : Theme.Px(2) + PixelFont.BlockHeight(hint, Theme.Hint, w); }
        int TickH { get { return TickLabels == null ? 0 : Theme.Px(3) + PixelFont.Height(Theme.Text); } }
        int TrackCenter { get { return HeaderH + HintH(Width) + Theme.Px(5 + KnobHU / 2); } }
        int TrackLeft { get { return Theme.Px(KnobWU / 2 + 1); } }
        int TrackRight { get { return Width - Theme.Px(KnobWU / 2 + 1); } }
        double Fraction { get { return max > min ? (val - min) / (max - min) : 0; } }
        int KnobX
        {
            get
            {
                int x = TrackLeft + (int)Math.Round((TrackRight - TrackLeft) * Fraction);
                return x - x % Theme.U; // keep the knob on the pixel grid
            }
        }

        public override Size GetPreferredSize(Size proposed)
        {
            int w = Theme.W(proposed, Width);
            return new Size(w, HeaderH + HintH(w) + Theme.Px(5 + KnobHU) + TickH + Theme.Px(2));
        }

        string ValueText { get { return Format != null ? Format(val) : val.ToString("0.##"); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            bool en = Enabled;
            int u = Theme.U;
            Color ink = en ? Theme.Ink : Theme.Disabled;
            Theme.Backdrop(g, this);

            string text = ValueText;
            int tagW = PixelFont.Width(text, Theme.Text) + Theme.Px(8);
            int tagX = Width - u - tagW;
            if (Theme.LcdReadouts) // a little LCD screen instead of a yellow tag
            {
                var lr = new Rectangle(tagX, 0, tagW, Theme.TagHeight);
                Theme.Frame(g, lr, en ? Theme.Lcd : Theme.DisabledFill, ink, Theme.C1, 0);
                Theme.Dither(g, Rectangle.Inflate(lr, -2 * u, -2 * u), Theme.Mix(Theme.Lcd, Theme.LcdDim, 0.5f), 1);
                PixelFont.DrawIn(g, text, lr, Theme.Text, en ? Theme.LcdText : Theme.Disabled, true);
            }
            else Theme.Tag(g, tagX, 0, text, Theme.Text, Theme.Yellow, en);
            PixelFont.DrawIn(g, label, new Rectangle(0, 0, tagX - Theme.Px(6), Theme.TagHeight), Theme.Bold, ink, false);
            if (!string.IsNullOrEmpty(hint))
                PixelFont.DrawBlock(g, hint, Theme.Hint, new Rectangle(0, HeaderH + Theme.Px(2), Width, Height), en ? Theme.HintInk : Theme.Disabled);

            int cy = TrackCenter;
            var track = new Rectangle(TrackLeft - Theme.Px(3), cy - Theme.Px(TrackHU / 2), TrackRight - TrackLeft + Theme.Px(6), Theme.Px(TrackHU));
            Theme.Frame(g, track, en ? Theme.Field : Theme.DisabledFill, ink, Theme.C1, 0);
            int kx = KnobX;
            if (Theme.SegmentedTrack) // separate cells, lit up to the knob
            {
                var lit = new List<Rectangle>();
                var dark = new List<Rectangle>();
                int cell = Theme.Px(4);
                for (int x = track.X + 2 * u; x + cell <= track.Right - 2 * u; x += cell + u)
                    (x + cell / 2 < kx ? lit : dark).Add(new Rectangle(x, track.Y + 2 * u, cell, track.Height - 4 * u));
                if (lit.Count > 0) using (var b = new SolidBrush(en ? Accent : Theme.Disabled)) g.FillRectangles(b, lit.ToArray());
                if (dark.Count > 0) using (var b = new SolidBrush(Theme.Mix(en ? Theme.Field : Theme.DisabledFill, Theme.Ink, 0.12f))) g.FillRectangles(b, dark.ToArray());
            }
            else
            {
                Theme.Fill(g, en ? Accent : Theme.Disabled, new Rectangle(track.X + u, track.Y + u, kx - track.X - u, track.Height - 2 * u));
                Theme.Fill(g, Theme.Mix(en ? Accent : Theme.Disabled, Theme.Ink, 0.2f), new Rectangle(track.X + u, track.Bottom - 2 * u, kx - track.X - u, u));
            }

            if (TickLabels != null && TickLabels.Length > 1)
            {
                int n = TickLabels.Length, ty = cy + Theme.Px(KnobHU / 2 + 3);
                for (int i = 0; i < n; i++)
                {
                    int x = TrackLeft + (TrackRight - TrackLeft) * i / (n - 1);
                    x -= x % u;
                    Theme.Fill(g, ink, new Rectangle(x, track.Bottom + u, u, Theme.Px(2)));
                    int w = PixelFont.Width(TickLabels[i], Theme.Text);
                    int lx = i == 0 ? 0 : i == n - 1 ? Width - u - w : x - w / 2;
                    PixelFont.Draw(g, TickLabels[i], lx, ty, Theme.Text, en ? Theme.InkSoft : Theme.Disabled);
                }
            }

            var knob = new Rectangle(kx - Theme.Px(KnobWU / 2), cy - Theme.Px(KnobHU / 2), Theme.Px(KnobWU), Theme.Px(KnobHU));
            Theme.Keycap(g, knob, Theme.Field, false, en, dragging || hover);
            if (Focused && ShowFocusCues) Theme.FocusDots(g, Rectangle.Inflate(knob, Theme.Px(2), Theme.Px(2)));
        }

        bool InTrackBand(int y) { return Math.Abs(y - TrackCenter) <= Theme.Px(KnobHU / 2 + 2); }

        void SetFromX(int x)
        {
            double t = (double)(x - TrackLeft) / Math.Max(1, TrackRight - TrackLeft);
            Value = min + t * (max - min);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Focused) Focus();
            if (e.Button != MouseButtons.Left || !InTrackBand(e.Y)) return;
            dragging = true;
            Capture = true;
            SetFromX(e.X);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = InTrackBand(e.Y);
            if (h != hover) { hover = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(); }
            if (dragging) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); dragging = false; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            double big = Math.Max(step, (max - min) / 10);
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Value = val - step; break;
                case Keys.Right: case Keys.Up: Value = val + step; break;
                case Keys.PageDown: Value = val - big; break;
                case Keys.PageUp: Value = val + big; break;
                case Keys.Home: Value = min; break;
                case Keys.End: Value = max; break;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // Single-line text box that draws with the pixel font. Supports typing, selection, the clipboard,
    // Home/End and arrow keys; Enter raises Commit.
    internal sealed class PixelTextBox : Control
    {
        int caret, anchor, scroll;
        bool caretOn, selecting;
        readonly Timer blink = new Timer { Interval = 530 };
        public event EventHandler Commit;

        public PixelTextBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.IBeam;
            blink.Tick += (s, e) => { caretOn = !caretOn; Invalidate(); };
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }
        public override Size GetPreferredSize(Size proposed) { return new Size(Theme.W(proposed, Width), Theme.Px(PixelFont.Rows + 10) + Theme.U); }

        int SelStart { get { return Math.Min(caret, anchor); } }
        int SelEnd { get { return Math.Max(caret, anchor); } }
        Rectangle Box { get { return new Rectangle(0, 0, Width - Theme.U, Height - Theme.U); } }
        Rectangle TextArea { get { var b = Box; return new Rectangle(b.X + Theme.Px(5), b.Y, b.Width - Theme.Px(10), b.Height); } }

        int CaretX(int i) { return i <= 0 ? 0 : PixelFont.Width(Text.Substring(0, Math.Min(i, Text.Length)), Theme.Text) + Theme.U; }

        int HitIndex(int x)
        {
            int rel = x - TextArea.X + scroll, best = 0, bestD = int.MaxValue;
            for (int i = 0; i <= Text.Length; i++)
            {
                int d = Math.Abs(CaretX(i) - rel);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void Touch()
        {
            caretOn = true;
            if (Focused) { blink.Stop(); blink.Start(); }
            int cx = CaretX(caret), w = TextArea.Width - Theme.U;
            if (cx - scroll > w) scroll = cx - w;
            if (cx - scroll < 0) scroll = cx;
            scroll = Math.Max(0, Math.Min(scroll, Math.Max(0, CaretX(Text.Length) - w)));
            Invalidate();
        }

        void Replace(string s)
        {
            string t = Text;
            int a = SelStart, b = SelEnd;
            Text = t.Substring(0, a) + s + t.Substring(b);
            caret = anchor = a + s.Length;
            Touch();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            caret = Math.Min(caret, Text.Length);
            anchor = Math.Min(anchor, Text.Length);
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Home: case Keys.End: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Enabled) return;
            bool shift = e.Shift;
            switch (e.KeyCode)
            {
                case Keys.Left: caret = Math.Max(0, caret - 1); break;
                case Keys.Right: caret = Math.Min(Text.Length, caret + 1); break;
                case Keys.Home: caret = 0; break;
                case Keys.End: caret = Text.Length; break;
                case Keys.Delete:
                    if (SelStart == SelEnd && caret < Text.Length) anchor = caret + 1;
                    Replace("");
                    e.Handled = true;
                    return;
                case Keys.Enter:
                    e.SuppressKeyPress = true;
                    if (Commit != null) Commit(this, EventArgs.Empty);
                    return;
                case Keys.A:
                    if (!e.Control) { base.OnKeyDown(e); return; }
                    anchor = 0; caret = Text.Length; Touch(); e.SuppressKeyPress = true;
                    return;
                case Keys.C:
                case Keys.X:
                    if (!e.Control) { base.OnKeyDown(e); return; }
                    if (SelEnd > SelStart)
                    {
                        try { Clipboard.SetText(Text.Substring(SelStart, SelEnd - SelStart)); } catch (Exception) { }
                        if (e.KeyCode == Keys.X) Replace("");
                    }
                    e.SuppressKeyPress = true;
                    return;
                case Keys.V:
                    if (!e.Control) { base.OnKeyDown(e); return; }
                    try
                    {
                        string s = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(s)) Replace(s.Replace("\r", " ").Replace("\n", " "));
                    }
                    catch (Exception) { }
                    e.SuppressKeyPress = true;
                    return;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            if (!shift) anchor = caret;
            Touch();
            e.Handled = true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (!Enabled) return;
            if (e.KeyChar == '\b')
            {
                if (SelStart == SelEnd && caret > 0) anchor = caret - 1;
                Replace("");
                e.Handled = true;
            }
            else if (e.KeyChar >= ' ' && e.KeyChar != (char)127)
            {
                Replace(e.KeyChar.ToString());
                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled) return;
            Focus();
            if (e.Button != MouseButtons.Left) return;
            caret = HitIndex(e.X);
            if ((ModifierKeys & Keys.Shift) == 0) anchor = caret;
            selecting = true;
            Capture = true;
            Touch();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!selecting) return;
            caret = HitIndex(e.X);
            Touch();
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); selecting = false; Capture = false; }
        protected override void OnDoubleClick(EventArgs e) { base.OnDoubleClick(e); anchor = 0; caret = Text.Length; Touch(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); blink.Start(); Touch(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); blink.Stop(); anchor = caret; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            bool en = Enabled;
            int u = Theme.U;
            Theme.Backdrop(g, this);
            var b = Box;
            Theme.Frame(g, b, en ? Theme.Field : Theme.DisabledFill, en ? Theme.Ink : Theme.Disabled, Theme.C1, 1);
            if (en) // sunken inner edge
            {
                Color shade = Theme.Mix(Theme.Field, Theme.Ink, 0.15f);
                Theme.Fill(g, shade, new Rectangle(b.X + u, b.Y + u, b.Width - 2 * u, u));
                Theme.Fill(g, shade, new Rectangle(b.X + u, b.Y + u, u, b.Height - 2 * u));
            }
            if (Focused) Theme.Fill(g, Theme.Link, new Rectangle(b.X + u, b.Bottom - 2 * u, b.Width - 2 * u, u));

            var area = TextArea;
            var state = g.Save();
            g.SetClip(area);
            int ty = area.Y + (area.Height - PixelFont.CapHeight(Theme.Text)) / 2;
            if (Focused && SelEnd > SelStart)
            {
                int x0 = area.X + CaretX(SelStart) - scroll, x1 = area.X + CaretX(SelEnd) - scroll;
                Theme.Fill(g, Theme.Selection, new Rectangle(x0, ty - 2 * u, x1 - x0, PixelFont.Height(Theme.Text) + 3 * u));
            }
            PixelFont.Draw(g, Text, area.X - scroll, ty, Theme.Text, en ? Theme.Ink : Theme.Disabled);
            if (Focused && caretOn)
                Theme.Fill(g, Theme.Ink, new Rectangle(area.X + CaretX(caret) - scroll - (caret > 0 ? u : 0), ty - 2 * u, u, PixelFont.Height(Theme.Text) + 2 * u));
            g.Restore(state);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) blink.Dispose();
            base.Dispose(disposing);
        }
    }

    // Label, hint and a pixel text box.
    internal sealed class RetroTextField : Control
    {
        public readonly PixelTextBox Box;
        readonly string label, hint;

        public RetroTextField(string label, string hint, string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.label = label;
            this.hint = Theme.TakeHint(this, hint);
            Box = new PixelTextBox { Text = text, AccessibleName = label, AccessibleDescription = hint };
            Controls.Add(Box);
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }
        int LabelH { get { return PixelFont.Height(Theme.Bold); } }
        int HintH(int w) { return string.IsNullOrEmpty(hint) ? 0 : Theme.Px(2) + PixelFont.BlockHeight(hint, Theme.Hint, w); }
        int BoxTop(int w) { return LabelH + HintH(w) + Theme.Px(4); }

        public override Size GetPreferredSize(Size proposed)
        {
            int w = Theme.W(proposed, Width);
            return new Size(w, BoxTop(w) + Box.GetPreferredSize(new Size(w, 0)).Height);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            Box.SetBounds(0, BoxTop(Width), Width, Box.GetPreferredSize(new Size(Width, 0)).Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            bool en = Enabled;
            Theme.Backdrop(g, this);
            PixelFont.Draw(g, label, 0, 0, Theme.Bold, en ? Theme.Ink : Theme.Disabled);
            if (!string.IsNullOrEmpty(hint))
                PixelFont.DrawBlock(g, hint, Theme.Hint, new Rectangle(0, LabelH + Theme.Px(2), Width, Height), en ? Theme.HintInk : Theme.Disabled);
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }

    // LCD-style read-only log with a pixel scrollbar; follows new lines while scrolled to the bottom.
    internal sealed class LogView : Control
    {
        readonly List<string> lines = new List<string>();
        readonly List<string> wrapped = new List<string>();
        int wrapWidth = -1, top;
        bool dragging;
        int dragOffset;
        public int BoxHeight = 130;
        const int PadU = 5, BarU = 5;

        public LogView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }
        public override Size GetPreferredSize(Size proposed) { return new Size(Theme.W(proposed, Width), Theme.Px(BoxHeight)); }

        public string AllText { get { return string.Join(Environment.NewLine, lines.ToArray()); } }
        public bool IsEmpty { get { return lines.Count == 0; } }

        int Sh { get { return Theme.Px(2); } }
        Rectangle Box { get { return new Rectangle(0, 0, Width - Sh, Height - Sh); } }
        Rectangle TextArea
        {
            get
            {
                var b = Box;
                int p = Theme.Px(PadU);
                return new Rectangle(b.X + p, b.Y + p, b.Width - 2 * p - Theme.Px(BarU + 3), b.Height - 2 * p);
            }
        }
        Rectangle BarArea
        {
            get
            {
                var b = Box;
                int p = Theme.Px(PadU);
                return new Rectangle(b.Right - p - Theme.Px(BarU), b.Y + p, Theme.Px(BarU), b.Height - 2 * p);
            }
        }
        int VisibleLines { get { return Math.Max(1, (TextArea.Height + Theme.Px(PixelFont.Advance - PixelFont.Rows)) / PixelFont.LineHeight(Theme.Text)); } }
        int MaxTop { get { return Math.Max(0, wrapped.Count - VisibleLines); } }

        void EnsureWrapped()
        {
            int w = TextArea.Width;
            if (w == wrapWidth) return;
            wrapWidth = w;
            wrapped.Clear();
            foreach (var l in lines) wrapped.AddRange(Wrap(l));
            top = Math.Min(top, MaxTop);
        }

        List<string> Wrap(string l)
        {
            var r = PixelFont.Wrap(l, Theme.Text, Math.Max(Theme.Px(40), wrapWidth));
            if (r.Count == 0) r.Add("");
            return r;
        }

        public void Append(string line)
        {
            EnsureWrapped();
            bool follow = top >= MaxTop;
            lines.Add(line);
            if (lines.Count > 1500)
            {
                lines.RemoveRange(0, 300);
                wrapWidth = -1;
                EnsureWrapped();
            }
            else wrapped.AddRange(Wrap(line));
            if (follow) top = MaxTop;
            Invalidate();
        }

        public void Clear()
        {
            lines.Clear();
            wrapped.Clear();
            top = 0;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            bool follow = top >= MaxTop;
            base.OnResize(e);
            EnsureWrapped();
            if (follow) top = MaxTop;
        }

        Rectangle Thumb
        {
            get
            {
                var bar = BarArea;
                int total = Math.Max(1, wrapped.Count);
                int h = Math.Max(Theme.Px(8), bar.Height * Math.Min(VisibleLines, total) / total);
                int y = MaxTop == 0 ? bar.Y : bar.Y + (bar.Height - h) * top / MaxTop;
                return new Rectangle(bar.X, y, bar.Width, h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            EnsureWrapped();
            Theme.Backdrop(g, this);
            Theme.Frame(g, Box, Theme.Lcd, Theme.Ink, Theme.C3, 2);
            Theme.Dither(g, Rectangle.Inflate(Box, -Theme.Px(3), -Theme.Px(3)), Theme.Mix(Theme.Lcd, Theme.LcdDim, 0.35f), 1);
            var area = TextArea;
            int lh = PixelFont.LineHeight(Theme.Text);
            for (int i = 0; i < VisibleLines && top + i < wrapped.Count; i++)
                PixelFont.Draw(g, wrapped[top + i], area.X, area.Y + i * lh, Theme.Text, Theme.LcdText);
            var bar = BarArea;
            Theme.Fill(g, Theme.LcdDim, bar);
            if (MaxTop > 0) Theme.Stepped(g, Theme.LcdText, Thumb, Theme.C1);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (MaxTop > 0)
            {
                top = Math.Max(0, Math.Min(MaxTop, top - Math.Sign(e.Delta) * 3));
                Invalidate();
                var h = e as HandledMouseEventArgs;
                if (h != null) h.Handled = true;
            }
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || MaxTop == 0 || !BarArea.Contains(e.Location)) return;
            var t = Thumb;
            if (t.Contains(e.Location)) { dragging = true; dragOffset = e.Y - t.Y; Capture = true; }
            else { top = Math.Max(0, Math.Min(MaxTop, top + (e.Y < t.Y ? -VisibleLines : VisibleLines))); Invalidate(); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            var bar = BarArea;
            int range = bar.Height - Thumb.Height;
            if (range <= 0) return;
            top = Math.Max(0, Math.Min(MaxTop, (int)Math.Round((double)(e.Y - dragOffset - bar.Y) * MaxTop / range)));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; }
    }

    // A tinted callout box with a round badge, text and an optional clickable action link.
    internal sealed class RetroNote : Control
    {
        Color fill;
        string badge, actionText;
        Rectangle actionRect;
        bool actionHover;
        public event EventHandler ActionClicked;
        const int ShU = 2, PadU = 7, BadgeU = 13;

        public RetroNote(string text, Color fill, string badge)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Text = text;
            this.fill = fill;
            this.badge = badge;
        }

        public void SetLook(Color fill, string badge) { this.fill = fill; this.badge = badge; Invalidate(); }
        public string ActionText { get { return actionText; } set { actionText = value; Theme.Relayout(this); Invalidate(); } }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }
        int TextX { get { return Theme.Px(PadU + BadgeU + 6); } }
        int TextW(int w) { return Math.Max(Theme.Px(30), w - Theme.Px(ShU + PadU) - TextX); }
        int ActionH { get { return string.IsNullOrEmpty(actionText) ? 0 : Theme.Px(5) + PixelFont.Height(Theme.Bold); } }

        public override Size GetPreferredSize(Size proposed)
        {
            int w = Theme.W(proposed, Width);
            int h = PixelFont.BlockHeight(Text, Theme.Text, TextW(w)) + ActionH;
            return new Size(w, Math.Max(h, Theme.Px(BadgeU)) + Theme.Px(2 * PadU + ShU));
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (Width > 0 && GetPreferredSize(new Size(Width, 0)).Height != Height) Theme.Relayout(this);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int u = Theme.U, pad = Theme.Px(PadU);
            Theme.Backdrop(g, this);
            var box = new Rectangle(0, 0, Width - Theme.Px(ShU), Height - Theme.Px(ShU));
            Theme.Frame(g, box, fill, Theme.Ink, Theme.CardCorners, ShU);
            Theme.Bevel(g, box, Theme.CardCorners, Theme.Mix(fill, Color.White, 0.5f), Theme.Mix(fill, Theme.Ink, 0.12f));
            Theme.PixelCircle(g, Theme.Ink, pad, pad, BadgeU);
            Theme.PixelCircle(g, Theme.Field, pad + u, pad + u, BadgeU - 2);
            PixelFont.DrawIn(g, badge, new Rectangle(pad + u, pad, Theme.Px(BadgeU), Theme.Px(BadgeU)), Theme.Bold, Theme.Ink, true);

            int tw = TextW(Width), th = PixelFont.BlockHeight(Text, Theme.Text, tw);
            PixelFont.DrawBlock(g, Text, Theme.Text, new Rectangle(TextX, pad + Theme.Px(2), tw, th), Theme.Ink);
            if (!string.IsNullOrEmpty(actionText))
            {
                int aw = PixelFont.Width(actionText, Theme.Bold);
                actionRect = new Rectangle(TextX, pad + Theme.Px(2) + th + Theme.Px(5), aw, PixelFont.Height(Theme.Bold));
                Color c = actionHover ? Theme.Link : Theme.Ink;
                PixelFont.Draw(g, actionText, actionRect.X, actionRect.Y, Theme.Bold, c);
                Theme.Fill(g, c, new Rectangle(actionRect.X, actionRect.Y + Theme.Px(PixelFont.Cap + 1), aw, u));
            }
            else actionRect = Rectangle.Empty;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = actionRect.Contains(e.Location);
            if (h == actionHover) return;
            actionHover = h;
            Cursor = h ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); actionHover = false; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (actionRect.Contains(e.Location) && ActionClicked != null) ActionClicked(this, EventArgs.Empty);
        }
    }

    // A small status tag; collapses to nothing when its text is empty.
    internal sealed class Pill : Control
    {
        Color fill = Theme.Pink;

        public Pill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public Color Fill { get { return fill; } set { fill = value; Invalidate(); } }
        protected override Padding DefaultMargin { get { return Padding.Empty; } }

        public override Size GetPreferredSize(Size proposed)
        {
            int w = string.IsNullOrEmpty(Text) ? 0 : PixelFont.Width(Text, Theme.Bold) + Theme.Px(9);
            return new Size(w, Theme.TagHeight + Theme.U);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (Parent != null) Parent.PerformLayout();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Backdrop(e.Graphics, this);
            if (!string.IsNullOrEmpty(Text)) Theme.Tag(e.Graphics, 0, 0, Text, Theme.Bold, fill, true);
        }
    }

    // Row of keycap tabs; the selected one is pressed down and tinted.
    internal sealed class RetroTabs : Control
    {
        readonly string[] items;
        readonly Color[] accents;
        int selected, hover = -1;
        public event EventHandler SelectedIndexChanged;

        public RetroTabs(string[] items, Color[] accents)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            this.items = items;
            this.accents = accents;
            BackColor = Theme.Bezel;
            Height = Theme.Px(32);
            if (Theme.TabsAt == 1)
            {
                int w = 0;
                for (int i = 0; i < items.Length; i++) w = Math.Max(w, TabWidth(i));
                Width = w + Theme.Px(18);
            }
            AccessibleRole = AccessibleRole.PageTabList;
        }

        // Optional control pinned to the right end of a horizontal tab bar (used when the header is hidden).
        Control trailing;
        public Control Trailing
        {
            get { return trailing; }
            set { trailing = value; Controls.Add(value); PerformLayout(); }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (trailing == null) return;
            var r = TabRect(0);
            int y = r.Y + (r.Height - Theme.Px(2) - trailing.Height) / 2;
            trailing.Location = new Point(Width - Theme.Px(10) - trailing.Width, y - y % Theme.U);
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value == selected || value < 0 || value >= items.Length) return;
                selected = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public int Count { get { return items.Length; } }

        Rectangle TabRect(int i)
        {
            if (Theme.TabsAt == 1)
                return new Rectangle(Theme.Px(8), Theme.Px(8) + i * Theme.Px(PixelFont.Rows + 11 + 5), Width - Theme.Px(18), Theme.Px(PixelFont.Rows + 11));
            int x = Theme.Px(10);
            for (int k = 0; k < i; k++) x += TabWidth(k) + Theme.Px(5);
            return new Rectangle(x, Theme.Px(7), TabWidth(i), Theme.Px(PixelFont.Rows + 11));
        }

        int TabWidth(int i) { return PixelFont.Width(items[i], Theme.Bold) + Theme.Px(16); }

        int HitTest(Point p)
        {
            for (int i = 0; i < items.Length; i++) if (TabRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Fill(g, BackColor, ClientRectangle);
            int u = Theme.U;
            if (Theme.TabsAt == 1) Theme.Fill(g, Theme.Ink, new Rectangle(Width - u, 0, u, Height));
            else if (Theme.TabsAt == 2) Theme.Fill(g, Theme.Ink, new Rectangle(0, 0, Width, u));
            else Theme.Fill(g, Theme.Ink, new Rectangle(0, Height - u, Width, u));
            for (int i = 0; i < items.Length; i++)
            {
                bool sel = i == selected;
                var face = Theme.Keycap(g, TabRect(i), sel ? accents[i] : Theme.Field, sel, true, hover == i);
                PixelFont.DrawIn(g, items[i], face, Theme.Bold, Theme.Ink, true);
                if (sel && Focused && ShowFocusCues) Theme.FocusDots(g, Rectangle.Inflate(face, -Theme.Px(3), -Theme.Px(3)));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h == hover) return;
            hover = h;
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int h = HitTest(e.Location);
            if (e.Button == MouseButtons.Left && h >= 0) SelectedIndex = h;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var k = keyData & Keys.KeyCode;
            return k == Keys.Left || k == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) { SelectedIndex = (selected + items.Length - 1) % items.Length; e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { SelectedIndex = (selected + 1) % items.Length; e.Handled = true; }
            else base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // Title banner: dithered band with the app plate on the left and battery + master switch on the right.
    internal sealed class HeaderBar : Control
    {
        public readonly RetroSwitch Switch;
        readonly ToolTip tip = Theme.PixelToolTip();
        string status = "";
        int percent = -1;
        bool charging;
        string battText = "...";
        const string AppTitle = "Trackpad Canvas";
        const string SwitchCaption = "Gestures";
        const int TopU = 6, ShU = 2;

        static readonly string[] Bolt =
        {
            "..##.",
            ".##..",
            "#####",
            "..##.",
            ".##..",
            ".#...",
        };

        public HeaderBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bezel;
            Switch = new RetroSwitch { PlateColor = Theme.Card, AccessibleName = "Gestures enabled" };
            Switch.Size = Switch.GetPreferredSize(Size.Empty);
            Controls.Add(Switch);
            Height = Theme.Px(Theme.HeaderMode == 1 ? SlimU : Theme.HeaderU);
        }

        const int SlimU = 28;
        public event EventHandler Changed;
        public string BatteryText { get { return battText; } }

        void RaiseChanged()
        {
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public string Status { get { return status; } set { status = value ?? ""; RaiseChanged(); } }

        public void SetBattery(int percent, bool charging, string text, string detail)
        {
            this.percent = percent;
            this.charging = charging;
            battText = text;
            tip.SetToolTip(this, detail);
            RaiseChanged();
        }

        Rectangle SlimPlate { get { return new Rectangle(Theme.Px(6), Theme.Px(3), Width - Theme.Px(12 + ShU), Height - Theme.Px(3 + 3 + ShU)); } }

        void PaintSlim(Graphics g)
        {
            int u = Theme.U;
            Theme.Fill(g, BackColor, ClientRectangle);
            Theme.Dither(g, new Rectangle(0, 0, Width, Height - u), Theme.Mix(BackColor, Theme.BezelDark, 0.35f), 2);
            Theme.Fill(g, Theme.Ink, new Rectangle(0, Height - u, Width, u));
            var p = SlimPlate;
            Theme.Frame(g, p, Theme.Card, Theme.Ink, Theme.C2, ShU);
            int iy = p.Y + (p.Height - Theme.Px(16)) / 2;
            AppIcon.DrawPixels(g, p.X + Theme.Px(3), iy - iy % u, u);
            int x = p.X + Theme.Px(23);
            int right = Switch.Parent == this ? Switch.Left - Theme.Px(8) : p.Right - Theme.Px(6);
            string batt = "Battery " + battText;
            int bw = PixelFont.Width(batt, Theme.Text);
            PixelFont.DrawIn(g, batt, new Rectangle(right - bw, p.Y, bw + u, p.Height), Theme.Text, Theme.InkSoft, false);
            int tw = PixelFont.Width(AppTitle, Theme.Bold);
            PixelFont.DrawIn(g, AppTitle, new Rectangle(x, p.Y, tw + u, p.Height), Theme.Bold, Theme.Ink, false);
            int sx = x + tw + Theme.Px(10);
            PixelFont.DrawIn(g, status, new Rectangle(sx, p.Y, Math.Max(0, right - bw - Theme.Px(10) - sx), p.Height), Theme.Text, Theme.InkSoft, false);
        }

        int PlateH { get { return Height - Theme.Px(2 * TopU + ShU + 1); } }
        int BattTextW { get { return Math.Max(PixelFont.Width("100%", Theme.Bold), PixelFont.Width("battery", Theme.Text)); } }
        int CaptionW { get { return PixelFont.Width(SwitchCaption, Theme.Text); } }

        Rectangle RightPlate
        {
            get
            {
                int w = Theme.Px(8 + 17 + 5) + BattTextW + Theme.Px(8 + 1 + 8) + CaptionW + Theme.Px(6) + Switch.Width + Theme.Px(6);
                return new Rectangle(Width - Theme.Px(10 + ShU) - w, Theme.Px(TopU), w, PlateH);
            }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (Switch == null || Switch.Parent != this) return;
            if (Theme.HeaderMode == 1)
            {
                var p = SlimPlate;
                int sy = p.Y + (p.Height - Switch.Height) / 2;
                Switch.Location = new Point(p.Right - Theme.Px(4) - Switch.Width, sy - sy % Theme.U);
                return;
            }
            var r = RightPlate;
            int y = r.Y + (r.Height - Switch.Height) / 2;
            Switch.Location = new Point(r.Right - Theme.Px(6) - Switch.Width, y - y % Theme.U);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int u = Theme.U;
            if (Theme.HeaderMode == 1) { PaintSlim(g); return; }
            Theme.Fill(g, BackColor, ClientRectangle);
            Theme.Dither(g, new Rectangle(0, 0, Width, Height - u), Theme.Mix(BackColor, Theme.BezelDark, 0.35f), 2);
            Theme.Fill(g, Theme.BezelLight, new Rectangle(0, 0, Width, u));
            Theme.Fill(g, Theme.Ink, new Rectangle(0, Height - u, Width, u));

            // Left plate: pixel icon, title and a one-line status.
            var right = RightPlate;
            int iconX = Theme.Px(5), textX = Theme.Px(5 + 32 + 6);
            int want = textX + Math.Max(PixelFont.Width(AppTitle, Theme.Title), PixelFont.Width(status, Theme.Text)) + Theme.Px(10);
            int lw = Math.Max(textX + Theme.Px(30), Math.Min(want, right.Left - Theme.Px(10 + ShU) - Theme.Px(10)));
            var left = new Rectangle(Theme.Px(10), Theme.Px(TopU), lw, PlateH);
            Theme.Frame(g, left, Theme.Card, Theme.Ink, Theme.C3, ShU);
            Theme.Bevel(g, left, Theme.C3, Theme.Mix(Theme.Card, Color.White, 0.5f), Theme.Mix(Theme.Card, Theme.Ink, 0.15f));
            int iy = left.Y + (left.Height - Theme.Px(32)) / 2;
            AppIcon.DrawPixels(g, left.X + iconX, iy - iy % u, 2 * u);
            int titleH = PixelFont.Height(Theme.Title), block = titleH + Theme.Px(2) + PixelFont.Height(Theme.Text);
            int ty = left.Y + (left.Height - block) / 2;
            ty -= ty % u;
            int tw = left.Width - textX - Theme.Px(6);
            PixelFont.Draw(g, PixelFont.Fit(AppTitle, Theme.Title, tw), left.X + textX, ty, Theme.Title, Theme.Ink);
            PixelFont.Draw(g, PixelFont.Fit(status, Theme.Text, tw), left.X + textX, ty + titleH + Theme.Px(2), Theme.Text, Theme.InkSoft);

            // Right plate: battery gauge and the master switch.
            Theme.Frame(g, right, Theme.Card, Theme.Ink, Theme.C3, ShU);
            Theme.Bevel(g, right, Theme.C3, Theme.Mix(Theme.Card, Color.White, 0.5f), Theme.Mix(Theme.Card, Theme.Ink, 0.15f));
            int cy = right.Y + right.Height / 2;
            cy -= cy % u;
            int bx = right.X + Theme.Px(8);
            var body = new Rectangle(bx, cy - Theme.Px(5), Theme.Px(15), Theme.Px(10));
            Theme.Frame(g, body, Theme.Field, Theme.Ink, Theme.C1, 0);
            Theme.Fill(g, Theme.Ink, new Rectangle(body.Right, cy - Theme.Px(2), Theme.Px(2), Theme.Px(4)));
            if (percent >= 0)
            {
                var inner = Rectangle.Inflate(body, -2 * u, -2 * u);
                Color level = percent <= 20 ? Color.FromArgb(236, 112, 136) : percent <= 50 ? Color.FromArgb(240, 192, 80) : Color.FromArgb(108, 192, 140);
                int cells = Math.Max(1, (inner.Width / u) * Math.Min(100, percent) / 100);
                Theme.Fill(g, level, new Rectangle(inner.X, inner.Y, cells * u, inner.Height));
            }
            if (charging) Theme.Pattern(g, Bolt, body.X + Theme.Px(5), body.Y + Theme.Px(2), '#', Theme.Ink);
            int tx = bx + Theme.Px(17 + 5);
            int gh = PixelFont.Height(Theme.Bold);
            int my = cy - (gh + Theme.Px(2) + gh) / 2;
            my -= my % u;
            PixelFont.Draw(g, battText, tx, my, Theme.Bold, Theme.Ink);
            PixelFont.Draw(g, "battery", tx, my + gh + Theme.Px(2), Theme.Text, Theme.InkSoft);

            int dx = tx + BattTextW + Theme.Px(8);
            Theme.Fill(g, Theme.Ink, new Rectangle(dx, right.Y + Theme.Px(6), u, right.Height - Theme.Px(12)));
            int capX = dx + Theme.Px(1 + 8);
            PixelFont.DrawIn(g, SwitchCaption, new Rectangle(capX, right.Y, CaptionW + u, right.Height), Theme.Text, Theme.InkSoft, false);
        }
    }

    // Optional bottom status bar (preview variants): mirrors the header's status and battery text.
    internal sealed class PixelStatusBar : Control
    {
        readonly HeaderBar source;

        public PixelStatusBar(HeaderBar source)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            this.source = source;
            source.Changed += (s, e) => Invalidate();
            Height = Theme.Px(PixelFont.Rows + 9);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int u = Theme.U;
            Theme.Fill(g, Theme.Bezel, ClientRectangle);
            Theme.Fill(g, Theme.Ink, new Rectangle(0, 0, Width, u));
            var strip = new Rectangle(Theme.Px(4), Theme.Px(3), Width - Theme.Px(8), Height - Theme.Px(5));
            Theme.Frame(g, strip, Theme.Lcd, Theme.BezelDark, Theme.C1, 0);
            string batt = "Battery: " + source.BatteryText;
            int bw = PixelFont.Width(batt, Theme.Text);
            var inner = Rectangle.Inflate(strip, -Theme.Px(5), 0);
            PixelFont.DrawIn(g, batt, new Rectangle(inner.Right - bw, inner.Y, bw + u, inner.Height), Theme.Text, Theme.LcdText, false);
            PixelFont.DrawIn(g, source.Status, new Rectangle(inner.X, inner.Y, inner.Width - bw - Theme.Px(10), inner.Height), Theme.Text, Theme.LcdText, false);
        }
    }

    // Live picture of the trackpad surface with the fingers currently touching it.
    internal sealed class TouchView : Control
    {
        List<Contact> contacts = new List<Contact>();
        string state = "Idle";
        double padW = 160, padH = 115;
        static readonly Color[] FingerColors = { Theme.Pink, Theme.Blue, Theme.Yellow, Theme.Mint, Theme.Lavender };
        const int ShU = 3;

        public TouchView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        protected override Padding DefaultMargin { get { return Padding.Empty; } }

        public void SetPad(double w, double h)
        {
            if (w <= 0 || h <= 0 || (w == padW && h == padH)) return;
            padW = w;
            padH = h;
            Theme.Relayout(this);
            Invalidate();
        }

        public void SetFrame(List<Contact> frame, string gesture)
        {
            contacts = frame ?? new List<Contact>();
            state = gesture ?? "";
            Invalidate();
        }

        int PadTop { get { return Theme.TagHeight + Theme.Px(7); } }

        public override Size GetPreferredSize(Size proposed)
        {
            int w = Theme.W(proposed, Width);
            int h = (int)Math.Min(Theme.Px(150), (w - Theme.Px(ShU)) * padH / padW);
            return new Size(w, PadTop + h + Theme.Px(ShU));
        }

        static int Snap(double v) { int u = Theme.U; int i = (int)v; return i - i % u; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int u = Theme.U;
            Theme.Backdrop(g, this);
            int n = contacts.Count;
            var t1 = Theme.Tag(g, 0, 0, n + (n == 1 ? " finger" : " fingers"), Theme.Text, Theme.Yellow, true);
            Theme.Tag(g, t1.Right + Theme.Px(5), 0, "gesture: " + state, Theme.Text, Theme.Blue, true);

            int top = PadTop;
            int availW = Width - Theme.Px(ShU), availH = Height - top - Theme.Px(ShU);
            if (availW <= 0 || availH <= 0) return;
            double scale = Math.Min(availW / padW, availH / padH);
            int pw = Snap(padW * scale), ph = Snap(padH * scale);
            var pad = new Rectangle(Snap((availW - pw) / 2.0), top, pw, ph);
            Theme.Frame(g, pad, Theme.Field, Theme.Ink, Theme.C4, ShU);

            int step = Math.Max(Theme.Px(6), Snap(10 * scale));
            using (var b = new SolidBrush(Theme.Dot))
                for (int y = pad.Y + step; y < pad.Bottom - Theme.Px(3); y += step)
                    for (int x = pad.X + step; x < pad.Right - Theme.Px(3); x += step)
                        g.FillRectangle(b, x, y, u, u);

            if (n == 0)
            {
                PixelFont.DrawIn(g, "Touch the trackpad", Rectangle.Inflate(pad, -Theme.Px(6), 0), Theme.Text, Theme.InkSoft, true);
                return;
            }

            var pts = new Point[n];
            for (int i = 0; i < n; i++)
            {
                double cx = Math.Max(0, Math.Min(padW, contacts[i].X)), cy = Math.Max(0, Math.Min(padH, contacts[i].Y));
                pts[i] = new Point(Snap(pad.X + cx * scale), Snap(pad.Y + cy * scale));
            }
            if (n >= 2) DashedLine(g, pts[0], pts[1]);
            int d = Math.Max(13, (int)(13 * scale / u));
            for (int i = 0; i < n; i++)
            {
                int x = pts[i].X - Theme.Px(d / 2), y = pts[i].Y - Theme.Px(d / 2);
                Theme.PixelCircle(g, Theme.Shadow, x + 2 * u, y + 2 * u, d);
                Theme.PixelCircle(g, Theme.Ink, x, y, d);
                Theme.PixelCircle(g, FingerColors[i % FingerColors.Length], x + u, y + u, d - 2);
                PixelFont.DrawIn(g, (i + 1).ToString(), new Rectangle(x + u, y, Theme.Px(d), Theme.Px(d)), Theme.Bold, Theme.Ink, true);
            }
        }

        static void DashedLine(Graphics g, Point a, Point b)
        {
            int u = Theme.U;
            int steps = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) / u;
            using (var br = new SolidBrush(Theme.Ink))
                for (int k = 0; k <= steps; k++)
                {
                    if ((k / 3) % 2 == 1) continue;
                    double t = steps == 0 ? 0 : (double)k / steps;
                    g.FillRectangle(br, Snap(a.X + (b.X - a.X) * t), Snap(a.Y + (b.Y - a.Y) * t), u, u);
                }
        }
    }
}
