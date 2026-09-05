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
    public RegionEditor(ProjectWorkspace workspace) : base("Region Editor")
    {
        this.Workspace = workspace;
        this.minimap = new(workspace, doPostRendering: false);

        minimap.Unselectable = true;
        minimap.SelectedIndex = -1;

        // Color the minimap based on the grid regions
        minimap.AfterRenderTileGrid += (_, _) =>
        {
            if (gridRegions == null)
                return;
            foreach (var r in gridRegions)
            {
                Color fillColor = r.Color.WithAlpha(0x80);
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
                        minimap.AddLine(topLeft + yoffset, topRight + yoffset, r.Color, thickness);
                    if (p.RightEdge)
                        minimap.AddLine(topRight - xoffset, bottomRight - xoffset, r.Color, thickness);
                    if (p.BottomEdge)
                        minimap.AddLine(bottomLeft - yoffset, bottomRight - yoffset, r.Color, thickness);
                    if (p.LeftEdge)
                        minimap.AddLine(topLeft + xoffset, bottomLeft + xoffset, r.Color, thickness);
                }
            }
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================

    static readonly string[] modeOptionStrings = {
        "Tileset",
        "Music",
        "Room Pack",
    };

    enum ModeOption
    {
        Tileset,
        Music,
        RoomPack,
        COUNT
    }

    ModeOption selectedMode;

    Minimap minimap;

    IReadOnlyList<GridRegion>? gridRegions;

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

        ImGui.Text("Operation Mode:");
        bool changedMode = ImGuiX.Combo(
            "##ModeCombo",
            new(() => (int)selectedMode, (s) => selectedMode = (ModeOption)s),
            modeOptionStrings);

        if (changedMode)
            UpdateGridRegions();

        ImGui.Separator();
        ImGui.Separator();

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

    public void SetFloorPlan(FloorPlan plan)
    {
        minimap.SetFloorPlan(plan);

        // TODO: Update whenever underlying data is changed
        UpdateGridRegions();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void UpdateGridRegions()
    {
        var plan = minimap.FloorPlan;
        Func<int, int, int> regionGetter;

        switch (selectedMode)
        {
            case ModeOption.Tileset:
                regionGetter = (x, y) => plan.GetRoom(x, y).TilesetIndex;
                break;
            case ModeOption.Music:
                regionGetter = (x, y) => plan.GetRoom(x, y).GetMusicID();
                break;
            case ModeOption.RoomPack:
                regionGetter = (x, y) => plan.GetRoom(x, y).RoomPack;
                break;
            default:
                throw new Exception("Internal error");
        }

        gridRegions = new GridRegionGenerator(plan.MapWidth, plan.MapHeight, regionGetter).Generate();
    }
}
