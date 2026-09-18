#nullable disable
namespace LynnaLab;

/// <summary>
/// This is the TileGrid showing the room's layout which can be drawn on to modify it. Also allows
/// manipulation of objects, warps, chests, etc. which can be optionally drawn on top of the
/// TileGrid.
/// </summary>
public class RoomLayoutEditor : TileGrid
{
    public class RoomLayoutOverlayBox : LynnaLab.IOverlayBoxUser
    {
        readonly RoomLayoutEditor editor;

        public RoomLayoutOverlayBox(RoomLayoutEditor editor)
        {
            this.editor = editor;
        }

        public bool SelectingRectangle { get { return editor.SelectingRectangle; } }
        public bool DraggingComponent
        {
            get { return editor.draggingComponent; }
            set { editor.draggingComponent = value; }
        }
        public Vector2 DraggingComponentOffset { get { return editor.draggingComponentOffset; } }
        public OverlayBox Selected { get { return editor.selectedOverlayBox; } }
        public float Scale { get { return editor.Scale; } }

        public void SetSelected(OverlayBox comp, bool startDrag = false, Vector2? dragOffset = null)
        {
            editor.SetSelectedOverlayBox(comp, startDrag, dragOffset);
        }

        public void SuppressCurrentClick()
        {
            editor.suppressCurrentClick = true;
        }
    }

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
        base.RequestedScale = 2;
        base.RenderOffset = new Vector2(8, 8) * base.RequestedScale;
        base.BrushInterfacer = BrushInterfacer.Create(brush, (index, scale) =>
        {
            if (Top.GlobalConfig.ShowBrushPreview)
                RoomEditor.TilesetViewer.DrawTileImage(index, scale);
        });

        // Register mouse buttons for tile selection & placement
        RegisterTilePlacementInputs(
            brush,
            (x, y) => RoomLayout.GetTile(x, y),
            (x, y, tile) => RoomLayout.SetTile(x, y, tile),
            () => RoomLayout.Width,
            () => RoomLayout.Height,
            () => Project.TransactionManager.InsertBarrier());

        QuickstartData.enableToggledEvent += (s, a) => UpdateQuickstartOverlayBox();
        RoomEditor.TabChangedEvent += (s, a) => UpdateOverlayBoxes();
        Project.AnnotationsAddedOrRemovedEvent += () => UpdateOverlayBoxes();

