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
    public RegionEditor(ProjectWorkspace workspace) : base("Region Editor")
    {
        base.DefaultSize = new Vector2(900.0f, 550.0f);

        this.Workspace = workspace;
        this.minimap = new(workspace, doPostRendering: false);

        minimap.Selectable = false;

        mapChooser = new(Project, minimap);

        minimap.ChangedFloorPlanEvent += (_, _) => OnFloorPlanChanged(minimap.FloorPlan);

        // Watch for dungeon rooms being rearranged, must recalculate the regions when that happens.
        dungeonEW.Bind<DungeonChangedEventArgs>("DungeonChangedEvent", (_, args) =>
        {
            UpdateGridRegions();
        });


        // --- Register the list of editable regions ---

        Func<int, Color> mapleColorMapper = (x) => x == 0 ? Color.Red : Color.LightBlue;

        // Helper to build a DescriptorRegionAccessor for a given region name.
        RegionOption DescriptorOption(string name, Func<Project, FloorPlan, bool> showIn)
        {
            return new() {
                name = name,
                showIn = showIn,
                colorMapper = null,
                constructAccessor = (p, plan) => new DescriptorRegionAccessor(Workspace,
                    (Point pt) => plan.GetRoom(pt.X, pt.Y).ValueReferenceGroup[name]),
            };
        }

        this.regionOptions = [
            DescriptorOption("Tileset", (_, _) => true),
            DescriptorOption("Music", (_, _) => true),
            DescriptorOption("Room Pack", (_, plan) => plan.HasRoomPack),
            new() {
                name = "Companion Callable",
                showIn = (_, plan) => plan.MainGroup == 0,
                colorMapper = mapleColorMapper,
                constructAccessor = (p, _) =>
                    new MapleRegionAccessor(p.GetMapleLocationTable("companionCallableRooms")),
            },
            new() {
                name = "Map Text",
                showIn = (_, plan) => plan.MainGroup == 0
                                      || (plan.Project.Game == Game.Ages && plan.MainGroup == 2),
                colorMapper = null,
                constructAccessor = (p, _) => new ByteTableAccessor(p,
                    "presentMapTextIndices",
                    p.Game == Game.Ages ? 14 : 16,
                    p.Game == Game.Ages ? 14 : 16),
            },
            new() {
                name = "Map Text",
                showIn = (_, plan) => (plan.Project.Game == Game.Seasons &&
                                       (plan.MainGroup >= 1 && plan.MainGroup < 4))
                                      || (plan.Project.Game == Game.Ages &&
                                          (plan.MainGroup == 1 || plan.MainGroup == 3)),
                colorMapper = null,
                constructAccessor = (p, _) => new ByteTableAccessor(p,
                    "pastMapTextIndices",
                    p.Game == Game.Ages ? 14 : 11,
                    p.Game == Game.Ages ? 14 : 8),
            },
            new() {
                name = "Maple (Ricky)",
                showIn = (_, plan) => plan.MainGroup == 0,
                colorMapper = mapleColorMapper,
                constructAccessor = (p, _) =>
                    new MapleRegionAccessor(p.GetMapleLocationTable(Companion.Ricky, false)),
            },
            new() {
                name = "Maple (Dimitri)",
                showIn = (_, plan) => plan.MainGroup == 0,
                colorMapper = mapleColorMapper,
                constructAccessor = (p, _) =>
                    new MapleRegionAccessor(p.GetMapleLocationTable(Companion.Dimitri, false)),
            },
            new() {
                name = "Maple (Moosh)",
                showIn = (_, plan) => plan.MainGroup == 0,
                colorMapper = mapleColorMapper,
                constructAccessor = (p, _) =>
                    new MapleRegionAccessor(p.GetMapleLocationTable(Companion.Moosh, false)),
            },
            new() {
                name = "Maple (Past)",
                showIn = (proj, plan) => proj.Game == Game.Ages && plan.MainGroup == 1,
                colorMapper = mapleColorMapper,
                constructAccessor = (p, _) =>
                    new MapleRegionAccessor(p.GetMapleLocationTable(Companion.None, true)),
            },
        ];

        // Call this after regionOptions are initialized
        OnFloorPlanChanged(minimap.FloorPlan);


        // --- Color the minimap based on the grid regions ---

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

                float thickness = ImGuiX.Unit(3.0f);

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

    // NOTE: The name field is used for display, but for DescriptorRegionAccessor-based options it
    // must also match the names used by the Room class's ValueReferenceGroup.
    class RegionOption
    {
        public required string name;
        public required Func<Project, FloorPlan, bool> showIn;
        public required Func<int, Color>? colorMapper;
        public required Func<Project, FloorPlan, RegionAccessor> constructAccessor;
    }

    List<RegionOption> regionOptions;

    static readonly string[] modeOptionStrings = {
        "Bulk Edit",
        "Tile Edit",
    };

    enum ModeOption
    {
        BulkEdit,
        TileEdit,
    }

    Minimap minimap;
    MapChooser mapChooser;
    EventWrapper<Dungeon> dungeonEW = new();
    FloorPlan? lastFloorPlan = null;

    RegionOption selectedRegionOption;
    ModeOption selectedMode;
    GridRegionGenerator gridRegions;

    // These change based on the RegionOption
    RegionAccessor regionAccessor;
    Action? unregisterEventHandlers = null;

    // Tile Edit Mode
    int regionValueToSet = 0;

    // Bulk Edit Mode
    GridRegion? selectedRegion = null;
    bool suppressChangeEvent = false;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; }
    public Project Project { get { return Workspace.Project; } }

    string RegionTypeString { get { return selectedRegionOption.name; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.BeginChild("Left pane", ImGuiX.Unit(250.0f, 0.0f));

        ImGuiLL.HelpButton("RegionEditor.md", Project);

        ImGui.Text("Data To Edit:");
        bool changedRegionType = ImGuiX.Combo(
            "##RegionCombo",
            new(() => selectedRegionOption.name,
                (s) => selectedRegionOption = GetRegionTypeOptions().First((o) => o.name == s)),
            GetRegionTypeOptions().Select((o) => o.name));

        ImGui.Text("Edit Mode:");
        bool changedMode = ImGuiX.Combo(
            "##ModeCombo",
            new(() => (int)selectedMode, (s) => selectedMode = (ModeOption)s),
            modeOptionStrings);

        if (changedMode)
        {
            selectedRegion = null;
            RegisterMouseActions();
        }

        if (changedRegionType)
        {
            selectedRegion = null;
            UpdateRegionAccessor();
            UpdateGridRegions();
        }

        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));

        if (selectedMode == ModeOption.TileEdit)
        {
            regionAccessor.RenderSelector(ref regionValueToSet);
        }
        else if (selectedRegion == null)
        {
            ImGui.Text("Select a region.");
        }
        else
        {
            Accessor<int> accessor = new(
                () => regionAccessor.Get(selectedRegion.Points.First().Point),
                (v) =>
                {
                    // Don't update the region grid while doing bulk edits, because we don't have a
                    // good way to keep the same region as before selected after it updates! This
                    // just means that some adjacent regions may have the same value, which normally
                    // isn't possible, but should make the editing experience better.
                    suppressChangeEvent = true;

                    Project.BeginTransaction($"Bulk region {RegionTypeString} edit", merge: true);
                    foreach (var p in selectedRegion.Points)
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
            mapChooser.Render();
        }
        ImGui.EndChild();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    [MemberNotNull(nameof(regionAccessor))]
    [MemberNotNull(nameof(gridRegions))]
    [MemberNotNull(nameof(selectedRegionOption))]
    void OnFloorPlanChanged(FloorPlan plan)
    {
        dungeonEW.ReplaceEventSource((plan as Dungeon.Floor)?.Dungeon);

        var options = GetRegionTypeOptions().ToList();
        if (selectedRegionOption == null || !options.Contains(selectedRegionOption))
        {
            selectedRegionOption = options.First();
        }

        // It's safe to not recalculate all of the fields if we're only changing the season.
        if (regionAccessor != null
            && gridRegions != null
            && plan.Season != Season.None
            && plan.MainGroup == lastFloorPlan?.MainGroup)
        {
            return;
        }

        selectedRegion = null;
        UpdateRegionAccessor();
        UpdateGridRegions();

        lastFloorPlan = plan;
    }

    [MemberNotNull(nameof(gridRegions))]
    void UpdateGridRegions()
    {
        var plan = minimap.FloorPlan;

        Func<int, Color>? colorMapper = selectedRegionOption.colorMapper;

        gridRegions = new GridRegionGenerator(
            plan.MapWidth, plan.MapHeight, regionAccessor.Get, true, colorMapper);

        if (selectedMode == ModeOption.BulkEdit)
        {
            // Best effort: Try to keep the selection the same or similar to before the grid regions
            // were regenerated.
            selectedRegion = gridRegions.Regions.FirstOrDefault(
                (r) => r.RegionID == selectedRegion?.RegionID)
                             ?? null;
        }
    }

    [MemberNotNull(nameof(regionAccessor))]
    void UpdateRegionAccessor()
    {
        regionAccessor = selectedRegionOption.constructAccessor(Project, minimap.FloorPlan);
        RegisterEventHandlers();
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

        unregisterEventHandlers = regionAccessor.RegisterEvents(OnRoomValueChanged);
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
    /// Get the list of region types applicable to the current FloorPlan.
    /// </summary>
    IEnumerable<RegionOption> GetRegionTypeOptions()
    {
        return regionOptions.Where((o) => o.showIn(Project, minimap.FloorPlan));
    }


    /// <summary>
    /// A class which is responsible for getting & setting some per-room value, and rendering a
    /// selector widget.
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
        public abstract Action RegisterEvents(Action action);

        public void RenderSelector(ref int value)
        {
            int v = value;
            RenderSelector(new(() => v));
            value = v;
        }
    }

    /// <summary>
    /// A RegionAccessor based on ValueReferences.
    /// </summary>
    class DescriptorRegionAccessor : RegionAccessor
    {
        public DescriptorRegionAccessor(ProjectWorkspace workspace, Func<Point, ValueReferenceDescriptor> getDescriptor)
        {
            this.Workspace = workspace;
            this.getDescriptor = getDescriptor;
        }

        // ================================================================================
        // Variables
        // ================================================================================

        Func<Point, ValueReferenceDescriptor> getDescriptor;

        // ================================================================================
        // Properties
        // ================================================================================
        ProjectWorkspace Workspace { get; }

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
            var descriptor = GetDefaultDescriptor();
            ConstantsMapping? mapping = descriptor.ConstantsMapping;
            int value = accessor.Get();
            int initial = value;
            if (mapping == null)
                ImGuiX.InputHex(descriptor.Name, ref value);
            else
            {
                if (ImGuiLL.ComboBoxFromConstants(descriptor.Name, mapping, ref value, true, false))
                    Workspace.ShowDocumentation(descriptor.Documentation);
            }
            if (initial != value)
                accessor.Set(value);
        }

        public override Action RegisterEvents(Action action)
        {
            var descriptor = GetDefaultDescriptor();
            return descriptor.Project.RegisterEventOnAllRooms(descriptor.Name, action);
        }

        ValueReferenceDescriptor GetDefaultDescriptor()
        {
            return getDescriptor(new(0, 0));
        }
    }

    /// <summary>
    /// A RegionAccessor specifically for handling maple location data.
    /// </summary>
    class MapleRegionAccessor : RegionAccessor
    {
        public MapleRegionAccessor(MapleLocationTable maple)
        {
            this.maple = maple;
        }

        // ================================================================================
        // Variables
        // ================================================================================

        MapleLocationTable maple;

        // ================================================================================
        // Public methods
        // ================================================================================

        public override int Get(Point point)
        {
            return maple.IsPresent(point) ? 1 : 0;
        }

        public override void Set(Point point, int value)
        {
            maple.SetPresent(point, value != 0);
        }

        public override void RenderSelector(Accessor<int> accessor)
        {
            ImGuiX.Checkbox("Spawns", new Accessor<bool>(
                () => accessor.Get() != 0,
                (v) => accessor.Set(v ? 1 : 0)));
        }

        public override Action RegisterEvents(Action action)
        {
            return maple.RegisterModifiedEvent(action);
        }
    }

    /// <summary>
    /// A RegionAccessor for byte tables of room text indices.
    /// </summary>
    class ByteTableAccessor : RegionAccessor
    {
        public ByteTableAccessor(Project project, string label, int width, int height)
        {
            this.width = width;
            this.height = height;

            // TODO: This is parsing-level stuff, should be moved to LynnaLib
            data = new Data[width * height];
            Data current = project.GetData(label);

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = current;

                current = current.NextData
                          ?? throw new AssemblyErrorException(
                              $"Unexpected end of byte table: {label}");
            }
        }

        // ================================================================================
        // Variables
        // ================================================================================

        int width;
        int height;
        Data[] data;

        // ================================================================================
        // Properties
        // ================================================================================

        Project Project { get { return data[0].Project; } }

        // ================================================================================
        // Public methods
        // ================================================================================

        public override int Get(Point point)
        {
            int index = GetIndex(point);
            return index < 0 ? 0 : data[index].GetIntValue(0);
        }

        public override void Set(Point point, int value)
        {
            int index = GetIndex(point);
            if (index >= 0)
                data[index].SetByteValue(0, (byte)value);
        }

        public override void RenderSelector(Accessor<int> accessor)
        {
            int value = accessor.Get();
            int initial = value;
            ImGuiX.InputHex("Map text index", ref value);
            if (initial != value)
                accessor.Set(value);
        }

        public override Action RegisterEvents(Action action)
        {
            return Project.RegisterEventOnAll(data.Select((d) => new EventSource<DataModifiedEventArgs>(
                (ev) => d.AddModifiedEventHandler(ev),
                (ev) => d.RemoveModifiedEventHandler(ev))),
                action);
        }

        // The source tables omit unused map columns. The dimensions supplied by the option
        // therefore describe the actual populated rectangle, rather than the 16x16 FloorPlan.
        int GetIndex(Point point)
        {
            if (!point.InRange(0, 0, width, height))
                return -1;
            return point.X + point.Y * width;
        }
    }
}
