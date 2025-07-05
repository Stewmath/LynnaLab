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

    public RoomEditor(ProjectWorkspace workspace)
        : base("Room Editor")
    {
        base.DefaultSize = new Vector2(1420.0f, 620.0f);
        this.Workspace = workspace;

        roomLayoutEditor = new RoomLayoutEditor(this.Workspace, this, Brush);

        // Need to load some room, any room to avoid null pointer exceptions
        roomLayoutEditor.SetRoomLayout(Project.GetIndexedDataType<Room>(0x100).GetLayout(Season.None));

        tilesetViewer = new TilesetViewer(this.Workspace);
        tilesetViewer.Unselectable = true;
        tilesetViewer.SetTileset(roomLayoutEditor.Room.GetTileset(Season.None, autoCorrect: true));

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
            });

        overworldMinimap = new Minimap(this.Workspace);
        dungeonMinimap = new Minimap(this.Workspace);
        objectGroupEditor = new ObjectGroupEditor(this, "Object Group Editor", Room.GetObjectGroup());
        warpEditor = new WarpEditor(this, "Warp Editor", Room.GetWarpGroup());

        SetRoom(0, 0);

        overworldMinimap.SetFloorPlan(Project.GetWorldMap(0, Project.Game == Game.Seasons ? Season.Spring : Season.None));
        dungeonMinimap.SetFloorPlan(Project.GetDungeon(0).GetFloor(0));

        ActiveMinimap = overworldMinimap;


        // Set up events

        roomLayoutEventWrapper.Bind<RoomTilesetChangedEventArgs>("TilesetChangedEvent", OnTilesetIndexChanged);
        overworldMinimap.SelectedEvent += (selectedIndex) =>
        {
            if (suppressEvents != 0)
                return;

            RoomLayout roomLayout = overworldMinimap.FloorPlan.GetRoomLayout(overworldMinimap.SelectedX, overworldMinimap.SelectedY);
            if (Workspace.AutoAdjustGroupNumber && roomLayout.Room.Index != roomLayout.Room.ExpectedIndex)
            {
                roomLayout = Project.GetIndexedDataType<Room>(roomLayout.Room.ExpectedIndex).GetLayout(roomLayout.Season);
            }
            SetRoomLayout(roomLayout, 1);
        };

        dungeonMinimap.SelectedEvent += (selectedIndex) =>
        {
            if (suppressEvents != 0)
                return;

            SetRoomLayout(dungeonMinimap.FloorPlan.GetRoomLayout(dungeonMinimap.SelectedX, dungeonMinimap.SelectedY), 0);
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

        // Watch for the destination warp we're handling being deleted under our nose (can happen
        // with undo/redo)
        warpDestEW.Bind<EventArgs>("ModifiedEvent", (sender, _) =>
        {
            if (!EditingWarpDestination.SourceGroup.ContainsWarp(EditingWarpDestination))
            {
                DisableWarpDestEditMode();
            }
        }, weak: false);

        roomLayoutEditor.HoverChangedEvent += (_, args) => OnCursorChanged(args);
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

    Warp _editingWarpDestination;
    EventWrapper<WarpGroup> warpDestEW = new();

    int suppressEvents = 0;
    bool handlingObjectSelection, handlingWarpSelection;
    string minimapTabToSelect; // Set to change right tab bar programmatically
    CursorPosition cursorPos = new() { room = -1, tileStart = -1, tileEnd = -1 };

    // Maps dungeon index to floor number. Allows the editor to remember what floor we were last
    // on for a given dungeon.
    Dictionary<int, int> floorDict = new Dictionary<int, int>();

    // ================================================================================
    // Events
    // ================================================================================

    // Event that fires when the selected left-side tab has been changed
    public event EventHandler TabChangedEvent;

    public event Action<CursorPosition> CursorPositionChangedEvent;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    public RoomLayout RoomLayout { get { return roomLayoutEditor.RoomLayout; } }
    public Room Room { get { return RoomLayout.Room; } }

    /// <summary>
    /// Last season selected. In Seasons, this should always be a valid season. In Ages, this is
    /// always going to be Season.Spring. (Shouldn't be using it for anything anyway.)
    /// </summary>
    public Season SelectedSeason { get; private set; } = Season.Spring;

    public TilesetViewer TilesetViewer { get { return tilesetViewer; } }

    public Brush<int> Brush { get { return Workspace.Brush; } }

    public bool ObjectTabActive { get { return ActiveLeftTabName == "Objects"; } }
    public bool WarpTabActive { get { return ActiveLeftTabName == "Warps"; } }
    public bool ChestTabActive { get { return ActiveLeftTabName == "Chests"; } }

    public Warp EditingWarpDestination
    {
        get { return _editingWarpDestination; }
        set
        {
            _editingWarpDestination = value;
            warpDestEW.ReplaceEventSource(value?.SourceGroup);
        }
    }

    public FloorPlan ActiveFloorPlan { get { return ActiveMinimap.FloorPlan; } }

    // Private properties

    Minimap ActiveMinimap { get; set; }

    // This is null if not on the dungeon tab
    Dungeon ActiveDungeon { get { return (ActiveFloorPlan as Dungeon.Floor)?.Dungeon; } }

    // Left: Editor mode tabs
    string ActiveLeftTabName { get; set; }

    // Right: Overworld tabs
    bool OverworldTabActive { get { return ActiveMinimap == overworldMinimap; } }
    bool DungeonTabActive { get { return ActiveMinimap == dungeonMinimap; } }

    // Only valid on the dungeon tab
    int SelectedFloor
    {
        get
        {
            return GetSelectedFloor(ActiveDungeon);
        }
        set
        {
            if (OverworldTabActive)
                throw new NotImplementedException();
            floorDict[(ActiveFloorPlan as Dungeon.Floor).Dungeon.Index] = value;
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        float X_OFFSET = ImGuiX.Unit(15.0f);

        float statusBarHeight = ImGui.GetTextLineHeight() + ImGuiX.Unit(8.0f);
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

        ImGui.BeginChild("Left Panel", new Vector2(tilesetViewer.WidgetSize.X + X_OFFSET + ImGuiX.Unit(20.0f), 0.0f));
        if (ImGui.BeginTabBar("##Left Panel Tabs"))
        {
            if (TrackedTabItem("Room"))
            {
                ImGui.SeparatorText("Tileset");
                tilesetViewer.Render();
                if (ImGui.Button("Open Tileset Editor"))
                {
                    Workspace.OpenTilesetEditor(RoomLayout.Tileset);
                }

                ImGui.SeparatorText("Room properties");
                ImGuiLL.RenderValueReferenceGroup(Room.ValueReferenceGroup, null, Workspace.ShowDocumentation);
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
                         ImGuiChildFlags.Borders);

        if (Workspace.IsNetworkActive)
        {
            ImGui.PushFont(Top.InfoFont);
            int count = Workspace.RemoteStates.Values.Where((s) => s.CursorPosition.room == Room.Index).Count();
            if (count == 1)
                ImGui.Text($"1 other is in this room.");
            else
                ImGui.Text($"{count} others are in this room.");
            ImGui.PopFont();
        }

        ImGui.SeparatorText("Room");
        roomLayoutEditor.Render();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("Right Panel", Vector2.Zero, ImGuiChildFlags.Borders);

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
                        ActiveMinimap.GetRoomLayout(ActiveMinimap.SelectedX, ActiveMinimap.SelectedY),
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
                        Season s = Season.None;
                        if (worldIndex == 0 && Project.Game == Game.Seasons)
                        {
                            s = SelectedSeason;
                        }
                        SetMap(Project.GetWorldMap(worldIndex, s));
                    }
                }

                if (ActiveFloorPlan.Season != Season.None)
                {
                    ImGui.SameLine();
                    int season = (int)RoomLayout.Season;
                    if (ImGuiX.InputHex("Season", ref season, 1))
                    {
                        if (Room.IsValidSeason((Season)season))
                            SetRoomLayout(Room.GetLayout((Season)season), 1);
                    }
                }

                ImGui.PopItemWidth();
            }

            overworldMinimap.Render();
            ImGui.EndTabItem();
        }
        if (minimapTabItem("Dungeon", dungeonMinimap))
        {
            Dungeon.Floor dungeonFloor = ActiveFloorPlan as Dungeon.Floor;

            // Input fields for dungeons
            {
                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);

                int dungeonIndex = ActiveDungeon.Index;
                if (ImGuiX.InputHex("Dungeon", ref dungeonIndex, 1))
                {
                    if (dungeonIndex >= 0 && dungeonIndex < Project.NumDungeons)
                    {
                        Dungeon d = Project.GetDungeon(dungeonIndex);
                        int floor = GetSelectedFloor(d);
                        SetMap(d.GetFloor(floor));
                    }
                }

                ImGui.SameLine();
                int newFloor = dungeonFloor.GetFloorIndex();
                if (ImGuiX.InputHex("Floor", ref newFloor, 1))
                {
                    if (newFloor >= 0 && newFloor < ActiveDungeon.NumFloors)
                    {
                        SelectedFloor = newFloor;
                        dungeonMinimap.SetFloorPlan(ActiveDungeon.GetFloor(SelectedFloor));
                        SetRoom(ActiveFloorPlan.GetRoom(ActiveMinimap.SelectedX, ActiveMinimap.SelectedY),
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
        if (Room == room)
            return;

        RoomLayout layout = room.HasSeasons
            ? room.GetLayout(SelectedSeason)
            : room.GetLayout(Season.None);

        SetRoomLayout(layout, updateMinimap);
    }

    /// <summary>
    /// Changes the loaded map, updates the loaded room accordingly
    /// </summary>
    public void SetMap(FloorPlan map)
    {
        suppressEvents++;

        ActiveMinimap.SetFloorPlan(map);
        RoomLayout roomLayout = map.GetRoomLayout(ActiveMinimap.SelectedX, ActiveMinimap.SelectedY);
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

    public void UpdateRoomComponents()
    {
        roomLayoutEditor.UpdateRoomComponents();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Gets the floor that was last selected for the given map
    /// </summary>
    int GetSelectedFloor(Dungeon dungeon)
    {
        if (ActiveDungeon == null)
            throw new Exception("Tried to get selected floor in non-dungeon");
        return floorDict.GetValueOrDefault(dungeon.Index, 0);
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

        if (roomLayout.Season != Season.None)
            SelectedSeason = roomLayout.Season;

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
            Dungeon.Floor dungeonFloor;
            int x, y;
            Dungeon dungeon = Project.GetRoomDungeon(roomLayout.Room, out dungeonFloor, out x, out y);

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
                overworldMinimap.SetFloorPlan(Project.GetWorldMap(roomLayout.Group, roomLayout.Season));
                overworldMinimap.SelectedIndex = roomLayout.Room.Index & 0xff;
            }
            else if (DungeonTabActive)
            {
                if (dungeon != null)
                {
                    dungeonMinimap.SetFloorPlan(dungeonFloor);
                    dungeonMinimap.SelectedIndex = y * dungeonFloor.MapWidth + x;
                    SelectedFloor = dungeonFloor.GetFloorIndex();
                }
            }
        }

        suppressEvents--;

        OnCursorChanged(null);
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
        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));

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
                ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(10.0f, 0.0f));
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
            if (DungeonTabActive && tileset.DungeonIndex != ActiveDungeon.Index)
                return string.Format("Tileset's dungeon index ({0:X}) doesn't match the dungeon ({1:X})",
                                     (int)tileset.DungeonIndex, ActiveDungeon.Index);

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
                int expectedGroup = Project.GetCanonicalLayoutGroup(Room.Group, RoomLayout.Season);
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

    /// <summary>
    /// Invoked when the room or hovering tile is changed, so that remote instances can see what
    /// you're hovering over.
    /// </summary>
    void OnCursorChanged(TileHoverEventArgs args)
    {
        if (args == null)
        {
            cursorPos = cursorPos with { room = Room.Index };
        }
        else if (args.IsSingleTile)
        {
            cursorPos = new CursorPosition()
            {
                room = Room.Index,
                tileStart = args.TileStart,
                tileEnd = args.TileStart,
            };
        }
        else // Rectangle select
        {
            cursorPos = new CursorPosition()
            {
                room = Room.Index,
                tileStart = args.TileStart,
                tileEnd = args.TileEnd,
            };
        }
        CursorPositionChangedEvent?.Invoke(cursorPos);
    }
}
