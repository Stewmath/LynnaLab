using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LynnaLib
{
    public static class GbGraphics
    {
        // Black & white palette; use when no other palette makes sense.
        public static readonly Color[] GrayPalette = {
            Color.FromRgb(255, 255, 255),
            Color.FromRgb(198, 198, 198),
            Color.FromRgb(100, 100, 100),
            Color.FromRgb(0, 0, 0)
        };

        /// <summary>
        /// Renders a tile onto the given Cairo.Context. (Supports 8x8 or 8x16 tiles; 8x16 are
        /// treated as sprites.)
        /// Caller should probably invoke Bitmap.MarkModified() after calling this.
        /// </summary>
        public static unsafe void RenderRawTile(Bitmap bitmap, int xO, int yO, ReadOnlySpan<byte> data, IList<Color> palette = null, int flags = 0)
        {
            Debug.Assert(data.Length == 16 || data.Length == 32);

            if (palette == null)
                palette = GrayPalette;

            // Use this as transparent color for sprites
            Color transparentColor = Color.FromRgba(0, 0, 0, 0);

            bool sprite = (data.Length == 32);
            int height = (sprite ? 16 : 8);

            Debug.Assert(xO >= 0 && yO >= 0 && xO + 8 <= bitmap.Width && yO + height <= bitmap.Height);

            int stride = bitmap.Stride;
            int bytesPerPixel = 4;

            byte* pixels = (byte*)bitmap.Lock();

            bool hflip = (flags & 0x20) == 0x20;
            bool vflip = (flags & 0x40) == 0x40;

            for (int y = 0; y < height; y++)
            {
                int b1 = data[y * 2];
                int b2 = data[y * 2 + 1] << 1;
                int row;
                if (vflip)
                    row = (height - 1 - y) * stride;
                else
                    row = y * stride;
                row += yO * stride;
                for (int x = 0; x < 8; x++)
                {
                    int color;

                    color = b1 & 1;
                    color |= b2 & 2;
                    b1 >>= 1;
                    b2 >>= 1;

                    int realX;
                    if (hflip)
                        realX = x;
                    else
                        realX = 7 - x;
                    realX += xO;

                    bool transparent = sprite && color == 0;
                    Color c = (transparent ? transparentColor : palette[color]);

                    pixels[row + realX * bytesPerPixel + 0] = (byte)c.B;
                    pixels[row + realX * bytesPerPixel + 1] = (byte)c.G;
                    pixels[row + realX * bytesPerPixel + 2] = (byte)c.R;
                    pixels[row + realX * bytesPerPixel + 3] = (byte)c.A;
                }
            }

            bitmap.Unlock();
        }

        /// <summary>
        /// Convert a single tile to a bitmap. (Supports 8x8 or 8x16 tiles; 8x16 are treated as
        /// sprites.)
        /// </summary>
        public static Bitmap RawTileToBitmap(ReadOnlySpan<byte> data, IList<Color> palette = null, int flags = 0)
        {
            Debug.Assert(data.Length == 16 || data.Length == 32);
            bool sprite = (data.Length == 32);
            int height = (sprite ? 16 : 8);

            Bitmap bitmap = new Bitmap(8, height, Cairo.Format.ARGB32);
            RenderRawTile(bitmap, 0, 0, data, palette, flags);
            return bitmap;
        }

        /// <summary>
        /// Converts a description of a tile from a tileset to a bitmap.
        /// </summary>
        public static void RenderTile(Bitmap bitmap, int xO, int yO, TileDescription tileDesc, IList<Color>[] palettes)
        {
            Debug.Assert(palettes.Length == 8);

            var renderSubTile = (Cairo.Context cr, SubTileDescription desc, IList<Color>[] palettes, int x, int y) =>
            {
                using (Bitmap subtile = RawTileToBitmap(desc.graphics, palettes[desc.flags & 7], desc.flags))
                {
                    cr.SetSourceSurface(subtile, x, y);
                    cr.Paint();
                }
            };

            bitmap.Lock();
            using (Cairo.Context cr = bitmap.CreateContext())
            {
                renderSubTile(cr, tileDesc.tileTL, palettes, xO + 0, yO + 0);
                renderSubTile(cr, tileDesc.tileTR, palettes, xO + 8, yO + 0);
                renderSubTile(cr, tileDesc.tileBL, palettes, xO + 0, yO + 8);
                renderSubTile(cr, tileDesc.tileBR, palettes, xO + 8, yO + 8);
            }
            bitmap.Unlock();
        }
    }
}

/// <summary>
/// A description of a tile from a tileset, containing references to the graphical data and flags
/// required to render it. (Palettes not included.)
/// This is a ref struct, meaning it is always on the stack (never on the heap), and therefore is
/// allowed to have Span member fields, which are also ref struct types. These types have a lot of
/// limitations on what you can do with them, ie. they cannot be used in arrays.
/// </summary>
public ref struct TileDescription
{
    public TileDescription(SubTileDescription tl, SubTileDescription tr, SubTileDescription bl, SubTileDescription br)
    {
        tileTL = tl;
        tileTR = tr;
        tileBL = bl;
        tileBR = br;
    }

    public readonly SubTileDescription tileTL, tileTR, tileBL, tileBR;
}

/// <summary>
/// Like above but for a single subtile
/// </summary>
public ref struct SubTileDescription
{
    public SubTileDescription(ReadOnlySpan<byte> gfx, byte flags)
    {
        Debug.Assert(gfx.Length == 16);
        this.graphics = gfx;
        this.flags = flags;
    }

    public readonly ReadOnlySpan<byte> graphics; // 16 bytes
    public readonly byte flags;
}
