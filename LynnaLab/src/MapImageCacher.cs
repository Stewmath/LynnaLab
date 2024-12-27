namespace LynnaLab;

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

        // EventWrappers for each position in the map, managing events to invoke when those
        // specific room images have been modified.
        var imageEventWrappers = new Dictionary<(int, int), EventWrapper<Image>>();

        // Set up the event handlers for room image modification. They will be bound to the
        // relevant room later, in the "RegenerateImageWatchers" function.
        for (int x = 0; x < key.map.MapWidth; x++)
        {
            for (int y = 0; y < key.map.MapHeight; y++)
            {
                int tileX = x, tileY = y; // New variables for closure
                EventHandler<ImageModifiedEventArgs> handler = (_, args) =>
                {
                    DrawTile(image, key, tileX, tileY);
                };

                var wrapper = new EventWrapper<Image>();
                wrapper.Bind<ImageModifiedEventArgs>("ModifiedEvent", handler, weak: false);
                imageEventWrappers[(x, y)] = wrapper;
            }
        }

        // Watch for changes to the dungeon layout (for dungeons only)
        if (key.map is Dungeon)
        {
            var dungeon = key.map as Dungeon;

            EventHandler<DungeonRoomChangedEventArgs> roomChangedHandler = (_, args) =>
            {
                if (args.all)
                {
                    Redraw(image, key);
                    RegenerateImageWatchers(image, key, imageEventWrappers);
                }
                else if (args.floor != key.floor)
                {
                    return;
                }
                else
                {
                    DrawTile(image, key, args.x, args.y);
                    RegenerateImageWatchers(image, key, imageEventWrappers);
                }
            };
            EventHandler<EventArgs> floorsChangedHandler = (_, args) =>
            {
                // Added or removed a floor: Just invalidate all floors to keep things simple.
                Redraw(image, key);
                RegenerateImageWatchers(image, key, imageEventWrappers);
            };

            dungeon.RoomChangedEvent += roomChangedHandler;
            dungeon.FloorsChangedEvent += floorsChangedHandler;
        }

        RegenerateImageWatchers(image, key, imageEventWrappers);
        Redraw(image, key);

        return image;
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Register event handlers for room images being modified
    /// </summary>
    void RegenerateImageWatchers(Image image, (Map map, int floor) key,
                                 Dictionary<(int, int), EventWrapper<Image>> imageEventWrappers)
    {
        for (int x = 0; x < key.map.MapWidth; x++)
        {
            for (int y = 0; y < key.map.MapHeight; y++)
            {
                var layout = key.map.GetRoomLayout(x, y, key.floor);
                Image roomImage = Workspace.GetCachedRoomImage(layout);

                // EventWrapper that should trigger when the room at this position is modified.
                // The event handler has been registered already but we need to update the event
                // source here.
                var wrapper = imageEventWrappers[(x, y)];
                wrapper.ReplaceEventSource(roomImage);
            }
        }
    }

    void Redraw(Image image, (Map map, int floor) key)
    {
        for (int x = 0; x < key.map.MapWidth; x++)
        {
            for (int y = 0; y < key.map.MapHeight; y++)
            {
                DrawTile(image, key, x, y);
            }
        }
    }

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
