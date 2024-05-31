using System;
using System.Collections.Generic;

namespace LynnaLib
{
    public static class CairoHelper1
    {
        public readonly struct CairoByteColor {
            public readonly byte R;
            public readonly byte G;
            public readonly byte B;
            public readonly byte A;

            public CairoByteColor(byte R, byte G, byte B, byte A)
            {
                this.R = R;
                this.G = G;
                this.B = B;
                this.A = A;
            }
        }

        /// Get a Cairo.Color's components as bytes
        public static CairoByteColor ByteColor(this Cairo.Color c) {
            return new CairoByteColor(
                (byte)(c.R * 255),
                (byte)(c.G * 255),
                (byte)(c.B * 255),
                (byte)(c.A * 255));
        }
    }

    // Implementation of Core.TileDrawer class
    public class CairoTileDrawer : TileDrawer
    {
        Cairo.Context cr;
        int xOffset, yOffset;

        public CairoTileDrawer(Cairo.Context cr, int xOffset = 0, int yOffset = 0)
        {
            this.cr = cr;
            this.xOffset = xOffset;
            this.yOffset = yOffset;
        }

        public override void Draw(MyBitmap bitmap, int x, int y)
        {
            cr.SetSourceSurface(bitmap, x + xOffset, y + yOffset);
            using (Cairo.SurfacePattern pattern = (Cairo.SurfacePattern)cr.GetSource())
            {
                pattern.Filter = Cairo.Filter.Nearest;
            }
            cr.Paint();
        }
    }

    class InvalidBitmapFormatException : Exception
    {
        public InvalidBitmapFormatException(String s) : base(s) { }
    }
}
