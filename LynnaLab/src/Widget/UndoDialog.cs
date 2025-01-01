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

        ImGui.Text("Committed transactions:");

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
            var pos = ImGui.GetCursorScreenPos();
            ImGuiX.ShiftCursorScreenPos(10.0f, 0.0f);
            if (ImGui.BeginChild(keyString + "Child"))
            {
                ImGui.Text("Deltas: " + t.deltas.Count);

                if (ImGui.CollapsingHeader("FileComponents"))
                {
                    foreach (object obj in t.deltas.Keys)
                    {
                        if (obj is FileComponent com)
                            ImGui.Text(com.GetString());
                    }
                }
                if (ImGui.CollapsingHeader("FileParsers"))
                {
                    foreach (object obj in t.deltas.Keys)
                    {
                        if (obj is FileParser parser)
                            ImGui.Text(parser.Filename);
                    }
                }
                if (ImGui.CollapsingHeader("Binary files"))
                {
                    foreach (object obj in t.deltas.Keys)
                    {
                        if (obj is MemoryFileStream stream)
                            ImGui.Text(stream.Name);
                    }
                }
                if (t.deltas.Keys.Contains(Project))
                    ImGui.Text("+Project");

                pos.Y = ImGui.GetCursorScreenPos().Y;
            }
            ImGui.EndChild();
            ImGui.SetCursorScreenPos(pos);
        }
    }
}
