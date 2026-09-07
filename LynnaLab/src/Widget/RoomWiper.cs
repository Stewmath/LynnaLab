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
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Minimap minimap;
    MapChooser mapChooser;

    bool wipeLayout;
    bool wipeObjects;
    bool wipeWarps;

    PRect? selectedRect;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.BeginChild("Checkboxes", ImGuiX.Unit(200.0f, 0.0f));

        ImGuiLL.HelpButton("RoomWiper.md");

        ImGui.Checkbox("Layout", ref wipeLayout);
        ImGui.Checkbox("Objects", ref wipeObjects);
        ImGui.Checkbox("Warps", ref wipeWarps);

        if (ImGui.Button("Clear Data"))
        {
            if (selectedRect is null)
                Modal.DisplayMessageModal("Must select a region!");
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

            if (wipeLayout)
                layout.Wipe();

            if (wipeObjects)
                layout.Room.GetObjectGroup().Wipe();

            if (wipeWarps)
                layout.Room.GetWarpGroup().Wipe();
        });

        Workspace.Project.EndTransaction();
    }

    void OnChangedFloorPlan(object? obj, EventArgs args)
    {
        selectedRect = null;
        minimap.SelectedIndex = -1;
    }
}
