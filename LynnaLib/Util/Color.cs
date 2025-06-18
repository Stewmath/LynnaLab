#nullable enable

using System.Numerics;
using System.Text.Json;
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
        [JsonRequired]
        public int R
        {
            get; init;
        }
        [JsonRequired]
        public int G
        {
            get; init;
        }
        [JsonRequired]
        public int B
        {
            get; init;
        }
        [JsonRequired]
        public int A
        {
            get; init;
        }

        // ================================================================================
        // Static methods
        // ================================================================================

        public static Color FromRgb(int r, int g, int b)
        {
            return FromRgba(r, g, b, 255);
        }

        public static Color FromRgba(int r, int g, int b, int a)
        {
            return new Color()
            {
                R = r,
                G = g,
                B = b,
                A = a,
            };
        }

        public static Color FromRgbDbl(double r, double g, double b)
        {
            return FromRgbaDbl(r, g, b, 1.0);
        }

        public static Color FromRgbaDbl(double r, double g, double b, double a)
        {
            return new Color()
            {
                R = (int)(r * 255),
                G = (int)(g * 255),
                B = (int)(b * 255),
                A = (int)(a * 255),
            };
        }

        /// <summary>
        /// Gets a color based on the format from the "ToString" method. May throw JsonException if
        /// deserialization fails. Used for clipboard copy/paste.
        /// </summary>
        public static Color? Deserialize(string s)
        {
            try
            {
                return JsonSerializer.Deserialize<Color>(s);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        /// <summary>
        /// Used for clipboard copy/paste.
        /// </summary>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }

        public override bool Equals(object? obj)
        {
            return obj is Color c && this == c;
        }

        public override int GetHashCode()
        {
            return R.GetHashCode() * 3 + B.GetHashCode() * 5 + G.GetHashCode() * 7 + A.GetHashCode();
        }

        public static bool operator==(Color c1, Color c2)
        {
            return c1.R == c2.R && c1.G == c2.G && c1.B == c2.B && c1.A == c2.A;
        }

        public static bool operator!=(Color c1, Color c2)
        {
            return !(c1 == c2);
        }

        /// <summary>
        /// This is used for clipboard sharing, so don't change without also changing FromString.
        /// </summary>
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
            return new Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
        }

        public static implicit operator Color(Vector4 c)
        {
            return Color.FromRgbaDbl(c.X, c.Y, c.Z, c.W);
        }



        // ================================================================================
        // Constants
        // ================================================================================

        // For clipboard copy/paste
        public static readonly string MimeType = "application/vnd.lynnalab.color";

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
