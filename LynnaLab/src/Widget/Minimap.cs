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
        base.RequestedScale = 0.2f;

        base.AfterRenderTileGrid += (_, _) =>
        {
            // Darken rooms which are already used in a dungeon, or rooms considered "duplicates"
            // (not the main version of the room). These both have the effect of discouraging
            // interacting with these rooms as there is a more canonical version of it somewhere.
            if (Workspace.DarkenDuplicateRooms && !(map is Dungeon))
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

            // Draw cursors for remote instances
            foreach (var remoteState in Workspace.RemoteStates.Values)
            {
                CursorPosition cursor = remoteState.CursorPosition;
                if (Map.GetRoomPosition(Project.GetRoom(cursor.room), out int x, out int y, out int f))
                {
                    if (f == Floor)
                    {
                        // Rectangle around whole room
                        FRect rect = base.TileRect(x, y);
                        base.AddRect(rect, remoteState.Color, ImGuiX.Unit(base.RectThickness));

                        float tileSize = 16 * base.Scale;

                        // Draw the cursor within the room
                        if (cursor.tileStart != -1)
                        {
                            float p1x = rect.X + (cursor.tileStart % map.RoomWidth) * tileSize;
                            float p1y = rect.Y + (cursor.tileStart / map.RoomWidth) * tileSize;
                            float p2x = rect.X + (cursor.tileEnd % map.RoomWidth) * tileSize + tileSize;
                            float p2y = rect.Y + (cursor.tileEnd / map.RoomWidth) * tileSize + tileSize;
                            base.AddRect(new FRect(p1x, p1y, p2x - p1x, p2y - p1y), remoteState.Color, base.RectThickness * base.Scale);
                        }
                    }
                }
            }
        };

        // Show coordinate tooltip
        base.OnHover = (int tile) =>
        {
            if (Top.GlobalConfig.ShowCoordinateTooltip)
            {
                int x = tile % Width;
                int y = tile / Width;
                ImGuiX.Tooltip($"{x}, {y} (room ${Map.GetRoom(x, y).Index:X3})");
            }
        };

        // Watch for dungeons removing a floor that we're currently looking at.
        // This is enough to prevent crashes, though it won't update the selected room in the RoomEditor.
        dungeonEW.Bind<DungeonChangedEventArgs>("ChangedEvent", (_, args) =>
        {
            if (!(map is Dungeon dungeon))
                return;
            if (!args.FloorsChanged)
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
    TextureBase image;
    EventWrapper<Dungeon> dungeonEW = new();

    // ================================================================================
    // Properties
    // ================================================================================

    public override TextureBase Texture
    {
        get
        {
            return image;
        }
    }

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    public Map Map { get { return map; } }

    public int Floor { get { return floor; } }

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

            this.image = Workspace.GetCachedMapTexture((Map, floor));
        }

        dungeonEW.ReplaceEventSource(map as Dungeon);
    }

    public RoomLayout GetRoomLayout(int x, int y)
    {
        return map.GetRoomLayout(x, y, floor);
    }
}
