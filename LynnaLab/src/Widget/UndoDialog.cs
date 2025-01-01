namespace LynnaLab;

public class UndoDialog : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public UndoDialog(ProjectWorkspace workspace, string name)
        : base(name)
    {
        this.Workspace = workspace;
        base.DefaultSize = new Vector2(350, 400);
    }

    // ================================================================================
    // Variables
    // ================================================================================


    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public UndoState UndoState { get { return Project.UndoState; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.PushFont(TopLevel.DefaultFont);

        if (UndoState.constructingTransaction.Empty)
        {
            ImGui.Text("Pending transaction: None");
            ImGui.Separator();
        }
        else
        {
            ImGui.Text("Pending transaction:");
            DrawTransaction(UndoState.constructingTransaction, -1);
        }

        ImGui.Text("Undo stack:");

        int index = 0;
        foreach (var transaction in UndoState.Transactions)
        {
            DrawTransaction(transaction, index++);
        }

        ImGui.PopFont();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void DrawTransaction(Transaction t, int index)
    {
        string keyString = "###Transaction" + index;
        if (ImGui.CollapsingHeader(t.description + keyString + "A"))
        {
            foreach (object obj in t.deltas.Keys)
            {
                string text = obj.GetType().ToString();

                if (obj is FileComponent com)
                    text += ": " + com.GetString();
                else if (obj is FileParser p)
                    text += ": " + p.Filename;
                else if (obj is MemoryFileStream stream)
                    text += ": " + stream.RelativeFilePath;

                ImGui.Text("- " + text);
            }
        }
    }
}
