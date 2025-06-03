#nullable enable

using System.Numerics;
using System.Text.Json.Serialization;
using PixelFormats = SixLabors.ImageSharp.PixelFormats;

namespace LynnaLib
{
    /// <summary>
    /// Represents a color. Immutable. Supports implicit conversion to and from
    /// ImageSharp.PixelFormats.Rgba32 (used by the Bitmap class) and Vector4 (used by ImGui).
    /// </summary>
    public struct Color
    {
        [JsonInclude]
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

        public override bool Equals(object? obj)
        {
            return obj is Color c && this == c;
        }

        public override int GetHashCode()
        {
            return r.GetHashCode() * 3 + b.GetHashCode() * 5 + g.GetHashCode() * 7 + a.GetHashCode();
        }

        public static bool operator==(Color c1, Color c2)
        {
            return c1.r == c2.r && c1.g == c2.g && c1.b == c2.b && c1.a == c2.a;
        }

        public static bool operator!=(Color c1, Color c2)
        {
            return !(c1 == c2);
        }

        public override string ToString()
        {
            return $"R: {R}, G: {G}, B: {B}, A: {A}";
        }

        // ================================================================================
        // Conversion
        // ================================================================================

        // Not implicit because automatic conversion to uint could be a bit too permissive
        public uint ToUInt()
        {
            return (uint)(R | (G<<8) | (B<<16) | (A<<24));
        }

        public static implicit operator PixelFormats.Rgba32(Color c)
        {
            return new PixelFormats.Rgba32((Vector4)c);
        }

        public static implicit operator Color(PixelFormats.Rgba32 c)
        {
            return Color.FromRgba(c.R, c.G, c.B, c.A);
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
