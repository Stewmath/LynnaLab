namespace LynnaLab;

/// <summary>
/// Tab in the RoomEditor for editing warps.
/// </summary>
public class WarpEditor : Frame
{
    // ================================================================================
    // Static variables
    // ================================================================================

    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    public static readonly Color WarpSourceColor = Color.FromRgba(186, 8, 206, 0xc0);


    // ================================================================================
    // Constructor
    // ================================================================================

    public WarpEditor(ProjectWorkspace workspace, string name, WarpGroup group) : base(name)
    {
        this.Workspace = workspace;

        warpSourceBox = new WarpSourceBox("Warp Sources", group);
        warpSourceBox.SelectedEvent += (index) =>
        {
            SelectedWarpEvent?.Invoke(this, null);
        };
    }



    // ================================================================================
    // Variables
    // ================================================================================


    // GUI stuff
    WarpSourceBox warpSourceBox;

    int map;

    // ================================================================================
    // Events
    // ================================================================================

    public event EventHandler<EventArgs> SelectedWarpEvent;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }

    public int SelectedIndex
    {
        get { return warpSourceBox.SelectedIndex; }
        set { warpSourceBox.SelectedIndex = value; }
    }

    // Should never be non-null
    public WarpGroup WarpGroup
    {
        get; private set;
    }

    public Warp SelectedWarp
    {
        get
        {
            if (SelectedIndex == -1)
                return null;
            else if (SelectedIndex >= WarpGroup.Count)
            {
                log.Warn("SelectedIndex >= WarpGroup.Count?");
                return null;
            }
            return WarpGroup.GetWarp(SelectedIndex);
        }
    }

    Project Project
    {
        get { return WarpGroup.Project; }
    }


    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.SeparatorText("Warps");

        if (ImGui.BeginChild("WarpSourceBox Frame",
                             warpSourceBox.WidgetSize + new Vector2(12.0f, 16.0f),
                             ImGuiChildFlags.Border))
        {
            warpSourceBox.Render();
        }
        ImGui.EndChild();

        if (SelectedWarp != null)
        {
            ImGuiLL.RenderValueReferenceGroup(SelectedWarp.ValueReferenceGroup, null, Workspace.ShowDocumentation, maxRows: 100);
        }
    }

    public void SetMap(int group, int map)
    {
        SetWarpGroup(Project.GetIndexedDataType<WarpGroup>((group << 8) | map));
        this.map = map;
        SetWarpIndex(-1);
    }

    public void SetWarpGroup(WarpGroup group)
    {
        if (WarpGroup != group)
        {
            WarpGroup = group;
            warpSourceBox.SetWarpGroup(group);
        }
    }

    // Load the i'th warp in the current map.
    public void SetWarpIndex(int i)
    {
        if (i >= WarpGroup.Count)
        {
            log.Warn(string.Format("Tried to select warp index {0} (highest is {1})", i, WarpGroup.Count - 1));
            i = WarpGroup.Count - 1;
        }

        SelectedIndex = i;
    }

    public void SetSelectedWarp(Warp warp)
    {
        if (warp == null)
        {
            SelectedIndex = -1;
            return;
        }

        int i = WarpGroup.IndexOf(warp);
        if (i == -1)
        {
            log.Warn("Couldn't find warp to select in WarpEditor");
            return;
        }
        SelectedIndex = i;
    }

    // Gets the index corresponding to a spin button value.
    Warp GetWarpIndex(int i)
    {
        if (i == -1)
            return null;
        return WarpGroup.GetWarp(i);
    }

    int GetWarpIndex(Warp warp)
    {
        return WarpGroup.IndexOf(warp);
    }


    class WarpSourceBox : SelectionBox
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        public WarpSourceBox(string name, WarpGroup warpGroup) : base(name)
        {
            base.Unselectable = true;
            base.RectThickness = 1.0f;

            warpGroupEventWrapper = new EventWrapper<WarpGroup>();
            warpGroupEventWrapper.Bind<EventArgs>("StructureModifiedEvent",
                                                  (_, _) => OnWarpGroupModified(),
                                                  weak: false);
            SetWarpGroup(warpGroup);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        EventWrapper<WarpGroup> warpGroupEventWrapper;

        // ================================================================================
        // Properties
        // ================================================================================

        public WarpGroup WarpGroup { get; private set; }

        // ================================================================================
        // Public methods
        // ================================================================================

        public void SetWarpGroup(WarpGroup group)
        {
            if (group != WarpGroup)
            {
                this.WarpGroup = group;
                SelectedIndex = -1;
                OnWarpGroupModified();
            }
        }

        // ================================================================================
        // SelectionBox overrides
        // ================================================================================

        protected override void OnMoveSelection(int oldIndex, int newIndex)
        {
            // Can't be moved; do nothing
        }

        // TODO
        protected override void ShowPopupMenu()
        {
        //     Gtk.Menu menu = new Gtk.Menu();
        //     {
        //         Gtk.MenuItem item = new Gtk.MenuItem("Add standard warp");
        //         menu.Append(item);

        //         item.Activated += (sender, args) =>
        //         {
        //             SelectedIndex = WarpGroup.AddWarp(WarpSourceType.Standard);
        //         };
        //     }

        //     {
        //         Gtk.MenuItem item = new Gtk.MenuItem("Add specific-position warp");
        //         menu.Append(item);

        //         item.Activated += (sender, args) =>
        //         {
        //             SelectedIndex = WarpGroup.AddWarp(WarpSourceType.Position);
        //         };
        //     }

        //     if (HoveringIndex != -1)
        //     {
        //         menu.Append(new Gtk.SeparatorMenuItem());

        //         Gtk.MenuItem deleteItem = new Gtk.MenuItem("Delete");
        //         deleteItem.Activated += (sender, args) =>
        //         {
        //             if (SelectedIndex != -1)
        //                 WarpGroup.RemoveWarp(SelectedIndex);
        //         };
        //         menu.Append(deleteItem);
        //     }

        //     menu.AttachToWidget(this, null);
        //     menu.ShowAll();
        //     menu.PopupAtPointer(ev);
        }

        protected override void TileDrawer(int index)
        {
            if (index >= WarpGroup.Count)
                return;

            // Draw purple square
            var rect = base.TileRect(index);
            ImGui.SetCursorScreenPos(base.origin + rect.TopLeft);
            base.AddRectFilled(rect, WarpEditor.WarpSourceColor);

            // Draw digit representing the warp index in the center of the rectangle
            ImGui.PushFont(TopLevel.OraclesFont);
            string text = index.ToString("X");
            Vector2 textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorScreenPos(base.origin + rect.Center - textSize / 2 + new Vector2(1, 0));
            ImGui.Text(text);
            ImGui.PopFont();
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        void OnWarpGroupModified()
        {
            if (WarpGroup.Count == 0)
            {
                base.MaxIndex = 0;
                Selectable = false;
            }
            else
            {
                base.MaxIndex = WarpGroup.Count;
                base.Selectable = true;
            }
        }
    }
}
