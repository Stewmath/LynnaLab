using System;
using System.Linq;
using System.Collections.Generic;

namespace LynnaLib
{
    public class GbGraphics
    {
        // Black & white palette; use when no other palette makes sense.
        public static readonly MyColor[] GrayPalette = {
            MyColor.FromRgb(255, 255, 255),
            MyColor.FromRgb(198, 198, 198),
            MyColor.FromRgb(100, 100, 100),
            MyColor.FromRgb(0, 0, 0)
        };

        /// <summary>
        ///  Convert a single tile to an image. (Supports 8x8 or 8x16 tiles; 8x16 are treated as
        ///  sprites.)
        /// </summary>
		public static MyBitmap TileToBitmap(IList<byte> data, IList<MyColor> palette = null, int flags = 0)
        {
            if (palette == null)
                palette = GrayPalette;

            // Use this as transparent color for sprites
            Cairo.Color transparentColor = new Cairo.Color(1, 1, 1);

            bool sprite = (data.Count == 32);
            int height = (sprite ? 16 : 8);

            int stride = 32;
            int bytesPerPixel = 4;
            byte[] pixels = new byte[stride * height];

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

                    bool transparent = sprite && color == 0;
                    Cairo.Color c = (transparent ? transparentColor : palette[color]);

                    pixels[row + realX * bytesPerPixel + 0] = c.ByteColor().B;
                    pixels[row + realX * bytesPerPixel + 1] = c.ByteColor().G;
                    pixels[row + realX * bytesPerPixel + 2] = c.ByteColor().R;
                    pixels[row + realX * bytesPerPixel + 3] = (byte)(transparent ? 0 : 255);
                }
            }

            return new MyBitmap(new Cairo.ImageSurface(pixels, Cairo.Format.ARGB32, 8, height, 32));
        }
    }
}
