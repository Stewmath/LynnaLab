namespace LynnaLab;

/// <summary>
/// An OverlayBox is a box-like thing that can be clicked on and dragged around using the mouse, ie.
/// objects in the room editor or in the emulator.
/// </summary>
public abstract class OverlayBox
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public OverlayBox(RoomLayoutEditor parent)
    {
        this.Parent = parent;
    }

    // ================================================================================
    // Events
    // ================================================================================

    public event EventHandler<EventArgs?>? SelectedEvent;

    // ================================================================================
    // Properties
    // ================================================================================

    public abstract Color? BoxColor { get; }

    public abstract bool Deletable { get; }
    public abstract bool HasXY { get; }
    public abstract bool HasShortenedXY { get; }
    public abstract int X { get; set; } // These are always in the full range even if "HasShortenedXY" is true
    public abstract int Y { get; set; }
    public abstract int BoxWidth { get; }
    public abstract int BoxHeight { get; }

    // TODO: Do something about this
    public RoomLayoutEditor Parent { get; private set; }

    public Vector2 BoxSize
    {
        get { return new Vector2(BoxWidth, BoxHeight); }
    }
    public virtual FRect BoxRectangle
    {
        get { return new FRect(X - BoxWidth / 2.0f, Y - BoxHeight / 2.0f, BoxWidth, BoxHeight); }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public abstract void Render(bool selected);

    public virtual void Select()
    {
        if (SelectedEvent != null)
            SelectedEvent(this, null);
    }

    /// <summary>
    /// Returns true if the underlying data is the same
    /// </summary>
    public abstract bool Compare(OverlayBox com);

    /// <summary>
    /// Right-click, select "delete", or press "Delete" key while selected
    /// </summary>
    public virtual void Delete() { }

    public virtual void OnDoubleClicked() {}

    /// <summary>
    /// Identifier used for uniquely identifying undo-able events.
    /// </summary>
    public abstract string GetTransactionIdentifier();

    // ================================================================================
    // Protected methods
    // ================================================================================

    /// <summary>
    /// Helper function for drawing an object's sprites at this position.
    /// </summary>
    protected void SpriteDrawer(Bitmap sprite, int xOffset, int yOffset)
    {
        // NOTE: Objects are drawn sprite-by-sprite, so don't try to use anything other than
        // nearest-neighbor interpolation here.
        var origin = ImGui.GetCursorScreenPos();
        var offset = new Vector2(X + xOffset, Y + yOffset);
        TextureBase texture = Top.TextureFromBitmapTracked(sprite);
        ImGui.SetCursorScreenPos(origin + offset * Parent.Scale);
        ImGuiX.DrawImage(texture, Parent.Scale);
        ImGui.SetCursorScreenPos(origin);
    }
}
