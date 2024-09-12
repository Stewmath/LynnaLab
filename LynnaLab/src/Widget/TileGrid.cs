namespace LynnaLab;

/// <summary>
/// Represents any kind of tile-based grid. It can be hovered over with the mouse, and
/// optionally allows one to select tiles by clicking, or define actions to occur with other
/// mouse buttons.
/// </summary>
public class TileGrid : SizedWidget
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TileGrid(string name)
    {
        this.Name = name;
        AddMouseAction(MouseButton.LeftClick, MouseModifier.Any, GridAction.Select);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    List<TileGridAction> actionList = new List<TileGridAction>();
    int selectedIndex;
    bool selectable, unselectable;
    int draggingTileIndex = -1;

    // ================================================================================
    // Events
    // ================================================================================

    /// <summary>
    /// Invoked when selected tile is changed, could possibly have -1 (unselected) as the tile.
    /// </summary>
    public event Action<int> SelectedEvent;

    // ================================================================================
    // Properties
    // ================================================================================

    // Widget overrides
    public override Vector2 WidgetSize
    {
        get
        {
            return new Vector2(CanvasWidth, CanvasHeight);
        }
    }

    // Number of tiles on each axis
    public int Width { get; protected set; }
    public int Height { get; protected set; }

    // Size of tiles on each axis (not accounting for scale)
    public int TileWidth { get; protected set; }
    public int TileHeight { get; protected set; }

    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// Whether tiles can be selected (selected tile is accessible through SelectedIndex).
    /// </summary>
    public bool Selectable
    {
        get
        {
            return selectable;
        }
        set
        {
            if (selectable == value)
                return;
            selectable = value;
            ValidateSelection();
        }
    }

    /// <summary>
    /// Whether it's valid for no tile to be selected. Clicking on a selected tile will unselect it.
    /// Has no effect if Selectable is false.
    /// </summary>
    public bool Unselectable
    {
        get
        {
            return unselectable;
        }
        set
        {
            if (unselectable == value)
                return;
            unselectable = value;
            ValidateSelection();
        }
    }

    /// <summary>
    /// Index of selected tile, -1 if none selected.
    /// </summary>
    public int SelectedIndex
    {
        get
        {
            return selectedIndex;
        }
        set
        {
            if (!Selectable)
                return;
            if (value < 0 || value > MaxIndex)
                selectedIndex = -1;
            if (selectedIndex != value)
            {
                selectedIndex = value;
                SelectedEvent?.Invoke(value);
            }
        }
    }

    public int SelectedX { get { return selectedIndex % Width; } }
    public int SelectedY { get { return selectedIndex / Width; } }

    public int MaxIndex { get { return Width * Height - 1; } }
    public int HoveringIndex
    {
        get
        {
            return CoordToTile(base.GetRelativeMousePos());
        }
    }

    public Color HoverColor { get; protected set; } = Color.Red;
    public Color SelectColor { get; protected set; } = Color.White;
    public Color DragColor { get; protected set; } = Color.Cyan;

    public float RectThickness { get; set; } = 3.0f;

    public Point ImageSize
    {
        get
        {
            return new Point(TileWidth * Width, TileHeight * Height);
        }
    }

    public float CanvasWidth
    {
        get
        {
            return TileWidth * Width * Scale;
        }
    }
    public float CanvasHeight
    {
        get
        {
            return TileHeight * Height * Scale;
        }
    }

    /// <summary>
    /// Name used for unique ImGui IDs
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Whether to show a preview of the tile while hovering over it
    /// </summary>
    public bool HoverImagePreview { get; set; } = false;

    /// <summary>
    /// Amount to scale tile image for drag preview
    /// </summary>
    public float HoverImagePreviewScale { get; set; } = 4.0f;

    /// <summary>
    /// Callback for ImGui drag functionality. The callback function can call
    /// ImGui.SetDragDropPayload or draw other things in the drag window.
    /// </summary>
    public Action<int> OnDrag { get; set; }

    /// <summary>
    /// Like above for drag destinations
    /// </summary>
    public Action OnDrop { get; set; }

    /// <summary>
    /// Callback for hovering on a tile. Good for making tooltips.
    /// Not invoked while dragging.
    /// </summary>
    public Action<int> OnHover { get; set; }

    protected virtual Image Image { get { return null; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public virtual void Render()
    {
        Render(false);
    }

    /// <summary>
    /// Pass inhibitMouse=true to skip mouse input checks and not draw hovering tile.
    /// </summary>
    public void Render(bool inhibitMouse)
    {
        base.RenderPrep();
        ImGui.BeginGroup();

        if (Image != null)
        {
            ImGuiX.DrawImage(Image, scale: Scale);
            bool dragging = false;

            for (int tile = 0; tile <= MaxIndex; tile++)
            {
                var (x, y) = TileToXY(tile);

                // Create an ImGui "Item" for this tile using InvisibleButton.
                // Creating a separate item for the tile is necessary for drag/drop to work,
                // among other ImGui features.
                ImGui.SetCursorScreenPos(base.origin +
                                         new Vector2(TileWidth * x, TileHeight * y) * Scale);
                ImGui.InvisibleButton($"{Name}-{x},{y}", new Vector2(TileWidth, TileHeight) * Scale);

                // Handle ImGui dragging
                if (OnDrag != null)
                {
                    if (ImGui.BeginDragDropSource())
                    {
                        dragging = true;
                        draggingTileIndex = XYToTile(x, y);
                        DrawTileImage(draggingTileIndex, HoverImagePreviewScale);
                        OnDrag(draggingTileIndex);
                        ImGui.EndDragDropSource();

                        // Draw rectangle on tile being dragged
                        FRect r = TileRect(draggingTileIndex);
                        base.AddRect(r, DragColor, thickness: RectThickness);

                        // Unselect tile. The issue is that we don't want tiles to be selected
                        // when they're actually being dragged. Not the best way to fix this,
                        // but it works.
                        if (Unselectable)
                        {
                            SelectedIndex = -1;
                        }
                    }
                }

                // Handle ImGui dropping
                if (OnDrop != null)
                {
                    if (ImGui.BeginDragDropTarget())
                    {
                        OnDrop();
                        ImGui.EndDragDropTarget();
                    }
                }

                if (ImGui.IsItemHovered() && draggingTileIndex == -1)
                {
                    if (HoverImagePreview)
                    {
                        ImGui.BeginTooltip();
                        DrawTileImage(tile, HoverImagePreviewScale);
                        ImGui.EndTooltip();
                    }
                    OnHover?.Invoke(tile);
                }
            }

            if (!dragging)
                draggingTileIndex = -1;
        }

        ImGui.EndGroup();

        if (!inhibitMouse && ImGui.IsItemHovered())
        {
            RenderMouse();
        }

        // Draw selection rectangle
        if (Selectable && SelectedIndex != -1)
        {
            FRect r = TileRect(SelectedIndex);
            base.AddRect(r, SelectColor, thickness: RectThickness);
        }
    }

    /// <summary>
    /// Check for mouse input & render hovering. Must be called after Render(), May need to be
    /// called manually if "inhibitMouse=true" was passed to "Render()".
    ///
    /// Only call if ImGui.IsItemHovered() returns true just after "Render()".
    /// </summary>
    public void RenderMouse()
    {
        int mouseIndex = CoordToTile(base.GetRelativeMousePos());

        // Draw hover rectangle
        if (mouseIndex != -1 && draggingTileIndex == -1)
        {
            FRect r = TileRect(mouseIndex);
            base.AddRect(r, HoverColor, thickness: RectThickness);

            // Check mouse input
            TileGridEventArgs args = new TileGridEventArgs();
            args.mouseAction = "click";
            args.selectedIndex = mouseIndex;

            foreach (TileGridAction action in actionList)
            {
                if (action.MatchesState())
                {
                    if (action.action == GridAction.Callback)
                    {
                        action.callback(this, args);
                    }
                    else if (action.action == GridAction.Select)
                    {
                        if (SelectedIndex == mouseIndex && Unselectable)
                        {
                            SelectedIndex = -1;
                        }
                        else if (Selectable)
                        {
                            SelectedIndex = mouseIndex;
                        }
                    }
                    else
                        throw new NotImplementedException();
                }
            }
        }
    }

    public void AddMouseAction(MouseButton button, MouseModifier mod, GridAction action, TileGridEventHandler callback = null)
    {
        TileGridAction act;
        if (action == GridAction.Callback || action == GridAction.SelectRangeCallback)
        {
            if (callback == null)
                throw new Exception("Need to specify a callback.");
            act = new TileGridAction(button, mod, action, callback);
        }
        else
        {
            if (callback != null)
                throw new Exception("This action doesn't take a callback.");
            act = new TileGridAction(button, mod, action);
        }

        // Insert at front of list; newest actions get highest priority
        actionList.Insert(0, act);
    }

    /// <summary>
    /// Converts a mouse position in pixels to a tile index.
    /// </summary>
    public int CoordToTile(Vector2 pos)
    {
        if (pos.X < 0 || pos.Y < 0 || pos.X >= CanvasWidth || pos.Y >= CanvasHeight)
            return -1;

        return ((int)(pos.Y / (TileHeight * Scale)) * Width)
            + (int)(pos.X / (TileWidth * Scale));
    }

    /// <summary>
    /// Like above but converts to X/Y position
    /// </summary>
    public (int, int) CoordToXY(Vector2 pos)
    {
        int tile = CoordToTile(pos);
        if (tile == -1)
            return (-1, -1);
        return (tile % Width, tile / Width);
    }

    public int XYToTile(int x, int y)
    {
        return x + y * Width;
    }

    public (int, int) TileToXY(int index)
    {
        if (index == -1)
            return (-1, -1);
        return (index % Width, index / Width);
    }

    /// <summary>
    /// Helper function to draw a tile from the current image. Can be used in various contexts, ie.
    /// in tooltips, not just internally.
    /// </summary>
    public void DrawTileImage(int index, float scale)
    {
        var tilePos = new Vector2((index % Width) * TileWidth,
                                  (index / Height) * TileHeight);
        var tileSize = new Vector2(TileWidth, TileHeight);
        ImGuiX.DrawImage(Image, scale, tilePos, tilePos + tileSize);
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Gets the bounds of a tile in a rectangle.
    /// </summary>
    FRect TileRect(int tileIndex)
    {
        int x = tileIndex % Width;
        int y = tileIndex / Width;

        Vector2 tl = new Vector2(x * TileWidth * Scale, y * TileHeight * Scale);
        return new FRect(tl.X, tl.Y, TileWidth * Scale, TileHeight * Scale);
    }

    /// <summary>
    /// Ensure that the SelectedIndex is valid, update if necessary
    /// </summary>
    void ValidateSelection()
    {
        if (selectable && !Unselectable)
        {
            if (selectedIndex == -1)
                SelectedIndex = 0;
        }
        else if (!selectable)
        {
            SelectedIndex = -1;
        }
    }


    // Nested class

    public delegate void TileGridEventHandler(object sender, TileGridEventArgs args);

    class TileGridAction
    {
        public readonly MouseButton button;
        public readonly MouseModifier mod;
        public readonly GridAction action;
        public readonly TileGridEventHandler callback;

        public TileGridAction(MouseButton button, MouseModifier mod, GridAction action, TileGridEventHandler callback = null)
        {
            this.button = button;
            this.mod = mod;
            this.action = action;
            this.callback = callback;
        }

        public bool MatchesState()
        {
            return ButtonMatchesState() && ModifierMatchesState();
        }

        public bool ButtonMatchesState()
        {
            Func<ImGuiMouseButton, bool> checker;
            if (mod.HasFlag(MouseModifier.Drag))
                checker = ImGui.IsMouseDown;
            else
                checker = ImGui.IsMouseClicked;
            bool left = checker(ImGuiMouseButton.Left);
            bool right = checker(ImGuiMouseButton.Right);
            if (button == MouseButton.Any && (left || right))
                return true;
            if (button == MouseButton.LeftClick && left)
                return true;
            if (button == MouseButton.RightClick && right)
                return true;
            return false;
        }

        public bool ModifierMatchesState()
        {
            if (mod.HasFlag(MouseModifier.Any))
                return true;
            MouseModifier flags = MouseModifier.None;
            if (ImGui.IsKeyDown(ImGuiKey.ModCtrl))
                flags |= MouseModifier.Ctrl;
            if (ImGui.IsKeyDown(ImGuiKey.ModShift))
                flags |= MouseModifier.Shift;

            return (mod & ~MouseModifier.Drag) == flags;
        }
    }
}

public enum MouseButton
{
    Any,
    LeftClick,
    RightClick
}

[Flags]
public enum MouseModifier
{
    // Exact combination of keys must be pressed, but "Any" allows any combination.
    Any = 1,
    None = 2,
    Ctrl = 4 | None,
    Shift = 8 | None,
    // Mouse can be dragged during the operation.
    Drag = 16,
}

public enum GridAction
{
    Select,       // Set the selected tile (bound to button "Any" with modifier "None" by default)
    Callback,     // Invoke a callback whenever the tile is clicked, or (optionally) the drag changes
    SelectRangeCallback,  // Select a range of tiles, takes a callback
}

public struct TileGridEventArgs
{
    // "click", "drag", "release"
    public string mouseAction;

    // For GridAction.Callback (selected one tile)
    public int selectedIndex;

    // For GridAction.SelectRange
    public Cairo.Point topLeft, bottomRight;

    public void Foreach(System.Action<int, int> action)
    {
        for (int x = topLeft.X; x <= bottomRight.X; x++)
        {
            for (int y = topLeft.Y; y <= bottomRight.Y; y++)
            {
                action(x, y);
            }
        }
    }
}
