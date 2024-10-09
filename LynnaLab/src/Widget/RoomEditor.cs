namespace LynnaLab;

/// <summary>
/// A Frame consisting of a RoomLayoutEditor, TilesetViewer, Minimap, etc. All the essentials for
/// editing a room.
/// </summary>
public class RoomEditor : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================

    /// <summary>
    /// Assumes that the TopLevel has a valid Project loaded.
    /// </summary>
    public RoomEditor(ProjectWorkspace workspace)
        : base("Room Editor")
    {
        this.Workspace = workspace;
        base.WindowFlags = ImGuiWindowFlags.MenuBar;

        roomLayoutEditor = new RoomLayoutEditor(this.Workspace, this, Brush);
        roomLayoutEditor.SetRoomLayout(Project.GetIndexedDataType<Room>(0x100).GetLayout(-1));

        tilesetViewer = new TilesetViewer(this.Workspace);
        tilesetViewer.Unselectable = true;
        tilesetViewer.SetTileset(roomLayoutEditor.Room.GetTileset(-1));

        // Tileset viewer: Selecting a single tile
        tilesetViewer.SelectedEvent += (index) =>
        {
            if (index != -1)
                Brush.SetTile(tilesetViewer, index);
        };
        // Tileset viewer: Selecting multiple tiles
        tilesetViewer.AddMouseAction(
            MouseButton.LeftClick,
            MouseModifier.None,
            MouseAction.ClickDrag,
            GridAction.SelectRangeCallback,
            (sender, args) =>
            {
                Brush.SetTiles(tilesetViewer, args.RectArray((x, y) => x + y * 16));
                tilesetViewer.SelectedIndex = -1;
            });

        overworldMinimap = new Minimap(this.Workspace);
        dungeonMinimap = new Minimap(this.Workspace);
        objectGroupEditor = new ObjectGroupEditor("Object Group Editor", Room.GetObjectGroup());

        SetRoom(0, false);

        overworldMinimap.SetMap(Project.GetWorldMap(0, 0));
        dungeonMinimap.SetMap(Project.GetDungeon(0));


        // Set up events

        roomLayoutEventWrapper.Bind<RoomTilesetChangedEventArgs>("TilesetChangedEvent", OnTilesetIndexChanged);
        overworldMinimap.SelectedEvent += (selectedIndex) =>
        {
            if (suppressEvents != 0)
                return;

            SetRoomLayout(overworldMinimap.Map.GetRoomLayout(overworldMinimap.SelectedX, overworldMinimap.SelectedY), false);
        };

        dungeonMinimap.SelectedEvent += (selectedIndex) =>
        {
            if (suppressEvents != 0)
                return;

            SetRoomLayout(dungeonMinimap.Map.GetRoomLayout(dungeonMinimap.SelectedX, dungeonMinimap.SelectedY, ActiveFloor), false);
        };

        Brush.BrushChanged += (_, _) =>
        {
            if (Brush.Source != tilesetViewer)
            {
                if (Brush.IsSingleTile)
                    tilesetViewer.SelectedIndex = Brush.GetTile(0, 0);
                else
                    tilesetViewer.SelectedIndex = -1;
            }
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // Windows / rendering areas
    RoomLayoutEditor roomLayoutEditor;
    TilesetViewer tilesetViewer;
    Minimap overworldMinimap, dungeonMinimap;
    ObjectGroupEditor objectGroupEditor;

    // Misc
    EventWrapper<RoomLayout> roomLayoutEventWrapper = new EventWrapper<RoomLayout>();

    int suppressEvents = 0;

    // Maps dungeon index to floor number. Allows the editor to remember what floor we were last
    // on for a given dungeon.
    Dictionary<int, int> floorDict = new Dictionary<int, int>();

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    public RoomLayout RoomLayout { get { return roomLayoutEditor.RoomLayout; } }
    public Room Room { get { return RoomLayout.Room; } }
    public int Season { get { return RoomLayout.Season; } }

    public TilesetViewer TilesetViewer { get { return tilesetViewer; } }

    public Brush Brush { get { return Workspace.Brush; } }


    // Private properties

    Minimap ActiveMinimap { get; set; }
    Map ActiveMap { get { return ActiveMinimap.Map; } }
    bool OverworldTabActive { get { return ActiveMinimap == overworldMinimap; } }
    bool DungeonTabActive { get { return ActiveMinimap == dungeonMinimap; } }

    int ActiveFloor
    {
        get
        {
            return GetSelectedFloor(ActiveMap);
        }
        set
        {
            if (OverworldTabActive)
                throw new NotImplementedException();
            floorDict[(ActiveMap as Dungeon).Index] = value;
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("Shift Room"))
            {
                ((int, int), string)[] directions = {
                    ((0, -1), "Up"),
                    ((1, 0), "Right"),
                    ((0, 1), "Down"),
                    ((-1, 0), "Left"),
                };

                foreach (var ((x, y), name) in directions)
                {
                    if (ImGui.MenuItem(name))
                    {
                        RoomLayout.ShiftTiles(x, y);
                    }
                }

                ImGui.EndMenu();
            }
            ImGui.EndMenuBar();
        }

        const float OFFSET = 15.0f;

        ImGui.BeginChild("Left Panel", new Vector2(tilesetViewer.WidgetSize.X + OFFSET, 0.0f));
        if (ImGui.BeginTabBar("##Left Panel Tabs"))
        {
            if (ImGui.BeginTabItem("Tileset"))
            {
                tilesetViewer.Render();
                ImGuiX.InputHex("Tileset", new Accessor<int>(() => Room.TilesetIndex));
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Objects"))
            {
                objectGroupEditor.Render();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Chests"))
            {
                DisplayChestTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("Middle Panel", new Vector2(roomLayoutEditor.WidgetSize.X + OFFSET, 0.0f),
                         ImGuiChildFlags.Border);
        ImGui.SeparatorText("Room");
        roomLayoutEditor.Render();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("Right Panel", Vector2.Zero, ImGuiChildFlags.Border);

        ImGui.SeparatorText("Map Selector");

        // Input fields for either minimap type
        {
            ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
            int roomIndex = Room.Index;
            if (ImGuiX.InputHex("Room", ref roomIndex, 3))
            {
                if (roomIndex >= 0 && roomIndex <= Project.NumRooms)
                    SetRoom(roomIndex, true);
            }
            ImGui.PopItemWidth();
        }

        var updateSelectedTab = (Minimap minimap) =>
        {
            if (ActiveMinimap != minimap)
            {
                ActiveMinimap = minimap;
                SetRoomLayout(
                    ActiveMap.GetRoomLayout(
                                  ActiveMinimap.SelectedX, ActiveMinimap.SelectedY),
                    false);
            }
        };

        ImGui.BeginTabBar("Map Tabs");
        if (ImGui.BeginTabItem("Overworld"))
        {
            updateSelectedTab(overworldMinimap);

            // Input fields for overworld minimaps
            {
                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);

                int worldIndex = Room.Group;
                if (ImGuiX.InputHex("World", ref worldIndex, 1))
                {
                    if (worldIndex >= 0 && worldIndex < Project.NumGroups)
                    {
                        int s = -1;
                        if (worldIndex == 0)
                            s = 0;
                        SetMap(Project.GetWorldMap(worldIndex, s));
                    }
                }

                ImGui.SameLine();
                int season = RoomLayout.Season;
                if (ImGuiX.InputHex("Season", ref season, 1))
                {
                    if (Room.IsValidSeason(season))
                        SetRoomLayout(Room.GetLayout(season), true);
                }

                ImGui.PopItemWidth();
            }

            overworldMinimap.Render();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Dungeon"))
        {
            updateSelectedTab(dungeonMinimap);

            if (ActiveFloor >= (ActiveMap as Dungeon).NumFloors)
                ActiveFloor = (ActiveMap as Dungeon).NumFloors - 1;

            // Input fields for dungeons
            {
                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);

                int dungeonIndex = (dungeonMinimap.Map as Dungeon).Index;
                if (ImGuiX.InputHex("Dungeon", ref dungeonIndex, 1))
                {
                    if (dungeonIndex >= 0 && dungeonIndex < Project.NumDungeons)
                    {
                        SetMap(Project.GetDungeon(dungeonIndex));
                    }
                }

                ImGui.SameLine();
                int newFloor = ActiveFloor;
                if (ImGuiX.InputHex("Floor", ref newFloor, 1))
                {
                    if (newFloor >= 0 && newFloor < (dungeonMinimap.Map as Dungeon).NumFloors)
                    {
                        ActiveFloor = newFloor;
                        dungeonMinimap.SetMap(dungeonMinimap.Map, ActiveFloor);
                        SetRoom(ActiveMap.GetRoom(
                                    ActiveMinimap.SelectedX, ActiveMinimap.SelectedY, ActiveFloor),
                                updateMinimap: false);
                    }
                }

                ImGui.PopItemWidth();
            }

            dungeonMinimap.Render();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();

        ImGui.EndChild();
    }

    public void SetRoom(int roomIndex, bool updateMinimap)
    {
        if (roomIndex == Room.Index)
            return;
        SetRoom(Project.GetIndexedDataType<Room>(roomIndex), updateMinimap);
    }

    /// <summary>
    /// Changes the loaded room, updates minimap & tileset viewer
    /// </summary>
    public void SetRoom(Room room, bool updateMinimap)
    {
        // Adjust the room index to its "expected" value, accounting for duplicates.
        if (room.ExpectedIndex != room.Index)
            room = Project.GetIndexedDataType<Room>(room.ExpectedIndex);

        if (Room == room)
            return;
        SetRoomLayout(room.GetLayout(Season), updateMinimap);
    }

    /// <summary>
    /// Changes the loaded map, updates the loaded room accordingly
    /// </summary>
    public void SetMap(Map map)
    {
        suppressEvents++;

        ActiveMinimap.SetMap(map, GetSelectedFloor(map));
        RoomLayout roomLayout = map.GetRoomLayout(
            ActiveMinimap.SelectedX, ActiveMinimap.SelectedY, ActiveFloor);
        SetRoomLayout(roomLayout, false);

        suppressEvents--;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Gets the floor that was last selected for the given map
    /// </summary>
    int GetSelectedFloor(Map map)
    {
        if (map is not Dungeon)
            return 0;
        return floorDict.GetValueOrDefault((map as Dungeon).Index, 0);
    }

    /// <summary>
    /// Changes the RoomLayout, updates all the viewers.
    /// </summary>
    void SetRoomLayout(RoomLayout roomLayout, bool updateMinimap)
    {
        suppressEvents++;

        roomLayoutEditor.SetRoomLayout(roomLayout);
        tilesetViewer.SetTileset(roomLayout.Tileset);
        objectGroupEditor.SetObjectGroup(roomLayout.Room.GetObjectGroup());
        roomLayoutEventWrapper.ReplaceEventSource(roomLayout);

        if (updateMinimap)
        {
            if (OverworldTabActive)
            {
                overworldMinimap.SetMap(Project.GetWorldMap(roomLayout.Group, roomLayout.Season));
                overworldMinimap.SelectedIndex = roomLayout.Room.Index & 0xff;
            }
            else if (DungeonTabActive)
            {
                int x, y, floor;
                Dungeon dungeon = Project.GetRoomDungeon(roomLayout.Room, out x, out y, out floor);

                if (dungeon != null)
                {
                    dungeonMinimap.SetMap(dungeon, floor);
                    dungeonMinimap.SelectedIndex = y * dungeon.MapWidth + x;
                }
            }
        }

        suppressEvents--;
    }

    /// <summary>
    /// Display contents of the "Chests" tab
    /// </summary>
    void DisplayChestTab()
    {
        if (Room.Chest == null)
        {
            ImGui.TextWrapped("Room does not contain a chest.");

            if (ImGui.Button("Add chest"))
            {
                Room.AddChest();
            }
            return;
        }

        ImGuiX.InputHex("ID", new Accessor<int>(() => Room.Chest.TreasureID));
        ImGuiX.InputHex("SubID", new Accessor<int>(() => Room.Chest.TreasureSubID));
    }

    /// <summary>
    /// Called when the room layout's tileset index changes.
    /// </summary>
    void OnTilesetIndexChanged(object sender, RoomTilesetChangedEventArgs args)
    {
        tilesetViewer.SetTileset(RoomLayout.Tileset);
    }
}
