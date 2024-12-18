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
            return;

        ImGui.PushFont(TopLevel.OraclesFont);

        if (ImGui.BeginTable("Field table", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("Key");
            ImGui.TableSetupColumn("Value");
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
    }
}
