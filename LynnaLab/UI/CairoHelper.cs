using System;
using System.Collections.Generic;
using System.Drawing.Imaging;

namespace LynnaLab
{
    public class CairoHelper
    {
        private static Dictionary<System.Drawing.Bitmap, BitmapData> bitmapDataDict = new Dictionary<System.Drawing.Bitmap, BitmapData>();

        /// <summary>
        ///  Constructs a Cairo.ImageSurface that uses a bitmap's data. Must call UnlockBitmap later to free it up.
        ///  Doesn't support all PixelFormats.
        /// </summary>
        /// <returns>The bitmap.</returns>
        /// <param name="bitmap">Bitmap.</param>
        public static Cairo.ImageSurface LockBitmap(System.Drawing.Bitmap bitmap) {
            Cairo.Format format;

            switch(bitmap.PixelFormat) {
            case PixelFormat.Format24bppRgb:
                format = Cairo.Format.Rgb24;
                break;
            case PixelFormat.Format32bppArgb:
                format = Cairo.Format.Argb32;
                break;
            default:
                Console.WriteLine(bitmap.PixelFormat);
                throw new InvalidBitmapFormatException("Couldn't convert System.Drawing format \"" + bitmap.PixelFormat + "\" to a Cairo format.");
            }

            BitmapData bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),
                                                 ImageLockMode.ReadOnly,
                                                 bitmap.PixelFormat);
            bitmapDataDict[bitmap] = bmpData;
            int stride = Math.Abs(bmpData.Stride);
            var ret = new Cairo.ImageSurface(bmpData.Scan0, format, bitmap.Width, bitmap.Height, stride);
            return ret;
        }

        /// <summary>
        ///  Caller still needs to call "Dispose" on the Surface after calling this.
        /// </summary>
        /// <param name="bitmap">Bitmap.</param>
        public static void UnlockBitmap(System.Drawing.Bitmap bitmap) {
            BitmapData data = bitmapDataDict[bitmap];
            bitmapDataDict.Remove(bitmap);
            bitmap.UnlockBits(data);
        }

        /// <summary>
        ///  Makes a copy of a bitmap as a Cairo.ImageSurface. No need to unlock the bitmap later.
        /// </summary>
        public static Cairo.Surface CopyBitmap(System.Drawing.Bitmap bitmap) {
            Cairo.ImageSurface surface = LockBitmap(bitmap);
            byte[] data = new byte[surface.Height*surface.Stride];
            System.Runtime.InteropServices.Marshal.Copy(surface.DataPtr, data, 0, data.Length);
            UnlockBitmap(bitmap);
            return new Cairo.ImageSurface(data, surface.Format, surface.Width, surface.Height, surface.Stride);
        }


        public static Cairo.Color ConvertColor(System.Drawing.Color color) {
            return new Cairo.Color(color.R / 256.0, color.G / 256.0, color.B / 256.0, color.A / 256.0);
        }
    }

    class InvalidBitmapFormatException : Exception {
        public InvalidBitmapFormatException(String s) : base(s) {}
    }
}
