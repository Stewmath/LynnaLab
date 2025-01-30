namespace LynnaLab;

/// <summary>
/// Caches textures for maps. Key is a tuple: (Map, int) where the int is the floor index.
///
/// The world maps hold the versions of the room images that are used by the "RoomTextureCacher"
/// class. Room rendering logic is actually in here. (It seemed more optimal to render everything at
/// once onto one large texture.)
/// </summary>
public class MapTextureCacher : IDisposeNotifier
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public MapTextureCacher(ProjectWorkspace workspace, Map map, int floor)
    {
        this.Workspace = workspace;
        this.Map = map;
        this.Floor = floor;

        GenerateTexture();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    RgbaTexture texture;

    // ================================================================================
    // Properties
    // ================================================================================

    public Map Map { get; private set; }
    public int Floor { get; private set; }

    ProjectWorkspace Workspace { get; set; }

    // ================================================================================
    // Events
    // ================================================================================

    public event EventHandler DisposedEvent;

    // ================================================================================
    // Public methods
    // ================================================================================

    public RgbaTexture GetTexture()
    {
        return texture;
    }

    // Will be called when a dungeon floor is deleted.
    public void Dispose()
    {
        texture.Dispose();
        texture = null;
        DisposedEvent?.Invoke(this, null);
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected void GenerateTexture()
    {
        texture = TopLevel.Backend.CreateTexture(
            Map.MapWidth * Map.RoomWidth * 16,
            Map.MapHeight * Map.RoomHeight * 16);

        // Watch for room assignment changes (dungeons only)

        if (Map is Dungeon dungeon)
        {
            EventHandler<DungeonRoomChangedEventArgs> roomChangedHandler = (_, args) =>
            {
                if (args.all)
                {
                    Redraw();
                }
                else if (args.floor == Floor)
                {
                    DrawTile(args.x, args.y);
                }
            };
            EventHandler<EventArgs> floorsChangedHandler = (_, args) =>
            {
                // Added or removed a floor: Just invalidate all floors to keep things simple. Also
                // must check whether the floor in question was deleted.
                if (Floor >= dungeon.NumFloors)
                {
                    Dispose();
                }
                else
                {
                    Redraw();
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

        Redraw();
    }

    /// <summary>
    /// Redraw all instances of the given room layout in this map. (Does a tile-by-tile rendering of
    /// the room with many CopyTexture calls.)
    /// </summary>
    public void RedrawRoom(RoomLayout layout)
    {
        if (layout.Season != Map.Season)
            return;

        foreach (var (x, y, f) in Map.GetRoomPositions(layout.Room))
        {
            if (f == Floor)
                DrawTile(x, y);
        }
    }

    /// <summary>
    /// Redraw from a pre-rendered version of the image (from another instance of MapTextureCacher).
    /// </summary>
    public void RedrawRoomFrom(RoomLayout layout, MapTextureCacher source, int srcX, int srcY)
    {
        if (layout.Season != Map.Season)
            return;

        Point roomSize = new Point(Map.RoomWidth, Map.RoomHeight) * 16;
        foreach (var (x, y, f) in Map.GetRoomPositions(layout.Room))
        {
            if (f == Floor)
            {
                texture.DrawFrom(source.texture, new Point(srcX, srcY) * roomSize, new Point(x, y) * roomSize, roomSize);
            }
        }
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    void Redraw()
    {
        for (int x = 0; x < Map.MapWidth; x++)
        {
            for (int y = 0; y < Map.MapHeight; y++)
            {
                DrawTile(x, y);
            }
        }
    }

    void DrawTile(int x, int y)
    {
        Point roomSize = new Point(Map.RoomWidth, Map.RoomHeight) * 16;
        var layout = Map.GetRoomLayout(x, y, Floor);

        var tilesetTexture = Workspace.GetCachedTilesetTexture(layout.Tileset);

        for (int i = 0; i < layout.Width; i++)
        {
            for (int j = 0; j < layout.Height; j++)
            {
                int tile = layout.GetTile(i, j);

                // This is not terribly fast on the Vulkan backend, possibly because Veldrid
                // always calls vkCmdCopyImage with a size of 1 and sends them as separate
                // commands. Seems ok with OpenGL (but maybe depends on the driver).
                texture.DrawFrom(tilesetTexture, new Point(tile % 16, tile / 16) * 16, new Point(x, y) * roomSize + new Point(i, j) * 16, new Point(16, 16));
            }
        }
    }
}
