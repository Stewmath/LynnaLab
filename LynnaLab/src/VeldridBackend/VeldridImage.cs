using System;
using Veldrid;

namespace VeldridBackend
{
    public class VeldridImage : LynnaLab.Image
    {
        public VeldridImage(ImGuiController controller, LynnaLib.Bitmap bitmap)
        {
            this.controller = controller;
            this.bitmap = bitmap;
            this.gd = controller.GraphicsDevice;

            Cairo.ImageSurface surface = (Cairo.ImageSurface)bitmap;

            PixelFormat pixelFormat;
            uint sizeInBytes;

            switch (surface.Format)
            {
                case Cairo.Format.ARGB32:
                    pixelFormat = PixelFormat.B8_G8_R8_A8_UNorm;
                    sizeInBytes = (uint)(4 * bitmap.Height * bitmap.Width);
                    break;
                default:
                    throw new Exception("Couldn't convert Cairo image format: " + surface.Format);
            }

            TextureDescription textureDescription = TextureDescription.Texture2D(
                (uint)bitmap.Width,
                (uint)bitmap.Height,
                mipLevels: 1,
                arrayLayers: 1,
                pixelFormat,
                TextureUsage.Sampled);

            this.texture = gd.ResourceFactory.CreateTexture(ref textureDescription);

            surface.Flush();
            IntPtr pixelData = surface.DataPtr;

            // Update the texture with the pixel data
            gd.UpdateTexture(texture, pixelData, sizeInBytes, 0, 0, 0,
                              (uint)bitmap.Width, (uint)bitmap.Height, 1, 0, 0);
        }

        // ================================================================================
        // Variables
        // ================================================================================
        ImGuiController controller;
        GraphicsDevice gd;
        LynnaLib.Bitmap bitmap;
        Texture texture;

        // ================================================================================
        // Properties
        // ================================================================================
        public override int Width { get { return bitmap.Width; } }
        public override int Height { get { return bitmap.Height; } }

        // ================================================================================
        // Public methods
        // ================================================================================

        public override IntPtr GetBinding()
        {
            return controller.GetOrCreateImGuiBinding(gd.ResourceFactory, texture);
        }

        public override void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }

        // ================================================================================
        // Private methods
        // ================================================================================
    }
}
