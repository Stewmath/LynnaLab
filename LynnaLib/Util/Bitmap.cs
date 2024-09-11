using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LynnaLib
{
    /// Thin wrapper over Cairo.ImageSurface. Called "Bitmap" due to baggage
    /// from using the deprecated System.Drawing.Common.Bitmap.
    public class Bitmap : System.IDisposable
    {
        /// Constructor for blank surface
        public Bitmap(int width, int height, Cairo.Format format = Cairo.Format.Argb32)
        {
            surface = new Cairo.ImageSurface(format, width, height);
        }

        /// Constructor from pixel data.
        /// Input "pixels" array must be unmanaged memory. Will be disposed of by this class when
        /// finished with it.
        public Bitmap(IntPtr pixels, Cairo.Format format, int width, int height, int stride)
        {
            this.surface = new Cairo.ImageSurface((IntPtr)pixels, format, width, height, stride);
            pixelsPointer = pixels;
        }

        /// Constructor from file
        public Bitmap(string filename)
        {
            this.surface = new Cairo.ImageSurface(filename);
        }

        ~Bitmap()
        {
            Dispose(false);
        }


        Cairo.ImageSurface surface;
        IntPtr pixelsPointer;

        public event Action ModifiedEvent;
        public event Action<object> DisposedEvent;


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

        public unsafe Color GetPixel(int x, int y)
        {
            // Ensure the coordinates are within the surface bounds
            if (x < 0 || x >= surface.Width || y < 0 || y >= surface.Height)
            {
                throw new ArgumentOutOfRangeException("Coordinates are out of bounds.");
            }

            // Get the pixel data
            surface.Flush();

            byte* data = (byte*)surface.DataPtr;
            int stride = surface.Stride;

            if (surface.Format == Cairo.Format.Argb32)
            {
                // Calculate the offset for the pixel
                int offset = y * stride + x * 4;

                // Read the pixel data (ARGB format)
                byte b = data[offset];
                byte g = data[offset + 1];
                byte r = data[offset + 2];
                byte a = data[offset + 3];

                // Convert to Color
                return Color.FromRgba(r, g, b, a);
            }
            else if (surface.Format == Cairo.Format.Rgb24)
            {
                // Calculate the offset for the pixel
                int offset = y * stride + x * 4;

                Debug.Assert(offset + 2 < stride * surface.Height);

                // Read the pixel data (ARGB format)
                byte b = data[offset];
                byte g = data[offset + 1];
                byte r = data[offset + 2];

                // Convert to Color
                return Color.FromRgb(r, g, b);
            }
            else
            {
                throw new Exception("Unsupported Cairo.Surface format: " + surface.Format);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            DisposedEvent?.Invoke(this);
            GC.SuppressFinalize(this);
        }

        public virtual void Dispose(bool disposing)
        {
            if (surface == null)
                return;

            if (disposing)
            {
                if (surface != null)
                    surface.Dispose();
                surface = null;
            }

            if (pixelsPointer != 0)
            {
                Marshal.FreeHGlobal(pixelsPointer);
                pixelsPointer = 0;
            }
        }

        // Should call this after modifying the surface, otherwise ImGui may not receive the update
        public void MarkModified()
        {
            if (ModifiedEvent != null)
                ModifiedEvent();
        }

        /// Implicit conversion to Cairo.ImageSurface, should be seamless
        public static implicit operator Cairo.ImageSurface(Bitmap b)
        {
            return b.surface;
        }
    }
}
