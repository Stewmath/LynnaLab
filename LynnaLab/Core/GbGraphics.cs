using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
	public class GbGraphics {
        static readonly Color[] standardPalette = {
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(198, 198, 198),
            Color.FromArgb(100, 100, 100),
            Color.FromArgb(0, 0, 0)
        };

		public static Bitmap TileToImage(IList<byte> data, Color[] palette = null, int flags=0) {
            if (palette == null)
                palette = standardPalette;

			Bitmap ret = new Bitmap(8, 8);
			Graphics g = Graphics.FromImage(ret);

			for (int y = 0; y < 8; y++) {
				int b1 = data[y * 2];
				int b2 = data[y * 2 + 1];
				for (int x = 0; x < 8; x++) {
                    int color;
                    if ((flags & 0x20) == 0x20) // Horizontal flip
                    {
                        color = (b1 >> x) & 1;
                        color |= ((b2 >> x) & 1) << 1;
                    }
                    else {
                        color = (b1 >> (7-x)) & 1;
                        color |= ((b2 >> (7-x)) & 1) << 1;
                    }
                    if ((flags & 0x40) == 0x40) // Vertical flip
                        g.FillRectangle(new SolidBrush(palette[color]), x, (7-y), 1, 1);
                    else
                        g.FillRectangle(new SolidBrush(palette[color]), x, y, 1, 1);
				}
			}

			return ret;
		}
    }
}
