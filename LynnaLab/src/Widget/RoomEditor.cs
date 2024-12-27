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
        warpEditor = new WarpEditor(this, "Warp Editor", Room.GetWarpGroup());

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
            if (EditingWarpDestination == null)
                warpEditor.SetSelectedWarp(warp);

            handlingWarpSelection = false;
        };

        warpEditor.SelectedWarpEvent += (s, a) => handleWarpSelection(warpEditor.SelectedWarp);
        roomLayoutEditor.ChangedSelectedRoomComponentEvent += (s, a) => handleWarpSelection(roomLayoutEditor.SelectedWarpSource);
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
    string minimapTabToSelect; // Set to change right tab bar programmatically

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

    public Brush<int> Brush { get { return Workspace.Brush; } }

    public bool ObjectTabActive { get { return ActiveLeftTabName == "Objects"; } }
    public bool WarpTabActive { get { return ActiveLeftTabName == "Warps"; } }
    public bool ChestTabActive { get { return ActiveLeftTabName == "Chests"; } }

    public Warp EditingWarpDestination { get; private set; }


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
        const float X_OFFSET = 15.0f;

        float statusBarHeight = ImGui.GetTextLineHeight() + 8.0f;
        ImGui.BeginChild("Above status bar", ImGui.GetContentRegionAvail() - new Vector2(0.0f, statusBarHeight));

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

        ImGui.BeginChild("Left Panel", new Vector2(tilesetViewer.WidgetSize.X + X_OFFSET + 20.0f, 0.0f));
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
        ImGui.BeginChild("Middle Panel", new Vector2(roomLayoutEditor.WidgetSize.X + X_OFFSET, 0.0f),
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
                if (ActiveMinimap != minimap && minimapTabToSelect == null)
                {
                    // This should only execute when the tab is changed via a click, NOT when it's
                    // changed programmatically.
                    ActiveMinimap = minimap;
                    SetRoomLayout(
                        ActiveMap.GetRoomLayout(ActiveMinimap.SelectedX, ActiveMinimap.SelectedY),
                        0);
                }

                // Only return true if ActiveMinimap == minimap, because the code within the tab item
                // may make this assumption. This condition may not be true while the tab is being
                // changed programmatically. In this case the tab may be blank for a frame, which
                // isn't an issue.
                if (ActiveMinimap == minimap)
                    return true;
                else
                {
                    ImGui.EndTabItem();
                    return false;
                }
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

        ImGui.EndChild(); // Right panel
        ImGui.EndChild(); // Above status bar

        ImGui.Separator();
        ImGui.Text(GetStatusBarText());
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
    /// Invoked when doing "right click -> follow" on a warp, or double-clicking on it
    /// </summary>
    public void FollowWarp(Warp warp)
    {
        SetRoom(warp.DestRoom, 2);
    }

    /// <summary>
    /// Invoked when doing "right click -> edit warp destination"
    /// </summary>
    public void EditWarpDestination(Warp warp)
    {
        EditingWarpDestination = warp;
        SetRoom(warp.DestRoom, 2);
        roomLayoutEditor.UpdateRoomComponents();
    }

    /// <summary>
    /// Called when doing "right click -> done" on the warp destination box
    /// </summary>
    public void DisableWarpDestEditMode()
    {
        if (EditingWarpDestination == null)
            return;
        Room room = EditingWarpDestination.SourceRoom;
        EditingWarpDestination = null;
        SetRoom(room.Index, 2);
        roomLayoutEditor.UpdateRoomComponents(); // This line only necessary if dest room is same as source room
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

        if (EditingWarpDestination != null)
        {
            // When in warp dest editing mode, selecting a room sets the warp's destination to that room.
            EditingWarpDestination.DestRoom = roomLayout.Room;
        }

        roomLayoutEditor.SetRoomLayout(roomLayout);
        tilesetViewer.SetTileset(roomLayout.Tileset);
        objectGroupEditor.SetObjectGroup(roomLayout.Room.GetObjectGroup());
        roomLayoutEventWrapper.ReplaceEventSource(roomLayout);

        if (EditingWarpDestination == null)
        {
            // Don't update the warp editor when in warp dest edit mode, because we want it to keep
            // showing the warp data from the source room, not the room we're currently in
            warpEditor.SetWarpGroup(roomLayout.Room.GetWarpGroup());
        }

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

        ImGui.SeparatorText("Chest Data");

        ImGuiLL.RenderValueReferenceGroup(Room.Chest.ValueReferenceGroup, null, (doc) => Workspace.ShowDocumentation(doc));

        ImGui.SeparatorText("");
        ImGuiX.ShiftCursorScreenPos(0.0f, 10.0f);

        TreasureObject treasureObj = Room.Chest.Treasure;

        if (treasureObj == null)
        {
            ImGui.TextWrapped($"Treasure {Room.Chest.TreasureID:X2} {Room.Chest.TreasureSubID:X2} doesn't exist.");
            if (ImGui.Button("Create new SubID"))
            {
                TreasureObject newObj = Room.Chest.TreasureGroup.AddTreasureObjectSubid();
                Room.Chest.TreasureSubID = newObj.SubID;
            }
        }
        else
        {
            int id = Room.Chest.TreasureID;
            int subid = Room.Chest.TreasureSubID;
            if (ImGui.CollapsingHeader($"Treasure object data: {id:X2} {subid:X2}###TreasureObjectHeader {id}"))
            {
                ImGuiX.ShiftCursorScreenPos(new Vector2(10.0f, 0.0f));
                ImGuiLL.RenderValueReferenceGroup(treasureObj.ValueReferenceGroup, null, Workspace.ShowDocumentation);
            }
        }
    }

    /// <summary>
    /// Text to show at the very bottom of the window.
    /// </summary>
    string GetStatusBarText()
    {
        Tileset tileset = tilesetViewer.Tileset;

        Func<string> GetWarnings = () =>
        {
            // Print warning if tileset is a dungeon but used on an overworld
            if (OverworldTabActive && tileset.DungeonFlag && RoomLayout.IsSmall)
                return "Dungeon tileset being used outside a dungeon";

            // Print warning if on the dungeon tab but tileset not marked as a dungeon
            if (DungeonTabActive && !tileset.DungeonFlag)
                return "Tileset is not marked as a dungeon, but is used in a dungeon";

            // Print warning if, on the dungeon tab, the tileset's dungeon index doesn't match the
            // dungeon itself
            if (DungeonTabActive && tileset.DungeonIndex != (ActiveMap as Dungeon)?.Index)
                return string.Format("Tileset's dungeon index ({0:X}) doesn't match the dungeon ({1:X})",
                                     (int)tileset.DungeonIndex, (ActiveMap as Dungeon)?.Index);

            // Print warning if tileset dungeon bit is unset, but dungeon value is not $F.
            // Only in Seasons, because Ages has better sanity checks for this.
            if (Project.Game == Game.Seasons && !tileset.DungeonFlag && tileset.DungeonIndex != 0x0f)
                return "If tileset is not a dungeon, dungeon index should be $F";

            // Warning for a room that's using a dungeon tileset but doesn't exist in that dungeon
            if (OverworldTabActive && tileset.DungeonFlag && tileset.DungeonIndex < Project.NumDungeons
                       && !Project.GetDungeon(tileset.DungeonIndex).RoomUsed(Room.Index))
                return $"Tileset is for dungeon {(int)tileset.DungeonIndex:X} but this room is not in that dungeon";

            // Print warnings when tileset's layout group does not match expected value. Does nothing on
            // the hack-base branch as the tileset's layout group is ignored.
            if (!Project.Config.ExpandedTilesets)
            {
                int expectedGroup = Project.GetCanonicalLayoutGroup(Room.Group, Season);
                if (tileset.LayoutGroup != expectedGroup)
                {
                    return string.Format(
                        "Layout group of tileset ({0:X}) does not match expected value ({1:X})!"
                        + " This room's layout data might be shared with another's.",
                        (int)tileset.LayoutGroup,
                        expectedGroup);
                }
            }

            return null;
        };

        if (EditingWarpDestination != null)
            return "Warp dest edit mode enabled. Select a room to set the warp's destination. Right-click on the W box and select \"Done\" to exit.";

        string warning = GetWarnings();
        if (warning != null)
            return "WARNING: " + warning;
        else
            return "";
    }

    /// <summary>
    /// Called when the room layout's tileset index changes.
    /// </summary>
    void OnTilesetIndexChanged(object sender, RoomTilesetChangedEventArgs args)
    {
        tilesetViewer.SetTileset(RoomLayout.Tileset);
    }
}
