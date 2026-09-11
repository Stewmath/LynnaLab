#nullable enable
namespace LynnaLab;

public class RoomWiper : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public RoomWiper(ProjectWorkspace workspace) : base("Room Wiper")
    {
        this.Workspace = workspace;
        this.minimap = new(workspace, doPostRendering: false);

        minimap.Unselectable = true;
        minimap.SelectedIndex = -1;

        minimap.AddMouseAction(
            MouseButton.Any, MouseModifier.Any, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
            (sender, args) => { selectedRect = args.SelectedRect; }
        );

        minimap.ChangedFloorPlanEvent += OnChangedFloorPlan;

        mapChooser = new(Workspace.Project, minimap);

        wiperList.AddRange([
            new()
            {
                label="Layout",
                wipe=(layout) => layout.Wipe(),
            },
            new()
            {
                label="Objects",
                wipe=(layout) => layout.Room.GetObjectGroup().Wipe(),
            },
            new()
            {
                label="Warps",
                wipe=(layout) => layout.Room.GetWarpGroup().Wipe(),
            },
            new()
            {
                label="Chests",
                wipe=(layout) => layout.Room.Chest?.Delete(),

            },
            new()
            {
                label="Dungeon Properties",
                wipe=(layout) =>
                {
                    if (layout.Room.HasDungeonFlags)
                        layout.Room.DungeonFlags = 0;
                },
                show = () => InDungeon,
            },
        ]);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    class DataWiper
    {
        public required string label;
        public required Action<RoomLayout> wipe;
        public Func<bool> show = () => true;
        public bool enable;
    }

    Minimap minimap;
    MapChooser mapChooser;

    List<DataWiper> wiperList = new();

    PRect? selectedRect;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; }

    Project Project { get { return Workspace.Project; } }
    bool InDungeon { get { return minimap.FloorPlan is Dungeon.Floor; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.BeginChild("Checkboxes", ImGuiX.Unit(220.0f, 0.0f));

        ImGuiLL.HelpButton(Project, "RoomWiper.md");

        foreach (var wiper in wiperList)
        {
            if (wiper.show())
                ImGui.Checkbox(wiper.label, ref wiper.enable);
            else
                wiper.enable = false;
        }

        if (ImGui.Button("Clear Data"))
        {
            if (selectedRect is null)
                Modal.DisplayMessageModal("Must select a region!");
            else if (!wiperList.Any((w) => w.enable))
                Modal.DisplayMessageModal("Must choose something to wipe!");
            else
            {
                Modal.YesNoModal("MapClearConfirm", "Really clear data in this region???",
                    (clickedYes) =>
                    {
                        if (clickedYes)
                        {
                            WipeRect();
                        }
                    });
            }
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("Minimap");
        if (minimap.TileWidth == 0)
            ImGui.Text("No map selected?");
        else
        {
            mapChooser.Render();
        }
        ImGui.EndChild();
    }

    public void SetFloorPlan(FloorPlan plan)
    {
        minimap.SetFloorPlan(plan);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Wipe the selected region.
    /// </summary>
    void WipeRect()
    {
        if (selectedRect is null)
            return;

        Workspace.Project.BeginTransaction("Wipe Room Data");

        selectedRect.ForEachPoint((p) =>
        {
            RoomLayout layout = minimap.GetRoomLayout(p.X, p.Y);

            foreach (var wiper in wiperList)
            {
                if (wiper.enable && wiper.show())
                {
                    wiper.wipe(layout);
                }
            }
        });

        Workspace.Project.EndTransaction();
    }

    void OnChangedFloorPlan(object? obj, EventArgs args)
    {
        selectedRect = null;
        minimap.SelectedIndex = -1;
    }
}
