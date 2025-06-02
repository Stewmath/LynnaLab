using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using PixelFormats = SixLabors.ImageSharp.PixelFormats;

namespace LynnaLib;

/// <summary>
/// Represents an image. The underlying type has shifted over time, from System.Drawing.Bitmap
/// (now deprecated), to Cairo.ImageSurface, and now finally ImageSharp.Image.
///
/// Contrast with Textures, which are used for gpu-based rendering (this is cpu-based).
/// </summary>
public class Bitmap : System.IDisposable
{
    static Bitmap()
    {
        // Set this ImageSharp option to make it possible for us to access an image's raw pixel
        // buffers in most cases. This is needed to be able to convert our images to Veldrid
        // textures efficiently.
        // The ImageSharp docs recommend against doing this because it makes large image handling
        // less efficient. But that's not really a concern for LynnaLab.
        // See: https://docs.sixlabors.com/articles/imagesharp/memorymanagement.html
        Configuration.Default.PreferContiguousImageBuffers = true;
    }

    // ================================================================================
    // Constructors
    // ================================================================================

    /// <summary>
    /// Constructor for blank surface
    /// </summary>
    public Bitmap(int width, int height)
    {
        image = new(width, height);
    }

    /// <summary>
    /// Constructor from file
    /// </summary>
    public Bitmap(string filename)
    {
        image = Image.Load<PixelFormats.Rgba32>(filename);
    }

    public Bitmap(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            throw new Exception("Bitmap: Empty data received");
        image = Image.Load<PixelFormats.Rgba32>(data);
    }

    ~Bitmap()
    {
        Dispose(false);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Image<PixelFormats.Rgba32> image;
    System.Buffers.MemoryHandle memoryHandle;
    int lockCount;

    public event Action ModifiedEvent;
    public event Action<object> DisposedEvent;

    // ================================================================================
    // Properties
    // ================================================================================

    public int Width
    {
        get { return image.Width; }
    }

    public int Height
    {
        get { return image.Height; }
    }

    public unsafe int Stride
    {
        get { return sizeof(PixelFormats.Rgba32) * Width; }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public unsafe Color GetPixel(int x, int y)
    {
        return image[x, y];
    }

    /// <summary>
    /// Prepares the bitmap for writing directly to the pixel array, returns the pointer to said array
    /// </summary>
    public unsafe nint Lock()
    {
        if (lockCount == 0)
        {
            // This operation should succeed for small images. See note in static constructor.
            if (!image.DangerousTryGetSinglePixelMemory(out var memory))
                throw new Exception("Couldn't get raw pointer to Image object");
            memoryHandle = memory.Pin();
        }
        lockCount++;
        return (nint)memoryHandle.Pointer;
    }

    /// <summary>
    /// Pair with a previous call to Lock to mark the pixel drawing operation as completed.
    /// </summary>
    public void Unlock()
    {
        lockCount--;
        if (lockCount == 0)
        {
            memoryHandle.Dispose();
            memoryHandle = default;
        }
    }

    /// <summary>
    /// Draw this image onto the destination image at the given position.
    /// </summary>
    public void DrawOn(Bitmap dest, int drawX, int drawY)
    {
        dest.image.ProcessPixelRows(image, (destAccessor, sourceAccessor) =>
        {
            for (int y=0; y<Height; y++)
            {
                var sourceRow = sourceAccessor.GetRowSpan(y);
                var destRow = destAccessor.GetRowSpan(y + drawY);

                for (int x=0; x<Width; x++)
                {
                    destRow[x + drawX] = sourceRow[x];
                }
            }
        });
    }

    public void Save(string path)
    {
        // Gameboy graphics work well with palette-based encoding, 2 bit colormap.
        // This doesn't order the palettes in the same way as the python scripts. Doesn't really
        // matter though.
        PngEncoder encoder = new()
        {
            BitDepth = PngBitDepth.Bit2,
            ColorType = PngColorType.Palette,
        };
        image.SaveAsPng(path, encoder);
    }

    public void Dispose()
    {
        Dispose(true);
        DisposedEvent?.Invoke(this);
        DisposedEvent = null;
        GC.SuppressFinalize(this);
    }

    public virtual void Dispose(bool disposing)
    {
        if (image == null)
            return;

        Debug.Assert(lockCount == 0);

        if (disposing)
        {
            if (image != null)
                image.Dispose();
            image = null;
            ModifiedEvent = null;
        }
    }

    // Should call this after modifying the surface, otherwise ImGui may not receive the update
    public void MarkModified()
    {
        if (ModifiedEvent != null)
            ModifiedEvent();
    }
}
