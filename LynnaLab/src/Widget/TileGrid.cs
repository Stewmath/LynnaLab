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
    bool isHovered; // Return value of "IsItemHovered()"
    bool suppressCurrentClick;

    // For rectangle selection
    TileGridAction activeRectSelectAction = null;
    int rectSelectStart, rectSelectEnd; // Always have valid values as long as activeRectSelectAction != null
    FRect lastHoverRect = null;

    // Used for scrolling operations to keep zoom focused around point of interest
    Vector2? lastMousePos = null;
    Vector2? centerScaledPos = null; // Position relative to internal window (Scaling + scroll applied)
    Vector2? centerUnscaledPos = null; // No scaling/scroll applied
    Vector2 lastFrameScroll;

    // ================================================================================
    // Events
    // ================================================================================

    /// <summary>
    /// Invoked when selected tile is changed, could possibly have -1 (unselected) as the tile.
    /// </summary>
    public event Action<int> SelectedEvent;

    /// <summary>
    /// Invoked when hovered tile changes. Could be a single tile or a rectangle of tiles.
    /// </summary>
    public event EventHandler<TileEventArgs> HoverChangedEvent;

    /// <summary>
    /// Invoked between RenderTileGrid() and RenderHoverAndSelection(). Use this to render stuff on
    /// top of the TileGrid but behind any cursors.
    /// </summary>
    public event EventHandler<EventArgs> AfterRenderTileGrid;

    // ================================================================================
    // Properties
    // ================================================================================

    // Widget overrides
    public override Vector2 WidgetSize
    {
        get
        {
            if (InChildWindow)
            {
                if (ViewportSize != Vector2.Zero)
                    return ViewportSize;
                else
                {
                    // This could change a lot when zooming is enabled. Preferable to set
                    // ViewportSize so that WidgetSize is something fairly static and predictable.
                    return CanvasSize;
                }
            }
            else
                return CanvasSize;
        }
    }

    /// <summary>
    /// Total size of the draw area, accounting for global scaling. This includes the area inside
    /// the scroll bars if InChildWindow = true.
    /// </summary>
    public Vector2 CanvasSize
    {
        get
        {
            return new Vector2(
                (PaddedTileWidth * Width + TilePaddingX) * Scale,
                (PaddedTileHeight * Height + TilePaddingY) * Scale);
        }
    }

    /// <summary>
    /// Size of window; if this gets any bigger scrollbars will appear.
    ///
    /// This value is in units from BEFORE global scaling is applied (ImGuiX.ScaleUnit).
    ///
    /// Has no effect unless InChildWindow = true. Otherwise, check CanvasSize or WidgetSize.
    /// </summary>
    public Vector2 RequestedViewportSize { get; set; }

    /// <summary>
    /// Like above, but after global scaling is applied.
    /// </summary>
    public Vector2 ViewportSize { get { return RequestedViewportSize * ImGuiX.ScaleUnit; } }


    // Number of tiles on each axis
    public int Width { get; protected set; }
    public int Height { get; protected set; }

    // Size of tiles on each axis in pixels (not accounting for scale)
    public int TileWidth { get; protected set; }
    public int TileHeight { get; protected set; }
    public Point TileSize { get { return new Point(TileWidth, TileHeight); } }

    // Padding = # of pixels for gap between tiles (usually 0).
    // Also applied to space before the first tile and after the last tile.
    public float TilePaddingX { get; set; }
    public float TilePaddingY { get; set; }

    public float PaddedTileWidth { get { return TileWidth + TilePaddingX; } }
    public float PaddedTileHeight { get { return TileHeight + TilePaddingY; } }

    /// <summary>
    /// Scale factor of the TileGrid, before global scaling is applied (see Scale for result after
    /// global scaling).
    /// </summary>
    public float RequestedScale { get; set; } = 1.0f;

    public float MaxScale { get; set; } = 1.0f;
    public float MinScale { get; set; } = 1.0f;

    /// <summary>
    /// Scale factor of the TileGrid, after accounting for global scaling.
    ///
    /// This is not writable; set "RequestedScale" instead.
    /// </summary>
    public float Scale { get { return RequestedScale * ImGuiX.ScaleUnit; } }

    /// <summary>
    /// If true, then the TileGrid is rendered within a scrollable window.
    /// </summary>
    public bool InChildWindow { get; set; }

    /// <summary>
    /// If false, scrollwheel moves scrollbars rather than zooming
    /// </summary>
    public bool ScrollToZoom { get { return Top.GlobalConfig.ScrollToZoom; } }

    public Interpolation Interpolation { get { return Top.GlobalConfig.Interpolation; } }

    public BrushInterfacer BrushInterfacer { get; set; }

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
            if (RectangleSelected)
                return -1;
            return selectedIndex;
        }
        set
        {
            if (!Selectable)
                return;
            if (value < 0 || value >= MaxIndex)
            {
                if (!unselectable)
                    throw new Exception("Tried to set SelectedIndex to out of range value " + value);
                value = -1;
            }
            if (RectangleSelected || selectedIndex != value)
            {
                RectangleSelected = false;
                selectedIndex = value;
                SelectedEvent?.Invoke(value);
            }
        }
    }

    public int SelectedX { get { return selectedIndex % Width; } }
    public int SelectedY { get { return selectedIndex / Width; } }

    /// <summary>
    /// This is only set to a valid value if a single tile is being hovered over. (Set to -1 if not
    /// hovering or if hovering with a rectangle pattern.) To handle rectangular hovers, see
    /// HoverChangedEvent.
    /// </summary>
    public int HoveredTile { get; private set; } = -1;

    /// <summary>
    /// This is 1 + the maximum index value which can be hovered over or selected. Set to "0" to
    /// disable hovering & selection entirely.
    /// This always returns a valid value. If maxIndexOverride is -1 then this is determined by the
    /// width & height of the TileGrid.
    /// </summary>
    public int MaxIndex
    {
        get
        {
            if (maxIndexOverride != -1)
                return maxIndexOverride;
            return Width * Height;
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

    /// <summary>
    /// Thickness of selection / hover rectangles, before global scaling is applied.
    /// </summary>
    public float RectThickness { get; set; } = 3.0f;

    /// <summary>
    /// Name used for unique ImGui IDs
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Whether to show a preview of the tile while hovering over it
    /// </summary>
    public bool TooltipImagePreview { get; set; } = false;

    /// <summary>
    /// Amount to scale tile image for drag preview (before global scaling is applied)
    /// </summary>
    public float TooltipImagePreviewScale { get; set; } = 4.0f;

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
    /// True if a rectangle region has been selected (process of selection is finished).
    /// </summary>
    public bool RectangleSelected { get; private set; }

    /// <summary>
    /// True if a rectangle region is in the process of being selected.
    /// </summary>
    public bool SelectingRectangle { get { return activeRectSelectAction != null; } }

    /// <summary>
    /// A derived class should override either the Texture get operator or the TileDrawer function in
    /// order to supply the texture to draw.
    /// </summary>
    public virtual TextureBase Texture { get { return null; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    // NOTE: RoomEditor overrides this and doesn't call it, instead opting to call RenderTileGrid()
    // and RenderHoverSelection() on its own terms.
    public virtual void Render()
    {
        // Start position of window containing the scrollbars
        var scrollOrigin = ImGui.GetCursorScreenPos();
        // Mouse position "above" the scroll widget (not affected by scrolling)
        var topLevelMousePos = ImGui.GetIO().MousePos - scrollOrigin;

        // Check whether to stick this in a scrollable child window
        if (InChildWindow)
        {
            UpdateScroll();

            ImGuiWindowFlags flags = ImGuiWindowFlags.HorizontalScrollbar;
            if (ScrollToZoom)
                flags |= ImGuiWindowFlags.NoScrollWithMouse;
            ImGui.BeginChild("TileGrid Child", ViewportSize, 0, flags);

            var interp = Interpolation;
            if (Scale == Math.Floor(Scale)) // Always use nearest-neighbor interpolation for integer scaling
                interp = Interpolation.Nearest;
            ImGuiX.PushInterpolation(interp);
        }

        RenderTileGrid();
        AfterRenderTileGrid?.Invoke(this, null);
        RenderHoverAndSelection();

        if (InChildWindow)
        {
            ImGuiX.PopInterpolation();

            if (isHovered && ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                if (lastMousePos != null)
                {
                    Vector2 delta = topLevelMousePos - (Vector2)lastMousePos;
                    var scroll = ImGuiX.GetScroll();
                    scroll -= delta;
                    ImGuiX.SetScroll(scroll);
                }

                this.lastMousePos = topLevelMousePos;
            }
            else
            {
                this.lastMousePos = null;
            }

            if (ScrollToZoom && isHovered)
            {
                float offset = ImGui.GetIO().MouseWheel;
                if (offset != 0.0f)
                {
                    // Keep the view centered around the mouse cursor
                    this.centerScaledPos = topLevelMousePos;
                    this.centerUnscaledPos = (centerScaledPos + ImGuiX.GetScroll()) / Scale;

                    RequestedScale *= 1.0f + (offset * 0.2f); // Adjust size by 20% per tick
                    if (RequestedScale >= MaxScale)
                        RequestedScale = MaxScale;
                    if (RequestedScale <= MinScale)
                        RequestedScale = MinScale;
                }
            }

            lastFrameScroll = ImGuiX.GetScroll();

            ImGui.EndChild();
        }
    }

    /// <summary>
    /// Render the scroll bar that controls the zoom level (this is optional). This must be called
    /// sometime before Render(). The reason for being a separate function is so that it can be
    /// embedded in some other control widget.
    /// </summary>
    public void RenderScrollBar()
    {
        if (!InChildWindow)
            throw new Exception("Can't call RenderScrollBar when InChildWindow == false");

        const int MAX_SLIDER_VALUE = 100;

        int minimapScale = (int)(((RequestedScale - MinScale) / (MaxScale - MinScale)) * 100);

        ImGui.PushItemWidth(200);
        ImGui.SameLine(); // Same line as whatever came before this (World/Season selector buttons)
        bool scaleChangedFromUI = ImGui.SliderInt("Scale", ref minimapScale, 0, MAX_SLIDER_VALUE);

        if (scaleChangedFromUI)
        {
            // Keep the view centered around the selected tile
            this.centerUnscaledPos = new Vector2(
                SelectedX * TileWidth + TileWidth / 2.0f,
                SelectedY * TileHeight + TileHeight / 2.0f);
            this.centerScaledPos = centerUnscaledPos * Scale - lastFrameScroll;

            RequestedScale = MinScale + (minimapScale / (float)MAX_SLIDER_VALUE) * (MaxScale - MinScale);
        }

        ImGui.PopItemWidth();
    }

    /// <summary>
    /// Renders the image. This should be called before RenderHover().
    /// </summary>
    public void RenderTileGrid()
    {
        base.RenderPrep();
        ImGui.BeginGroup();

        if (Texture != null)
        {
            ImGuiX.DrawImage(Texture, scale: Scale);
        }

        bool dragging = false;

        for (int tile = 0; tile < MaxIndex; tile++)
        {
            var (x, y) = TileToXY(tile);

            // Use per-tile drawing method if no image was supplied
            if (Texture == null)
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
                    DrawTileImage(draggingTileIndex, TooltipImagePreviewScale * ImGuiX.ScaleUnit);
                    OnDrag(draggingTileIndex);
                    ImGui.EndDragDropSource();

                    // Draw rectangle on tile being dragged
                    FRect r = TileRect(draggingTileIndex);
                    base.AddRect(r, DragColor, thickness: RectThickness * ImGuiX.ScaleUnit);

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
                if (TooltipImagePreview)
                {
                    ImGui.BeginTooltip();
                    DrawTileImage(tile, TooltipImagePreviewScale * ImGuiX.ScaleUnit);
                    ImGui.EndTooltip();
                }
                OnHover?.Invoke(tile);
            }
        }

        if (!dragging)
            draggingTileIndex = -1;

        ImGui.EndGroup();

        isHovered = ImGui.IsItemHovered();
    }

    /// <summary>
    /// Render red hover box and white selection box, and also handle mouse inputs related to those.
    ///
    /// Should be called after RenderTileGrid.
    /// </summary>
    public void RenderHoverAndSelection(bool enable = true)
    {
        if (!enable)
        {
            HoveredTile = -1;
            return;
        }

        TileEventArgs hoverChangedArgs = null;

        // Determine hovered tile
        HoveredTile = -1;

        int hoverWidth = 1, hoverHeight = 1;
        if (BrushInterfacer != null)
        {
            hoverWidth = BrushInterfacer.BrushWidth;
            hoverHeight = BrushInterfacer.BrushHeight;
        }

        int mouseIndex = -1;
        if (isHovered)
        {
            mouseIndex = CoordToTile(base.GetRelativeMousePos());
            if (mouseIndex != -1 && draggingTileIndex == -1)
            {
                int mouseX = mouseIndex % Width;
                int mouseY = mouseIndex / Width;

                // This only executes if not currently doing a rectangle select
                if (activeRectSelectAction == null)
                {
                    if (hoverWidth == 1 && hoverHeight == 1)
                    {
                        HoveredTile = XYToTile(mouseX, mouseY);
                        if (HoveredTile >= MaxIndex)
                            HoveredTile = -1;
                    }
                }
            }
        }

        bool isMousePosValid = isHovered && mouseIndex != -1 && draggingTileIndex == -1;
        bool selectingRectangleThisFrame = SelectingRectangle;

        // Check mouse actions
        foreach (TileGridAction action in actionList)
        {
            TileGridEventArgs args = new TileGridEventArgs(this);
            args.selectedIndex = mouseIndex;

            bool actionTriggered = action.ActionTriggered();
            bool activeThisFrame = actionTriggered && isMousePosValid;

            // suppressCurrentClick is an override telling us to ignore all mouse
            // actions until the mouse is released and clicked again
            if (suppressCurrentClick)
            {
                if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseDown(ImGuiMouseButton.Right))
                    suppressCurrentClick = false;
                activeThisFrame = false;
            }

            // Rectangle select actions override others
            if (selectingRectangleThisFrame && action != activeRectSelectAction)
                activeThisFrame = false;

            if (activeThisFrame)
            {
                // Code here may execute once (for "click" actions) or many times (for "clickdrag" actions)

                if (action.action == GridAction.Callback)
                {
                    action.callback(this, args);
                }
                else if (action.action == GridAction.SelectRangeCallback)
                {
                    if (!action.activatedLastFrame)
                    {
                        activeRectSelectAction = action;
                        rectSelectStart = mouseIndex;
                        rectSelectEnd = mouseIndex;
                    }
                    else
                    {
                        // Button still being held
                        if (mouseIndex != -1)
                        {
                            rectSelectEnd = mouseIndex;
                        }

                        // If clicked the alternate button, cancel the rectangle selection without doing anything
                        if (ImGui.IsMouseClicked(activeRectSelectAction.GetCancelButton()))
                        {
                            suppressCurrentClick = true;
                            activeRectSelectAction = null;
                        }
                    }
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

                action.activatedLastFrame = true;
            }

            // Allow rectangle select action to remain active even when we move out of the window
            bool actionJustEnded = !activeThisFrame && action.activatedLastFrame
                && !(action == activeRectSelectAction && actionTriggered);

            if (actionJustEnded)
            {
                // Action ended (usually means the mouse button was released)
                action.activatedLastFrame = false;

                if (action == activeRectSelectAction)
                {
                    args = new TileGridEventArgs(this);
                    args.gridAction = activeRectSelectAction.action;
                    (args.topLeft, args.bottomRight) = GetSelectRectBounds();

                    if (Selectable)
                    {
                        int width = args.bottomRight.X - args.topLeft.X + 1;
                        int height = args.bottomRight.Y - args.topLeft.Y + 1;

                        // Could consider just setting SelectedIndex if width == height == 1...
                        RectangleSelected = true;
                        selectedIndex = -1;
                    }

                    activeRectSelectAction.callback(this, args);
                    activeRectSelectAction = null;
                }

                action.onRelease?.Invoke();
            }
        }

        // Finally, do hover rendering

        RenderBrushPreview();

        FRect hoverRect = null;
        int hoverTile1 = -1;
        int hoverTile2 = -1;

        if (SelectingRectangle)
        {
            // Draw hover rectangle (currently actively selecting a rectangle)
            hoverTile1 = rectSelectStart;
            hoverTile2 = rectSelectEnd;
            hoverRect = TileRangeRect(rectSelectStart, rectSelectEnd);
            base.AddRect(hoverRect, SelectColor, RectThickness * ImGuiX.ScaleUnit);
        }
        else if (isMousePosValid)
        {
            // Draw hover rectangle (based on brush size)
            int hoverX = mouseIndex % Width;
            int hoverY = mouseIndex / Width;
            hoverTile1 = mouseIndex;
            hoverTile2 = XYToTile(
                hoverX + hoverWidth - 1,
                hoverY + hoverHeight - 1,
                clamp: true);
            hoverRect = TileRangeRect(mouseIndex, hoverTile2);
            base.AddRect(hoverRect, HoverColor, thickness: RectThickness * ImGuiX.ScaleUnit);
        }

        if (hoverRect != lastHoverRect)
        {
            if (hoverRect == null)
                hoverChangedArgs = new(-1);
            else
                hoverChangedArgs = new(hoverTile1, hoverTile2);
        }

        lastHoverRect = hoverRect;

        if (RectangleSelected)
        {
            // Draw rectangle selection range
            base.AddRect(TileRangeRect(rectSelectStart, rectSelectEnd), SelectColor, RectThickness * ImGuiX.ScaleUnit);
        }

        if (!RectangleSelected && !SelectingRectangle && Selectable && SelectedIndex != -1)
        {
            // Draw regular (1-tile) selection rectangle
            FRect r = TileRect(SelectedIndex);
            base.AddRect(r, SelectColor, thickness: RectThickness * ImGuiX.ScaleUnit);
        }

        if (hoverChangedArgs != null)
            HoverChangedEvent?.Invoke(this, hoverChangedArgs);
    }

    /// <summary>
    /// Renders the transparent preview of what will be drawn at the current position with the given brush
    /// </summary>
    public void RenderBrushPreview()
    {
        if (BrushInterfacer == null)
            return;

        var prepTile = (int x, int y) =>
        {
            if (!XYValid(x, y))
                return false;
            ImGui.SetCursorScreenPos(origin + TileToCoord(XYToTile(x, y)));
            return true;
        };

        if (SelectingRectangle)
        {
            if (activeRectSelectAction.brushPreview)
            {
                var (topLeft, bottomRight) = GetSelectRectBounds();
                var (x1, y1) = (topLeft.X, topLeft.Y);
                var (x2, y2) = (bottomRight.X, bottomRight.Y);
                ImGuiX.PushAlpha(1.0f);
                BrushInterfacer.Draw(prepTile, x1, y1, x2 - x1 + 1, y2 - y1 + 1, Scale);
                ImGuiX.PopAlpha();
            }
        }
        else if (isHovered)
        {
            int mouseIndex = CoordToTile(base.GetRelativeMousePos());
            if (mouseIndex != -1)
            {
                ImGuiX.PushAlpha(0.5f);
                BrushInterfacer.Draw(prepTile, mouseIndex % Width, mouseIndex / Width,
                                     BrushInterfacer.BrushWidth, BrushInterfacer.BrushHeight, Scale);
                ImGuiX.PopAlpha();
            }
        }

    }

    public void AddMouseAction(MouseButton button, MouseModifier mod, MouseAction mouseAction,
        GridAction action, TileGridEventHandler callback = null, bool brushPreview = false, string name = null,
        Action onRelease = null)
    {
        TileGridAction act;
        if (action == GridAction.Callback || action == GridAction.SelectRangeCallback)
        {
            if (callback == null)
                throw new Exception("Need to specify a callback.");
        }
        else
        {
            if (callback != null)
                throw new Exception("This action doesn't take a callback.");
        }

        act = new TileGridAction(button, mod, mouseAction, action, callback, brushPreview, name, onRelease);

        // Insert at front of list; newest actions get highest priority
        actionList.Insert(0, act);
    }

    public void RemoveMouseAction(string name)
    {
        Debug.Assert(name != null);
        actionList.RemoveAll(act => act.name == name);
    }

    /// <summary>
    /// Converts a RELATIVE ImGui position vector in pixels to a tile index.
    /// </summary>
    public int CoordToTile(Vector2 pos)
    {
        if (pos.X < 0 || pos.Y < 0 || pos.X >= CanvasSize.X || pos.Y >= CanvasSize.Y)
            return -1;

        int y = (int)((pos.Y - TilePaddingY * Scale / 2) / (PaddedTileHeight * Scale));
        int x = (int)((pos.X - TilePaddingX * Scale / 2) / (PaddedTileWidth * Scale));

        int index = x + y * Width;
        if (index >= MaxIndex || index < 0)
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

    public int XYToTile(int x, int y, bool clamp = false)
    {
        int tile;
        if (clamp)
        {
            if (x < 0)
                x = 0;
            if (x >= Width)
                x = Width - 1;
            if (y < 0)
                y = 0;
            if (y >= Height)
                y = Height - 1;
            tile = x + y * Width;
            if (tile >= MaxIndex)
                tile = MaxIndex - 1;
        }
        else
        {
            tile = x + y * Width;
            Debug.Assert(x >= 0 && x < Width);
            Debug.Assert(y >= 0 && y < Height);
            Debug.Assert(tile < MaxIndex);
        }
        return tile;
    }

    public (int, int) TileToXY(int index)
    {
        if (index == -1)
            throw new Exception("Can't convert tile index -1 to X/Y coordinates");
        return (index % Width, index / Width);
    }

    /// <summary>
    /// Helper function to draw a tile from the current texture. Can be used in various contexts,
    /// ie. in tooltips, not just internally.
    /// </summary>
    public void DrawTileImage(int index, float scale)
    {
        var tilePos = new Vector2((index % Width) * TileWidth,
                                  (index / Height) * TileHeight);
        var tileSize = new Vector2(TileWidth, TileHeight);

        ImGuiX.DrawImage(Texture, scale, tilePos, tilePos + tileSize);
    }

    /// <summary>
    /// Register mouse inputs used by RoomLayoutEditor and ScratchPad for drawing tiles
    /// </summary>
    public void RegisterTilePlacementInputs<T>(
        Brush<T> brush,
        Func<int, int, T> tileGetter,
        Action<int, int, T> tileSetter,
        Func<int> maxX,
        Func<int> maxY,
        Action setterFinishedAction = null)
    {

        // Left click to set tile
        AddMouseAction(
            MouseButton.LeftClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
            (sender, args) =>
            {
                int x = args.selectedIndex % Width;
                int y = args.selectedIndex / Width;
                brush.Draw((x2, y2, t) =>
                {
                    if (x2 < 0 || y2 < 0 || x2 >= maxX() || y2 >= maxY())
                        return;
                    tileSetter(x2, y2, t);
                }, x, y, brush.BrushWidth, brush.BrushHeight);
            },
            onRelease: setterFinishedAction
        );

        // Ctrl+Left click to set range of tiles
        AddMouseAction(
            MouseButton.LeftClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
            (sender, args) =>
            {
                brush.Draw(tileSetter, args.topLeft.X, args.topLeft.Y, args.SelectedWidth, args.SelectedHeight);
                setterFinishedAction?.Invoke();
            },
            brushPreview: true);

        // Right-click to select a tile or a range of tiles
        AddMouseAction(
            MouseButton.RightClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
            (_, args) =>
            {
                brush.SetTiles(this, args.RectArray((x2, y2) => tileGetter(x2, y2)));
            });
    }

    /// <summary>
    /// Returns true if x/y coords correspond to a valid tile index
    /// </summary>
    public bool XYValid(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height && x + y * Width < MaxIndex;
    }

    /// <summary>
    /// Gets the bounds of a tile in a rectangle.
    /// </summary>
    public FRect TileRect(int tileIndex)
    {
        var (x, y) = TileToXY(tileIndex);
        return TileRect(x, y);
    }

    public FRect TileRect(int x, int y)
    {
        Vector2 tl = new Vector2((TilePaddingX + x * PaddedTileWidth) * Scale,
                                 (TilePaddingY + y * PaddedTileHeight) * Scale);
        return new FRect(tl.X, tl.Y, TileWidth * Scale, TileHeight * Scale);
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    /// <summary>
    /// A derived class may choose to override TileDrawer instead of the Texture property in order
    /// to supply its image tile-by-tile.
    /// </summary>
    protected virtual void TileDrawer(int index) {}

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
            // Must set the variable selectedIndex instead of the property SelectedIndex as the
            // property cannot be written to while selectable == false
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
            else if (maxIndexOverride != -1 && selectedIndex >= maxIndexOverride)
            {
                SelectedIndex = maxIndexOverride - 1; // If maxIndexOverride == 0 then this could be -1
            }
        }
    }

    /// <summary>
    /// Gets the start & end points of the selection rectangle.
    /// </summary>
    (Point, Point) GetSelectRectBounds()
    {
        var (x1, y1) = TileToXY(rectSelectStart);
        var (x2, y2) = TileToXY(rectSelectEnd);

        Point topLeft = new Point(
            Math.Min(x1, x2),
            Math.Min(y1, y2)
        );
        Point bottomRight = new Point(
            Math.Max(x1, x2),
            Math.Max(y1, y2)
        );

        return (topLeft, bottomRight);
    }

    /// <summary>
    /// Updates the scrollbar. Deals with smoothly zooming in and out relative to a particular
    /// point.
    /// Must be called just before the BeginChild containing the scrollable area.
    /// </summary>
    void UpdateScroll()
    {
        ImGui.SetNextWindowContentSize(new Vector2(CanvasSize.X, CanvasSize.Y));

        if (centerScaledPos != null)
        {
            // centerUnscaledPos is the unscaled position within the TileGridViewer that must be
            // placed at centerScaledPos (above the scrollbar window) in order for the zooming
            // to focus on the point of interest.
            // These variables should have been calculated before the Scale was updated. We now
            // calculate the new scroll value using the updated Scale.
            var scroll = centerUnscaledPos * Scale - centerScaledPos;

            ImGui.SetNextWindowScroll((Vector2)scroll);
            centerScaledPos = null;
            centerUnscaledPos = null;
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
        public readonly string name; // An identifier used to disable previously added actions, often null

        // Conditions to trigger action
        public readonly MouseButton button;
        public readonly MouseModifier mod;
        public readonly MouseAction mouseAction;
        public readonly bool brushPreview; // Whether to show a brush preview (only for rectangle selection)

        // Action to perform when conditions are cleared
        public readonly GridAction action;
        public readonly TileGridEventHandler callback;
        public readonly Action onRelease;

        public bool activatedLastFrame = false;

        public TileGridAction(MouseButton button, MouseModifier mod, MouseAction mouseAction,
                              GridAction action, TileGridEventHandler callback, bool brushPreview, string name, Action onRelease)
        {
            this.button = button;
            this.mod = mod;
            this.mouseAction = mouseAction;
            this.action = action;
            this.callback = callback;
            this.brushPreview = brushPreview;
            this.name = name;
            this.onRelease = onRelease;
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
        bool ButtonMatchesState()
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
        bool ModifierMatchesState()
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

        public ImGuiMouseButton GetCancelButton()
        {
            if (button == MouseButton.LeftClick)
                return ImGuiMouseButton.Right;
            else if (button == MouseButton.RightClick)
                return ImGuiMouseButton.Left;
            else
                return ImGuiMouseButton.Middle;
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
    public TileGridEventArgs(TileGrid grid)
    {
        this.gridWidth = grid.Width;
        this.gridHeight = grid.Height;
    }

    // For all actions
    public GridAction gridAction;
    public int gridWidth, gridHeight;

    // For GridAction.Callback (selected one tile)
    public int selectedIndex;

    // For GridAction.SelectRange
    public Point topLeft, bottomRight;

    public int SelectedWidth { get { return bottomRight.X - topLeft.X + 1; } }
    public int SelectedHeight { get { return bottomRight.Y - topLeft.Y + 1; } }

    public void Foreach(System.Action<int, int> action)
    {
        if (gridAction == GridAction.SelectRangeCallback)
        {
            for (int x = topLeft.X; x <= bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y <= bottomRight.Y; y++)
                {
                    action(x, y);
                }
            }
        }
        else
        {
            action(selectedIndex % gridWidth, selectedIndex / gridWidth);
        }
    }

    public void Foreach(System.Action<int> action)
    {
        if (gridAction == GridAction.SelectRangeCallback)
        {
            for (int x = topLeft.X; x <= bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y <= bottomRight.Y; y++)
                {
                    action(x + y * gridWidth);
                }
            }
        }
        else
        {
            action(selectedIndex);
        }
    }

    /// <summary>
    /// For rectangle selections, returns the grid of selected tiles as a 2d array.
    /// Takes a function which converts the X/Y coordinates to some other arbitrary value (usually
    /// an int representing the tile index).
    /// </summary>
    public T[,] RectArray<T>(Func<int, int, T> tileGetter)
    {
        T[,] array = new T[SelectedWidth, SelectedHeight];

        for (int x = 0; x < SelectedWidth; x++)
        {
            for (int y = 0; y < SelectedHeight; y++)
            {
                array[x,y] = tileGetter(topLeft.X + x, topLeft.Y + y);
            }
        }

        return array;
    }
}

public class TileEventArgs
{
    public TileEventArgs(int tile)
    {
        IsSingleTile = true;
        TileStart = tile;
        TileEnd = tile;
    }

    public TileEventArgs(int tileStart, int tileEnd)
    {
        IsSingleTile = false;
        TileStart = tileStart;
        TileEnd = tileEnd;

        if (tileStart == -1 || tileEnd == -1)
            throw new Exception("TileEventArgs: tile == -1");
    }

    public bool IsSingleTile { get; }

    public int TileStart { get; }
    public int TileEnd { get; }
}
