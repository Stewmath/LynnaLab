using System.Net;

namespace LynnaLab;

public class NetworkDialog : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public NetworkDialog(ProjectWorkspace workspace, string name)
        : base(name)
    {
        this.Workspace = workspace;
        base.WindowFlags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // For server
    string serveAddressTextInput = "0.0.0.0";
    int servePort = 2310;

    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.PushFont(Top.InfoFont);

        Workspace.NetworkConnection.Switch(
            (none) =>
            {
                ImGui.Text("The LynnaLab server is currently not running.");

                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
                ImGui.BeginGroup();
                ImGui.InputText("Listen Address", ref serveAddressTextInput, 20);
                ImGui.InputInt("Listen Port", ref servePort);
                ImGui.EndGroup();
                ImGuiX.TooltipOnHover("In most cases, you shouldn't need to modify these.");
                ImGui.PopItemWidth();

                IPEndPoint endPoint;
                string toParse = $"{serveAddressTextInput}:{servePort}";

                if (!IPEndPoint.TryParse(toParse, out endPoint))
                {
                    ImGui.Text($"Error: Couldn't parse address '{toParse}'.");
                }
                else
                {
                    if (ImGui.Button("Start server"))
                    {
                        Workspace.BeginServer(endPoint);
                    }
                }
            },
            (server) =>
            {
                var connections = server.GetConnections();

                ImGui.Text($"The LynnaLab server is listening on {server.ListenEndPoint}.");

                if (ImGui.Button("Stop server"))
                {
                    server.Stop();
                }

                if (connections.Count == 0)
                    ImGui.Text($"There are no active connections.");
                else if (connections.Count == 1)
                    ImGui.Text($"There is 1 active connection:");
                else
                    ImGui.Text($"There are {connections.Count} active connections:");

                if (connections.Count != 0)
                {
                    if (ImGui.BeginTabBar("Connection Tabs"))
                    {
                        int index = 1;
                        foreach (ConnectionController controller in connections)
                        {
                            if (ImGui.BeginTabItem($"{index}"))
                            {
                                RenderConnection(controller);
                                ImGui.EndTabItem();
                            }
                            index++;
                        }
                        ImGui.EndTabBar();
                    }
                }
            },
            (client) =>
            {
                ImGui.Text($"You are connected to a LynnaLab server.");
                RenderConnection(client);
            }
        );

        ImGui.PopFont();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    public void RenderConnection(ConnectionController controller)
    {
        string whoRejects = controller.Role == NetworkRole.Server ? "us" : "them";

        ImGui.Text($"Source: {controller.RemoteEndPoint}");
        ImGui.Text($"Total outbound traffic: {PrettyNumber(controller.TotalSentBytes)}B");
        ImGui.Text($"Total inbound traffic: {PrettyNumber(controller.TotalReceivedBytes)}B");
        ImGui.Text($"Missing ACKs: {controller.OutstandingAcks}");
        ImGui.Text($"Transactions rejected by {whoRejects}: {controller.RejectedTransactions}");

        if (ImGui.Button("Close this connection"))
        {
            controller.Close();
        }
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    static string PrettyNumber(int n)
    {
        if (n < 1024)
            return n.ToString();
        else if (n < 1024 * 1024)
            return $"{(n / 1024).ToString()}K";
        else if (n < 1024 * 1024 * 1024)
            return $"{(n / 1024 / 1024).ToString()}M";
        else
            return $"{(n / 1024 / 1024 / 1024).ToString()}G";
    }
}
