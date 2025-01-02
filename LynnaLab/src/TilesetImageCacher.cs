namespace LynnaLab;

/// <summary>
/// Caches images for tilesets arranged in a 16x16 configuration.
/// </summary>
public class TilesetImageCacher : ImageCacher<Tileset>
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TilesetImageCacher(ProjectWorkspace workspace)
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

    protected override Image GenerateImage(Tileset tileset)
    {
        Image image = TopLevel.Backend.CreateImage(16 * 16, 16 * 16);

        RedrawAll(image, tileset);

        tileset.TileModifiedEvent += (sender, tile) =>
        {
            Image image = base.CacheLookup(sender as Tileset);
            if (tile == -1)
                RedrawAll(image, sender as Tileset);
            else
                DrawTile(image, sender as Tileset, tile % 16, tile / 16);
        };

        tileset.DisposedEvent += (sender) =>
        {
            base.DisposeImage(sender as Tileset);
        };

        return image;
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    void RedrawAll(Image image, Tileset tileset)
    {
        image.BeginAtomicOperation();
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                DrawTile(image, tileset, x, y);
            }
        }
        image.EndAtomicOperation();
    }

    void DrawTile(Image image, Tileset tileset, int x, int y)
    {
        int index = x + y * 16;
        var bitmap = tileset.GetTileBitmap(index);
        var bitmapImage = TopLevel.ImageFromBitmapTracked(bitmap);

        bitmapImage.DrawOn(image,
                           new Point(0, 0),
                           new Point(x * 16, y * 16),
                           new Point(16, 16));
    }
}
