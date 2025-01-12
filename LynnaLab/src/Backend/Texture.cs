namespace LynnaLab;

/// <summary>
/// A Texture is used by the graphics backend (Veldrid) to render stuff with Imgui. It is stored on
/// the GPU so can be drawn efficiently.
///
/// Contrast with LynnaLib.Bitmap which is an image used within LynnaLib rendered on the cpu side.
/// This is converted to a Texture through IBackend.TextureFromBitmap so that it can be rendered to
/// the screen.
/// </summary>
public abstract class Texture : IDisposable
{
    // ================================================================================
    // Variables
    // ================================================================================

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
    public abstract IntPtr GetBinding();

    /// <summary>
    /// Draw this texture onto another texture
    /// </summary>
    public abstract void DrawOn(Texture destTexture, Point srcPos, Point destPos, Point size);

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
            modifiedEvent?.Invoke(this, new TextureModifiedEventArgs {});
        }
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected void InvokeModifiedHandler()
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

    protected void InvokeDisposedHandler()
    {
        DisposedEvent?.Invoke(this, null);
    }
}

// stub in case we need it later
public struct TextureModifiedEventArgs { }
