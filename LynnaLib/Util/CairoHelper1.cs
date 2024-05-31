using System;
using System.Collections.Generic;

namespace LynnaLib
{
    // Implementation of Core.TileDrawer class (TODO: Delete this)
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

        public override void Draw(Bitmap bitmap, int x, int y)
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
