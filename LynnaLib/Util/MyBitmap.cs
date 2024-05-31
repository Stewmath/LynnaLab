using System;

namespace LynnaLib
{
    public class MyBitmap : System.IDisposable
    {
        Cairo.ImageSurface surface;

        /// Constructor for blank surface
        public MyBitmap(int width, int height)
        {
            surface = new Cairo.ImageSurface(Cairo.Format.RGB24, width, height);
        }

        /// Constructor from ImageSurface
        public MyBitmap(Cairo.ImageSurface surface)
        {
            this.surface = surface;
        }

        /// Constructor from file
        public MyBitmap(string filename)
        {
            this.surface = new Cairo.ImageSurface(filename);
        }


        public int Width
        {
            get { return surface.Width; }
        }

        public int Height
        {
            get { return surface.Height; }
        }


        public Cairo.Context CreateContext()
        {
            return new Cairo.Context(surface);
        }

        public Cairo.Color GetPixel(int x, int y)
        {
            // Ensure the coordinates are within the surface bounds
            if (x < 0 || x >= surface.Width || y < 0 || y >= surface.Height)
            {
                throw new ArgumentOutOfRangeException("Coordinates are out of bounds.");
            }

            // Get the pixel data
            surface.Flush();
            IntPtr dataPtr = surface.DataPtr;
            int stride = surface.Stride;

            // Calculate the offset for the pixel
            int offset = y * stride + x * 4;

            // Read the pixel data (ARGB format)
            byte b = System.Runtime.InteropServices.Marshal.ReadByte(dataPtr, offset);
            byte g = System.Runtime.InteropServices.Marshal.ReadByte(dataPtr, offset + 1);
            byte r = System.Runtime.InteropServices.Marshal.ReadByte(dataPtr, offset + 2);
            byte a = System.Runtime.InteropServices.Marshal.ReadByte(dataPtr, offset + 3);

            // Convert to Cairo.Color (normalized to 0.0 - 1.0)
            return new Cairo.Color(r / 255.0, g / 255.0, b / 255.0, a / 255.0);
        }

        public System.Drawing.Bitmap ToBitmap()
        {
            var bitmap = new System.Drawing.Bitmap(surface.Width, surface.Height);
            using (var cr = new BitmapContext(bitmap))
            {
                cr.SetSource(surface);
                cr.Paint();
            }
            return bitmap;
        }

        public Cairo.Surface AsSurface()
        {
            return surface;
        }

        public void Dispose()
        {
            if (surface != null)
                surface.Dispose();
            surface = null;
            System.GC.SuppressFinalize(this);
        }

        /// Implicit conversion to Cairo.ImageSurface, should be seamless
        public static implicit operator Cairo.ImageSurface(MyBitmap b)
        {
            return b.surface;
        }

        /// Implicit conversion to System.Drawing.Bitmap. EXPENSIVE, and does
        /// not allow modifying the original surface. Using this as a stopgap
        /// while replacing System.Drawing.Bitmap.
        public static implicit operator System.Drawing.Bitmap(MyBitmap b)
        {
            return b.ToBitmap();
        }


        /// Create a MyBitmap as a copy of a Bitmap. This can't be a constructor
        /// because implicit conversion can cause ambiguity.
        public static MyBitmap FromBitmap(System.Drawing.Bitmap bitmap)
        {
            var myBitmap = new MyBitmap(bitmap.Width, bitmap.Height);
            myBitmap.surface = new Cairo.ImageSurface(Cairo.Format.RGB24,
                                             bitmap.Size.Width,
                                             bitmap.Size.Height);
            using (var source = new BitmapSurface(bitmap))
            using (var cr = new Cairo.Context(myBitmap.surface))
            {
                cr.SetSource(source);
                cr.Paint();
            }

            return myBitmap;
        }
    }
}
