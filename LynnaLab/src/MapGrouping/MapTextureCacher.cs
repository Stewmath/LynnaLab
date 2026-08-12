using SixLabors.ImageSharp;
using PixelFormats = SixLabors.ImageSharp.PixelFormats;

using Point = Util.Point;

namespace LynnaLab;

/// <summary>
/// Caches textures for FloorPlans (maps).
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
    public MapTextureCacher(ProjectWorkspace workspace, FloorPlan floorPlan)
    {
        this.Workspace = workspace;
        this.FloorPlan = floorPlan;

        GenerateTexture();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    RgbaTexture texture;
    bool dirty = false;

    // ================================================================================
    // Properties
    // ================================================================================

    public FloorPlan FloorPlan { get; private set; }

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

    /// <summary>
    /// Called once per frame.
    /// </summary>
    public void UpdateFrame()
    {
        if (dirty)
        {
            Redraw();
            dirty = false;
        }
    }

    public void CaptureImage(Action<Image<PixelFormats.Rgba32>> callback)
    {
        texture.CaptureImage(callback);
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected void GenerateTexture()
    {
        texture = Top.Backend.CreateTexture(
            FloorPlan.MapWidth * FloorPlan.RoomWidth * 16,
            FloorPlan.MapHeight * FloorPlan.RoomHeight * 16,
            staging: true);

        // Watch for room assignment changes (dungeons only)

        if (FloorPlan is Dungeon.Floor dungeonFloor)
        {
            Dungeon dungeon = dungeonFloor.Dungeon;

            EventHandler<DungeonChangedEventArgs> changedHandler = (_, args) =>
            {
                if (args.FloorsChanged && !dungeon.FloorPlans.Contains(dungeonFloor))
                {
                    Dispose();
                }
                else if (args.AllRoomsChanged || (args.SingleFloorChanged == FloorPlan))
                {
                    dirty = true;
                }
                else if (args.RoomPosValid && args.RoomPos.floorPlan == dungeonFloor)
                {
                    DrawTile(args.RoomPos.x, args.RoomPos.y);
                }
            };

            dungeon.DungeonChangedEvent += changedHandler;

            texture.DisposedEvent += (_, _) =>
            {
                dungeon.DungeonChangedEvent -= changedHandler;
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
        if (layout.Season != FloorPlan.Season)
            return;

        foreach (var (x, y) in FloorPlan.GetRoomPositions(layout.Room))
        {
            DrawTile(x, y);
        }
    }

    /// <summary>
    /// Redraw from a pre-rendered version of the image (from another instance of MapTextureCacher).
    /// </summary>
    public void RedrawRoomFrom(RoomLayout layout, MapTextureCacher source, int srcX, int srcY)
    {
        if (layout.Season != FloorPlan.Season)
            return;

        Point roomSize = new Point(FloorPlan.RoomWidth, FloorPlan.RoomHeight) * 16;
        foreach (var (x, y) in FloorPlan.GetRoomPositions(layout.Room))
        {
            texture.DrawFrom(source.texture, new Point(srcX, srcY) * roomSize, new Point(x, y) * roomSize, roomSize);
        }
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    void Redraw()
    {
        for (int x = 0; x < FloorPlan.MapWidth; x++)
        {
            for (int y = 0; y < FloorPlan.MapHeight; y++)
            {
                DrawTile(x, y);
            }
        }
    }

    void DrawTile(int x, int y)
    {
        Point roomSize = new Point(FloorPlan.RoomWidth, FloorPlan.RoomHeight) * 16;
        var layout = FloorPlan.GetRoomLayout(x, y);

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
