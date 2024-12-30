namespace LynnaLab;

public class Minimap : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public Minimap(ProjectWorkspace workspace)
        : base("Minimap")
    {
        this.Workspace = workspace;

        base.Selectable = true;
        base.InChildWindow = true;
        base.MinScale = 0.15f;
        base.MaxScale = 1.0f;
        base.Scale = 0.2f;

        base.AfterRenderTileGrid += (_, _) =>
        {
            // Darken rooms which are already used in a dungeon, or rooms considered "duplicates"
            // (not the main version of the room). These both have the effect of discouraging
            // interacting with these rooms as there is a more canonical version of it somewhere.
            if (Workspace.DarkenUsedDungeonRooms && !(map is Dungeon))
            {
                for (int tile = 0; tile < MaxIndex; tile++)
                {
                    int x = tile % Width, y = tile / Width;
                    Room room = map.GetRoom(x, y);
                    if (Project.RoomUsedInDungeon(room.Index) || room.Index != room.ExpectedIndex)
                    {
                        var rect = base.TileRect(tile);
                        base.AddRectFilled(rect, Color.FromRgba(0, 0, 0, 0xa0));
                    }
                }
            }
        };

        // Watch for dungeons removing a floor that we're currently looking at.
        // This is enough to prevent crashes, though it won't update the selected room in the RoomEditor.
        dungeonEW.Bind<EventArgs>("FloorsChangedEvent", (_, _) =>
        {
            if (!(map is Dungeon dungeon))
                return;
            if (floor >= dungeon.NumFloors)
            {
                SetMap(dungeon, dungeon.NumFloors - 1);
            }
        }, weak: false);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Map map;
    int floor;
    Image image;
    EventWrapper<Dungeon> dungeonEW = new();

    // ================================================================================
    // Properties
    // ================================================================================

    public override Image Image
    {
        get
        {
            return image;
        }
    }

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    public Map Map { get { return map; } }

    public RoomLayout SelectedRoomLayout
    {
        get { return map.GetRoomLayout(SelectedX, SelectedY, 0); }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.SameLine(); // Same line as whatever came before this (World/Season selector buttons)
        base.RenderScrollBar();
        base.Render();
    }

    /// <summary>
    /// Sets the map to display
    /// </summary>
    public void SetMap(Map map, int floor = 0)
    {
        if (this.map == map && this.floor == floor)
            return;

        this.map = map;
        this.floor = floor;

        this.image = null;

        if (map != null)
        {
            base.TileWidth = map.RoomWidth * 16;
            base.TileHeight = map.RoomHeight * 16;
            base.Width = map.MapWidth;
            base.Height = map.MapHeight;

            this.image = Workspace.GetCachedMapImage((Map, floor));
        }

        dungeonEW.ReplaceEventSource(map as Dungeon);
    }
}
