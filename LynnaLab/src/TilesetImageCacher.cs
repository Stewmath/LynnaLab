using LynnaLib;

using Point = Cairo.Point;

namespace LynnaLab
{
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

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    DrawTile(image, tileset, x, y);
                }
            }

            return image;
        }


        // ================================================================================
        // Private methods
        // ================================================================================

        void DrawTile(Image image, Tileset tileset, int x, int y)
        {
            int index = x + y * 16;
            var bitmap = tileset.GetTileImage(index);
            var bitmapImage = TopLevel.ImageFromBitmap(bitmap);

            bitmapImage.DrawOn(image,
                               new Point(0, 0),
                               new Point(x * 16, y * 16),
                               new Point(16, 16));
        }
    }
}
