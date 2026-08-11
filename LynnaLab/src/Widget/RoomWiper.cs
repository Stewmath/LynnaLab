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
    }

    // ================================================================================
    // Variables
    // ================================================================================

    bool wipeLayout;
    bool wipeObjects;

    Minimap minimap;
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
        ImGui.Checkbox("Layout", ref wipeLayout);
        ImGui.Checkbox("Objects", ref wipeObjects);

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
            minimap.Render();
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
        });

        Workspace.Project.EndTransaction();
    }
}