        // Show coordinate tooltip
        base.OnHover = (int tile) =>
        {
            if (Top.GlobalConfig.ShowCoordinateTooltip && this.HoveredTile != -1)
            {
                int x = tile % Width;
                int y = tile / Width;
                ImGuiX.Tooltip($"{x}, {y} (${y:X}{x:X})");
            }
        };

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
            (_, _) => UpdateOverlayBoxes(),
            weak: false);

        warpGroupEventWrapper = new EventWrapper<WarpGroup>();
        warpGroupEventWrapper.Bind<EventArgs>(
            "ModifiedEvent",
            (_, _) => UpdateOverlayBoxes(),
            weak: false);

        overlayBoxUser = new(this);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    TextureBase texture;
    OverlayBox selectedOverlayBox;
    List<OverlayBox> overlayBoxes;
    bool draggingComponent;
    Vector2 draggingComponentOffset;
    bool suppressCurrentClick;

    EventWrapper<Room> roomEventWrapper;
    EventWrapper<ObjectGroup> objectGroupEventWrapper;
    EventWrapper<Chest> chestEventWrapper;
    EventWrapper<WarpGroup> warpGroupEventWrapper;

    RoomLayoutOverlayBox overlayBoxUser;

    // ================================================================================
    // Events
    // ================================================================================

    // Invoked when the selected OverlayBox is changed.
    public event EventHandler ChangeSelectedOverlayBoxEvent;

    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public Room Room { get { return RoomLayout?.Room; } }
    public RoomLayout RoomLayout { get; private set; }
    public QuickstartData QuickstartData { get { return Workspace.QuickstartData; } }
    public Brush<int> Brush { get; private set; }

    public ObjectDefinition SelectedObject { get { return (selectedOverlayBox as ObjectOverlayBox)?.obj; } }
    public Warp SelectedWarpSource { get { return (selectedOverlayBox as WarpSourceOverlayBox)?.warp; } }

    // TileGrid overrides

    public override Vector2 WidgetSize
    {
        get
        {
            // Always keep it 256x256 so that objects can be rendered even out of bounds, and
            // also so that it doesn't fluctuate when moving between small & large rooms
            return new Vector2(256.0f * Scale, 256.0f * Scale) + RenderOffset * ImGuiX.ScaleUnit;
        }
    }

    public override TextureBase Texture { get { return texture; } }

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
    bool DrawAnnotations { get { return Workspace.ViewAnnotations && RoomEditor.EditingWarpDestination == null; } }


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

        // Render remote cursors
        foreach (var remoteState in Workspace.RemoteStates.Values)
        {
            CursorPosition cursor = remoteState.CursorPosition;
            if (cursor.room == Room.Index && cursor.tileStart != -1)
            {
                base.AddRect(base.TileRangeRect(cursor.tileStart, cursor.tileEnd),
                             remoteState.Color, ImGuiX.Unit(base.RectThickness));
            }
        }

        var hoveringComponent = IOverlayBoxUser.UpdateOverlayBoxes(
            Project,
            overlayBoxUser,
            this,
            overlayBoxes,
            alwaysFullPrecision: false,
            clipToCanvas: false);

        // Render right-click menu
        if (ImGui.BeginPopup(Top.RightClickPopupName))
        {
            var com = selectedOverlayBox;

            // This can happen if a remote client deleted the object we were looking at.
            if (com == null)
            {
                ImGui.CloseCurrentPopup();
            }
            else if (com is ObjectOverlayBox ocom)
            {
                ObjectBox.ObjectPopupMenu(ocom.obj, Room, Workspace);
            }
            else if (com is WarpSourceOverlayBox wcom)
            {
                WarpEditor.WarpPopupMenu(wcom.warp, RoomEditor);
            }
            else if (com is WarpDestOverlayBox dcom)
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

        if (hoveringComponent == null && !draggingComponent && !SuppressTileSelection)
            base.RenderHoverAndSelection(true);
        else
            base.RenderHoverAndSelection(false);

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
            if (selectedOverlayBox is ObjectOverlayBox)
                SetSelectedOverlayBox(null);
            return;
        }

        foreach (OverlayBox com in overlayBoxes)
        {
            if ((com as ObjectOverlayBox)?.obj == obj)
            {
                SetSelectedOverlayBox(com);
                return;
            }
        }

        // Not fonud
        SetSelectedOverlayBox(null);
    }

    /// <summary>
    /// Sets the selected room component to the specified warp source if it's in the room component list.
    /// </summary>
    public void SelectWarpSource(Warp warp)
    {
        if (warp == null)
        {
            if (selectedOverlayBox is WarpSourceOverlayBox)
                SetSelectedOverlayBox(null);
            return;
        }

        foreach (OverlayBox com in overlayBoxes)
        {
            if ((com as WarpSourceOverlayBox)?.warp == warp)
            {
                SetSelectedOverlayBox(com);
                break;
            }
        }
    }

    /// <summary>
    /// If any room component is selected, unselect it.
    /// </summary>
    public void UnselectOverlayBox()
    {
        SetSelectedOverlayBox(null);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void SetSelectedOverlayBox(OverlayBox comp, bool startDrag = false, Vector2? dragOffset = null)
    {
        // Even if the OverlayBox object changed, the underlying data may actually be the same as before.
        // "isDifferent" checks the underlying data rather than the OverlayBoxes themselves.
        bool isDifferent = comp == null || !comp.Compare(selectedOverlayBox);
        if (isDifferent || startDrag)
        {
            draggingComponent = startDrag;
            if (startDrag)
                draggingComponentOffset = (Vector2)dragOffset;
        }

        if (selectedOverlayBox == comp)
            return;
        OverlayBox old = selectedOverlayBox;
        selectedOverlayBox = comp;

        if (isDifferent)
            ChangeSelectedOverlayBoxEvent?.Invoke(this, null);
    }

    /// <summary>
    /// Called when the room being edited is changed
    /// </summary>
    void OnRoomChanged()
    {
        texture = Workspace.GetCachedRoomTexture(RoomLayout);

        base.Width = RoomLayout.Width;
        base.Height = RoomLayout.Height;

        UpdateOverlayBoxes();

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
        UpdateOverlayBoxes();
    }

    /// <summary>
    /// Regenerate room component list
    /// </summary>
    public void UpdateOverlayBoxes()
    {
        overlayBoxes = new List<OverlayBox>();

        if (Room == null)
            return;

        UpdateQuickstartOverlayBox();

        // Object room components
        if (DrawObjects)
        {
            foreach (var group in Room.GetObjectGroup().GetAllGroups())
            {
                foreach (var obj in group.GetObjects())
                {
                    if (obj.HasXY())
                    {
                        var com = new ObjectOverlayBox(this, obj);
                        overlayBoxes.Add(com);
                    }
                }
            }
        }

        // Chest
        if (DrawChests)
        {
            overlayBoxes.Add(new ChestOverlayBox(this, Room.Chest));
        }

        // Warps
        if (DrawWarps)
        {
            var warpGroup = Room.GetWarpGroup();
            for (int warpIndex = 0; warpIndex < warpGroup.Count; warpIndex++)
            {
                Warp warp = warpGroup.GetWarp(warpIndex);

                Action<int, int, int, int> addWarpComponent = (x, y, width, height) =>
                {
                    var rect = new FRect(x, y, width, height);
                    var com = new WarpSourceOverlayBox(this, warp, warpIndex, rect);
                    overlayBoxes.Add(com);
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

        if (DrawAnnotations)
        {
            foreach (Annotation ann in Project.GetRoomAnnotations(Room.Index))
            {
                overlayBoxes.Add(new AnnotationOverlayBox(this, ann));
            }
        }

        // Warp destination editing mode
        if (RoomEditor.EditingWarpDestination != null)
        {
            var com = new WarpDestOverlayBox(this, RoomEditor.EditingWarpDestination);
            overlayBoxes.Add(com);
        }

        // Below here, we are concerned with updating the selectedOverlayBox.
        if (selectedOverlayBox == null)
            return;

        // Check if the previous selectedOverlayBox has an equivalent in the new list. If not,
        // unselect it.
        foreach (var com in overlayBoxes)
        {
            if (com.Compare(selectedOverlayBox))
            {
                SetSelectedOverlayBox(com);
                return;
            }
        }

        // We are unselecting the previous selected OverlayBox. If we were dragging it, suppress
        // mouse inputs so that we don't suddenly start drawing room tiles.
        // This can occur when double-clicking on a warp to follow it, or toggling quickstart (F4
        // key) while dragging the Link icon.
        if (draggingComponent)
            suppressCurrentClick = true;
        SetSelectedOverlayBox(null);
    }

    /// <summary>
    /// Called when quickstart is toggled or room component list is updated
    /// </summary>
    void UpdateQuickstartOverlayBox()
    {
        bool validInRoom = QuickstartData.Enabled
            && QuickstartData.group == Room.Group
            && QuickstartData.room == (Room.Index & 0xff);

        // Search for existing QuickstartOverlayBox
        var existingComponents = overlayBoxes.Where((com) => com is QuickstartOverlayBox);

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
                overlayBoxes.Remove(com);
                if (com == selectedOverlayBox)
                {
                    SetSelectedOverlayBox(null);
                }
            }
        }
        else // Doesn't exist in list
        {
            if (validInRoom)
            {
                var com = new QuickstartOverlayBox(this, QuickstartData);
                overlayBoxes.Add(com);
            }
            else
                return;
        }
    }

    // ================================================================================
    // Nested classes
    // ================================================================================

    class QuickstartOverlayBox : OverlayBox
    {
        QuickstartData quickstart;

        public QuickstartOverlayBox(RoomLayoutEditor parent, QuickstartData data) : base(parent)
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

        public override void Render(bool selected)
        {
            // Get component position relative to widget origin
            Vector2 pos = new Vector2(X, Y) * Parent.Scale;
            Vector2 size = new Vector2(BoxWidth, BoxHeight) * Parent.Scale;

            ImGuiX.ShiftCursorScreenPos(pos - size / 2);
            TextureBase linkTexture = Top.TextureFromBitmapTracked(Parent.Project.LinkBitmap);
            ImGuiX.DrawImage(linkTexture, Parent.Scale);
        }

        public override bool Compare(OverlayBox com)
        {
            return quickstart == (com as QuickstartOverlayBox)?.quickstart;
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
    class ChestOverlayBox : OverlayBox
    {
        Chest chest;

        public ChestOverlayBox(RoomLayoutEditor parent, Chest chest) : base(parent)
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

        public override void Render(bool selected)
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

        public override bool Compare(OverlayBox com)
        {
            return chest == (com as ChestOverlayBox)?.chest;
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

    class ObjectOverlayBox : OverlayBox
    {
        public ObjectDefinition obj;


        public ObjectOverlayBox(RoomLayoutEditor parent, ObjectDefinition obj) : base(parent)
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


        public override void Render(bool selected)
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

        public override bool Compare(OverlayBox com)
        {
            return obj == (com as ObjectOverlayBox)?.obj;
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

    class WarpSourceOverlayBox : OverlayBox
    {
        public Warp warp;
        public int index;

        FRect rect;


        public WarpSourceOverlayBox(RoomLayoutEditor parent, Warp warp, int index, FRect rect) : base(parent)
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


        public override void Render(bool selected)
        {
            // Draw digit representing the warp index in the center of the rectangle
            ImGuiLL.PushMenuFont(ImGuiLL.MenuFontLargeScale);
            var origin = ImGui.GetCursorScreenPos();
            string text = index.ToString("X");
            ImGuiX.DrawTextAt(text, origin + BoxRectangle.Center * Parent.Scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.PopFont();
        }

        public override bool Compare(OverlayBox com)
        {
            return warp == (com as WarpSourceOverlayBox)?.warp;
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
    class WarpDestOverlayBox : OverlayBox
    {
        public Warp warp;

        public WarpDestOverlayBox(RoomLayoutEditor parent, Warp warp) : base(parent)
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


        public override void Render(bool selected)
        {
            ImGuiLL.PushMenuFont(ImGuiLL.MenuFontLargeScale);
            var origin = ImGui.GetCursorScreenPos();
            ImGuiX.DrawTextAt("W", origin + BoxRectangle.Center * Parent.Scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.PopFont();
        }

        public override bool Compare(OverlayBox com)
        {
            return warp == (com as WarpDestOverlayBox)?.warp;
        }

        public override string GetTransactionIdentifier()
        {
            return "warp dest#" + warp.TransactionIdentifier;
        }
    }

    /// <summary>
    /// Text strings inserted into the room as notes.
    /// </summary>
    public class AnnotationOverlayBox : OverlayBox
    {
        Annotation annotation;
        bool wasOpenLastFrame = false;
        bool isActive = false;

        public AnnotationOverlayBox(RoomLayoutEditor parent, Annotation annotation) : base(parent)
        {
            this.annotation = annotation;
        }


        public override Color? BoxColor
        {
            get
            {
                return annotation.Color;
            }
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
            get { return annotation.X; }
            set
            {
                annotation.X = (byte)value;
            }
        }
        public override int Y
        {
            get { return annotation.Y; }
            set
            {
                annotation.Y = (byte)value;
            }
        }

        public override int BoxWidth { get { return 16; } }
        public override int BoxHeight { get { return 16; } }


        public override void Render(bool selected)
        {
            var origin = ImGui.GetCursorScreenPos();
            var realRect = BoxRectangle * Parent.Scale + origin;
            var mousePos = ImGui.GetMousePos();

            // This is checked to auto-close the popup window when it's cilcked off of, _unless_
            // we're clicking onto the OverlayBox. Can't use IsItemHovered() for this because it's
            // not triggered while the mouse is held down.
            bool hovering = realRect.Contains(mousePos);

            if (selected)
                isActive = true;

            // Show popup window if it's active
            if (isActive && !ImGui.IsPopupOpen(Top.RightClickPopupName))
            {
                ImGuiWindowFlags flags = 0
                    | ImGuiWindowFlags.NoNavFocus
                    | ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoCollapse
                    | ImGuiWindowFlags.NoSavedSettings;

                // Popup window follows the OverlayBox
                ImGui.SetNextWindowPos(origin + BoxRectangle.TopLeft * Parent.Scale
                                       + ImGuiX.Unit(16, 0) * Parent.RequestedScale,
                                       0,
                                       new Vector2(0, 0));

                bool focused = false;

                if (ImGui.Begin("Annotation", ref isActive, flags))
                {
                    ImGuiLL.PushInfoFont();

                    // Letter input
                    ImGui.Text("Letter:");
                    ImGui.SameLine();
                    ImGui.PushItemWidth(ImGuiX.Unit(20.0f));
                    string letterInput = annotation.Letter.ToString();
                    ImGui.InputText("##Letter", ref letterInput, 1, ImGuiInputTextFlags.ReadOnly);
                    ImGui.PopItemWidth();
                    ImGui.SameLine();
                    ImGui.PushItemFlag(ImGuiItemFlags.ButtonRepeat, true);
                    if (ImGui.Button("-"))
                    {
                        char c = annotation.Letter;
                        if ((c-1) >= 'A')
                            annotation.Letter = (char)(c-1);
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("+"))
                    {
                        char c = annotation.Letter;
                        if ((c+1) <= 'Z')
                            annotation.Letter = (char)(c+1);
                    }
                    ImGui.PopItemFlag();

                    // Color selection
                    ImGui.SameLine();
                    ImGui.PushItemWidth(ImGuiX.Unit(100.0f));
                    int selectedColor = (int)annotation.ColorIndex;
                    if (ImGui.Combo(
                            "##Color",
                            ref selectedColor,
                            Enum.GetNames(typeof(Annotation.AnnotationColor)),
                            Enum.GetNames(typeof(Annotation.AnnotationColor)).Length))
                    {
                        annotation.ColorIndex = (Annotation.AnnotationColor)selectedColor;
                    }
                    ImGui.PopItemWidth();

                    // Text input
                    if (!wasOpenLastFrame)
                        ImGui.SetKeyboardFocusHere();
                    string text = annotation.Text;
                    if (ImGui.InputTextMultiline("##Text input", ref text, Annotation.MAX_TEXT_LENGTH, ImGuiX.Unit(600, 300),
                                                 ImGuiInputTextFlags.CtrlEnterForNewLine))
                    {
                        annotation.Text = text;
                    }
                    ImGui.Text("Press Ctrl+Enter to insert a new line, Enter to close this.");
                    ImGui.PopFont();
                    focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);

                    // If we're hovering over the icon, give focus back to the window so that we
                    // won't accidentally close it if we move the mouse too fast (outside the bounds
                    // of the icon) later on.
                    if (hovering)
                        ImGui.SetWindowFocus();
                }
                ImGui.End();

                bool onlyEnterPressed = (ImGui.IsKeyPressed(ImGuiKey.Enter)
                                         && !ImGui.IsKeyChordPressed((int)(ImGuiKey.Enter | ImGuiKey.ModCtrl)));
                if (!isActive || onlyEnterPressed || (!focused && !hovering) || !selected)
                {
                    isActive = false;
                    if (selected)
                        Parent.UnselectOverlayBox();
                }

                wasOpenLastFrame = true;
            }
            else
            {
                ImGuiX.TooltipOnHover(annotation.Text == ""
                                      ? "Annotation goes here! Use this to jot down notes."
                                      : annotation.Text);
                wasOpenLastFrame = false;
            }

            ImGuiLL.PushMenuFont(ImGuiLL.MenuFontLargeScale);
            ImGuiX.DrawTextAt(annotation.Letter.ToString(), origin + BoxRectangle.Center * Parent.Scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.PopFont();
        }

        public override bool Compare(OverlayBox com)
        {
            return annotation == (com as AnnotationOverlayBox)?.annotation;
        }

        public override void Delete()
        {
            annotation.Delete();
        }

        public override string GetTransactionIdentifier()
        {
            return "annotation#" + annotation.TransactionIdentifier;
        }
    }
}

