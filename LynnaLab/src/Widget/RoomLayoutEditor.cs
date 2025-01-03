namespace LynnaLab;

/// <summary>
/// This is the TileGrid showing the room's layout which can be drawn on to modify it. Also allows
/// manipulation of objects, warps, chests, etc. which can be optionally drawn on top of the
/// TileGrid.
/// </summary>
public class RoomLayoutEditor : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public RoomLayoutEditor(ProjectWorkspace workspace, RoomEditor roomEditor, Brush<int> brush)
        : base("Room Layout Editor")
    {
        this.Workspace = workspace;
        this.Brush = brush;
        this.RoomEditor = roomEditor;

        base.TileWidth = 16;
        base.TileHeight = 16;
        base.Scale = 2;
        base.RenderOffset = new Vector2(8, 8) * Scale;
        base.BrushInterfacer = BrushInterfacer.Create(brush, (index, scale) =>
        {
            if (Workspace.ShowBrushPreview)
                RoomEditor.TilesetViewer.DrawTileImage(index, scale);
        });

        // Register mouse buttons for tile selection & placement
        RegisterTilePlacementInputs(
            brush,
            (x, y) => RoomLayout.GetTile(x, y),
            (x, y, tile) => RoomLayout.SetTile(x, y, tile),
            () => RoomLayout.Width,
            () => RoomLayout.Height,
            () => Project.UndoState.InsertBarrier());

        QuickstartData.enableToggledEvent += (s, a) => UpdateQuickstartRoomComponent();
        RoomEditor.TabChangedEvent += (s, a) => UpdateRoomComponents();

        roomEventWrapper = new EventWrapper<Room>();
        roomEventWrapper.Bind<EventArgs>(
            "ChestAddedEvent",
            (s, a) => UpdateChest(),
            weak: false);

        chestEventWrapper = new EventWrapper<Chest>();
        chestEventWrapper.Bind<EventArgs>(
            "DeletedEvent",
            (_, _) => UpdateChest(),
            weak: false);

        objectGroupEventWrapper = new EventWrapper<ObjectGroup>();
        objectGroupEventWrapper.Bind<EventArgs>(
            "StructureModifiedEvent",
            (_, _) => UpdateRoomComponents(),
            weak: false);

        warpGroupEventWrapper = new EventWrapper<WarpGroup>();
        warpGroupEventWrapper.Bind<EventArgs>(
            "ModifiedEvent",
            (_, _) => UpdateRoomComponents(),
            weak: false);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Image image;
    RoomComponent selectedRoomComponent;
    List<RoomComponent> roomComponents;
    bool draggingComponent;
    Vector2 draggingComponentOffset;
    bool suppressCurrentClick;

    EventWrapper<Room> roomEventWrapper;
    EventWrapper<ObjectGroup> objectGroupEventWrapper;
    EventWrapper<Chest> chestEventWrapper;
    EventWrapper<WarpGroup> warpGroupEventWrapper;

    // ================================================================================
    // Events
    // ================================================================================

    // Invoked when the selected RoomComponent is changed.
    public event EventHandler ChangedSelectedRoomComponentEvent;

    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public Room Room { get { return RoomLayout?.Room; } }
    public RoomLayout RoomLayout { get; private set; }
    public QuickstartData QuickstartData { get { return Workspace.QuickstartData; } }
    public Brush<int> Brush { get; private set; }

    public ObjectDefinition SelectedObject { get { return (selectedRoomComponent as ObjectRoomComponent)?.obj; } }
    public Warp SelectedWarpSource { get { return (selectedRoomComponent as WarpSourceRoomComponent)?.warp; } }

    // TileGrid overrides

    public override Vector2 WidgetSize
    {
        get
        {
            // Always keep it 256x256 so that objects can be rendered even out of bounds, and
            // also so that it doesn't fluctuate when moving between small & large rooms
            return new Vector2(256.0f * Scale, 256.0f * Scale) + RenderOffset;
        }
    }

    public override Image Image { get { return image; } }

    // Private properties

    private RoomEditor RoomEditor { get; set; }

    // If true, cannot draw on or select the underlying TileGrid.
    bool SuppressTileSelection
    {
        get
        {
            return suppressCurrentClick || RoomEditor.EditingWarpDestination != null;
        }
    }

    bool DrawObjects { get { return (Workspace.ViewObjects || RoomEditor.ObjectTabActive) && RoomEditor.EditingWarpDestination == null; } }
    bool DrawWarps { get { return (Workspace.ViewWarps || RoomEditor.WarpTabActive) && RoomEditor.EditingWarpDestination == null; } }
    bool DrawChests { get { return RoomEditor.ChestTabActive && Room.Chest != null; } }


    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Set the room layout
    /// </summary>
    public void SetRoomLayout(RoomLayout layout)
    {
        if (layout != RoomLayout)
        {
            RoomLayout = layout;
            OnRoomChanged();
        }
    }

    public override void Render()
    {
        base.RenderTileGrid();
        var endPos = ImGui.GetCursorScreenPos();
        bool isWindowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

        RoomComponent hoveringComponent = null;
        var mousePos = base.GetRelativeMousePos() / Scale;

        // Render room components
        foreach (var com in roomComponents)
        {
            // Draw background box
            if (com.BoxColor != null)
            {
                base.AddRectFilled(com.BoxRectangle * Scale, (Color)com.BoxColor);
            }

            // Draw it
            ImGui.SetCursorScreenPos(origin);
            com.Render();

            // Create an InvisibleButton so that we can select it without dragging the entire window.
            // Names are not unique... probably not an issue? These don't really do anything.
            ImGui.SetCursorScreenPos(origin + com.BoxRectangle.TopLeft * Scale);
            ImGui.InvisibleButton($"RoomComponent button", com.BoxSize * Scale);

            // Check if we're hovering over it. But don't draw the hovering rectangle right now
            // because we don't want to draw it on more than one RoomComponent.
            var rect = com.BoxRectangle;
            if (isWindowHovered && rect.Contains(mousePos))
            {
                hoveringComponent = com;
            }
        }

        // If the mouse is clicked while not focused on the window, do not allow the tile grid to be
        // modified through mouse dragging until the mouse is released and clicked again. Prevents
        // accidental tile grid modifications in some situations, ie. when clicking outside a
        // right-click popup menu.
        if (!isWindowHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !SelectingRectangle)
            suppressCurrentClick = true;

        // Draw hover outline if something is being hovered over
        if (hoveringComponent != null)
        {
            base.AddRect(hoveringComponent.BoxRectangle * Scale, Color.Cyan, thickness: Scale);
        }
        // Draw selection outline if something is selected
        if (selectedRoomComponent != null)
        {
            base.AddRect(selectedRoomComponent.BoxRectangle * Scale, Color.White, thickness: Scale);
        }

        // Check if we clicked on the hoveringComponent
        if (hoveringComponent != null)
        {
            var rect = hoveringComponent.BoxRectangle;

            if (rect.Contains(mousePos))
            {
                // Left or right click
                if ((ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)) && !draggingComponent)
                {
                    // Set dragOffset to non-null if we should start dragging it.
                    // In any case the hovered object will become the selected object.
                    Vector2? dragOffset = null;
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (hoveringComponent.HasXY)
                            dragOffset = new Vector2(hoveringComponent.X, hoveringComponent.Y) - mousePos;
                        else
                            dragOffset = new Vector2(0, 0); // Just set it to anything non-null
                    }
                    SetSelectedRoomComponent(hoveringComponent, dragOffset != null, dragOffset);
                }

                // Right click
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !draggingComponent)
                {
                    ImGui.OpenPopup(TopLevel.RightClickPopupName);
                }

                // Double-click
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    hoveringComponent.OnDoubleClicked();
                }
            }
        }

        // Render right-click menu
        if (ImGui.BeginPopup(TopLevel.RightClickPopupName))
        {
            var com = selectedRoomComponent;

            if (com is ObjectRoomComponent ocom)
            {
                ObjectBox.ObjectPopupMenu(ocom.obj, Room, Workspace);
            }
            else if (com is WarpSourceRoomComponent wcom)
            {
                WarpEditor.WarpPopupMenu(wcom.warp, RoomEditor);
            }
            else if (com is WarpDestRoomComponent dcom)
            {
                if (ImGui.Selectable("Done"))
                {
                    Room room = dcom.warp.SourceRoom;
                    RoomEditor.DisableWarpDestEditMode();
                }
            }
            else // Not an object or a warp source
            {
                if (com.Deletable && ImGui.Selectable("Delete"))
                {
                    com.Delete();
                }
            }
            ImGui.EndPopup();
        }

        // Update room component dragging
        if (draggingComponent)
        {
            var com = selectedRoomComponent;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                Project.BeginTransaction("Move " + com.GetTransactionIdentifier(), true);

                // No XY: Can't move this at all by dragging.
                if (!com.HasXY)
                {
                }
                // Shortened XY: Snapping is "automatic" due to low resolution of XY variables;
                // also don't add draggingComponentOffset as it only works well with smooth
                // movement
                else if (com.HasShortenedXY)
                {
                    com.X = (int)Math.Round(mousePos.X);
                    com.Y = (int)Math.Round(mousePos.Y);
                }
                // Ctrl pressed: No snapping
                else if (ImGui.IsKeyDown(ImGuiKey.ModCtrl))
                {
                    com.X = (int)Math.Round(mousePos.X + draggingComponentOffset.X);
                    com.Y = (int)Math.Round(mousePos.Y + draggingComponentOffset.Y);
                }
                // Ctrl not pressed: Snap to 8x8
                else
                {
                    const int snap = 8;

                    var snapTo = (int oldPos, int mousePos) =>
                    {
                        int snapLog = (int)Math.Log(snap, 2);
                        int data = oldPos + snap / 2;
                        int align = data % snap;
                        int newp = (mousePos - align) >> snapLog;
                        newp = newp * snap + align + snap / 2;
                        return newp;
                    };

                    com.X = snapTo(com.X, (int)Math.Round(mousePos.X));
                    com.Y = snapTo(com.Y, (int)Math.Round(mousePos.Y));
                }

                Project.EndTransaction();
            }
            else
            {
                // Released the component
                Project.UndoState.InsertBarrier();
                draggingComponent = false;
            }
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            if (selectedRoomComponent?.Deletable ?? false)
                selectedRoomComponent.Delete();
        }

        ImGui.SetCursorScreenPos(endPos);

        if (hoveringComponent == null && !draggingComponent)
        {
            if (!SuppressTileSelection)
                base.RenderHoverAndSelection();
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            suppressCurrentClick = false;
    }

    /// <summary>
    /// Sets the selected room component to the specified object if it's in the room component list.
    /// </summary>
    public void SelectObject(ObjectDefinition obj)
    {
        if (obj == null)
        {
            if (selectedRoomComponent is ObjectRoomComponent)
                SetSelectedRoomComponent(null);
            return;
        }

        foreach (RoomComponent com in roomComponents)
        {
            if ((com as ObjectRoomComponent)?.obj == obj)
            {
                SetSelectedRoomComponent(com);
                return;
            }
        }

        // Not fonud
        SetSelectedRoomComponent(null);
    }

    /// <summary>
    /// Sets the selected room component to the specified warp source if it's in the room component list.
    /// </summary>
    public void SelectWarpSource(Warp warp)
    {
        if (warp == null)
        {
            if (selectedRoomComponent is WarpSourceRoomComponent)
                SetSelectedRoomComponent(null);
            return;
        }

        foreach (RoomComponent com in roomComponents)
        {
            if ((com as WarpSourceRoomComponent)?.warp == warp)
            {
                SetSelectedRoomComponent(com);
                break;
            }
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void SetSelectedRoomComponent(RoomComponent comp, bool startDrag = false, Vector2? dragOffset = null)
    {
        // Even if the RoomComponent object changed, the underlying data may actually be the same as before.
        // "isDifferent" checks the underlying data rather than the RoomComponents themselves.
        bool isDifferent = comp == null || !comp.Compare(selectedRoomComponent);
        if (isDifferent || startDrag)
        {
            draggingComponent = startDrag;
            if (startDrag)
                draggingComponentOffset = (Vector2)dragOffset;
        }

        if (selectedRoomComponent == comp)
            return;
        RoomComponent old = selectedRoomComponent;
        selectedRoomComponent = comp;

        if (isDifferent)
            ChangedSelectedRoomComponentEvent?.Invoke(this, null);
    }

    /// <summary>
    /// Called when the room being edited is changed
    /// </summary>
    void OnRoomChanged()
    {
        image = Workspace.GetCachedRoomImage(RoomLayout);

        base.Width = RoomLayout.Width;
        base.Height = RoomLayout.Height;

        UpdateRoomComponents();

        roomEventWrapper.ReplaceEventSource(Room);
        chestEventWrapper.ReplaceEventSource(Room.Chest);
        objectGroupEventWrapper.ReplaceEventSource(Room.GetObjectGroup());
        warpGroupEventWrapper.ReplaceEventSource(Room.GetWarpGroup());
    }

    /// <summary>
    /// Called when a chest has been added to or removed from the current room
    /// </summary>
    void UpdateChest()
    {
        chestEventWrapper.ReplaceEventSource(Room.Chest);
        UpdateRoomComponents();
    }

    /// <summary>
    /// Regenerate room component list
    /// </summary>
    public void UpdateRoomComponents()
    {
        roomComponents = new List<RoomComponent>();

        if (Room == null)
            return;

        UpdateQuickstartRoomComponent();

        // Object room components
        if (DrawObjects)
        {
            foreach (var group in Room.GetObjectGroup().GetAllGroups())
            {
                foreach (var obj in group.GetObjects())
                {
                    if (obj.HasXY())
                    {
                        var com = new ObjectRoomComponent(this, obj);
                        roomComponents.Add(com);
                    }
                }
            }
        }

        // Chest
        if (DrawChests)
        {
            roomComponents.Add(new ChestRoomComponent(this, Room.Chest));
        }

        // Warps
        if (DrawWarps)
        {
            var warpGroup = Room.GetWarpGroup();
            for (int warpIndex = 0; warpIndex < warpGroup.GetWarps().Count; warpIndex++)
            {
                Warp warp = warpGroup.GetWarp(warpIndex);

                Action<int, int, int, int> addWarpComponent = (x, y, width, height) =>
                {
                    var rect = new FRect(x, y, width, height);
                    var com = new WarpSourceRoomComponent(this, warp, warpIndex, rect);
                    roomComponents.Add(com);
                };

                if (warp.WarpSourceType == WarpSourceType.Standard)
                {
                    int middle;
                    if (Room.Width == 15) // Large room
                        middle = ((Room.Width + 1) / 2) * 16;
                    else // Small room
                        middle = ((Room.Width + 1) / 2) * 16 + 8;
                    int right = Room.Width * 16;
                    int bottom = Room.Height * 16 - 8;

                    if (warp.TopLeft)
                        addWarpComponent(0, -8, middle, 16);
                    if (warp.TopRight)
                        addWarpComponent(middle, -8, right - middle, 16);
                    if (warp.BottomLeft)
                        addWarpComponent(0, bottom, middle, 16);
                    if (warp.BottomRight)
                        addWarpComponent(middle, bottom, right - middle, 16);

                    if (!warp.TopLeft && !warp.TopRight && !warp.BottomLeft && !warp.BottomRight)
                    {
                        addWarpComponent(0, 16 * 13, Room.Width * 16, 32);
                    }
                }
                else if (warp.WarpSourceType == WarpSourceType.Position)
                {
                    addWarpComponent(warp.SourceX * TileWidth, warp.SourceY * TileHeight, TileWidth, TileHeight);
                }
            }
        }

        // Warp destination editing mode
        if (RoomEditor.EditingWarpDestination != null)
        {
            var com = new WarpDestRoomComponent(this, RoomEditor.EditingWarpDestination);
            roomComponents.Add(com);
        }

        // Below here, we are concerned with updating the selectedRoomComponent.
        if (selectedRoomComponent == null)
            return;

        // Check if the previous selectedRoomComponent has an equivalent in the new list. If not,
        // unselect it.
        foreach (var com in roomComponents)
        {
            if (com.Compare(selectedRoomComponent))
            {
                SetSelectedRoomComponent(com);
                return;
            }
        }

        // We are unselecting the previous selected RoomComponent. If we were dragging it, suppress
        // mouse inputs so that we don't suddenly start drawing room tiles.
        // This can occur when double-clicking on a warp to follow it, or toggling quickstart (F4
        // key) while dragging the Link icon.
        if (draggingComponent)
            suppressCurrentClick = true;
        SetSelectedRoomComponent(null);
    }

    /// <summary>
    /// Called when quickstart is toggled or room component list is updated
    /// </summary>
    void UpdateQuickstartRoomComponent()
    {
        bool validInRoom = QuickstartData.Enabled
            && QuickstartData.group == Room.Group
            && QuickstartData.room == (Room.Index & 0xff);

        // Search for existing QuickstartRoomComponent
        var existingComponents = roomComponents.Where((com) => com is QuickstartRoomComponent);

        if (existingComponents.Count() >= 2)
        {
            throw new Exception("Internal error: Multiple QuickstartData's in a room");
        }

        if (existingComponents.Count() == 1)
        {
            var com = existingComponents.First();
            if (validInRoom)
                return;
            else
            {
                roomComponents.Remove(com);
                if (com == selectedRoomComponent)
                {
                    SetSelectedRoomComponent(null);
                }
            }
        }
        else // Doesn't exist in list
        {
            if (validInRoom)
            {
                var com = new QuickstartRoomComponent(this, QuickstartData);
                roomComponents.Add(com);
            }
            else
                return;
        }
    }

    // ================================================================================
    // Nested classes
    // ================================================================================

    /// <summary>
    /// Things drawn on top of the room (like objects and warps) which can be selected and moved
    /// around.
    /// </summary>
    abstract class RoomComponent
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public RoomComponent(RoomLayoutEditor parent)
        {
            this.Parent = parent;
        }

        // ================================================================================
        // Events
        // ================================================================================

        public event EventHandler<EventArgs> SelectedEvent;

        // ================================================================================
        // Properties
        // ================================================================================

        public abstract Color? BoxColor { get; }

        public abstract bool Deletable { get; }
        public abstract bool HasXY { get; }
        public abstract bool HasShortenedXY { get; }
        public abstract int X { get; set; } // These are always in the full range even if "HasShortedenXY" is true
        public abstract int Y { get; set; }
        public abstract int BoxWidth { get; }
        public abstract int BoxHeight { get; }

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

        public abstract void Render();

        public virtual void Select()
        {
            if (SelectedEvent != null)
                SelectedEvent(this, null);
        }

        /// <summary>
        /// Returns true if the underlying data is the same
        /// </summary>
        public abstract bool Compare(RoomComponent com);

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
            var origin = ImGui.GetCursorScreenPos();
            var offset = new Vector2(X + xOffset, Y + yOffset);
            Image image = TopLevel.ImageFromBitmapTracked(sprite);
            ImGui.SetCursorScreenPos(origin + offset * Parent.Scale);
            ImGuiX.DrawImage(image, Parent.Scale);
            ImGui.SetCursorScreenPos(origin);
        }
    }

    class QuickstartRoomComponent : RoomComponent
    {
        QuickstartData quickstart;

        public QuickstartRoomComponent(RoomLayoutEditor parent, QuickstartData data) : base(parent)
        {
            this.quickstart = data;
        }

        public override Color? BoxColor
        {
            get { return null; }
        }

        public override bool Deletable
        {
            get { return true; }
        }
        public override bool HasXY
        {
            get { return true; }
        }
        public override bool HasShortenedXY
        {
            get { return false; }
        }
        public override int X
        {
            get { return quickstart.x; }
            set { quickstart.x = (byte)value; }
        }
        public override int Y
        {
            get { return quickstart.y; }
            set { quickstart.y = (byte)value; }
        }
        public override int BoxWidth { get { return 16; } }
        public override int BoxHeight { get { return 16; } }

        public override void Render()
        {
            // Get component position relative to widget origin
            Vector2 pos = new Vector2(X, Y) * Parent.Scale;
            Vector2 size = new Vector2(BoxWidth, BoxHeight) * Parent.Scale;

            ImGuiX.ShiftCursorScreenPos(pos - size / 2);
            var linkImage = TopLevel.ImageFromBitmapTracked(Parent.Project.LinkBitmap);
            ImGuiX.DrawImage(linkImage, Parent.Scale);
        }

        public override bool Compare(RoomComponent com)
        {
            return quickstart == (com as QuickstartRoomComponent)?.quickstart;
        }

        public override void Delete()
        {
            Parent.Workspace.QuickstartData.Enabled = false;
        }

        public override string GetTransactionIdentifier()
        {
            return "quickstart";
        }
    }

    /// <summary>
    /// Draggable chest
    /// </summary>
    class ChestRoomComponent : RoomComponent
    {
        Chest chest;

        public ChestRoomComponent(RoomLayoutEditor parent, Chest chest) : base(parent)
        {
            this.chest = chest;
        }

        Project Project { get { return chest.Project; } }

        public override Color? BoxColor
        {
            get
            {
                return Color.FromRgba(204, 51, 153, 0xc0);
            }
        }

        public override bool Deletable { get { return true; } }
        public override bool HasXY { get { return true; } }
        public override bool HasShortenedXY { get { return true; } }
        public override int X
        {
            get { return chest.ValueReferenceGroup.GetIntValue("X") * 16 + 8; }
            set { chest.ValueReferenceGroup.SetValue("X", value / 16); }
        }
        public override int Y
        {
            get { return chest.ValueReferenceGroup.GetIntValue("Y") * 16 + 8; }
            set { chest.ValueReferenceGroup.SetValue("Y", value / 16); }
        }
        public override int BoxWidth { get { return 18; } }
        public override int BoxHeight { get { return 18; } }

        public override void Render()
        {
            if (chest.Treasure == null)
                return;
            GameObject obj = Project.GetIndexedDataType<InteractionObject>(
                    Project.Eval("INTERAC_TREASURE") * 256 + chest.Treasure.Graphics);
            try
            {
                obj.DefaultAnimation.GetFrame(0).Draw(base.SpriteDrawer);
            }
            catch (InvalidAnimationException)
            {
            }
        }

        public override void Select()
        {
            base.Select();
        }

        public override bool Compare(RoomComponent com)
        {
            return chest == (com as ChestRoomComponent)?.chest;
        }

        public override void Delete()
        {
            chest.Delete();
        }

        public override string GetTransactionIdentifier()
        {
            return "chest#" + chest.TransactionIdentifier;
        }
    }

    class ObjectRoomComponent : RoomComponent
    {
        public ObjectDefinition obj;


        public ObjectRoomComponent(RoomLayoutEditor parent, ObjectDefinition obj) : base(parent)
        {
            this.obj = obj;
        }


        public override Color? BoxColor
        {
            get
            {
                Color color = ObjectGroupEditor.GetObjectColor(obj.GetObjectType());
                return Color.FromRgba(color.R, color.G, color.B, (int)(0.75 * 255));
            }
        }

        public override bool Deletable
        {
            get { return true; }
        }
        public override bool HasXY
        {
            get { return obj.HasXY(); }
        }
        public override bool HasShortenedXY
        {
            get { return obj.HasShortenedXY(); }
        }
        public override int X
        {
            get { return obj.GetX(); }
            set { obj.SetX((byte)value); }
        }
        public override int Y
        {
            get { return obj.GetY(); }
            set { obj.SetY((byte)value); }
        }
        public override int BoxWidth { get { return 16; } }
        public override int BoxHeight { get { return 16; } }


        public override void Render()
        {
            if (obj.GetGameObject() == null)
                return;

            try
            {
                ObjectAnimationFrame frame = obj.GetGameObject().DefaultAnimation.GetFrame(0);
                frame.Draw(base.SpriteDrawer);
            }
            catch (NoAnimationException)
            {
                // No animation defined
            }
            catch (InvalidAnimationException)
            {
                // Error parsing an animation; draw a blue X to indicate the error (TODO)
            }
        }

        public override bool Compare(RoomComponent com)
        {
            return obj == (com as ObjectRoomComponent)?.obj;
        }

        public override void Delete()
        {
            obj.Remove();
        }

        public override string GetTransactionIdentifier()
        {
            return "object#" + obj.TransactionIdentifier;
        }
    }

    class WarpSourceRoomComponent : RoomComponent
    {
        public Warp warp;
        public int index;

        FRect rect;


        public WarpSourceRoomComponent(RoomLayoutEditor parent, Warp warp, int index, FRect rect) : base(parent)
        {
            this.warp = warp;
            this.index = index;
            this.rect = rect;
        }


        public override Color? BoxColor
        {
            get
            {
                return WarpEditor.WarpSourceColor;
            }
        }

        public override bool Deletable
        {
            get { return true; }
        }
        public override bool HasXY
        {
            get { return warp.WarpSourceType == WarpSourceType.Position; }
        }
        public override bool HasShortenedXY
        {
            get { return true; }
        }
        public override int X
        {
            get { return warp.SourceX * 16 + 8; }
            set
            {
                warp.SourceX = value / 16;
                UpdateRect();
            }
        }
        public override int Y
        {
            get { return warp.SourceY * 16 + 8; }
            set
            {
                warp.SourceY = value / 16;
                UpdateRect();
            }
        }

        public override int BoxWidth { get { return (int)rect.Width; } }
        public override int BoxHeight { get { return (int)rect.Height; } }

        public override FRect BoxRectangle { get { return rect; } }


        public override void Render()
        {
            // Draw digit representing the warp index in the center of the rectangle
            ImGui.PushFont(TopLevel.OraclesFont24px);
            var origin = ImGui.GetCursorScreenPos();
            string text = index.ToString("X");
            ImGuiX.DrawTextAt(text, origin + BoxRectangle.Center * Parent.Scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.PopFont();
        }

        public override bool Compare(RoomComponent com)
        {
            return warp == (com as WarpSourceRoomComponent)?.warp;
        }

        public override void Delete()
        {
            warp.Remove();
        }

        public override void OnDoubleClicked()
        {
            Parent.RoomEditor.FollowWarp(warp);
        }

        void UpdateRect()
        {
            rect = new FRect(X - rect.Width / 2.0f, Y - rect.Height / 2.0f, rect.Width, rect.Height);
        }

        public override string GetTransactionIdentifier()
        {
            return "warp#" + warp.TransactionIdentifier;
        }
    }

    // Represents the singular "warp destination" object that appears when in warp dest editing mode
    class WarpDestRoomComponent : RoomComponent
    {
        public Warp warp;

        public WarpDestRoomComponent(RoomLayoutEditor parent, Warp warp) : base(parent)
        {
            this.warp = warp;
        }


        public override Color? BoxColor
        {
            get
            {
                return WarpEditor.WarpSourceColor;
            }
        }

        public override bool Deletable
        {
            get { return false; }
        }
        public override bool HasXY
        {
            get { return true; }
        }
        public override bool HasShortenedXY
        {
            get { return true; }
        }
        public override int X
        {
            get { return warp.DestX * 16 + 8; }
            set
            {
                warp.DestX = value / 16;
            }
        }
        public override int Y
        {
            get { return warp.DestY * 16 + 8; }
            set
            {
                warp.DestY = value / 16;
            }
        }

        public override int BoxWidth { get { return 16; } }
        public override int BoxHeight { get { return 16; } }


        public override void Render()
        {
            ImGui.PushFont(TopLevel.OraclesFont24px);
            var origin = ImGui.GetCursorScreenPos();
            ImGuiX.DrawTextAt("W", origin + BoxRectangle.Center * Parent.Scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.PopFont();
        }

        public override bool Compare(RoomComponent com)
        {
            return warp == (com as WarpDestRoomComponent)?.warp;
        }

        public override string GetTransactionIdentifier()
        {
            return "warp dest#" + warp.TransactionIdentifier;
        }
    }
}
