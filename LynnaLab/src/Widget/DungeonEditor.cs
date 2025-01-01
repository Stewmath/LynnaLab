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

        minimap = new Minimap(Workspace);
        SetDungeon(Project.GetDungeon(0));

        dungeonEW.Bind<EventArgs>("FloorsChangedEvent", (_, _) =>
        {
            ReloadMap();
        }, weak: false);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Minimap minimap;
    int floor;
    EventWrapper<Dungeon> dungeonEW = new();

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public Dungeon Dungeon { get; private set; }

    // Private properties

    Room SelectedRoom
    {
        get
        {
            return Dungeon.GetRoom(minimap.SelectedX, minimap.SelectedY, floor);
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
            if (ImGuiX.InputHex("Floor", ref floor, min: 0, max: Dungeon.NumFloors - 1))
            {
                minimap.SetMap(Dungeon, floor);
            }
        }

        // Left panel
        {
            ImGui.BeginChild(
                "Left panel",
                new Vector2(370.0f, 0.0f),
                ImGuiChildFlags.Border);

            ImGui.SeparatorText("Dungeon properties");
            ImGuiLL.RenderValueReferenceGroup(Dungeon.ValueReferenceGroup, null, Workspace.ShowDocumentation);

            if (ImGui.Button("Add floor above"))
            {
                floor = floor + 1;
                Dungeon.InsertFloor(floor);
                // Dungeon floors changed event will trigger
            }
            ImGui.SameLine();
            if (ImGui.Button("Add floor below"))
            {
                Dungeon.InsertFloor(floor);
                // Dungeon floors changed event will trigger
            }
            if (ImGui.Button("Delete floor") && Dungeon.NumFloors > 1)
            {
                Dungeon.RemoveFloor(floor);
                // Dungeon floors changed event will trigger
            }

            ImGui.SeparatorText("Room properties");

            ImGuiX.InputHex("Room", SelectedRoom.Index & 0xff, (newIndex) =>
            {
                Dungeon.SetRoom(minimap.SelectedX, minimap.SelectedY, floor, newIndex);
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
        Dungeon = dungeon;
        dungeonEW.ReplaceEventSource(dungeon);
        ReloadMap();
    }

    void ReloadMap()
    {
        floor = Math.Min(floor, Dungeon.NumFloors - 1);
        minimap.SetMap(Dungeon, floor);
    }
}
