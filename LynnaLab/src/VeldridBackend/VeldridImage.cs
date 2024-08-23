using System;
using Veldrid;

using Bitmap = LynnaLib.Bitmap;
using Image = LynnaLab.Image;
using Point = Cairo.Point;

namespace VeldridBackend
{
    public class VeldridImage : Image
    {
        private VeldridImage(ImGuiController controller)
        {
            this.controller = controller;
            this.gd = controller.GraphicsDevice;
        }

        public VeldridImage(ImGuiController controller, Bitmap bitmap)
            : this(controller)
        {
            var (sizeInBytes, pixelFormat) = GetBitmapFormat(bitmap);

            TextureDescription textureDescription = TextureDescription.Texture2D(
                (uint)bitmap.Width,
                (uint)bitmap.Height,
                mipLevels: 1,
                arrayLayers: 1,
                pixelFormat,
                TextureUsage.Sampled);

            this.texture = gd.ResourceFactory.CreateTexture(ref textureDescription);

            this.width = bitmap.Width;
            this.height = bitmap.Height;

            OnBitmapModified(bitmap);

            var modifiedEventHandler = () => OnBitmapModified(bitmap);
            bitmap.ModifiedEvent += modifiedEventHandler;
            this.unsubscribeFromBitmapChanges = () => bitmap.ModifiedEvent -= modifiedEventHandler;
        }

        public VeldridImage(ImGuiController controller, int width, int height)
            : this(controller)
        {
            var pixelFormat = PixelFormat.B8_G8_R8_A8_UNorm;

            TextureDescription textureDescription = TextureDescription.Texture2D(
                (uint)width,
                (uint)height,
                mipLevels: 1,
                arrayLayers: 1,
                pixelFormat,
                TextureUsage.Sampled);

            this.texture = gd.ResourceFactory.CreateTexture(ref textureDescription);

            this.width = width;
            this.height = height;
        }

        // ================================================================================
        // Variables
        // ================================================================================
        ImGuiController controller;
        GraphicsDevice gd;
        Texture texture;
        int width, height;
        Action unsubscribeFromBitmapChanges = null;

        // ================================================================================
        // Properties
        // ================================================================================
        public override int Width { get { return width; } }
        public override int Height { get { return height; } }

        // ================================================================================
        // Public methods
        // ================================================================================

        public override void DrawOn(Image _destImage, Point srcPos, Point destPos, Point size)
        {
            VeldridImage destImage = (VeldridImage)_destImage;
            var cl = gd.ResourceFactory.CreateCommandList();

            // Create a framebuffer for the destination texture
            FramebufferDescription fbDesc = new FramebufferDescription(null, destImage.texture);
            var framebuffer = gd.ResourceFactory.CreateFramebuffer(fbDesc);

            cl.Begin();
            cl.SetFramebuffer(framebuffer);
            //cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));

            cl.CopyTexture(texture,
                           (uint)srcPos.X, (uint)srcPos.Y, 0,
                           0,
                           0,
                           destImage.texture,
                           (uint)destPos.X, (uint)destPos.Y, 0,
                           0,
                           0,
                           (uint)size.X, (uint)size.Y, 1,
                           1);

            cl.End();
            gd.SubmitCommands(cl);

            cl.Dispose();
            framebuffer.Dispose();
        }

        public override IntPtr GetBinding()
        {
            return controller.GetOrCreateImGuiBinding(gd.ResourceFactory, texture);
        }

        public override void Dispose()
        {
            texture?.Dispose();
            texture = null;

            if (unsubscribeFromBitmapChanges != null)
                unsubscribeFromBitmapChanges();
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        void OnBitmapModified(Bitmap bitmap)
        {
            Cairo.ImageSurface surface = (Cairo.ImageSurface)bitmap;

            surface.Flush();
            IntPtr pixelData = surface.DataPtr;
            var (sizeInBytes, _) = GetBitmapFormat(bitmap);

            // Update the texture with the pixel data
            gd.UpdateTexture(texture, pixelData, sizeInBytes, 0, 0, 0,
                              (uint)bitmap.Width, (uint)bitmap.Height, 1, 0, 0);
        }

        (uint, PixelFormat) GetBitmapFormat(Bitmap bitmap)
        {
            Cairo.ImageSurface surface = (Cairo.ImageSurface)bitmap;

            uint sizeInBytes;
            PixelFormat pixelFormat;

            switch (surface.Format)
            {
                case Cairo.Format.ARGB32:
                    pixelFormat = PixelFormat.B8_G8_R8_A8_UNorm;
                    sizeInBytes = (uint)(4 * bitmap.Height * bitmap.Width);
                    break;
                default:
                    throw new Exception("Couldn't convert Cairo image format: " + surface.Format);
            }

            return (sizeInBytes, pixelFormat);
        }
    }
}
