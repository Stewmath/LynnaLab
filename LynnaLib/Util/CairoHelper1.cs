using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using Bitmap = System.Drawing.Bitmap;

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

        public static Cairo.Color ConvertColor(System.Drawing.Color color)
        {
            return new Cairo.Color(color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0);
        }

        public static Cairo.Color ConvertColor(int r, int g, int b, int a = 256)
        {
            return new Cairo.Color(r / 255.0, g / 255.0, b / 255.0, a / 255.0);
        }

    }

    // Cairo.Surface based on a System.Drawing.Bitmap
    public class BitmapSurface : Cairo.ImageSurface
    {
        Bitmap bitmap;

        public BitmapSurface(Bitmap bitmap)
            : base(LockBitmap(bitmap).Scan0, GetFormat(bitmap), bitmap.Width, bitmap.Height, GetStride(bitmap))
        {
            this.bitmap = bitmap;
        }

        protected override void Dispose(bool disposeAll)
        {
            base.Dispose(disposeAll);
            if (bitmap != null)
            {
                bitmap.UnlockBits(bitmapDataDict[bitmap]);
                bitmapDataDict.Remove(bitmap);
                bitmap = null;
            }
        }



        // Static stuff

        private static Dictionary<System.Drawing.Bitmap, BitmapData> bitmapDataDict = new Dictionary<System.Drawing.Bitmap, BitmapData>();

        static BitmapData LockBitmap(Bitmap bitmap)
        {
            if (bitmapDataDict.ContainsKey(bitmap))
                throw new Exception("Tried to lock an already locked bitmap!");
            bitmapDataDict[bitmap] = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadWrite,
                    bitmap.PixelFormat);
            return bitmapDataDict[bitmap];
        }

        static Cairo.Format GetFormat(Bitmap bitmap)
        {
            switch (bitmap.PixelFormat)
            {
                case PixelFormat.Format24bppRgb:
                    return Cairo.Format.Rgb24;
                case PixelFormat.Format32bppArgb:
                    return Cairo.Format.Argb32;
                default:
                    Console.WriteLine(bitmap.PixelFormat);
                    throw new InvalidBitmapFormatException("Couldn't convert System.Drawing format \"" + bitmap.PixelFormat + "\" to a Cairo format.");
            }
        }

        static int GetStride(Bitmap bitmap)
        {
            return Math.Abs(bitmapDataDict[bitmap].Stride);
        }
    }

    // Cairo.Context based on a System.Drawing.Bitmap
    public class BitmapContext : Cairo.Context
    {
        Bitmap bitmap;

        public BitmapContext(Bitmap bitmap) : base(GetBitmapSurface(bitmap))
        {
            this.bitmap = bitmap;
        }

        protected override void Dispose(bool dispose)
        {
            base.Dispose(dispose);
            if (bitmap != null)
            {
                surfaceDict[bitmap].Dispose();
                surfaceDict.Remove(bitmap);
                bitmap = null;
            }
        }


        static Dictionary<Bitmap, BitmapSurface> surfaceDict = new Dictionary<Bitmap, BitmapSurface>();

        static BitmapSurface GetBitmapSurface(Bitmap bitmap)
        {
            if (surfaceDict.ContainsKey(bitmap))
                throw new Exception("Tried to lock an already locked bitmap!");
            surfaceDict[bitmap] = new BitmapSurface(bitmap);
            return surfaceDict[bitmap];
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
