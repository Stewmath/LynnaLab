using LynnaLib;

using Point = Cairo.Point;

namespace LynnaLab
{
    /// <summary>
    /// Caches images for room layouts.
    /// </summary>
    public class RoomImageCacher : ImageCacher<RoomLayout>
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public RoomImageCacher(ProjectWorkspace workspace)
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

        protected override Image GenerateImage(RoomLayout layout)
        {
            Image image = TopLevel.Backend.CreateImage(layout.Width * 16, layout.Height * 16);

            for (int x = 0; x < layout.Width; x++)
            {
                for (int y = 0; y < layout.Height; y++)
                {
                    DrawTile(image, layout, x, y);
                }
            }

            return image;
        }


        // ================================================================================
        // Private methods
        // ================================================================================

        void DrawTile(Image image, RoomLayout layout, int x, int y)
        {
            int tileIndex = layout.GetTile(x, y);
            var bitmap = layout.Tileset.GetTileImage(tileIndex);
            var tileImage = TopLevel.ImageFromBitmap(bitmap);

            tileImage.DrawOn(image,
                             new Point(0, 0),
                             new Point(x * 16, y * 16),
                             new Point(16, 16));
        }
    }
}
