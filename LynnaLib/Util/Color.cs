using System.Numerics;

namespace LynnaLib
{
    /// I don't like Cairo.Color (it uses doubles) so I made this
    public class Color
    {
        int r, g, b, a;

        public int R
        {
            get { return r; }
        }
        public int G
        {
            get { return g; }
        }
        public int B
        {
            get { return b; }
        }
        public int A
        {
            get { return a; }
        }

        private Color() { }

        public static Color FromRgb(int r, int g, int b)
        {
            return FromRgba(r, g, b, 255);
        }

        public static Color FromRgba(int r, int g, int b, int a)
        {
            Color c = new Color();
            c.r = r;
            c.g = g;
            c.b = b;
            c.a = a;
            return c;
        }

        public static Color FromRgbDbl(double r, double g, double b)
        {
            return FromRgbaDbl(r, g, b, 1.0);
        }

        public static Color FromRgbaDbl(double r, double g, double b, double a)
        {
            Color c = new Color();
            c.r = (int)(r * 255);
            c.g = (int)(g * 255);
            c.b = (int)(b * 255);
            c.a = (int)(a * 255);
            return c;
        }

        // ================================================================================
        // Conversion
        // ================================================================================

        public static Color FromCairo(Cairo.Color c)
        {
            return FromRgbaDbl(c.R, c.G, c.B, c.A);
        }

        public static implicit operator Cairo.Color(Color c)
        {
            return new Cairo.Color(c.r / 255.0, c.g / 255.0, c.b / 255.0, c.a / 255.0);
        }

        public static implicit operator Vector4(Color c)
        {
            return new Vector4(c.r / 255.0f, c.g / 255.0f, c.b / 255.0f, c.a / 255.0f);
        }

        public static implicit operator Color(Vector4 c)
        {
            return Color.FromRgbaDbl(c.X, c.Y, c.Z, c.W);
        }



        // ================================================================================
        // Constants
        // ================================================================================

        // Some colors copied over from System.Drawing to keep things consistent
        public static readonly Color Black = FromRgb(0x00, 0x00, 0x00);
        public static readonly Color Blue = FromRgb(0x00, 0x00, 0xff);
        public static readonly Color Cyan = FromRgb(0x00, 0xff, 0xff);
        public static readonly Color DarkOrange = FromRgb(0xff, 0x8c, 0x00);
        public static readonly Color Gray = FromRgb(0x80, 0x80, 0x80);
        public static readonly Color Green = FromRgb(0x00, 0x80, 0x00);
        public static readonly Color Lime = FromRgb(0x00, 0xff, 0x00);
        public static readonly Color Red = FromRgb(0xff, 0x00, 0x00);
        public static readonly Color Purple = FromRgb(0x80, 0x00, 0x80);
        public static readonly Color White = FromRgb(0xff, 0xff, 0xff);
        public static readonly Color Yellow = FromRgb(0xff, 0xff, 0x00);
    }
}
