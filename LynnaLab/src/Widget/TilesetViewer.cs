namespace LynnaLab;

/// <summary>
/// Viewing a tileset & selecting tiles from it
/// </summary>
public class TilesetViewer : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TilesetViewer(ProjectWorkspace workspace)
        : base("Tileset Viewer")
    {
        this.Workspace = workspace;

        base.TileWidth = 16;
        base.TileHeight = 16;
        base.Width = 16;
        base.Height = 16;
        base.Selectable = true;
        base.TooltipImagePreview = true;

        base.OnHover = (tile) =>
        {
            if (!base.TooltipImagePreview)
                return;
            ImGui.BeginTooltip();
            if (subTileMode)
            {
                var (t, x, y) = ToSubTileIndex(tile);
                ImGui.Text($"Subtile {Tileset.GetSubTileIndex(t, x, y):X2}");
                ImGui.Text($"Palette {Tileset.GetSubTilePalette(t, x, y)}");
                ImGuiX.Checkbox("Flip X", (Tileset.GetSubTileFlags(t, x, y) & 0x20) != 0, (_) => {});
                ImGuiX.Checkbox("Flip Y", (Tileset.GetSubTileFlags(t, x, y) & 0x40) != 0, (_) => {});
                ImGuiX.Checkbox("Priority", (Tileset.GetSubTileFlags(t, x, y) & 0x40) != 0, (_) => {});
            }
            else
            {
                ImGui.Text($"Tile {tile:X2}");
            }
            ImGui.EndTooltip();
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Tileset tileset;
    Image image;
    bool subTileMode; // If true, we select subtiles (1/4th of a tile) instead of whole tiles

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    public Tileset Tileset { get { return tileset; } }

    public bool SubTileMode
    {
        get { return subTileMode; }
        set
        {
            if (subTileMode == value)
                return;
            subTileMode = value;
            if (subTileMode)
            {
                base.TileWidth = 8;
                base.TileHeight = 8;
                base.Width = 32;
                base.Height = 32;
            }
            else
            {
                base.TileWidth = 16;
                base.TileHeight = 16;
                base.Width = 16;
                base.Height = 16;
            }
        }
    }

    protected override Image Image
    {
        get
        {
            return image;
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        base.Render();
    }

    public void SetTileset(Tileset t)
    {
        if (tileset != t)
        {
            tileset = t;
            OnTilesetChanged();
        }
    }

    /// <summary>
    /// In subtile mode, takes a TileGrid index and returns:
    /// - t: The tile in the tileset (0-255)
    /// - x: The x offset of the subtile (0-1)
    /// - y: The y offset of the subtile (0-1)
    /// </summary>
    public (int, int, int) ToSubTileIndex(int index)
    {
        int t = ((index % 32) / 2) + ((index / 32) / 2) * 16;
        int x = index % 2;
        int y = (index / 32) % 2;

        return (t, x, y);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Called when the tileset is changed
    /// </summary>
    void OnTilesetChanged()
    {
        image = null;

        if (tileset != null)
        {
            image = Workspace.GetCachedTilesetImage(tileset);
        }
    }
}
