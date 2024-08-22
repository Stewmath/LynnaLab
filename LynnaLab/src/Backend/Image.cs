using System;

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
        /// Replaces the contents of the image with the given bitmap.
        /// </summary>
        public abstract void UpdateFromBitmap(LynnaLib.Bitmap bitmap);

        public abstract void Dispose();

        // ================================================================================
        // Private methods
        // ================================================================================
    }
}
