namespace LynnaLab;

public class TransactionDialog : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TransactionDialog(ProjectWorkspace workspace, string name)
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
    public TransactionManager TransactionManager { get { return Project.TransactionManager; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.PushFont(Top.InfoFont);

        if (TransactionManager.constructingTransaction.Empty)
        {
            ImGui.Text("Pending transaction: None");
            ImGui.Separator();
        }
        else
        {
            ImGui.Text("Pending transaction: " + TransactionManager.constructingTransaction.Description);
        }

        ImGui.Text("Transaction History:");

        int index = 0;
        foreach (var transactionNode in TransactionManager.TransactionHistory.Reverse())
        {
            DrawTransaction(transactionNode, index++);
        }

        ImGui.PopFont();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void DrawTransaction(TransactionNode node, int index)
    {
        string keyString = "###Transaction" + index;
        if (ImGui.CollapsingHeader((node.Apply ? "Apply" : "Unapply") + $": {node.Description}{keyString}"))
        {
            ImGui.Text("CreatorID: " + node.Transaction.CreatorID);
            ImGui.Text("TransactionID: " + node.Transaction.TransactionID);
        }
    }
}
