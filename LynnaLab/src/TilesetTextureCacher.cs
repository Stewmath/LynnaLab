namespace LynnaLab;

/// <summary>
/// Caches textures for tilesets arranged in a 16x16 configuration.
/// </summary>
public class TilesetTextureCacher : TextureCacher<Tileset>
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TilesetTextureCacher(ProjectWorkspace workspace)
        : base(workspace)
    {

    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================

    // ================================================================================
    // Public methods
    // ================================================================================

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected override Texture GenerateTexture(Tileset tileset)
    {
        Texture texture = TopLevel.Backend.CreateTexture(16 * 16, 16 * 16);

        RedrawAll(texture, tileset);

        tileset.TileModifiedEvent += (sender, tile) =>
        {
            Texture texture = base.CacheLookup(sender as Tileset);
            if (tile == -1)
                RedrawAll(texture, sender as Tileset);
            else
                DrawTile(texture, sender as Tileset, tile % 16, tile / 16);
        };

        tileset.DisposedEvent += (sender) =>
        {
            base.DisposeTexture(sender as Tileset);
        };

        return texture;
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    void RedrawAll(Texture texture, Tileset tileset)
    {
        texture.BeginAtomicOperation();
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                DrawTile(texture, tileset, x, y);
            }
        }
        texture.EndAtomicOperation();
    }

    void DrawTile(Texture texture, Tileset tileset, int x, int y)
    {
        int index = x + y * 16;
        var bitmap = tileset.GetTileBitmap(index);
        var bitmapTexture = TopLevel.TextureFromBitmapTracked(bitmap);

        bitmapTexture.DrawOn(texture,
                           new Point(0, 0),
                           new Point(x * 16, y * 16),
                           new Point(16, 16));
    }
}
