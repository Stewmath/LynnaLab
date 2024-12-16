using Veldrid;

using Interpolation = LynnaLab.Interpolation;
using Image = LynnaLab.Image;
using ImageModifiedEventArgs = LynnaLab.ImageModifiedEventArgs;

namespace VeldridBackend;

// TODO: Should this implement IDisposable?
public class VeldridImage : Image
{
    private VeldridImage(ImGuiController controller, Interpolation interpolation)
    {
        this.controller = controller;
        this.gd = controller.GraphicsDevice;
        this.interpolation = interpolation;
    }

    /// <summary>
    /// Image from bitmap
    /// </summary>
    public VeldridImage(ImGuiController controller, Interpolation interpolation, Bitmap bitmap, float alpha)
        : this(controller, interpolation)
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
        this.Alpha = alpha;

        OnBitmapModified(bitmap);

        var modifiedEventHandler = () => OnBitmapModified(bitmap);
        bitmap.ModifiedEvent += modifiedEventHandler;
        this.unsubscribeFromBitmapChanges = () => bitmap.ModifiedEvent -= modifiedEventHandler;
    }

    /// <summary>
    /// Blank image
    /// </summary>
    public VeldridImage(ImGuiController controller, Interpolation interpolation, int width, int height, float alpha)
        : this(controller, interpolation)
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
        this.Alpha = alpha;
    }

    /// <summary>
    /// Reference to another image with different interpolation/alpha
    /// </summary>
    public VeldridImage(VeldridImage image, Interpolation interpolation, float newAlpha)
    {
        this.controller = image.controller;
        this.gd = image.gd;
        this.texture = image.texture;
        this.interpolation = interpolation;
        this.width = image.width;
        this.height = image.height;
        this.Alpha = newAlpha;
    }

    // ================================================================================
    // Variables
    // ================================================================================
    ImGuiController controller;
    GraphicsDevice gd;
    Texture texture; // May be shared with other Image instances (see constructor)
    Interpolation interpolation;

    int width, height;

    Action unsubscribeFromBitmapChanges = null;

    // ================================================================================
    // Properties
    // ================================================================================
    public override int Width { get { return width; } }
    public override int Height { get { return height; } }
    public override Interpolation Interpolation { get { return interpolation; } }

    public Texture Texture { get { return texture; } }
    public float Alpha { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    // Draw command functions (return a handle usable with ImGui.Image())
    public override IntPtr GetBinding(Interpolation? interpolation, float? alpha)
    {
        return controller.GetOrCreateImGuiBinding(this, interpolation, alpha);
    }

    public override void DrawOn(Image _destImage, Point srcPos, Point destPos, Point size)
    {
        VeldridImage destImage = (VeldridImage)_destImage;
        var cl = controller.Backend.CommandList;

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

        destImage.modifiedEvent.Invoke(destImage, new ImageModifiedEventArgs { });
    }

    public override void SetInterpolation(Interpolation interpolation)
    {
        this.interpolation = interpolation;
    }

    public override void Dispose()
    {
        controller.UnbindImage(this);
        texture.Dispose();
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

        modifiedEvent.Invoke(this, new ImageModifiedEventArgs { });
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
