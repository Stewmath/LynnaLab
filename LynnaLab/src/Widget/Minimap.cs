using LynnaLib;

using Point = Cairo.Point;

namespace LynnaLab
{
    public class Minimap : TileGridViewer
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public Minimap(TopLevel topLevel)
        {
            this.topLevel = topLevel;
        }

        // ================================================================================
        // Variables
        // ================================================================================
        TopLevel topLevel;
        Map map;
        Image image;

        // ================================================================================
        // Properties
        // ================================================================================

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

        /// <summary>
        /// Sets the map to display
        /// </summary>
        public void SetMap(Map map)
        {
            if (this.map == map)
                return;

            this.map = map;

            if (image != null)
            {
                image.Dispose();
                image = null;
            }

            if (map == null)
                return;

            base.TileWidth = map.RoomWidth * 16;
            base.TileHeight = map.RoomHeight * 16;
            base.Width = map.MapWidth;
            base.Height = map.MapHeight;

            GenerateImage();
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Generate the full minimap image
        /// </summary>
        void GenerateImage()
        {
            image?.Dispose();

            image = topLevel.Backend.CreateImage(base.CanvasWidth, base.CanvasHeight);

            for (int x=0; x<map.MapWidth; x++)
            {
                for (int y=0; y<map.MapHeight; y++)
                {
                    Room room = map.GetRoom(x, y);
                    int season = map.Season;
                    Image roomImage = topLevel.ImageFromBitmap(room.GetLayout(season).GetImage());

                    roomImage.DrawOn(image,
                                     new Point(0, 0),
                                     new Point(x * TileWidth, y * TileHeight),
                                     new Point(TileWidth, TileHeight));
                }
            }
        }
    }
}
