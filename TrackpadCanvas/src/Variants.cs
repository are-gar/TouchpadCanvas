using System.Drawing;

namespace TrackpadCanvas
{
    // Preview variants for the hidden --variant N switch (1..50). They only change Theme settings before the
    // UI is built; with no switch the defaults in Retro.cs are used unchanged.
    internal static partial class Theme
    {
        static Color H(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        static bool IsDark(Color c) { return c.R * 299 + c.G * 587 + c.B * 114 < 110000; }

        // Sets a whole palette; the secondary colours are derived from the main ones.
        static void P(int paper, int paperAlt, int card, int field, int ink, int inkSoft,
                      int bezel, int bezelLight, int bezelDark, int lcd, int lcdText)
        {
            Paper = H(paper); PaperAlt = H(paperAlt); Card = H(card); Field = H(field);
            Ink = H(ink); InkSoft = H(inkSoft);
            Bezel = H(bezel); BezelLight = H(bezelLight); BezelDark = H(bezelDark);
            Lcd = H(lcd); LcdText = H(lcdText);
            LcdDim = Mix(Lcd, LcdText, 0.2f);
            Disabled = Mix(InkSoft, Card, 0.45f);
            DisabledFill = Mix(Card, Ink, 0.08f);
            Hover = Mix(Field, Card, 0.5f);
            Selection = Mix(Bezel, Field, 0.5f);
            Link = IsDark(Card) ? Mix(Ink, Bezel, 0.3f) : BezelDark;
            PaperDark = Mix(Card, Ink, 0.15f);
            Dot = Mix(Field, Ink, 0.25f);
            Shadow = IsDark(Card) ? Color.FromArgb(140, 0, 0, 0) : Color.FromArgb(80, 20, 20, 60);
            TitleFill = Lcd; TitleText = LcdText;
        }

        static void A(int pink, int blue, int yellow, int mint, int lavender, int peach)
        {
            Pink = H(pink); Blue = H(blue); Yellow = H(yellow); Mint = H(mint); Lavender = H(lavender); Peach = H(peach);
        }

        public static void ApplyVariant(int n)
        {
            Variant = n;
            switch (n)
            {
                // ---- Colour palettes
                case 1: // previous default: blue bezel / mint LCD on black
                    Paper = Color.Black; PaperAlt = H(0xE2065A); PaperDark = H(0x96C8A8); Dot = H(0xB8C0EC);
                    Card = H(0xB2DCC0); Field = Color.White; Ink = H(0x161E48); InkSoft = H(0x345A50);
                    Disabled = H(0x748C84); DisabledFill = H(0xCADAD0);
                    A(0xFA7098, 0x7C90FA, 0xFCDE6E, 0x6CD094, 0xA698F6, 0xFCAA82);
                    Hover = H(0xE8F4EC); Link = H(0x2838C8); Selection = H(0x98A8FA);
                    Lcd = H(0xA4D2B4); LcdText = H(0x123220); LcdDim = H(0x88BC9A);
                    Bezel = H(0x4860E0); BezelLight = H(0x8896FA); BezelDark = H(0x3228AA);
                    TitleFill = Lcd; TitleText = LcdText;
                    break;
                case 2: // dark purple soundboard
                    P(0x3C1460, 0x4A1C74, 0x141416, 0x26262C, 0xE6E6EC, 0xA89CC8, 0x1C1A20, 0x3A3644, 0x5A32C8, 0x2A1E5A, 0xF0B4DC);
                    A(0x8C3C78, 0x4A3CA0, 0x7A5A1C, 0x2C6A4A, 0x5A3CA0, 0x8A4A2C);
                    break;
                case 3: // Game Boy 4-shade green
                    P(0x0F380F, 0x306230, 0x8BAC0F, 0x9BBC0F, 0x0F380F, 0x306230, 0x306230, 0x8BAC0F, 0x0F380F, 0x9BBC0F, 0x0F380F);
                    A(0x6E8C1E, 0x6E8C1E, 0x6E8C1E, 0x6E8C1E, 0x6E8C1E, 0x6E8C1E);
                    Disabled = H(0x306230); DisabledFill = H(0x8BAC0F); Hover = H(0x9BBC0F); Dot = H(0x306230);
                    break;
                case 4: // amber CRT monochrome
                    P(0x0A0600, 0x1A1000, 0x140C00, 0x201400, 0xFFB000, 0xB07A10, 0x2A1A00, 0x5A3A00, 0x000000, 0x1E1300, 0xFFC040);
                    A(0x7A4A00, 0x5A3A00, 0x806000, 0x6A5000, 0x5A3A10, 0x7A4010);
                    break;
                case 5: // pastel original on a cream checker
                    P(0xF5EDDC, 0xEEE3CE, 0xFFFCF4, 0xFFFFFF, 0x2E293E, 0x706880, 0xCEC0F2, 0xEAE2FF, 0x9C8CD0, 0xC8E8CC, 0x264C38);
                    A(0xF6B6C6, 0xA8D6EC, 0xFCE296, 0xB8E4C8, 0xCEC0F2, 0xFACCAA);
                    Shadow = Color.FromArgb(70, 46, 41, 62);
                    BgStyle = 1;
                    break;
                case 6: // high-contrast black / white
                    P(0x000000, 0xFFFFFF, 0xFFFFFF, 0xFFFFFF, 0x000000, 0x202020, 0x000000, 0x000000, 0x000000, 0xFFFFFF, 0x000000);
                    A(0xFFE600, 0x00E5FF, 0xFFE600, 0x00FF66, 0xFF66FF, 0xFFA000);
                    Shadow = Color.Transparent; Disabled = H(0x707070); DisabledFill = H(0xE0E0E0); Selection = H(0x00E5FF);
                    break;
                case 7: // vaporwave pink / cyan
                    P(0x1A0B2E, 0x2D1450, 0xE0FBFF, 0xFFFFFF, 0x2A0A4A, 0x6A3A8A, 0xFF71CE, 0xFFB8EA, 0xB026A0, 0x9CF2FF, 0x2A0A4A);
                    A(0xFF71CE, 0x01CDFE, 0xFFFB96, 0x05FFA1, 0xB967FF, 0xFFB38A);
                    break;
                case 8: // navy + gold
                    P(0x0B1430, 0x16224A, 0xF4EBD0, 0xFFFFFF, 0x0B1430, 0x4A5270, 0x1C2A5C, 0xD4AF37, 0x0B1430, 0xE8C860, 0x1C1A08);
                    A(0xE8A0A0, 0x9DB4E8, 0xE8C860, 0xA8D8B0, 0xC0B0E8, 0xE8B890);
                    break;
                case 9: // sepia paper
                    P(0xD9C4A0, 0xCDB48A, 0xF2E6CE, 0xFBF5E8, 0x3B2A1A, 0x7A6248, 0x8B6B47, 0xB8946A, 0x5A4028, 0xE6D2AE, 0x3B2A1A);
                    A(0xD8A090, 0xA8B8C0, 0xE0C080, 0xB0C090, 0xC0A8B8, 0xE0B090);
                    break;
                case 10: // grayscale Windows 95
                    P(0x808080, 0x707070, 0xC0C0C0, 0xFFFFFF, 0x000000, 0x404040, 0xC0C0C0, 0xFFFFFF, 0x808080, 0xFFFFFF, 0x000000);
                    A(0xE4E4E4, 0xD4D4D4, 0xECECEC, 0xDCDCDC, 0xD8D8D8, 0xE0E0E0);
                    TitleFill = H(0x404040); TitleText = Color.White;
                    break;
                case 11: // ocean teal
                    P(0x062A30, 0x0B3D45, 0xD8F3F0, 0xFFFFFF, 0x063A40, 0x3A6E70, 0x0E8C8C, 0x5CC8C0, 0x065A60, 0xA8E6DE, 0x053A3A);
                    A(0xF4A6A0, 0x7EC8E8, 0xF6DC8A, 0x7EDCB0, 0xA8B8F0, 0xF6BC90);
                    break;
                case 12: // sunset orange / purple
                    P(0x2A1030, 0x3A1640, 0xFFE8D6, 0xFFFFFF, 0x3A1030, 0x7A4060, 0xE8603C, 0xFFA070, 0x8A2A6A, 0xFFD2B0, 0x4A1030);
                    A(0xFF7A9A, 0xB088E8, 0xFFC850, 0xF0A070, 0xC890E0, 0xFF9A60);
                    break;

                // ---- Backgrounds (current palette)
                case 13: break; // solid black (same as current)
                case 14: Paper = H(0xFCFAFC); PaperAlt = H(0xE2065A); BgStyle = 1; break;
                case 15: PaperAlt = H(0x1E2450); BgStyle = 2; break;
                case 16: PaperAlt = H(0x202860); BgStyle = 3; break;
                case 17: PaperAlt = H(0x3A4494); BgStyle = 4; break;
                case 18: PaperAlt = H(0x1A1E48); BgStyle = 5; break;

                // ---- Panel and frame styles
                case 19: break; // blue bezel + LCD body (current)
                case 20: CardStyle = 1; break;
                case 21: BezelU = 6; break;
                case 22: CardStyle = 2; CardCorners = new int[0]; TitleFill = H(0x000080); TitleText = Color.White; break;
                case 23: CardCorners = C4; BodyCorners = C3; break;
                case 24: CardCorners = new int[0]; BodyCorners = new int[0]; break;
                case 25: Shadow = Color.Transparent; break;
                case 26: Shadow = H(0x6E6E78); break;
                case 27: DoubleOutline = true; BezelU = 4; break;
                case 28: AccentBezel = true; break;

                // ---- Layout
                case 29: Space = 60; break;
                case 30: Space = 140; WindowWU = 520; break;
                case 31: WindowWU = 360; Title = new PFont(1, true); break; // 2x title doesn't fit beside the battery plate
                case 32: WindowWU = 780; TwoColumns = true; break;
                case 33: TabsAt = 1; WindowWU = 580; break;
                case 34: TabsAt = 2; break;
                case 35: HeaderMode = 1; break;
                case 36: HeaderMode = 2; break;
                case 37: TitleAlign = 1; break;
                case 38: TitleAlign = 2; break;
                case 39: PageMarginU = 0; break;
                case 40: StatusBar = true; break;

                // ---- Typography and controls
                case 41: Title = new PFont(3, true); HeaderU = 64; WindowWU = 600; break;
                case 42: AllCaps = true; WindowWU = 520; break; // capitals are wider; keeps the title from truncating
                case 43: HintMode = 1; break;
                case 44: HintMode = 2; Hint = new PFont(0, false); break;
                case 45: CheckSwitches = true; break;
                case 46: KeyScale = 2; break;
                case 47: FlatKeys = true; break;
                case 48: LcdReadouts = true; break;
                case 49: SegmentedTrack = true; break;
                case 50: // inverted: light text on dark panels
                    P(0x000000, 0x000000, 0x1C2240, 0x2A3260, 0xE6F0FF, 0xA0B4D8, 0x4860E0, 0x8896FA, 0x3228AA, 0x10301E, 0xA8F0C0);
                    A(0xA03060, 0x3048B0, 0x8A6A10, 0x1E7A4A, 0x5A44B0, 0xA0502A);
                    break;
            }
        }
    }
}
