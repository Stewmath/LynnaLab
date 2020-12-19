using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace LynnaLib
{
	public class GbGraphics {
        // Black & white palette; use when no other palette makes sense.
        public static readonly Color[] GrayPalette = {
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(198, 198, 198),
            Color.FromArgb(100, 100, 100),
            Color.FromArgb(0, 0, 0)
        };

        /// <summary>
        ///  Convert a single tile to an image. (Supports 8x8 or 8x16 tiles; 8x16 are treated as
        ///  sprites.)
        /// </summary>
		public unsafe static Bitmap TileToBitmap(IList<byte> data, Color[] palette = null, int flags=0) {
            if (palette == null)
                palette = GrayPalette;

            // Use this as transparent color for sprites
            Color transparentColor = Color.FromArgb(1,1,1);

            bool sprite = (data.Count == 32);
            int height = (sprite ? 16 : 8);
            Bitmap ret;

            ret = new Bitmap(8, height, PixelFormat.Format24bppRgb);

            BitmapData imageData = ret.LockBits(new Rectangle(0, 0, ret.Width, 
                        ret.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            byte* pixels = (byte*)imageData.Scan0.ToPointer();
            int bytesPerPixel = 3;

            bool hflip = (flags&0x20) == 0x20;
            bool vflip = (flags&0x40) == 0x40;

			for (int y = 0; y < height; y++) {
				int b1 = data[y * 2];
				int b2 = data[y * 2 + 1] << 1;
                byte* row;
                if (vflip)
                    row = pixels + (height-1-y)*imageData.Stride;
                else
                    row = pixels + y*imageData.Stride;
				for (int x = 0; x < 8; x++) {
                    int color;

                    color = b1 & 1;
                    color |= b2 & 2;
                    b1>>=1;
                    b2>>=1;

                    int realX;
                    if (hflip)
                        realX = x;
                    else
                        realX = 7-x;

                    Color c = (sprite && color == 0 ? transparentColor : palette[color]);
                    row[realX*bytesPerPixel+0] = c.B;
                    row[realX*bytesPerPixel+1] = c.G;
                    row[realX*bytesPerPixel+2] = c.R;
				}
			}

            ret.UnlockBits(imageData);
            ret.MakeTransparent(transparentColor);
			return ret;
		}

        // These are experiments, dunno if they're really fast at all
        public unsafe static void QuickDraw(BitmapData data, Bitmap src, int x, int y) {
            BitmapData srcData = src.LockBits(new Rectangle(0, 0, src.Width, 
                        src.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            QuickDraw(data, srcData, x, y);
        }
        public unsafe static void QuickDraw(BitmapData data, BitmapData srcData, int x, int y) {
            int bytesPerPixel = 3;

            for (int i=0;i<srcData.Height;i++) {
                byte* dest = (byte*)data.Scan0 + data.Stride*(y+i) + x*bytesPerPixel;
                byte* srcBuf = (byte*)srcData.Scan0 + srcData.Stride*(i);
                for (int j=0;j<(srcData.Width)*bytesPerPixel;j++)
                    dest[j] = srcBuf[j];
            }
        }
    }
}
