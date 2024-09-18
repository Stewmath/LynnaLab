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
        AddMouseAction(MouseButton.LeftClick, MouseModifier.Any, MouseAction.Click, GridAction.Select);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    List<TileGridAction> actionList = new List<TileGridAction>();
    int selectedIndex;
    bool selectable, unselectable;
    int draggingTileIndex = -1;
    int maxIndexOverride = -1;

    // For rectangle selection
    TileGridAction activeRectSelectAction = null;
    int rectSelectStart, rectSelectEnd;

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

    // Padding = # of pixels for gap between tiles (usually 0).
    // Also applied to space before the first tile and after the last tile.
    public float TilePaddingX { get; set; }
    public float TilePaddingY { get; set; }

    public float PaddedTileWidth { get { return TileWidth + TilePaddingX; } }
    public float PaddedTileHeight { get { return TileHeight + TilePaddingY; } }

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
            {
                if (!unselectable)
                    throw new Exception("Tried to set SelectedIndex to out of range value " + value);
                selectedIndex = -1;
            }
            if (selectedIndex != value)
            {
                selectedIndex = value;
                SelectedEvent?.Invoke(value);
            }
        }
    }

    public int SelectedX { get { return selectedIndex % Width; } }
    public int SelectedY { get { return selectedIndex / Width; } }

    public int MaxIndex
    {
        get
        {
            if (maxIndexOverride != -1)
                return maxIndexOverride;
            return Width * Height - 1;
        }
        set
        {
            maxIndexOverride = value;
            ValidateSelection();
        }
    }

    public Color HoverColor { get; protected set; } = Color.Red;
    public Color SelectColor { get; protected set; } = Color.White;
    public Color DragColor { get; protected set; } = Color.Cyan;

    public float RectThickness { get; set; } = 3.0f;

    public float CanvasWidth
    {
        get
        {
            return (PaddedTileWidth * Width + TilePaddingX) * Scale;
        }
    }
    public float CanvasHeight
    {
        get
        {
            return (PaddedTileHeight * Height + TilePaddingY) * Scale;
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

    /// <summary>
    /// A derived class should override either the Image get operator or the TileDrawer function in
    /// order to supply the image to draw.
    /// </summary>
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
        }

        bool dragging = false;

        for (int tile = 0; tile <= MaxIndex; tile++)
        {
            var (x, y) = TileToXY(tile);

            // Use per-tile drawing method if no image was supplied
            if (Image == null)
            {
                ImGui.SetCursorScreenPos(origin + TileToCoord(tile));
                TileDrawer(tile);
            }

            // Create an ImGui "Item" for this tile using InvisibleButton.
            // Creating a separate item for the tile is necessary for drag/drop to work,
            // among other ImGui features.
            ImGui.SetCursorScreenPos(origin + TileToCoord(tile));
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

        ImGui.EndGroup();

        if (!inhibitMouse && ImGui.IsItemHovered())
        {
            RenderMouse();
        }

        // Draw regular (1-tile) selection rectangle
        if (Selectable && SelectedIndex != -1)
        {
            FRect r = TileRect(SelectedIndex);
            base.AddRect(r, SelectColor, thickness: RectThickness);
        }

        // Rectangle selection stuff (only executes if rectangle selection has begun)
        if (activeRectSelectAction != null)
        {
            int mouseIndex = CoordToTile(base.GetRelativeMousePos());
            if (!ImGui.IsItemHovered())
                mouseIndex = -1;

            if (activeRectSelectAction.ActionTriggered())
            {
                // Button still being held
                if (mouseIndex != -1)
                    rectSelectEnd = mouseIndex;
            }
            else
            {
                // Released the button
                var args = new TileGridEventArgs();

                var (x1, y1) = TileToXY(rectSelectStart);
                var (x2, y2) = TileToXY(rectSelectEnd);

                args.topLeft = new Point(
                    Math.Min(x1, x2),
                    Math.Min(y1, y2)
                );
                args.bottomRight = new Point(
                    Math.Max(x1, x2),
                    Math.Max(y1, y2)
                );

                activeRectSelectAction.callback(this, args);
                activeRectSelectAction = null;
            }

            // Draw rectangle selection range
            base.AddRect(TileRangeRect(rectSelectStart, rectSelectEnd), HoverColor, RectThickness);
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

        if (mouseIndex != -1 && mouseIndex <= MaxIndex && draggingTileIndex == -1)
        {
            // Check mouse input
            TileGridEventArgs args = new TileGridEventArgs();
            args.selectedIndex = mouseIndex;

            // This only executes if not currently doing a rectangle select
            if (activeRectSelectAction == null)
            {
                // Draw hover rectangle
                FRect r = TileRect(mouseIndex);
                base.AddRect(r, HoverColor, thickness: RectThickness);

                // Check mouse actions
                foreach (TileGridAction action in actionList)
                {
                    if (action.ActionTriggered())
                    {
                        if (action.action == GridAction.Callback)
                        {
                            action.callback(this, args);
                        }
                        else if (action.action == GridAction.SelectRangeCallback)
                        {
                            // We're just starting the rectangle selection
                            activeRectSelectAction = action;
                            rectSelectStart = mouseIndex;
                            rectSelectEnd = rectSelectStart;
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
    }

    public void AddMouseAction(MouseButton button, MouseModifier mod, MouseAction mouseAction,
        GridAction action, TileGridEventHandler callback = null)
    {
        TileGridAction act;
        if (action == GridAction.Callback || action == GridAction.SelectRangeCallback)
        {
            if (callback == null)
                throw new Exception("Need to specify a callback.");
            act = new TileGridAction(button, mod, mouseAction, action, callback);
        }
        else
        {
            if (callback != null)
                throw new Exception("This action doesn't take a callback.");
            act = new TileGridAction(button, mod, mouseAction, action);
        }

        // Insert at front of list; newest actions get highest priority
        actionList.Insert(0, act);
    }

    /// <summary>
    /// Converts a RELATIVE ImGui position vector in pixels to a tile index.
    /// </summary>
    public int CoordToTile(Vector2 pos)
    {
        if (pos.X < 0 || pos.Y < 0 || pos.X >= CanvasWidth || pos.Y >= CanvasHeight)
            return -1;

        int y = (int)((pos.Y - TilePaddingY * Scale / 2) / (PaddedTileHeight * Scale));
        int x = (int)((pos.X - TilePaddingX * Scale / 2) / (PaddedTileWidth * Scale));

        int index = x + y * Width;
        if (index > MaxIndex || index < 0)
            return -1;
        return index;
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

    /// <summary>
    /// Tile index to RELATIVE ImGui position vector (top left of tile, after padding)
    /// </summary>
    public Vector2 TileToCoord(int tile)
    {
        if (tile == -1)
            throw new Exception("Couldn't convert tile index -1 to coordinate");
        var (x, y) = TileToXY(tile);
        var pos = new Vector2(TilePaddingX + PaddedTileWidth * x,
                              TilePaddingY + PaddedTileHeight * y) * Scale;
        return pos;
    }

    public int XYToTile(int x, int y)
    {
        return x + y * Width;
    }

    public (int, int) TileToXY(int index)
    {
        if (index == -1)
            throw new Exception("Can't convert tile index -1 to X/Y coordinates");
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
    // Protected methods
    // ================================================================================

    /// <summary>
    /// A derived class may choose to override TileDrawer instead of the Image property in order to
    /// supply its image tile-by-tile.
    /// </summary>
    protected virtual void TileDrawer(int index) {}

    /// <summary>
    /// Gets the bounds of a tile in a rectangle.
    /// </summary>
    protected FRect TileRect(int tileIndex)
    {
        var (x, y) = TileToXY(tileIndex);

        Vector2 tl = new Vector2((TilePaddingX + x * PaddedTileWidth) * Scale,
                                 (TilePaddingY + y * PaddedTileHeight) * Scale);
        return new FRect(tl.X, tl.Y, TileWidth * Scale, TileHeight * Scale);
    }

    /// <summary>
    /// Gets the bounds of a range of tiles in a rectangle.
    /// </summary>
    protected FRect TileRangeRect(int tile1, int tile2)
    {
        var coord1 = TileToCoord(tile1);
        var coord2 = TileToCoord(tile2);

        var tl = new Vector2(
            Math.Min(coord1.X, coord2.X),
            Math.Min(coord1.Y, coord2.Y)
        );
        var br = new Vector2(
            Math.Max(coord1.X, coord2.X),
            Math.Max(coord1.Y, coord2.Y)
        );

        br += new Vector2(TileWidth, TileHeight) * Scale;

        return FRect.FromVectors(tl, br);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Ensure that the SelectedIndex is valid, update if necessary
    /// </summary>
    void ValidateSelection()
    {
        if (!selectable)
        {
            if (selectedIndex != -1)
            {
                selectedIndex = -1;
                SelectedEvent?.Invoke(selectedIndex);
            }
        }
        else
        {
            if (!Unselectable && selectedIndex == -1)
            {
                SelectedIndex = 0;
            }
            else if (maxIndexOverride != -1 && selectedIndex > maxIndexOverride)
            {
                SelectedIndex = maxIndexOverride;
            }
        }
    }


    // Nested class

    public delegate void TileGridEventHandler(object sender, TileGridEventArgs args);

    /// <summary>
    /// Represents a mouse action:
    /// - The button & modifier keys required to activate the action
    /// - Whether the action is for "click", "release" or "drag"
    /// - The action to perform when the conditions are correct
    /// </summary>
    class TileGridAction
    {
        // Conditions to trigger action
        public readonly MouseButton button;
        public readonly MouseModifier mod;
        public readonly MouseAction mouseAction;

        // Action to perform when conditions are cleared
        public readonly GridAction action;
        public readonly TileGridEventHandler callback;

        public TileGridAction(MouseButton button, MouseModifier mod, MouseAction mouseAction,
                              GridAction action, TileGridEventHandler callback = null)
        {
            this.button = button;
            this.mod = mod;
            this.mouseAction = mouseAction;

            this.action = action;
            this.callback = callback;
        }

        /// <summary>
        /// Returns true if the conditions are met for the action to be triggered.
        /// In the case of rectangle selection "triggered" means the selection is active.
        /// </summary>
        public bool ActionTriggered()
        {
            return ButtonMatchesState() && ModifierMatchesState();
        }

        /// <summary>
        /// Return true if the current pressed mouse buttons match the "button" variable.
        /// </summary>
        public bool ButtonMatchesState()
        {
            Func<ImGuiMouseButton, bool> checker;

            if (mouseAction == MouseAction.ClickDrag)
                checker = ImGui.IsMouseDown;
            else if (mouseAction == MouseAction.Click)
                checker = ImGui.IsMouseClicked;
            else if (mouseAction == MouseAction.Release)
                checker = ImGui.IsMouseReleased;
            else if (mouseAction == MouseAction.Drag)
                checker = ImGui.IsMouseDragging;
            else
                throw new NotImplementedException();

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

        /// <summary>
        /// Return true if the modifier keys (ctrl, shift) match the "mod" variable.
        /// </summary>
        public bool ModifierMatchesState()
        {
            if (mod.HasFlag(MouseModifier.Any))
                return true;

            MouseModifier flags = MouseModifier.None;

            if (ImGui.IsKeyDown(ImGuiKey.ModCtrl))
                flags |= MouseModifier.Ctrl;
            if (ImGui.IsKeyDown(ImGuiKey.ModShift))
                flags |= MouseModifier.Shift;

            return mod == flags;
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
}

public enum MouseAction
{
    Click,
    Release,
    Drag,
    ClickDrag,
}

public enum GridAction
{
    Select,       // Set the selected tile (bound to button "Any" with modifier "None" by default)
    Callback,     // Invoke a callback whenever the tile is clicked, or (optionally) the drag changes
    SelectRangeCallback,  // Select a range of tiles, takes a callback
}

public struct TileGridEventArgs
{
    // For GridAction.Callback (selected one tile)
    public int selectedIndex;

    // For GridAction.SelectRange
    public Point topLeft, bottomRight;

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
