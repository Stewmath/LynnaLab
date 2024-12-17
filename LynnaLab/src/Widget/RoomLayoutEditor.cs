namespace LynnaLab;

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
            (s, a) => UpdateChestComponent(),
            weak: false);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Image image;
    RoomComponent selectedRoomComponent;
    ChestRoomComponent chestRoomComponent;
    List<RoomComponent> roomComponents;
    bool draggingComponent;
    Vector2 draggingComponentOffset;
    EventWrapper<Room> roomEventWrapper;

    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public Room Room { get { return RoomLayout?.Room; } }
    public RoomLayout RoomLayout { get; private set; }
    public QuickstartData QuickstartData { get { return Workspace.QuickstartData; } }
    public Brush Brush { get; private set; }

    public bool DrawChest { get { return true; } }


    // TileGrid overrides

    public override Vector2 WidgetSize
    {
        get
        {
            // Always keep it 256x256 so that objects can be rendered even out of bounds, and
            // also so that it doesn't fluctuate when moving between small & large rooms
            return new Vector2(256.0f * Scale, 256.0f * Scale);
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

        bool inhibitMouse = false;

        Action<RoomComponent> renderRoomComponent = (com) =>
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

            // Draw hover outline if mouse covering
            var mousePos = base.GetRelativeMousePos() / Scale;
            var rect = com.BoxRectangle;
            if (rect.Contains(mousePos))
            {
                base.AddRect(rect * Scale, Color.Cyan, thickness: Scale);
            }

            // Draw selection outline if selected
            if (com == selectedRoomComponent)
            {
                base.AddRect(rect * Scale, Color.White, thickness: Scale);
            }

            // Check mouse operations
            if (rect.Contains(mousePos))
            {
                inhibitMouse = true;

                // Clicked on
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    selectedRoomComponent = com;
                    draggingComponent = true;
                    draggingComponentOffset.X = com.X - mousePos.X;
                    draggingComponentOffset.Y = com.Y - mousePos.Y;
                }
            }

            // Update room component dragging
            if (draggingComponent && com == selectedRoomComponent)
            {
                inhibitMouse = true;

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

                        var snapTo = (int oldPos, int mousePos) => {
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
        };

        // Render room components
        foreach (var com in roomComponents)
        {
            renderRoomComponent(com);
        }
        if (chestRoomComponent != null)
        {
            renderRoomComponent(chestRoomComponent);
        }

        ImGui.SetCursorScreenPos(endPos);

        if (!inhibitMouse)
        {
            if (Workspace.ShowBrushPreview)
            {
                base.RenderBrushPreview(Brush, (index) =>
                {
                    RoomEditor.TilesetViewer.DrawTileImage(index, Scale, transparent: true);
                });
            }

            base.RenderHoverAndSelection(Brush);
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Called when room is changed
    /// </summary>
    void OnRoomChanged()
    {
        image = Workspace.GetCachedRoomImage(RoomLayout);

        base.Width = RoomLayout.Width;
        base.Height = RoomLayout.Height;

        UpdateChestComponent();
        UpdateRoomComponents();

        roomEventWrapper.ReplaceEventSource(Room);
    }

    /// <summary>
    /// Called when a chest has been added to the current room
    /// </summary>
    void UpdateChestComponent()
    {
        if (Room.Chest != null)
        {
            this.chestRoomComponent = new ChestRoomComponent(this, Room.Chest);
        }
        else
        {
            this.chestRoomComponent = null;
        }
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
                roomComponents.Remove(com);
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
