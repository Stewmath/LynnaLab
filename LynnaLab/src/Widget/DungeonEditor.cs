namespace LynnaLab;

public class DungeonEditor : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public DungeonEditor(ProjectWorkspace workspace, string name)
        : base(name)
    {
        this.Workspace = workspace;
        base.DefaultSize = new Vector2(800, 500);

        minimap = new Minimap(Workspace);
        SetDungeon(Project.GetDungeon(0));

        dungeonEW.Bind<DungeonChangedEventArgs>("DungeonChangedEvent", (_, args) =>
        {
            if (args.FloorsChanged && suppressFloorChangedEvent == 0)
            {
                ReloadMap();
            }
        }, weak: false);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Minimap minimap;
    int floorIndex;
    int suppressFloorChangedEvent = 0;
    EventWrapper<Dungeon> dungeonEW = new();

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    Dungeon Dungeon { get { return FloorPlan.Dungeon; } }
    Dungeon.Floor FloorPlan { get; set; }

    // Private properties

    Room SelectedRoom
    {
        get
        {
            return FloorPlan.GetRoom(minimap.SelectedX, minimap.SelectedY);
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);

        // Top bar inputs
        {
            int dungeonIndex = Dungeon.Index;
            if (ImGuiX.InputHex("Dungeon", ref dungeonIndex, 1))
            {
                if (dungeonIndex >= 0 && dungeonIndex < Project.NumDungeons)
                {
                    SetDungeon(Project.GetDungeon(dungeonIndex));
                }
            }

            ImGui.SameLine();
            if (ImGuiX.InputHex("Floor", ref floorIndex, min: 0, max: Dungeon.NumFloors - 1))
            {
                FloorPlan = Dungeon.GetFloor(floorIndex);
                ReloadMap();
            }
        }

        // Left panel
        {
            ImGui.BeginChild(
                "Left panel",
                ImGuiX.Unit(370.0f, 0.0f),
                ImGuiChildFlags.Borders);

            ImGui.SeparatorText("Dungeon properties");
            ImGuiLL.RenderValueReferenceGroup(Dungeon.ValueReferenceGroup, null, Workspace.ShowDocumentation);

            if (ImGui.Button("Add floor above"))
            {
                suppressFloorChangedEvent++;
                floorIndex = floorIndex + 1;
                Dungeon.InsertFloor(floorIndex);
                FloorPlan = Dungeon.GetFloor(floorIndex);
                ReloadMap();
                suppressFloorChangedEvent--;
            }
            ImGui.SameLine();
            if (ImGui.Button("Add floor below"))
            {
                suppressFloorChangedEvent++;
                Dungeon.InsertFloor(floorIndex);
                FloorPlan = Dungeon.GetFloor(floorIndex);
                ReloadMap();
                suppressFloorChangedEvent--;
            }
            if (ImGui.Button("Delete floor") && Dungeon.NumFloors > 1)
            {
                Dungeon.RemoveFloor(floorIndex);
                // Dungeon floors changed event will trigger
            }

            ImGui.SeparatorText("Room properties");

            ImGuiX.InputHex("Room", SelectedRoom.Index & 0xff, (newIndex) =>
            {
                FloorPlan.SetRoom(minimap.SelectedX, minimap.SelectedY, newIndex);
            }, digits: 2, min: 0, max: 255);

            if (ImGui.BeginTable("Room property table", 2))
            {
                Project.BeginTransaction($"Edit dungeon room property#{Dungeon.TransactionIdentifier}r{SelectedRoom.Index:X3}");

                ImGui.TableNextColumn();
                ImGuiX.Checkbox("Up", new Accessor<bool>(() => SelectedRoom.DungeonFlagUp));
                ImGuiX.Checkbox("Right", new Accessor<bool>(() => SelectedRoom.DungeonFlagRight));
                ImGuiX.Checkbox("Down", new Accessor<bool>(() => SelectedRoom.DungeonFlagDown));
                ImGuiX.Checkbox("Left", new Accessor<bool>(() => SelectedRoom.DungeonFlagLeft));
                ImGui.TableNextColumn();
                ImGuiX.Checkbox("Key", new Accessor<bool>(() => SelectedRoom.DungeonFlagKey));
                ImGuiX.Checkbox("Chest", new Accessor<bool>(() => SelectedRoom.DungeonFlagChest));
                ImGuiX.Checkbox("Boss", new Accessor<bool>(() => SelectedRoom.DungeonFlagBoss));
                ImGuiX.Checkbox("Dark", new Accessor<bool>(() => SelectedRoom.DungeonFlagDark));
                ImGui.EndTable();

                Project.EndTransaction();
            }

            ImGui.EndChild();
        }

        // Minimap panel
        {
            ImGui.SameLine();
            ImGui.BeginChild("Minimap");
            ImGui.SeparatorText("Floor map");
            minimap.Render();
            ImGui.EndChild();
        }

        ImGui.PopItemWidth();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void SetDungeon(Dungeon dungeon)
    {
        FloorPlan = dungeon.GetFloor(0);
        dungeonEW.ReplaceEventSource(dungeon);
        ReloadMap();
    }

    /// <summary>
    /// This is called when the dungeon or floor index may have been modified. Responsible for
    /// updating the minimap display and keeping the floor numbering coherent.
    /// </summary>
    void ReloadMap()
    {
        if (FloorPlan.WasDeleted())
        {
            if (floorIndex >= Dungeon.NumFloors)
                floorIndex = Dungeon.NumFloors - 1;
            FloorPlan = Dungeon.GetFloor(floorIndex);
        }
        else
            this.floorIndex = FloorPlan.GetFloorIndex();
        minimap.SetFloorPlan(FloorPlan);
    }
}
