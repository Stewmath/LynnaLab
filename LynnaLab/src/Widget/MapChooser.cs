namespace LynnaLab;

/// <summary>
/// Renders a tab bar to select between a "world" or "dungeon" minimap.
/// </summary>
public class MapChooser
{
    public MapChooser(Project project, Minimap minimap)
    {
        this.Project = project;
        this.minimap = minimap;

        dungeonTab = false;
        worldIndex = 0;
        season = project.DefaultGroupSeason(worldIndex);
        dungeonIndex = 0;
        dungeonFloor = 0;

        minimap.SetFloorPlan(project.GetWorldMap(worldIndex, season));
    }

    // ================================================================================
    // Variables
    // ================================================================================

    bool dungeonTab;

    int worldIndex;
    Season season;

    int dungeonIndex;
    int dungeonFloor;

    Minimap minimap;

    // ================================================================================
    // Properties
    // ================================================================================

    public Project Project { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render()
    {
        var minimapTabItem = (string name, bool selected) =>
        {
            ImGuiTabItemFlags flags = selected ? ImGuiTabItemFlags.SetSelected : 0;
            return ImGuiX.BeginTabItem(name, flags);
        };

        if (ImGui.BeginTabBar("Map Tabs"))
        {
            if (minimapTabItem("World", false))
            {
                if (dungeonTab) // Just selected the tab
                {
                    dungeonTab = false;
                    minimap.SetFloorPlan(
                        Project.GetWorldMap(worldIndex, season));
                }
                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
                ImGuiX.InputHex("World", worldIndex, (v) => OnWorldChanged(v, season), max: Project.NumGroups-1);
                if (Project.GroupHasSeason(worldIndex))
                {
                    ImGui.SameLine();

                    int newSeason = (int)season;
                    ImGuiLL.ComboBoxFromConstants($"##Season", Project.SeasonMapping, ref newSeason,
                        withIntInput: false, omitPrefix: true);

                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGuiX.Unit(4.0f, 0.0f));
                    ImGui.SameLine();
                    if (ImGui.Button("-"))
                        newSeason = (int)season - 1;
                    ImGui.SameLine();
                    if (ImGui.Button("+"))
                        newSeason = (int)season + 1;
                    ImGui.PopStyleVar();

                    if (newSeason != (int)season && newSeason >= 0 && newSeason <= 3)
                        OnWorldChanged(worldIndex, (Season)newSeason);
                }
                ImGui.PopItemWidth();
                minimap.Render();
                ImGui.EndTabItem();
            }
            if (minimapTabItem("Dungeon", false))
            {
                if (!dungeonTab) // Just selected the tab
                {
                    dungeonTab = true;
                    minimap.SetFloorPlan(
                        Project.GetDungeon(dungeonIndex).GetFloor(dungeonFloor));
                }
                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
                ImGuiX.InputHex("Dungeon", dungeonIndex, (v) => OnDungeonChanged(v, dungeonFloor), max: Project.NumDungeons-1);
                ImGui.SameLine();
                ImGuiX.InputHex("Floor", dungeonFloor, (v) => OnDungeonChanged(dungeonIndex, v));
                ImGui.PopItemWidth();
                minimap.Render();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void OnWorldChanged(int g, Season s)
    {
        worldIndex = g;
        season = Project.ValidateGroupSeason(worldIndex, s, autoCorrect: true);
        minimap.SetFloorPlan(Project.GetWorldMap(worldIndex, season));
    }

    void OnDungeonChanged(int d, int f)
    {
        dungeonIndex = d;
        Dungeon dungeon = Project.GetDungeon(d);
        dungeonFloor = Math.Min(dungeon.NumFloors - 1, f);
        minimap.SetFloorPlan(dungeon.GetFloor(dungeonFloor));
    }
}
