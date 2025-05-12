namespace LynnaLab;

public class DocumentationDialog : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public DocumentationDialog(ProjectWorkspace workspace, string name)
        : base(name)
    {
        this.Workspace = workspace;
        base.DisplayName = "Documentation";
        base.DefaultSize = new Vector2(700, 700);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    // The documentation currently being displayed
    public Documentation Documentation { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        if (Documentation == null)
        {
            ImGui.TextWrapped("No documentation found.");
            return;
        }

        ImGui.PushFont(Top.OraclesFont);

        if (Documentation.Description != null && Documentation.Description != "")
        {
            ImGui.TextWrapped(Documentation.Description);
            ImGuiX.ShiftCursorScreenPos(0.0f, 10.0f);
        }

        if (ImGui.BeginTable("Field table", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn(Documentation.KeyName);
            ImGui.TableSetupColumn("Description");
            ImGui.TableHeadersRow();

            foreach (string key in Documentation.Keys)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(key);
                ImGui.TableSetColumnIndex(1);
                ImGui.TextWrapped(Documentation.GetField(key));
            }

            ImGui.EndTable();
        }

        ImGui.PopFont();
    }

    public void SetDocumentation(Documentation doc)
    {
        Documentation = doc;
        if (Documentation != null)
            DisplayName = "Documentation: " + Documentation.Name;
        else
            DisplayName = "Documentation";
    }
}
