using System;

namespace LynnaLib
{
    /// Thin wrapper over Cairo.ImageSurface. Called "Bitmap" due to baggage
    /// from using the deprecated System.Drawing.Common.Bitmap.
    public class Bitmap : System.IDisposable
    {
        Cairo.ImageSurface surface;

        /// Constructor for blank surface
        public Bitmap(int width, int height, Cairo.Format format = Cairo.Format.Argb32)
        {
            surface = new Cairo.ImageSurface(format, width, height);
        }

        /// Constructor from ImageSurface
        /// Implicit type conversion also makes this work as a "Bitmap(Bitmap)" constructor
        public Bitmap(Cairo.ImageSurface surface)
        {
            // I ran into some bizarre corruption issues when this constructor used to just set
            // this.surface to the parameter of this function. We need to make a copy of it to avoid
            // issues for some reason. Caller must dispose the surface it passes in.
            this.surface = new Cairo.ImageSurface(surface.Format, surface.Width, surface.Height);
            using (Cairo.Context cr = new Cairo.Context(this.surface))
            {
                cr.SetSourceSurface(surface, 0, 0);
                cr.Paint();
            }
        }

        /// Constructor from file
        public Bitmap(string filename)
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

        public Color GetPixel(int x, int y)
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

            // Convert to Color
            return Color.FromRgba(r, g, b, a);
        }

        public void Dispose()
        {
            if (surface != null)
                surface.Dispose();
            surface = null;
            System.GC.SuppressFinalize(this);
        }

        /// Implicit conversion to Cairo.ImageSurface, should be seamless
        public static implicit operator Cairo.ImageSurface(Bitmap b)
        {
            return b.surface;
        }
    }
}
