using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using Bitmap = System.Drawing.Bitmap;

namespace LynnaLab
{
    public static class CairoHelper
    {
        /// <summary>
        ///  Makes a copy of a bitmap as a Cairo.ImageSurface. No need to unlock the bitmap later.
        ///  I don't know why, but this seems to have caused segfaults, so don't use it I guess...
        /// </summary>
        public static Cairo.Surface CopyBitmap(System.Drawing.Bitmap bitmap) {
            throw new Exception("This function caused segfaults, don't use it...");
            byte[] data;
            using (Cairo.ImageSurface surface = new BitmapSurface(bitmap)) {
                data = new byte[surface.Height*surface.Stride];
                System.Runtime.InteropServices.Marshal.Copy(surface.DataPtr, data, 0, data.Length);
                return new Cairo.ImageSurface(data, surface.Format, surface.Width, surface.Height, surface.Stride);
            }
        }


        public static Cairo.Color ConvertColor(System.Drawing.Color color) {
            return new Cairo.Color(color.R / 256.0, color.G / 256.0, color.B / 256.0, color.A / 256.0);
        }

        public static Cairo.Color ConvertColor(int r, int g, int b, int a = 256) {
            return new Cairo.Color(r / 256.0, g / 256.0, b / 256.0, a / 256.0);
        }

        public static void DrawText(Cairo.Context cr, string text, int size, Cairo.Rectangle rect) {
            cr.Save();
            cr.Translate(rect.X, rect.Y);
            using (var context = Pango.CairoHelper.CreateContext(cr))
            using (var layout = new Pango.Layout(context)) {
                layout.Width = Pango.Units.FromPixels((int)rect.Width);
                layout.Height = Pango.Units.FromPixels((int)rect.Height);
                layout.Alignment = Pango.Alignment.Center;

                // TODO: install the font on the system
                layout.FontDescription = Pango.FontDescription.FromString("ZeldaOracles " + size);
                //layout.FontDescription.Weight = (Pango.Weight)10000;

                layout.SetText(text);

                // Center vertically
                int pixelWidth, pixelHeight;
                layout.GetPixelSize(out pixelWidth, out pixelHeight);
                cr.Translate(0, ((int)rect.Height - pixelHeight) / 2.0);

                Pango.CairoHelper.ShowLayout(cr, layout);
            }
            cr.Restore();
        }

        public static void DrawText(Cairo.Context cr, string text, int size, int x, int y, int width, int height) {
            DrawText(cr, text, size, new Cairo.Rectangle(x, y, width, height));
        }

        // Draws the outline of a rectangle without drawing anything outside of it. The "lineWidth"
        // is all drawn inside the rectangle instead of being equally outside and inside.
        public static void DrawRectOutline(Cairo.Context cr, double lineWidth, Cairo.Rectangle rect) {
            cr.LineWidth = lineWidth;
            cr.Rectangle(rect.X + lineWidth / 2, rect.Y + lineWidth / 2,
                    rect.Width - lineWidth, rect.Height - lineWidth);
            cr.Stroke();
        }

        public static void DrawRectOutline(Cairo.Context cr, double lineWidth, double x, double y, double width, double height) {
            DrawRectOutline(cr, lineWidth, new Cairo.Rectangle(x, y, width, height));
        }

        public static bool PointInRect(Cairo.Point p, Cairo.Rectangle rect) {
            return p.X >= rect.X && p.Y >= rect.Y
                && p.X < rect.X + rect.Width && p.Y < rect.Y + rect.Height;
        }

        public static bool PointInRect(int x, int y, Cairo.Rectangle rect) {
            return PointInRect(new Cairo.Point(x, y), rect);
        }

        public static bool PointInRect(int x, int y, int left, int top, int width, int height) {
            return PointInRect(new Cairo.Point(x, y), new Cairo.Rectangle(left, top, width, height));
        }

        public static bool RectsOverlap(Cairo.Rectangle rect1, Cairo.Rectangle rect2) {
            double r1 = rect1.X + rect1.Width;
            double r2 = rect2.X + rect2.Width;
            double b1 = rect1.Y + rect1.Height;
            double b2 = rect2.Y + rect2.Height;

            return r1 > rect2.X && r2 > rect1.X
                && b1 > rect2.Y && b2 > rect1.Y;
        }


        // Extension methods

