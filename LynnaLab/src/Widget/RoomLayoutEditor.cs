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
    public RoomLayoutEditor(ProjectWorkspace workspace, RoomEditor roomEditor, Brush brush)
        : base("Room Layout Editor")
    {
        this.Workspace = workspace;
        this.Brush = brush;
        this.RoomEditor = roomEditor;

        base.TileWidth = 16;
        base.TileHeight = 16;
        base.Scale = 2;
        base.RenderOffset = new Vector2(8, 8) * Scale;

        // Register mouse buttons for tile selection & placement
        RegisterTilePlacementInputs(
            brush,
            (x, y) => RoomLayout.GetTile(x, y),
            (x, y, tile) => RoomLayout.SetTile(x, y, tile),
            () => RoomLayout.Width,
            () => RoomLayout.Height);

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
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Image image;
    RoomComponent selectedRoomComponent;
    List<RoomComponent> roomComponents;
    bool draggingComponent;
    Vector2 draggingComponentOffset;
    EventWrapper<Room> roomEventWrapper;
    EventWrapper<ObjectGroup> objectGroupEventWrapper;
    EventWrapper<Chest> chestEventWrapper;

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
    public Brush Brush { get; private set; }
    public ObjectDefinition SelectedObject { get { return (selectedRoomComponent as ObjectRoomComponent)?.obj; } }


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

    protected override Image Image { get { return image; } }

    private RoomEditor RoomEditor { get; set; }


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

        RoomComponent hoveringComponent = null;
        var mousePos = base.GetRelativeMousePos() / Scale;

        // Render room components
        foreach (var com in roomComponents)
        {
            // Draw background box
            if (com.BoxColor != null)
            {
                base.AddRectFilled(com.BoxRectangle * Scale, com.BoxColor);
            }

            // Draw it
            ImGui.SetCursorScreenPos(origin);
            com.Render();

            // Create an InvisibleButton so that we can select it without dragging the entire window.
            // Names are not unique... probably not an issue? These don't really do anything.
            ImGui.SetCursorScreenPos(origin + (new Vector2(com.X, com.Y) - com.BoxSize / 2) * Scale);
            ImGui.InvisibleButton($"RoomComponent button", com.BoxSize * Scale);

            // Check if we're hovering over it. But don't draw the hovering rectangle right now
            // because we don't want to draw it on more than one RoomComponent.
            var rect = com.BoxRectangle;
            if (rect.Contains(mousePos))
            {
                hoveringComponent = com;
            }
        }

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
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !draggingComponent)
                {
                    bool changed = selectedRoomComponent != hoveringComponent;
                    selectedRoomComponent = hoveringComponent;
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        draggingComponent = true;
                        draggingComponentOffset.X = hoveringComponent.X - mousePos.X;
                        draggingComponentOffset.Y = hoveringComponent.Y - mousePos.Y;
                    }
                    if (changed)
                        ChangedSelectedRoomComponentEvent?.Invoke(this, null);
                }

                // Right click
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !draggingComponent)
                {
                    ImGui.OpenPopup(TopLevel.RightClickPopupName);
                }
            }
        }

        // Render right-click menu
        if (ImGui.BeginPopup(TopLevel.RightClickPopupName))
        {
            var com = selectedRoomComponent;

            if (com.Deletable && ImGui.Selectable("Delete"))
            {
                com.Delete();
            }
            ImGui.EndPopup();
        }

        // Update room component dragging
        if (draggingComponent)
        {
            var com = selectedRoomComponent;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                // Shortened XY: Snapping is "automatic" due to low resolution of XY variables;
                // also don't add draggingComponentOffset as it only works well with smooth
                // movement
                if (com.HasShortenedXY)
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
            }
            else
            {
                draggingComponent = false;
            }
        }

        ImGui.SetCursorScreenPos(endPos);

        if (hoveringComponent == null && !draggingComponent)
        {
            if (Workspace.ShowBrushPreview)
            {
                base.RenderBrushPreview(Brush, (index) =>
                {
                    RoomEditor.TilesetViewer.DrawTileImage(index, Scale, !SelectingRectangle);
                });
            }

            base.RenderHoverAndSelection(Brush);
        }
    }

    /// <summary>
    /// Sets the selected room component to the specified object if it's in the room component list.
    /// </summary>
    public void SelectObject(ObjectDefinition obj)
    {
        foreach (RoomComponent com in roomComponents)
        {
            if ((com as ObjectRoomComponent)?.obj == obj)
            {
                if (selectedRoomComponent != com)
                {
                    selectedRoomComponent = com;
                    draggingComponent = false;
                    ChangedSelectedRoomComponentEvent?.Invoke(this, null);
                }
                break;
            }
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

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
    void UpdateRoomComponents()
    {
        roomComponents = new List<RoomComponent>();

        if (Room == null)
            return;

        UpdateQuickstartRoomComponent();

        // Object room components
        if (RoomEditor.ObjectTabActive)
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
        if (RoomEditor.ChestTabActive && Room.Chest != null)
        {
            roomComponents.Add(new ChestRoomComponent(this, Room.Chest));
        }

        // Check if the previous selectedRoomComponent has an equivalent in the new list. If not,
        // unselect it.
        foreach (var com in roomComponents)
        {
            if (com.Compare(selectedRoomComponent))
            {
                selectedRoomComponent = com;
                return;
            }
        }
        if (selectedRoomComponent != null)
        {
            selectedRoomComponent = null;
            draggingComponent = false;
            ChangedSelectedRoomComponentEvent(this, null);
        }
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
                    selectedRoomComponent = null;
                    draggingComponent = false;
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

        public abstract Color BoxColor { get; }

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
        public FRect BoxRectangle
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
            Image image = TopLevel.ImageFromBitmap(sprite);
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

        public override Color BoxColor
        {
            get { return null; }
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
            var linkImage = TopLevel.ImageFromBitmap(Parent.Project.LinkBitmap);
            ImGuiX.DrawImage(linkImage, Parent.Scale);
        }

        public override bool Compare(RoomComponent com)
        {
            return quickstart == (com as QuickstartRoomComponent)?.quickstart;
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

        public override Color BoxColor
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
                    Project.EvalToInt("INTERAC_TREASURE") * 256 + chest.Treasure.Graphics);
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
    }

    class ObjectRoomComponent : RoomComponent
    {
        public ObjectDefinition obj;


        public ObjectRoomComponent(RoomLayoutEditor parent, ObjectDefinition obj) : base(parent)
        {
            this.obj = obj;
        }


        public override Color BoxColor
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
                var origin = ImGui.GetCursorScreenPos();
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

        // TODO: Right click -> "Clone" button

        public override bool Compare(RoomComponent com)
        {
            return obj == (com as ObjectRoomComponent)?.obj;
        }

        public override void Delete()
        {
            obj.Remove();
        }
    }
}
