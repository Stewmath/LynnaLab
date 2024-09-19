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
        this.Dungeon = Project.GetDungeon(2);

        minimap = new Minimap(Workspace);
        minimap.SetMap(Dungeon, 0);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Minimap minimap;
    int floor;

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
            if (ImGuiX.InputHex("Floor", ref floor, min: 0, max: Dungeon.NumFloors - 1))
            {
                minimap.SetMap(Dungeon, floor);
            }
        }

        // Left panel
        {
            ImGui.BeginChild(
                "Left panel",
                new Vector2(300.0f, 0.0f),
                ImGuiChildFlags.Border);

            ImGuiX.InputHex("Room", SelectedRoom.Index & 0xff, (newIndex) =>
            {
                Dungeon.SetRoom(minimap.SelectedX, minimap.SelectedY, floor, newIndex);
            }, digits: 2, min: 0, max: 255);

            ImGui.SeparatorText("Room properties");

            if (ImGui.BeginTable("Room property table", 2))
            {
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
}
