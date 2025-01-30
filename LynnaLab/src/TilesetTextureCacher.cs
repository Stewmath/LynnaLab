namespace LynnaLab;

/// <summary>
/// Caches textures for tilesets arranged in a 16x16 configuration.
/// </summary>
public class TilesetTextureCacher : IDisposeNotifier
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TilesetTextureCacher(ProjectWorkspace workspace, Tileset tileset)
    {
        Workspace = workspace;
        Tileset = tileset;

        GenerateTexture();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    RgbaTexture tilesetTexture;
    bool modified = false;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Tileset Tileset { get; private set; }

    // ================================================================================
    // Events
    // ================================================================================

    public event EventHandler DisposedEvent;

    // ================================================================================
    // Public methods
    // ================================================================================

    public TextureBase GetTexture()
    {
        return tilesetTexture;
    }

    /// <summary>
    /// Called once per frame. Renders anything that was queued up.
    /// </summary>
    public void UpdateFrame()
    {
        if (modified)
            Render();
        modified = false;
    }

    public void Dispose()
    {
        tilesetTexture.Dispose();
        tilesetTexture = null;
        DisposedEvent?.Invoke(this, null);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void GenerateTexture()
    {
        tilesetTexture = TopLevel.Backend.CreateTexture(256, 256, renderTarget: true);
        Render();

        Tileset.TileModifiedEvent += (sender, tile) =>
        {
            modified = true;
        };

        Tileset.DisposedEvent += (sender) =>
        {
            Dispose();
        };
    }


    void Render()
    {
        TopLevel.Backend.RenderTileset(tilesetTexture, Tileset);
    }

    /// <summary>
    /// This redraws a single tile using software rendering.
    ///
    /// Our GPU pipeline is only capable of redrawing the entire tileset at once. Even so, that
    /// works well enough, so this function is unused. (Anyway, there could be annoying cases where
    /// this gets called multiple times for the same tile in a single frame - so it's not ideal
    /// anyway.)
    /// </summary>
    void DrawTile(int x, int y)
    {
        int index = x + y * 16;
        var bitmap = Tileset.GetTileBitmap(index);
        var bitmapTexture = TopLevel.TextureFromBitmapTracked(bitmap);

        bitmapTexture.DrawOn(tilesetTexture,
                           new Point(0, 0),
                           new Point(x * 16, y * 16),
                           new Point(16, 16));
    }
}
