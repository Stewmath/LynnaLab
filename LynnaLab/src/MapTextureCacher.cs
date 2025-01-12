namespace LynnaLab;

/// <summary>
/// Caches textures for maps. Key is a tuple: (Map, int) where the int is the floor index.
///
/// The code here is quite simple compared to RoomTextureCacher because the RoomTextureCacher
/// accounts for almost anything that would affect a minimap's texture. So this class only needs
/// to watch for changes to the room textures.
/// </summary>
public class MapTextureCacher : TextureCacher<(Map map, int floor)>
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public MapTextureCacher(ProjectWorkspace workspace)
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

    protected override Texture GenerateTexture((Map map, int floor) key)
    {
        Texture texture = TopLevel.Backend.CreateTexture(
            key.map.MapWidth * key.map.RoomWidth * 16,
            key.map.MapHeight * key.map.RoomHeight * 16);

        // EventWrappers for each position in the map, managing events to invoke when those
        // specific room textures have been modified.
        var textureEventWrappers = new Dictionary<(int, int), EventWrapper<Texture>>();

        // Set up the event handlers for room texture modification. They will be bound to the
        // relevant room later, in the "RegenerateTextureWatchers" function.
        for (int x = 0; x < key.map.MapWidth; x++)
        {
            for (int y = 0; y < key.map.MapHeight; y++)
            {
                int tileX = x, tileY = y; // New variables for closure
                EventHandler<TextureModifiedEventArgs> handler = (_, args) =>
                {
                    DrawTile(texture, key, tileX, tileY);
                };

                var wrapper = new EventWrapper<Texture>();
                wrapper.Bind<TextureModifiedEventArgs>("ModifiedEvent", handler, weak: false);
                textureEventWrappers[(x, y)] = wrapper;
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
                    Redraw(texture, key);
                    RegenerateTextureWatchers(texture, key, textureEventWrappers);
                }
                else if (args.floor != key.floor)
                {
                    return;
                }
                else
                {
                    DrawTile(texture, key, args.x, args.y);
                    RegenerateTextureWatchers(texture, key, textureEventWrappers);
                }
            };
            EventHandler<EventArgs> floorsChangedHandler = (_, args) =>
            {
                // Added or removed a floor: Just invalidate all floors to keep things simple. Also
                // must check whether the floor in question was deleted.
                if (key.floor >= dungeon.NumFloors)
                {
                    DisposeTexture(key);
                }
                else
                {
                    Redraw(texture, key);
                    RegenerateTextureWatchers(texture, key, textureEventWrappers);
                }
            };

            dungeon.RoomChangedEvent += roomChangedHandler;
            dungeon.FloorsChangedEvent += floorsChangedHandler;

            texture.DisposedEvent += (_, _) =>
            {
                dungeon.RoomChangedEvent -= roomChangedHandler;
                dungeon.FloorsChangedEvent -= floorsChangedHandler;
            };
        }

        texture.DisposedEvent += (_, _) =>
        {
            foreach (EventWrapper<Texture> wrapper in textureEventWrappers.Values)
                wrapper.UnbindAll();
        };

        RegenerateTextureWatchers(texture, key, textureEventWrappers);
        Redraw(texture, key);

        return texture;
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Register event handlers for room textures being modified
    /// </summary>
    void RegenerateTextureWatchers(Texture texture, (Map map, int floor) key,
                                 Dictionary<(int, int), EventWrapper<Texture>> textureEventWrappers)
    {
        for (int x = 0; x < key.map.MapWidth; x++)
        {
            for (int y = 0; y < key.map.MapHeight; y++)
            {
                var layout = key.map.GetRoomLayout(x, y, key.floor);
                Texture roomTexture = Workspace.GetCachedRoomTexture(layout);

                // EventWrapper that should trigger when the room at this position is modified.
                // The event handler has been registered already but we need to update the event
                // source here.
                var wrapper = textureEventWrappers[(x, y)];
                wrapper.ReplaceEventSource(roomTexture);
            }
        }
    }

    void Redraw(Texture texture, (Map map, int floor) key)
    {
        for (int x = 0; x < key.map.MapWidth; x++)
        {
            for (int y = 0; y < key.map.MapHeight; y++)
            {
                DrawTile(texture, key, x, y);
            }
        }
    }

    void DrawTile(Texture texture, (Map map, int floor) key, int x, int y)
    {
        RoomLayout layout = key.map.GetRoomLayout(x, y, key.floor);
        Texture roomTexture = Workspace.GetCachedRoomTexture(layout);

        roomTexture.DrawOn(texture,
                         new Point(0, 0),
                         new Point(x * key.map.RoomWidth * 16, y * key.map.RoomHeight * 16),
                         new Point(layout.Width * 16, layout.Height * 16));
    }
}