        /// Convert Cairo.Color to Gdk.Color
        public static Gdk.RGBA ToRGBA(this Cairo.Color self) {
            return new Gdk.RGBA { Red = self.R, Green = self.G, Blue = self.B, Alpha = self.A };
        }

        /// Convert Gdk.Color to System.Drawing.Color
        public static System.Drawing.Color ToDrawingColor(this Gdk.RGBA color) {
            return System.Drawing.Color.FromArgb((byte)(color.Red * 256), (byte)(color.Green * 256), (byte)(color.Blue * 256));
        }
    }


    // Cairo.Surface based on a System.Drawing.Bitmap
    public class BitmapSurface : Cairo.ImageSurface {
        Bitmap bitmap;

        public BitmapSurface(Bitmap bitmap)
            : base(LockBitmap(bitmap).Scan0, GetFormat(bitmap), bitmap.Width, bitmap.Height, GetStride(bitmap)) {
            this.bitmap = bitmap;
        }

        protected override void Dispose(bool disposeAll) {
            base.Dispose(disposeAll);
            if (bitmap != null) {
                bitmap.UnlockBits(bitmapDataDict[bitmap]);
                bitmapDataDict.Remove(bitmap);
                bitmap = null;
            }
        }



        // Static stuff

        private static Dictionary<System.Drawing.Bitmap, BitmapData> bitmapDataDict = new Dictionary<System.Drawing.Bitmap, BitmapData>();

        static BitmapData LockBitmap(Bitmap bitmap) {
            if (bitmapDataDict.ContainsKey(bitmap))
                throw new Exception("Tried to lock an already locked bitmap!");
            bitmapDataDict[bitmap] = bitmap.LockBits(
                    new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),
                    ImageLockMode.ReadWrite,
                    bitmap.PixelFormat);
            return bitmapDataDict[bitmap];
        }

        static Cairo.Format GetFormat(Bitmap bitmap) {
            switch(bitmap.PixelFormat) {
            case PixelFormat.Format24bppRgb:
                return Cairo.Format.Rgb24;
            case PixelFormat.Format32bppArgb:
                return Cairo.Format.Argb32;
            default:
                Console.WriteLine(bitmap.PixelFormat);
                throw new InvalidBitmapFormatException("Couldn't convert System.Drawing format \"" + bitmap.PixelFormat + "\" to a Cairo format.");
            }
        }

        static int GetStride(Bitmap bitmap) {
            return Math.Abs(bitmapDataDict[bitmap].Stride);
        }
    }

    // Cairo.Context based on a System.Drawing.Bitmap
    public class BitmapContext : Cairo.Context {
        Bitmap bitmap;

        public BitmapContext(Bitmap bitmap) : base(GetBitmapSurface(bitmap)) {
            this.bitmap = bitmap;
        }

        protected override void Dispose(bool dispose) {
            base.Dispose(dispose);
            if (bitmap != null) {
                surfaceDict[bitmap].Dispose();
                surfaceDict.Remove(bitmap);
                bitmap = null;
            }
        }


        static Dictionary<Bitmap, BitmapSurface> surfaceDict = new Dictionary<Bitmap, BitmapSurface>();

        static BitmapSurface GetBitmapSurface(Bitmap bitmap) {
            if (surfaceDict.ContainsKey(bitmap))
                throw new Exception("Tried to lock an already locked bitmap!");
            surfaceDict[bitmap] = new BitmapSurface(bitmap);
            return surfaceDict[bitmap];
        }
    }

    // Implementation of Core.TileDrawer class
    public class CairoTileDrawer : TileDrawer {
        Cairo.Context cr;
        int xOffset, yOffset;

        public CairoTileDrawer(Cairo.Context cr, int xOffset = 0, int yOffset = 0) {
            this.cr = cr;
            this.xOffset = xOffset;
            this.yOffset = yOffset;
        }

        public override void Draw(Bitmap bitmap, int x, int y) {
            using (Cairo.Surface s = new BitmapSurface(bitmap)) {
                cr.SetSourceSurface(s, x + xOffset, y + yOffset);
                using (Cairo.SurfacePattern pattern = (Cairo.SurfacePattern)cr.GetSource()) {
                    pattern.Filter = Cairo.Filter.Nearest;
                }
                cr.Paint();
            }
        }
    }

    class InvalidBitmapFormatException : Exception {
        public InvalidBitmapFormatException(String s) : base(s) {}
    }
}
