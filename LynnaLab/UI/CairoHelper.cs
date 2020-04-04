using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using Bitmap = System.Drawing.Bitmap;

// NOTE: Classes in this file are used by "Core/ObjectAnimationFrame.cs" which breaks the Core/UI
// separation. Should probably move the relevant Core code into the UI folder.
namespace LynnaLab
{
    public class CairoHelper
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

    class InvalidBitmapFormatException : Exception {
        public InvalidBitmapFormatException(String s) : base(s) {}
    }
}
