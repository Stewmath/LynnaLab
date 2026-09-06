#nullable enable
namespace LynnaLab;

/// <summary>
/// RegionEditor: Editing per-room values (music, tileset, etc) on a map coloured such that squares
/// with the same values have the same colour.
/// </summary>
public class RegionEditor : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public RegionEditor(ProjectWorkspace workspace, FloorPlan plan) : base("Region Editor")
    {
        this.Workspace = workspace;
        this.minimap = new(workspace, doPostRendering: false);

        minimap.Selectable = false;

        SetFloorPlan(plan);

        // Color the minimap based on the grid regions
        minimap.AfterRenderTileGrid += (_, _) =>
        {
            GridRegion? hoveredRegion = null;
            if (minimap.HoveredTile != -1)
                hoveredRegion = gridRegions.GetRegionForPoint(minimap.PointFromIndex(minimap.HoveredTile));

            foreach (var r in gridRegions.Regions)
            {
                Color color = r.Color;
                Color fillColor;

                if (r == selectedRegion)
                    color = Color.White;

                if (selectedMode == ModeOption.BulkEdit && r == hoveredRegion)
                    fillColor = color.WithAlpha(0xc0);
                else
                    fillColor = color.WithAlpha(0x80);

                float thickness = 4.0f; // TODO: Account for DPI?

                foreach (GridPoint p in r.Points)
                {
                    var rect = minimap.TileRect(p.X, p.Y);

                    Vector2 xoffset = new Vector2(thickness, 0) / 2.0f;
                    Vector2 yoffset = new Vector2(0, thickness) / 2.0f;

                    // Must do this to align to pixels (and thickness must be a multiple of 2?)
                    Vector2 off1 = new Vector2(-0.5f, -0.5f);

                    Vector2 topLeft = rect.TopLeft + off1;
                    Vector2 topRight = rect.TopRight + off1;
                    Vector2 bottomLeft = rect.BottomLeft + off1;
                    Vector2 bottomRight = rect.BottomRight + off1;

                    minimap.AddRectFilled(rect, fillColor);

                    if (p.TopEdge)
                        minimap.AddLine(topLeft + yoffset, topRight + yoffset, color, thickness);
                    if (p.RightEdge)
                        minimap.AddLine(topRight - xoffset, bottomRight - xoffset, color, thickness);
                    if (p.BottomEdge)
                        minimap.AddLine(bottomLeft - yoffset, bottomRight - yoffset, color, thickness);
                    if (p.LeftEdge)
                        minimap.AddLine(topLeft + xoffset, bottomLeft + xoffset, color, thickness);
                }
            }
        };

        RegisterMouseActions();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // These strings are used for display, but they also must match with the names used by the Room
    // class's ValueReferenceGroup in order to register event handlers based on the corresponding
    // ValueReferences.
    static readonly string[] regionOptionStrings = {
        "Tileset",
        "Music",
        "Room Pack",
    };

    static readonly string[] modeOptionStrings = {
        "Tile Edit",
        "Bulk Edit",
    };

    enum RegionOption
    {
        Tileset,
        Music,
        RoomPack,
        COUNT
    }

    enum ModeOption
    {
        TileEdit,
        BulkEdit,
    }

    Minimap minimap;

    RegionOption selectedRegionType;
    ModeOption selectedMode;

    GridRegionGenerator gridRegions;

    // These change based on the RegionOption
    RegionAccessor regionAccessor;
    Action? unregisterEventHandlers = null;

    // Tile Edit Mode
    int regionValueToSet = 0;

    // Bulk Edit Mode
    GridRegion? selectedRegion = null; // null iff selectedMode == TileEdit
    bool suppressChangeEvent = false;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; }
    public Project Project { get { return Workspace.Project; } }

    string RegionTypeString { get { return regionOptionStrings[(int)selectedRegionType]; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.BeginChild("Checkboxes", ImGuiX.Unit(200.0f, 0.0f));

        ImGui.Text("Region To Edit:");
        bool changedRegionType = ImGuiX.Combo(
            "##RegionCombo",
            new(() => (int)selectedRegionType, (s) => selectedRegionType = (RegionOption)s),
            regionOptionStrings);

        ImGui.Text("Edit Mode:");
        bool changedMode = ImGuiX.Combo(
            "##ModeCombo",
            new(() => (int)selectedMode, (s) => selectedMode = (ModeOption)s),
            modeOptionStrings);

        if (changedMode)
        {
            if (selectedMode == ModeOption.TileEdit)
            {
                selectedRegion = null;
            }
            else
            {
                selectedRegion = gridRegions.Regions[0];
            }
            RegisterMouseActions();
        }

        if (changedRegionType)
        {
            UpdateGridRegions();
            RegisterEventHandlers();
        }

        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));

        if (selectedMode == ModeOption.TileEdit)
        {
            regionAccessor.RenderSelector(ref regionValueToSet);
        }
        else
        {
            Accessor<int> accessor = new(
                () => regionAccessor.Get(selectedRegion!.Points.First().Point),
                (v) =>
                {
                    // Don't update the region grid while doing bulk edits, because we don't have a
                    // good way to keep the same region as before selected after it updates! This
                    // just means that some adjacent regions may have the same value, which normally
                    // isn't possible, but should make the editing experience better.
                    suppressChangeEvent = true;

                    Project.BeginTransaction($"Bulk region {RegionTypeString} edit", merge: true);
                    foreach (var p in selectedRegion!.Points)
                    {
                        regionAccessor.Set(p.Point, v);
                    }
                    Project.EndTransaction();
                    suppressChangeEvent = false;
                }
            );
            regionAccessor.RenderSelector(accessor);
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("Minimap");
        if (minimap.TileWidth == 0)
            ImGui.Text("No map selected?");
        else
        {
            minimap.Render();
        }
        ImGui.EndChild();
    }

    [MemberNotNull(nameof(regionAccessor))]
    [MemberNotNull(nameof(gridRegions))]
    public void SetFloorPlan(FloorPlan plan)
    {
        minimap.SetFloorPlan(plan);

        // TODO: Update whenever underlying data is changed (dungeon floors rearranged)
        UpdateGridRegions();
        RegisterEventHandlers();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    [MemberNotNull(nameof(regionAccessor))]
    [MemberNotNull(nameof(gridRegions))]
    void UpdateGridRegions()
    {
        var plan = minimap.FloorPlan;

        regionAccessor = new DescriptorRegionAccessor((Point p) => plan.GetRoom(p.X, p.Y).ValueReferenceGroup[RegionTypeString]);

        gridRegions = new GridRegionGenerator(plan.MapWidth, plan.MapHeight, regionAccessor.Get);

        if (selectedMode == ModeOption.BulkEdit)
        {
            selectedRegion = gridRegions.Regions.FirstOrDefault(
                (r) => r.RegionID == selectedRegion!.RegionID)
                             ?? gridRegions.Regions[0];
        }
    }

    void RegisterMouseActions()
    {
        minimap.ResetMouseActions();

        if (selectedMode == ModeOption.TileEdit)
            RegisterTileEditMouseActions();
        else
            RegisterBulkEditMouseActions();
    }

    void RegisterTileEditMouseActions()
    {
        // Unhide hover rectangle
        minimap.HoverColor = Color.Red;
        minimap.ShowCoordinateTooltip = true;

        // Left click to set tile
        minimap.AddMouseAction(
            MouseButton.LeftClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
            (sender, args) =>
            {
                Project.BeginTransaction($"Edit region {RegionTypeString}#{minimap.FloorPlan.Identifier}", merge: true);
                int x = args.selectedIndex % minimap.Width;
                int y = args.selectedIndex / minimap.Width;
                regionAccessor.Set(new Point(x, y), regionValueToSet);
                Project.EndTransaction();
            },
            onRelease: () => Project.TransactionManager.InsertBarrier()
        );

        // Ctrl+Left click to set range of tiles
        minimap.AddMouseAction(
            MouseButton.LeftClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
            (sender, args) =>
            {
                Project.BeginTransaction($"Edit region {RegionTypeString}");
                args.Foreach((i) =>
                {
                    int x = i % minimap.Width;
                    int y = i / minimap.Width;
                    regionAccessor.Set(new Point(x, y), regionValueToSet);
                });
                Project.EndTransaction();
            },
            brushPreview: true);

        // Right-click to select a tile
        minimap.AddMouseAction(
            MouseButton.RightClick, MouseModifier.None, MouseAction.Click, GridAction.Callback,
            (_, args) =>
            {
                int x = args.selectedIndex % minimap.Width;
                int y = args.selectedIndex / minimap.Width;
                regionValueToSet = regionAccessor.Get(new Point(x, y));
            });
    }

    void RegisterBulkEditMouseActions()
    {
        // Hide hover rectangle
        minimap.HoverColor = Color.Transparent;
        minimap.ShowCoordinateTooltip = false;

        TileGrid.TileGridEventHandler selectAction = (object sender, TileGridEventArgs args) =>
        {
            SetSelectedRegion(gridRegions.GetRegionForPoint(minimap.PointFromIndex(args.selectedIndex)));
        };

        // Click to select
        minimap.AddMouseAction(
            MouseButton.Any, MouseModifier.None, MouseAction.Click, GridAction.Callback,
            selectAction);
    }

    void SetSelectedRegion(GridRegion region)
    {
        Debug.Assert(selectedMode == ModeOption.BulkEdit);
        Debug.Assert(gridRegions.Regions.Contains(region));

        selectedRegion = region;
    }

    /// <summary>
    /// Events are tied to the FloorPlan, so this only needs to be done once per FloorPlan change,
    /// or when selectedRegionType changes.
    /// </summary>
    [MemberNotNull(nameof(unregisterEventHandlers))]
    void RegisterEventHandlers()
    {
        if (unregisterEventHandlers != null)
        {
            unregisterEventHandlers();
            unregisterEventHandlers = null;
        }

        string name = regionOptionStrings[(int)selectedRegionType];
        unregisterEventHandlers = Project.RegisterEventOnAllRooms(name, OnRoomValueChanged);
    }

    /// <summary>
    /// Called whenever any room has its watch value changed (music, tileset, etc).
    ///
    /// Yes, ANY room, not just the ones displayed. I don't feel like handling dungeons where the
    /// room indexes can change arbitrarily.
    /// </summary>
    void OnRoomValueChanged()
    {
        if (suppressChangeEvent)
            return;
        UpdateGridRegions();
    }


    /// <summary>
    /// A class which is responsible for getting & setting some per-room value, and rendering a
    /// selector widget.
    ///
    /// Does this really need to have an abstract base class? Probably not...
    /// </summary>
    abstract class RegionAccessor
    {
        public RegionAccessor()
        {
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public abstract int Get(Point point);
        public abstract void Set(Point point, int value);

        public abstract void RenderSelector(Accessor<int> accessor);

        public void RenderSelector(ref int value)
        {
            int v = value;
            RenderSelector(new(() => v));
            value = v;
        }
    }

    class DescriptorRegionAccessor : RegionAccessor
    {
        public DescriptorRegionAccessor(Func<Point, ValueReferenceDescriptor> getDescriptor)
        {
            this.getDescriptor = getDescriptor;
        }

        // ================================================================================
        // Variables
        // ================================================================================

        Func<Point, ValueReferenceDescriptor> getDescriptor;

        // ================================================================================
        // Public methods
        // ================================================================================

        public override int Get(Point point)
        {
            return getDescriptor(point).GetIntValue();
        }

        public override void Set(Point point, int value)
        {
            getDescriptor(point).SetValue(value);
        }

        public override void RenderSelector(Accessor<int> accessor)
        {
            var descriptor = getDescriptor(new(0, 0));
            ConstantsMapping? mapping = descriptor.ConstantsMapping;
            int value = accessor.Get();
            int initial = value;
            if (mapping == null)
                ImGuiX.InputHex(descriptor.Name, ref value);
            else
                ImGuiLL.ComboBoxFromConstants(descriptor.Name, mapping, ref value, true, true);
            if (initial != value)
                accessor.Set(value);
        }
    }
}
