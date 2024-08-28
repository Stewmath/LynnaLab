using System;
using LynnaLib;

using Point = Cairo.Point;

namespace LynnaLab
{
    /// <summary>
    /// Caches images for maps. Key is a tuple: (Map, int) where the int is the floor index.
    ///
    /// The code here is quite simple compared to RoomImageCacher because the RoomImageCacher
    /// accounts for almost anything that would affect a minimap's image. So this class only needs
    /// to watch for changes to the room images.
    /// </summary>
    public class MapImageCacher : ImageCacher<(Map map, int floor)>
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public MapImageCacher(ProjectWorkspace workspace)
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

        protected override Image GenerateImage((Map map, int floor) key)
        {
            Image image = TopLevel.Backend.CreateImage(
                key.map.MapWidth * key.map.RoomWidth * 16,
                key.map.MapHeight * key.map.RoomHeight * 16);

            for (int x = 0; x < key.map.MapWidth; x++)
            {
                for (int y = 0; y < key.map.MapHeight; y++)
                {
                    DrawTile(image, key, x, y);

                    // Watch for changes to the room image.
                    // TODO: Must account for dungeon room assignments changing, this always looks
                    // at the initially loaded room at that position

                    int tileX = x, tileY = y; // New variables for closure
                    EventHandler<ImageModifiedEventArgs> modifiedEventHandler = (_, args) =>
                    {
                        DrawTile(image, key, tileX, tileY);
                    };

                    var layout = key.map.GetRoomLayout(x, y, key.floor);
                    Image roomImage = Workspace.GetCachedRoomImage(layout);
                    roomImage.AddModifiedEventHandler(modifiedEventHandler);
                }
            }

            return image;
        }


        // ================================================================================
        // Private methods
        // ================================================================================

        void DrawTile(Image image, (Map map, int floor) key, int x, int y)
        {
            RoomLayout layout = key.map.GetRoomLayout(x, y, key.floor);
            Image roomImage = Workspace.GetCachedRoomImage(layout);

            roomImage.DrawOn(image,
                             new Point(0, 0),
                             new Point(x * key.map.RoomWidth * 16, y * key.map.RoomHeight * 16),
                             new Point(layout.Width * 16, layout.Height * 16));
        }
    }
}
