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

    RegionOption selectedRegionType;
    ModeOption selectedMode;

    Minimap minimap;

    GridRegionGenerator gridRegions;

    Accessor<Point, int> regionAccessor;

    // Tile Edit Mode
    int regionValueToSet = 0;

    // Bulk Edit Mode
    GridRegion? selectedRegion = null; // null iff selectedMode == TileEdit

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.BeginChild("Checkboxes", ImGuiX.Unit(200.0f, 0.0f));

        ImGui.Text("Region To Edit:");
        bool changedRegion = ImGuiX.Combo(
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

        if (changedRegion)
            UpdateGridRegions();

        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));

        if (selectedMode == ModeOption.TileEdit)
        {
            ImGuiX.InputHex("Value", ref regionValueToSet);
        }
        else
        {
            Accessor<int> accessor = new(
                () => regionAccessor.Get(selectedRegion!.Points.First().Point),
                (v) =>
                {
                    foreach (var p in selectedRegion!.Points)
                    {
                        regionAccessor.Set(p.Point, v);
                    }
                }
            );
            ImGuiX.InputHex("Value", accessor);
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

        // TODO: Update whenever underlying data is changed
        UpdateGridRegions();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    [MemberNotNull(nameof(regionAccessor))]
    [MemberNotNull(nameof(gridRegions))]
    void UpdateGridRegions()
    {
        var plan = minimap.FloorPlan;

        switch (selectedRegionType)
        {
            case RegionOption.Tileset:
                regionAccessor = new(
                    (p) => plan.GetRoom(p.X, p.Y).TilesetIndex,
                    (p, v) => plan.GetRoom(p.X, p.Y).TilesetIndex = v);
                break;
            case RegionOption.Music:
                regionAccessor = new(
                    (p) => plan.GetRoom(p.X, p.Y).GetMusicID(),
                    (p, v) => plan.GetRoom(p.X, p.Y).SetMusicID(v));
                break;
            case RegionOption.RoomPack:
                regionAccessor = new(
                    (p) => plan.GetRoom(p.X, p.Y).RoomPack,
                    (p, v) => plan.GetRoom(p.X, p.Y).RoomPack = (byte)v);
                break;
            default:
                throw new Exception("Internal error");
        }

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
                int x = args.selectedIndex % minimap.Width;
                int y = args.selectedIndex / minimap.Width;
                regionAccessor.Set(new Point(x, y), regionValueToSet);

                // TODO: Update whenever underlying data is changed
                UpdateGridRegions();
            });

        // Ctrl+Left click to set range of tiles
        minimap.AddMouseAction(
            MouseButton.LeftClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
            (sender, args) =>
            {
                args.Foreach((i) =>
                {
                    int x = i % minimap.Width;
                    int y = i / minimap.Width;
                    regionAccessor.Set(new Point(x, y), regionValueToSet);

                    // TODO: Update whenever underlying data is changed
                    UpdateGridRegions();
                });
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
            setSelectedRegion(gridRegions.GetRegionForPoint(minimap.PointFromIndex(args.selectedIndex)));
        };

        // Click to select
        minimap.AddMouseAction(
            MouseButton.Any, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
            selectAction);
    }

    void setSelectedRegion(GridRegion region)
    {
        Debug.Assert(selectedMode == ModeOption.BulkEdit);
        Debug.Assert(gridRegions.Regions.Contains(region));

        selectedRegion = region;
    }
}
