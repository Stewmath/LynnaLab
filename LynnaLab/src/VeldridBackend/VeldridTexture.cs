using Veldrid;

using Point = Util.Point; // Don't care about Veldrid.Point

namespace VeldridBackend;

/// <summary>
/// A Texture is used by the graphics backend (Veldrid) to render stuff with Imgui. It is stored on
/// the GPU so can be drawn efficiently.
///
/// Contrast with LynnaLib.Bitmap which is an image used within LynnaLib rendered on the cpu side.
/// This is converted to a Texture through Backend.TextureFromBitmap so that it can be rendered to
/// the screen.
/// </summary>
public abstract class VeldridTextureBase : IDisposable
{
    public VeldridTextureBase(ImGuiController controller)
    {
        this.controller = controller;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    protected readonly ImGuiController controller;

    int modifiedEventLocked;
    bool modifiedEventInvoked;

    // ================================================================================
    // Events
    // ================================================================================

    public event EventHandler<EventArgs> DisposedEvent;

    event EventHandler<TextureModifiedEventArgs> modifiedEvent;

    // ================================================================================
    // Properties
    // ================================================================================
    public abstract int Width { get; }
    public abstract int Height { get; }

    /// <summary>
    /// Invoked when the texture is modified
    /// </summary>
    public event EventHandler<TextureModifiedEventArgs> ModifiedEvent
    {
        add
        {
            modifiedEvent += value;
        }
        remove
        {
            modifiedEvent -= value;
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Returns an image binding usable with ImGui.Image().
    /// </summary>
    public IntPtr GetBinding()
    {
        return controller.GetOrCreateImGuiBinding(this);
    }


    public virtual void Dispose()
    {
        controller.UnbindTexture(this);
    }


    /// <summary>
    /// Call this to inhibit modifiedEvent invocations until EndAtomicOperation() is invoked.
    /// </summary>
    public void BeginAtomicOperation()
    {
        modifiedEventLocked++;
    }

    public void EndAtomicOperation()
    {
        modifiedEventLocked--;
        if (modifiedEventLocked == 0 && modifiedEventInvoked)
        {
            modifiedEventInvoked = false;
            modifiedEvent?.Invoke(this, new TextureModifiedEventArgs {});
        }
    }

    public void InvokeModifiedHandler()
    {
        if (modifiedEventLocked == 0)
        {
            modifiedEvent?.Invoke(this, new TextureModifiedEventArgs { });
        }
        else
        {
            modifiedEventInvoked = true;
        }
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected void InvokeDisposedHandler()
    {
        DisposedEvent?.Invoke(this, null);
    }
}

// stub in case we need it later
public struct TextureModifiedEventArgs
{
}

// Not to be confused with Veldrid.Texture.
public class VeldridRgbaTexture : VeldridTextureBase
{
    private VeldridRgbaTexture(ImGuiController controller)
        : base(controller)
    {
        this.gd = controller.GraphicsDevice;
    }

    /// <summary>
    /// Texture from bitmap (changes to the bitmap are tracked)
    /// </summary>
    public VeldridRgbaTexture(ImGuiController controller, Bitmap bitmap)
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
        var disposedEventHandler = (object _) => OnBitmapDisposed(bitmap);

        bitmap.ModifiedEvent += modifiedEventHandler;
        bitmap.DisposedEvent += disposedEventHandler;

        this.unsubscribeFromBitmapChanges = () =>
        {
            bitmap.ModifiedEvent -= modifiedEventHandler;
            bitmap.DisposedEvent -= disposedEventHandler;
        };
    }

    /// <summary>
    /// Blank texture
    /// </summary>
    public VeldridRgbaTexture(ImGuiController controller, int width, int height, bool renderTarget=false)
        : this(controller)
    {
        var pixelFormat = PixelFormat.R8_G8_B8_A8_UNorm;

        TextureDescription textureDescription = TextureDescription.Texture2D(
            (uint)width,
            (uint)height,
            mipLevels: 1,
            arrayLayers: 1,
            pixelFormat,
            TextureUsage.Sampled | (renderTarget ? TextureUsage.RenderTarget : 0));

        this.texture = gd.ResourceFactory.CreateTexture(ref textureDescription);

        this.width = width;
        this.height = height;
    }

    // ================================================================================
    // Variables
    // ================================================================================
    GraphicsDevice gd;
    Veldrid.Texture texture;
    Framebuffer framebuffer;

    int width, height;

    Action unsubscribeFromBitmapChanges = null;

    // ================================================================================
    // Properties
    // ================================================================================
    public override int Width { get { return width; } }
    public override int Height { get { return height; } }

    internal Veldrid.Texture Texture { get { return texture; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void DrawFrom(VeldridTextureBase srcTexture, Point srcPos, Point destPos, Point size)
    {
        (srcTexture as VeldridRgbaTexture).DrawOn(this, srcPos, destPos, size);
    }

    public void DrawOn(VeldridRgbaTexture destTexture, Point srcPos, Point destPos, Point size)
    {
        var cl = controller.Backend.CommandList;

        cl.CopyTexture(Texture,
                       (uint)srcPos.X, (uint)srcPos.Y, 0,
                       0,
                       0,
                       destTexture.Texture,
                       (uint)destPos.X, (uint)destPos.Y, 0,
                       0,
                       0,
                       (uint)size.X, (uint)size.Y, 1,
                       1);

        destTexture.InvokeModifiedHandler();
    }

    public override void Dispose()
    {
        base.Dispose();

        Debug.Assert(texture != null);

        if (framebuffer != null)
        {
            gd.DisposeWhenIdle(framebuffer);
            framebuffer = null;
        }

        gd.DisposeWhenIdle(texture);
        texture = null;

        if (unsubscribeFromBitmapChanges != null)
        {
            unsubscribeFromBitmapChanges();
            unsubscribeFromBitmapChanges = null;
        }

        InvokeDisposedHandler();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void OnBitmapModified(Bitmap bitmap)
    {
        IntPtr pixelData = bitmap.Lock();
        var (sizeInBytes, _) = GetBitmapFormat(bitmap);

        // Update the texture with the pixel data
        gd.UpdateTexture(texture, pixelData, sizeInBytes, 0, 0, 0,
                         (uint)bitmap.Width, (uint)bitmap.Height, 1, 0, 0);

        bitmap.Unlock();
        base.InvokeModifiedHandler();
    }

    void OnBitmapDisposed(Bitmap bitmap)
    {
        unsubscribeFromBitmapChanges();
        unsubscribeFromBitmapChanges = null;
    }

    (uint, PixelFormat) GetBitmapFormat(Bitmap bitmap)
    {
        // All bitmaps use this format
        uint sizeInBytes = (uint)(4 * bitmap.Height * bitmap.Width);
        PixelFormat pixelFormat = PixelFormat.R8_G8_B8_A8_UNorm;

        return (sizeInBytes, pixelFormat);
    }

    internal Veldrid.Framebuffer GetFramebuffer()
    {
        if (framebuffer != null)
            return framebuffer;

        FramebufferDescription desc = new FramebufferDescription(
            null,
            new Texture[] { Texture }
        );
        framebuffer = controller.GraphicsDevice.ResourceFactory.CreateFramebuffer(desc);
        return framebuffer;
    }
}

/// <summary>
/// A read-only texture that references a portion of an RgbaTexture.
/// </summary>
public class VeldridTextureWindow : VeldridTextureBase
{
    public VeldridTextureWindow(ImGuiController controller, VeldridRgbaTexture source, Point topLeft, Point size)
        : base(controller)
    {
        this.TopLeft = topLeft;
        this.Size = size;

        this.Texture = source.Texture;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================

    public override int Width { get { return Size.X; } }
    public override int Height { get { return Size.Y; } }

    internal Texture Texture { get; private set; }
    internal Point TopLeft { get; private set; }
    internal Point Size { get; private set; }

    public override void Dispose()
    {
        base.Dispose();
        // this.Texture is not owned by us, so nothing more to do
    }
}

/// <summary>
/// Represents a palette stored on the GPU side.
/// </summary>
public class VeldridPalette : IDisposable
{
    public VeldridPalette(ImGuiController controller, Color[] palette, int transparentIndex)
    {
        this.controller = controller;

        SetPalette(palette, transparentIndex);
    }

    ImGuiController controller;

    public Texture PaletteTexture { get; private set; }
    GraphicsDevice gd { get { return controller.GraphicsDevice; } }

    public unsafe void SetPalette(Color[] palette, int transparentIndex)
    {
        TextureDescription textureDescription = TextureDescription.Texture2D(
            (uint)palette.Length,
            (uint)1,
            mipLevels: 1,
            arrayLayers: 1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled);
        this.PaletteTexture = gd.ResourceFactory.CreateTexture(ref textureDescription);

        // The palette is a 1D texture where each pixel is a color in the palette
        byte[] paletteData = new byte[palette.Length * 4];
        for (int i=0; i<palette.Length; i++)
        {
            paletteData[i*4 + 0] = (byte)palette[i].R;
            paletteData[i*4 + 1] = (byte)palette[i].G;
            paletteData[i*4 + 2] = (byte)palette[i].B;

            if (transparentIndex == i)
                paletteData[i*4 + 3] = 0;
            else
                paletteData[i*4 + 3] = (byte)palette[i].A;
        }

        fixed (byte* ptr = paletteData)
        {
            // Update the texture with the pixel data
            gd.UpdateTexture(PaletteTexture, (nint)ptr, (uint)paletteData.Length, 0, 0, 0,
                             (uint)palette.Length, 1, 1, 0, 0);
        }
    }

    public void Dispose()
    {
        PaletteTexture.Dispose();
        PaletteTexture = null;
    }
}
