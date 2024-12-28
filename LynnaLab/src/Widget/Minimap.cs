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
            // Darken rooms which are already used in a dungeon (effectively highlights any rooms
            // that are not used in dungeons)
            if (Workspace.DarkenUsedDungeonRooms && !(map is Dungeon) && map.RoomWidth == 15)
            {
                for (int tile = 0; tile < MaxIndex; tile++)
                {
                    int x = tile % Width, y = tile / Width;
                    Room room = map.GetRoom(x, y);
                    if (Project.RoomUsedInDungeon(room.Index))
                    {
                        var rect = base.TileRect(tile);
                        base.AddRectFilled(rect, Color.FromRgba(0, 0, 0, 0xa0));
                    }
                }
            }
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Map map;
    int floor;
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

        if (map == null)
            return;

        base.TileWidth = map.RoomWidth * 16;
        base.TileHeight = map.RoomHeight * 16;
        base.Width = map.MapWidth;
        base.Height = map.MapHeight;

        this.image = Workspace.GetCachedMapImage((Map, floor));
    }
}
