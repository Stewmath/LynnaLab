namespace LynnaLab;

/// <summary>
/// Viewing a tileset & selecting tiles from it
/// </summary>
public class TilesetViewer : TileGridViewer
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
        base.HoverImagePreview = true;

        base.OnHover = (tile) =>
        {
            ImGui.BeginTooltip();
            ImGui.Text($"Tile {tile:X2}");
            ImGui.EndTooltip();
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Tileset tileset;
    Image image;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public TopLevel TopLevel { get { return Workspace.TopLevel; } }

    public Tileset Tileset { get { return tileset; } }

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
