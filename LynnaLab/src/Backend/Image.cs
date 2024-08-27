using System;

using Point = Cairo.Point;

namespace LynnaLab
{
    /// <summary>
    /// An Image is used by the graphics backend (Veldrid) to render stuff with Imgui.
    ///
    /// Contrast with LynnaLib.Bitmap which is a Cairo surface used within LynnaLib. This is
    /// converted to an Image through IBackend.ImageFromBitmap so that it can be rendered to the
    /// screen.
    /// </summary>
    public abstract class Image : IDisposable
    {
        // ================================================================================
        // Variables
        // ================================================================================

        // Invoked when the image is modified
        public event Action<ImageModifiedEventArgs> ModifiedEvent;

        // ================================================================================
        // Properties
        // ================================================================================
        public abstract int Width { get; }
        public abstract int Height { get; }

        // ================================================================================
        // Public methods
        // ================================================================================

        /// <summary>
        /// Returns an image binding usable with ImGui.Image().
        /// </summary>
        public abstract IntPtr GetBinding();

        /// <summary>
        /// Draw this image onto another image
        /// </summary>
        public abstract void DrawOn(Image destImage, Point srcPos, Point destPos, Point size);

        /// <summary>
        /// Sets the interpolation mode for the image.
        /// </summary>
        public abstract void SetInterpolation(Interpolation interpolation);

        /// <summary>
        /// Replaces the contents of the image with the given bitmap.
        /// </summary>
        //public abstract void UpdateFromBitmap(LynnaLib.Bitmap bitmap);

        public abstract void Dispose();

        // ================================================================================
        // Protected methods
        // ================================================================================

        /// <summary>
        /// This function allows child classes to invoke the event
        /// </summary>
        protected void InvokeModifiedEvent(ImageModifiedEventArgs args)
        {
            ModifiedEvent?.Invoke(args);
        }
    }

    // stub in case we need it later
    public struct ImageModifiedEventArgs {}
}
