namespace LynnaLab;

/// <summary>
/// An Image is used by the graphics backend (Veldrid) to render stuff with Imgui. It is stored
/// on the GPU so can be drawn efficiently.
///
/// Contrast with LynnaLib.Bitmap which is a Cairo surface used within LynnaLib rendered on the
/// cpu side. This is converted to an Image through IBackend.ImageFromBitmap so that it can be
/// rendered to the screen.
/// </summary>
public abstract class Image : IDisposable
{
    // ================================================================================
    // Variables
    // ================================================================================

    event EventHandler<ImageModifiedEventArgs> modifiedEvent;
    int modifiedEventLocked;
    bool modifiedEventInvoked;

    // ================================================================================
    // Properties
    // ================================================================================
    public abstract int Width { get; }
    public abstract int Height { get; }

    /// <summary>
    /// Invoked when the image is modified
    /// </summary>
    public event EventHandler<ImageModifiedEventArgs> ModifiedEvent
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
    public abstract IntPtr GetBinding();

    /// <summary>
    /// Draw this image onto another image
    /// </summary>
    public abstract void DrawOn(Image destImage, Point srcPos, Point destPos, Point size);

    /// <summary>
    /// Replaces the contents of the image with the given bitmap.
    /// </summary>
    //public abstract void UpdateFromBitmap(LynnaLib.Bitmap bitmap);

    public abstract void Dispose();


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
            modifiedEvent?.Invoke(this, new ImageModifiedEventArgs {});
        }
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected void InvokeModifiedHandler()
    {
        if (modifiedEventLocked == 0)
        {
            modifiedEvent?.Invoke(this, new ImageModifiedEventArgs { });
        }
        else
        {
            modifiedEventInvoked = true;
        }
    }
}

// stub in case we need it later
public struct ImageModifiedEventArgs { }
