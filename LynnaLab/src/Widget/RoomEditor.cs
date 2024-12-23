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
            MouseButton.Any,
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
        objectGroupEditor = new ObjectGroupEditor(this.Workspace, "Object Group Editor", Room.GetObjectGroup());
        warpEditor = new WarpEditor(Workspace, "Warp Editor", Room.GetWarpGroup());

        SetRoom(0, 0);

        overworldMinimap.SetMap(Project.GetWorldMap(0, 0));
        dungeonMinimap.SetMap(Project.GetDungeon(0));

        ActiveMinimap = overworldMinimap;


        // Set up events

        roomLayoutEventWrapper.Bind<RoomTilesetChangedEventArgs>("TilesetChangedEvent", OnTilesetIndexChanged);
        overworldMinimap.SelectedEvent += (selectedIndex) =>
        {
            if (suppressEvents != 0)
                return;

            SetRoomLayout(overworldMinimap.Map.GetRoomLayout(overworldMinimap.SelectedX, overworldMinimap.SelectedY), 0);
        };

        dungeonMinimap.SelectedEvent += (selectedIndex) =>
        {
            if (suppressEvents != 0)
                return;

            SetRoomLayout(dungeonMinimap.Map.GetRoomLayout(dungeonMinimap.SelectedX, dungeonMinimap.SelectedY, ActiveFloor), 0);
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

        // Keeping selected object synchronized between ObjectGroupEditor & RoomLayoutEditor
        var handleObjectSelection = (ObjectDefinition obj) =>
        {
            if (handlingObjectSelection)
                return;
            handlingObjectSelection = true;

            roomLayoutEditor.SelectObject(obj);
            if (obj == null)
                objectGroupEditor.Unselect();
            else
                objectGroupEditor.SelectObject(obj.ObjectGroup, obj);

            handlingObjectSelection = false;
        };

        objectGroupEditor.ObjectSelectedEvent += (s, a) => handleObjectSelection(objectGroupEditor.SelectedObject);
        roomLayoutEditor.ChangedSelectedRoomComponentEvent += (s, a) => handleObjectSelection(roomLayoutEditor.SelectedObject);

        // Keeping selected warp synchronized between ObjectGroupEditor & RoomLayoutEditor
        var handleWarpSelection = (Warp warp) =>
        {
            if (handlingWarpSelection)
                return;
            handlingWarpSelection = true;

            roomLayoutEditor.SelectWarpSource(warp);
            warpEditor.SetSelectedWarp(warp);

            handlingWarpSelection = false;
        };

        warpEditor.SelectedWarpEvent += (s, a) => handleWarpSelection(warpEditor.SelectedWarp);
        roomLayoutEditor.ChangedSelectedRoomComponentEvent += (s, a) => handleWarpSelection(roomLayoutEditor.SelectedWarpSource);

        warpEditor.FollowEvent += (_, warp) => FollowWarp(warp);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // Windows / rendering areas
    RoomLayoutEditor roomLayoutEditor;
    TilesetViewer tilesetViewer;
    Minimap overworldMinimap, dungeonMinimap;
    ObjectGroupEditor objectGroupEditor;
    WarpEditor warpEditor;

    // Misc
    EventWrapper<RoomLayout> roomLayoutEventWrapper = new EventWrapper<RoomLayout>();

    int suppressEvents = 0;
    bool handlingObjectSelection, handlingWarpSelection;
    string minimapTabToSelect;

    // Maps dungeon index to floor number. Allows the editor to remember what floor we were last
    // on for a given dungeon.
    Dictionary<int, int> floorDict = new Dictionary<int, int>();

    // ================================================================================
    // Events
    // ================================================================================

    // Event that fires when the selected left-side tab has been changed
    public event EventHandler TabChangedEvent;

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

    public bool ObjectTabActive { get { return ActiveLeftTabName == "Objects"; } }
    public bool ChestTabActive { get { return ActiveLeftTabName == "Chests"; } }


    // Private properties

    Minimap ActiveMinimap { get; set; }
    Map ActiveMap { get { return ActiveMinimap.Map; } }

    // Left: Editor mode tabs
    string ActiveLeftTabName { get; set; }

    // Right: Overworld tabs
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

        // Wrapper over ImGui.BeginTabItem() to help us keep track of which tab is open at any given time
        var TrackedTabItem = (string tabName) => {
            if (ImGui.BeginTabItem(tabName))
            {
                bool changed = ActiveLeftTabName != tabName;
                ActiveLeftTabName = tabName;
                if (changed)
                    TabChangedEvent?.Invoke(this, null);
                return true;
            }
            else
            {
                return false;
            }
        };

        ImGui.BeginChild("Left Panel", new Vector2(tilesetViewer.WidgetSize.X + OFFSET, 0.0f));
        if (ImGui.BeginTabBar("##Left Panel Tabs"))
        {
            if (TrackedTabItem("Tileset"))
            {
                tilesetViewer.Render();
                ImGuiX.InputHex("Tileset", new Accessor<int>(() => Room.TilesetIndex));
                ImGui.EndTabItem();
            }
            if (TrackedTabItem("Objects"))
            {
                objectGroupEditor.Render();
                ImGui.EndTabItem();
            }
            if (TrackedTabItem("Warps"))
            {
                warpEditor.Render();
                ImGui.EndTabItem();
            }
            if (TrackedTabItem("Chests"))
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
                    SetRoom(roomIndex, 1);
            }
            ImGui.PopItemWidth();
        }

        var minimapTabItem = (string name, Minimap minimap) =>
        {
            var flags = minimapTabToSelect == name ? ImGuiTabItemFlags.SetSelected : 0;
            if (ImGuiX.BeginTabItem(name, flags))
            {
                if (ActiveMinimap != minimap)
                {
                    ActiveMinimap = minimap;
                    SetRoomLayout(
                        ActiveMap.GetRoomLayout(ActiveMinimap.SelectedX, ActiveMinimap.SelectedY),
                        0);
                }

                return true;
            }

            return false;
        };

        ImGui.BeginTabBar("Map Tabs");
        if (minimapTabItem("Overworld", overworldMinimap))
        {
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
                        SetRoomLayout(Room.GetLayout(season), 1);
                }

                ImGui.PopItemWidth();
            }

            overworldMinimap.Render();
            ImGui.EndTabItem();
        }
        if (minimapTabItem("Dungeon", dungeonMinimap))
        {
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
                                updateMinimap: 0);
                    }
                }

                ImGui.PopItemWidth();
            }

            dungeonMinimap.Render();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();

        minimapTabToSelect = null;

        ImGui.EndChild();
    }

    public void SetRoom(int roomIndex, int updateMinimap)
    {
        if (roomIndex == Room.Index)
            return;
        SetRoom(Project.GetIndexedDataType<Room>(roomIndex), updateMinimap);
    }

    /// <summary>
    /// Changes the loaded room, updates minimap & tileset viewer
    /// </summary>
    public void SetRoom(Room room, int updateMinimap)
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
        SetRoomLayout(roomLayout, 0);

        suppressEvents--;
    }

    /// <summary>
    /// Invoked when doing "right click -> follow" on a warp
    /// </summary>
    public void FollowWarp(Warp warp)
    {
        SetRoom(warp.DestRoom, 2);
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
    /// updateMinimap: 0 = don't change minimap, 1 = change minimap to match the room, 2 = change
    /// between overworld/dungeon tabs as necessary
    /// </summary>
    void SetRoomLayout(RoomLayout roomLayout, int updateMinimap)
    {
        suppressEvents++;

        roomLayoutEditor.SetRoomLayout(roomLayout);
        tilesetViewer.SetTileset(roomLayout.Tileset);
        objectGroupEditor.SetObjectGroup(roomLayout.Room.GetObjectGroup());
        warpEditor.SetWarpGroup(roomLayout.Room.GetWarpGroup());
        roomLayoutEventWrapper.ReplaceEventSource(roomLayout);

        if (updateMinimap != 0)
        {
            int x, y, floor;
            Dungeon dungeon = Project.GetRoomDungeon(roomLayout.Room, out x, out y, out floor);

            if (updateMinimap == 2)
            {
                // Change which tab is selected
                if (dungeon == null)
                {
                    ActiveMinimap = overworldMinimap;
                    minimapTabToSelect = "Overworld";
                }
                else
                {
                    ActiveMinimap = dungeonMinimap;
                    minimapTabToSelect = "Dungeon";
                }
            }

            if (OverworldTabActive)
            {
                overworldMinimap.SetMap(Project.GetWorldMap(roomLayout.Group, roomLayout.Season));
                overworldMinimap.SelectedIndex = roomLayout.Room.Index & 0xff;
            }
            else if (DungeonTabActive)
            {
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
