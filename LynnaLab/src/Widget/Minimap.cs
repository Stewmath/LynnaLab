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
            if (Workspace.DarkenDuplicateRooms && !(floorPlan is Dungeon.Floor))
            {
                for (int tile = 0; tile < MaxIndex; tile++)
                {
                    int x = tile % Width, y = tile / Width;
                    Room room = floorPlan.GetRoom(x, y);
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
                if (FloorPlan.GetRoomPosition(Project.GetRoom(cursor.room), out int x, out int y))
                {
                    // Rectangle around whole room
                    FRect rect = base.TileRect(x, y);
                    base.AddRect(rect, remoteState.Color, ImGuiX.Unit(base.RectThickness));

                    float tileSize = 16 * base.Scale;

                    // Draw the cursor within the room
                    if (cursor.tileStart != -1)
                    {
                        float p1x = rect.X + (cursor.tileStart % floorPlan.RoomWidth) * tileSize;
                        float p1y = rect.Y + (cursor.tileStart / floorPlan.RoomWidth) * tileSize;
                        float p2x = rect.X + (cursor.tileEnd % floorPlan.RoomWidth) * tileSize + tileSize;
                        float p2y = rect.Y + (cursor.tileEnd / floorPlan.RoomWidth) * tileSize + tileSize;
                        base.AddRect(new FRect(p1x, p1y, p2x - p1x, p2y - p1y), remoteState.Color, base.RectThickness * base.Scale);
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
                ImGuiX.Tooltip($"{x}, {y} (room ${FloorPlan.GetRoom(x, y).Index:X3})");
            }
        };

        // Watch for dungeons removing a floor that we're currently looking at.
        // This is enough to prevent crashes, though it won't update the selected room in the RoomEditor.
        dungeonEW.Bind<DungeonChangedEventArgs>("DungeonChangedEvent", (_, args) =>
        {
            if (!(floorPlan is Dungeon.Floor floor))
            {
                log.Warn("Minimap: Invoked DungeonChangedEvent without dungeon selected?");
                return;
            }
            if (!args.FloorsChanged)
                return;
            Dungeon dungeon = floor.Dungeon;
            if (!floor.Dungeon.FloorPlans.Contains(floor))
            {
                SetFloorPlan(dungeon.FloorPlans.First());
            }
        }, weak: false);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    private static readonly log4net.ILog log = LogHelper.GetLogger();

    FloorPlan floorPlan;
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

    public FloorPlan FloorPlan { get { return floorPlan; } }

    public RoomLayout SelectedRoomLayout
    {
        get { return floorPlan.GetRoomLayout(SelectedX, SelectedY); }
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
    public void SetFloorPlan(FloorPlan plan)
    {
        if (this.floorPlan == plan)
            return;

        this.floorPlan = plan;

        this.image = null;

        if (plan != null)
        {
            base.TileWidth = plan.RoomWidth * 16;
            base.TileHeight = plan.RoomHeight * 16;
            base.Width = plan.MapWidth;
            base.Height = plan.MapHeight;

            this.image = Workspace.GetCachedMapTexture(FloorPlan);
        }

        dungeonEW.ReplaceEventSource((plan as Dungeon.Floor)?.Dungeon);
    }

    public RoomLayout GetRoomLayout(int x, int y)
    {
        return floorPlan.GetRoomLayout(x, y);
    }
}
