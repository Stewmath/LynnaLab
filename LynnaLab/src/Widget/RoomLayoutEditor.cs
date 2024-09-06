namespace LynnaLab;

public class RoomLayoutEditor : TileGridViewer
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public RoomLayoutEditor(ProjectWorkspace workspace)
    {
        this.Workspace = workspace;

        base.TileWidth = 16;
        base.TileHeight = 16;
        base.Scale = 2;

        QuickstartData.enableToggledEvent += (s, a) => UpdateQuickstartRoomComponent();
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Image image;
    RoomComponent selectedRoomComponent;
    List<RoomComponent> roomComponents;
    bool draggingComponent;

    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public TopLevel TopLevel { get { return Workspace.TopLevel; } }
    public Room Room { get { return RoomLayout?.Room; } }
    public RoomLayout RoomLayout { get; private set; }
    public QuickstartData QuickstartData { get { return Workspace.QuickstartData; } }


    // TileGridViewer overrides

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
            RoomChanged();
        }
    }

    public override void Render()
    {
        base.Render(inhibitMouse: true);

        bool hovered = ImGui.IsItemHovered();
        bool inhibitMouse = false;

        Action<RoomComponent> renderRoomComponent = (com) =>
        {
            // Draw it
            ImGui.SetCursorScreenPos(origin);
            com.Render();

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
                }
            }

            // Update dragging
            if (draggingComponent && com == selectedRoomComponent)
            {
                inhibitMouse = true;

                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    com.X = (int)Math.Round(mousePos.X);
                    com.Y = (int)Math.Round(mousePos.Y);
                }
                else
                {
                    draggingComponent = false;
                }
            }
        };

        // Render room components
        var endPos = ImGui.GetCursorScreenPos();
        foreach (var com in roomComponents)
        {
            renderRoomComponent(com);
        }
        ImGui.SetCursorScreenPos(endPos);

        if (hovered && !inhibitMouse)
            base.RenderMouse();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Called when room is changed
    /// </summary>
    void RoomChanged()
    {
        image = null;

        if (RoomLayout != null)
        {
            image = Workspace.GetCachedRoomImage(RoomLayout);

            base.Width = RoomLayout.Width;
            base.Height = RoomLayout.Height;
        }

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
        public abstract int X { get; set; } // These are always in the full range even if "ShortPosition" is true
        public abstract int Y { get; set; }
        public abstract int BoxWidth { get; }
        public abstract int BoxHeight { get; }

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
    }

    class QuickstartRoomComponent : RoomComponent
    {
        QuickstartData quickstart;
        RoomLayoutEditor parent;

        public QuickstartRoomComponent(RoomLayoutEditor parent, QuickstartData data)
        {
            this.parent = parent;
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
            Vector2 pos = new Vector2(X, Y) * parent.Scale;
            Vector2 size = new Vector2(BoxWidth, BoxHeight) * parent.Scale;

            ImGuiX.ShiftCursorScreenPos(pos - size / 2);
            var linkImage = parent.TopLevel.ImageFromBitmap(parent.Project.LinkBitmap);
            ImGuiX.DrawImage(linkImage, parent.Scale);
        }

        public override bool Compare(RoomComponent com)
        {
            return quickstart == (com as QuickstartRoomComponent)?.quickstart;
        }
    }
}
