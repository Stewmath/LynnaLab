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

        warpSourceBox = new WarpSourceBox(this, "Warp Sources", group);
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

    // Invoked when "right click -> follow" happens
    public event EventHandler<Warp> FollowEvent;

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
            return warpSourceBox.SelectedWarp;
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

    // ================================================================================
    // Private methods
    // ================================================================================

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

    // ================================================================================
    // Static methods
    // ================================================================================

    public static void WarpPopupMenu(Warp warp, Action followAction)
    {
        if (ImGui.Selectable("Follow"))
        {
            followAction();
        }
        if (ImGui.Selectable("Delete"))
        {
            warp.Remove();
        }
    }


    // ================================================================================
    // Subclass: WarpSourceBox
    // ================================================================================

    class WarpSourceBox : SelectionBox
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        public WarpSourceBox(WarpEditor parent, string name, WarpGroup warpGroup) : base(name)
        {
            this.Parent = parent;
            base.Unselectable = true;
            base.RectThickness = 1.0f;

            warpGroupEventWrapper = new EventWrapper<WarpGroup>();
            warpGroupEventWrapper.Bind<EventArgs>("ModifiedEvent",
                                                  (_, _) => OnWarpGroupModified(),
                                                  weak: false);
            SetWarpGroup(warpGroup);

            // Right click on a warp to open a menu
            base.AddMouseAction(
                MouseButton.RightClick,
                MouseModifier.None,
                MouseAction.Click,
                GridAction.Callback,
                (s, arg) =>
                {
                    if (arg.selectedIndex != -1)
                    {
                        base.SelectedIndex = arg.selectedIndex;
                        ImGui.OpenPopup("WarpPopupMenu");
                    }
                });
        }

        // ================================================================================
        // Variables
        // ================================================================================

        EventWrapper<WarpGroup> warpGroupEventWrapper;

        // ================================================================================
        // Properties
        // ================================================================================

        public WarpEditor Parent { get; private set; }
        public WarpGroup WarpGroup { get; private set; }
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

        // ================================================================================
        // Public methods
        // ================================================================================

        public override void Render()
        {
            base.Render();

            // Popup menu (right clicked on an object)
            if (SelectedWarp != null)
            {
                if (ImGui.BeginPopup("WarpPopupMenu"))
                {
                    WarpEditor.WarpPopupMenu(SelectedWarp, () => Parent.FollowEvent?.Invoke(null, SelectedWarp));
                    ImGui.EndPopup();
                }
            }
        }

        public void SetWarpGroup(WarpGroup group)
        {
            if (group != WarpGroup)
            {
                this.WarpGroup = group;
                SelectedIndex = -1;
                warpGroupEventWrapper.ReplaceEventSource(group);
                OnWarpGroupModified();
            }
        }

        // ================================================================================
        // SelectionBox overrides
        // ================================================================================

        protected override void OnMoveSelection(int oldIndex, int newIndex)
        {
            // Warp order can't be rearranged in the general case; so do nothing.
        }

        protected override void RenderPopupMenu()
        {
            // List of warp types that can be added
            (WarpSourceType, string)[] warpTypes = {
                (WarpSourceType.Standard, "Standard warp"),
                (WarpSourceType.Position, "Specific-position warp"),
            };

            ImGui.Text("Add Warp...");
            ImGui.Separator();

            foreach (var (warpType, name) in warpTypes)
            {
                if (ImGui.Selectable(name))
                {
                    SelectedIndex = WarpGroup.AddWarp(warpType);
                }
            }

            ImGui.EndPopup();
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
                base.Selectable = false;
            }
            else
            {
                base.MaxIndex = WarpGroup.Count;
                base.Selectable = true;
            }
        }
    }
}
