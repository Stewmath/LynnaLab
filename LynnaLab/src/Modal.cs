using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LynnaLab;

/// <summary>
/// Deals with displaying modal windows and status messages.
/// </summary>
public static class Modal
{
    // ================================================================================
    // Variables
    // ================================================================================

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    static Queue<ModalStruct> modalQueue = new Queue<ModalStruct>();
    static List<MessageStruct> messageList = new List<MessageStruct>();
    static bool rememberGameChoice;
    static int messageIDCounter = 0;

    // For connect to server dialog
    static int connectToServerStage;

    // ================================================================================
    // Properties
    // ================================================================================

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Open a modal window with the given name, using the given function to render its contents.
    /// It will be opened after any other modal windows have been closed.
    /// </summary>
    public static void OpenModal(string name, Func<bool> renderFunc)
    {
        modalQueue.Enqueue(new ModalStruct { Name = name, RenderFunc = renderFunc });
    }

    /// <summary>
    /// Render code for modal windows.
    /// </summary>
    public static void RenderModalsAndMessages()
    {
        if (modalQueue.Count != 0)
        {
            ModalStruct modal = (ModalStruct)modalQueue.Peek();

            // There seems to be no harm in calling this repeatedly instead of just once
            ImGui.OpenPopup(modal.Name);

            // Put it in the center of the screen
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), 0, new Vector2(0.5f, 0.5f));

            if (ImGui.BeginPopupModal(modal.Name, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            {
                ImGui.PushFont(Top.InfoFont);
                if (modal.RenderFunc())
                {
                    modalQueue.Dequeue();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopFont();
                ImGui.EndPopup();
            }
        }

        // Display messages (these appear in the bottom-right and disappear automatically after a
        // few seconds)

        List<MessageStruct> messagesToRemove = new();

        var vp = ImGui.GetMainViewport();
        Vector2 nextMessageBoxPos = vp.WorkPos + vp.WorkSize * new Vector2(0.99f, 0.99f);

        foreach (MessageStruct message in messageList)
        {
            // Put it near the bottom-right of the screen
            ImGui.SetNextWindowPos(nextMessageBoxPos, 0, new Vector2(1.0f, 1.0f));

            ImGuiWindowFlags flags = 0
                | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoSavedSettings
                ;

            bool enabled = true;

            // Display message boxes (if there are multiple, newer ones are higher up)
            if (ImGui.Begin($"{message.Title}##MessageBox {message.ID}", ref enabled, flags))
            {
                ImGui.PushFont(Top.InfoFont);

                ImGui.Text(message.Message);

                if (message.Exception != null)
                {
                    if (ImGui.Button("Show full error"))
                    {
                        message.DisplayException = !message.DisplayException;
                    }

                    if (message.DisplayException)
                    {
                        DisplayException(message.Exception);
                    }
                }

                if (ImGui.IsWindowHovered() || message.DisplayException)
                    message.Watch.Restart();

                // Display a timer as a bar
                ImGui.Dummy(ImGuiX.Unit(0.0f, 3.0f));
                float timerHeight = ImGuiX.Unit(5.0f);
                Vector2 pos = ImGui.GetCursorScreenPos();
                float timerWidthAvail = ImGui.GetWindowSize().X - ImGui.GetStyle().WindowPadding.X * 2;
                float timerRatio = ((message.DisplaySeconds * 1000 - (float)message.Watch.Elapsed.TotalMilliseconds)
                                    / (message.DisplaySeconds * 1000));
                float timerWidth = timerWidthAvail * timerRatio;

                ImGui.Dummy(new Vector2(0.0f, timerHeight));

                ImGui.GetWindowDrawList().AddRectFilled(
                    pos,
                    pos + new Vector2(timerWidth, timerHeight),
                    Color.FromRgb(102, 153, 255).ToUInt());

                nextMessageBoxPos -= new Vector2(0.0f, ImGui.GetWindowSize().Y);

                ImGui.PopFont();
                ImGui.End();
            }

            if (!enabled || (message.DisplaySeconds != 0 && message.Watch.Elapsed.Seconds >= message.DisplaySeconds))
                messagesToRemove.Add(message);
        }

        foreach (var m in messagesToRemove)
            messageList.Remove(m);
    }

    /// <summary>
    /// Display a message for several seconds.
    /// </summary>
    public static void DisplayInfoMessage(string message)
    {
        messageList.Add(new MessageStruct
        {
            Message = message,
            Title = "Info",
            Exception = null,
            Watch = Stopwatch.StartNew(),
            DisplaySeconds = 5,
            ID = messageIDCounter++,
        });
    }

    /// <summary>
    /// Display an error message for several seconds.
    /// </summary>
    public static void DisplayErrorMessage(string message, Exception exception = null)
    {
        messageList.Add(new MessageStruct
        {
            Message = message,
            Title = "Error",
            Exception = exception,
            Watch = Stopwatch.StartNew(),
            DisplaySeconds = 5,
            ID = messageIDCounter++,
        });
    }

    // ================================================================================
    // Modal windows
    // ================================================================================

    // Offer to save the project before closing. Pops up when attempting to close the window, or
    // "Project -> Close" (slightly different things).
    // If the project is not modified then it is immediately closed.
    public static void CloseProjectModal(ProjectWorkspace workspace, Action callback = null)
    {
        Debug.Assert(workspace != null);

        var close = () =>
        {
            Top.CloseProject();
            if (callback != null)
                callback();
        };

        if (workspace.IsClientRunning || !workspace.Project.Modified)
        {
            Top.DoNextFrame(close);
            return;
        }

        OpenModal("Close Project", () =>
        {
            ImGui.Text("Save project before closing?");

            if (ImGui.Button("Save"))
            {
                workspace.Project.Save();
                Top.DoNextFrame(close);
                return true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Don't save"))
            {
                Top.DoNextFrame(close);
                return true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                return true;
            }
            return false;
        });
    }

    public static void OpenProjectModal()
    {
        string selectedFolder = null;
        bool callbackReceived = false;

        Top.Backend.ShowOpenFolderDialog(null, (folder) =>
        {
            selectedFolder = folder;
            callbackReceived = true;
        });

        OpenModal("Open project", () =>
        {
            ImGui.Text("Waiting for file dialog...");

            if (callbackReceived)
            {
                if (selectedFolder != null)
                {
                    Top.OpenProject(selectedFolder);
                }
                return true;
            }

            return false;
        });
    }

    /// <summary>
    /// Dispays a modal for a single frame. Call this when performing some action that interrupts
    /// ImGui rendering for an extended period of time and you want to set up a modal before
    /// starting the action.
    /// </summary>
    public static void LoadingModal(string message, Action action)
    {
        int frameCounter = 0;

        OpenModal("Loading", () =>
        {
            ImGui.Text(message);

            if (frameCounter == 2)
            {
                action();
                return true;
            }
            frameCounter++;
            return false;
        });
    }

    public static void DisplayMessageModal(string title, string message)
    {
        OpenModal(title, () =>
        {
            ImGui.Text(message);
            return ImGui.Button("OK");
        });
    }

    /// <summary>
    /// Display a message for a certain number of seconds. Can left click anywhere to close.
    /// </summary>
    public static void DisplayTimedMessageModal(string title, string message, float seconds = 3.0f)
    {
        Stopwatch watch = Stopwatch.StartNew();
        OpenModal(title, () =>
        {
            ImGui.Text(message);

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                return true;

            return watch.Elapsed.Seconds >= seconds;
        });
    }

    /// <summary>
    /// Display a modal window for an exception handler.
    ///
    /// This is a bit fragile, since it depends on all of my rendering code working correctly.
    /// Currently it's unused in favor of SDL dialog boxes.
    /// </summary>
    public static void DisplayExceptionModal(Exception e, Action saveProject)
    {
        modalQueue.Clear();
        OpenModal("Exception", () =>
        {
            ImGui.Text("An unhandled exception occurred!\n\nYou can attempt to resume LynnaLab, but the program may be in an invalid state.\n\nException details:");

            DisplayException(e);

            bool retval = false;

            if (ImGui.Button("Save project and quit"))
            {
                Program.handlingException = null;
                saveProject();
                Environment.Exit(1);
                retval = true;
            }
            ImGui.SameLine();

            if (ImGui.Button("Quit without saving"))
            {
                Environment.Exit(1);
                retval = true;
            }
            ImGui.SameLine();

            if (ImGui.Button("Attempt to resume (not recommended)"))
            {
                Program.handlingException = null;
                retval = true;
            }

            return retval;
        });
    }

    static void DisplayException(Exception e)
    {
        Vector2 exceptionViewSize = ImGui.GetMainViewport().Size * 0.8f;

        if (ImGui.BeginChild("Exception details", exceptionViewSize, ImGuiChildFlags.FrameStyle))
        {
            ImGui.Text(e.ToString());
        }
        ImGui.EndChild();
    }

    public static void SelectGameModal(string path, Action<string, bool> onSelected)
    {
        // Deciding which game to edit after selecting a project
        OpenModal("Select Game", () =>
        {
            string gameChoice = null;

            ImGui.Text("Opening project at: " + path + "\n\n");
            ImGui.Text("Which game to edit?");
            if (ImGui.Button("Ages"))
                gameChoice = "ages";
            ImGui.SameLine();
            if (ImGui.Button("Seasons"))
                gameChoice = "seasons";
            ImGui.Checkbox("Remember my choice", ref rememberGameChoice);

            if (gameChoice != null)
            {
                onSelected(gameChoice, rememberGameChoice);

                rememberGameChoice = false;
                return true;
            }

            return false;
        });
    }

    public static void ConnectToServerModal()
    {
        string serverAddressTextInput = "127.0.0.1";
        int serverPort = 2310;
        Task connectToServerTask = null;
        IPEndPoint endPoint = null;
        CancellationTokenSource cancelSource = null;
        bool viewFullException = false;

        OpenModal("Connect to server", () =>
        {
            if (connectToServerTask != null)
            {
                if (connectToServerTask.IsFaulted)
                {
                    if (cancelSource != null)
                    {
                        cancelSource.Dispose();
                        cancelSource = null;
                    }

                    ImGui.Text("An error occured while connecting to the server:");

                    foreach (Exception e in connectToServerTask.Exception.InnerExceptions)
                    {
                        // Errors establishing the connection
                        if (e is SocketException se)
                        {
                            ImGui.Text(se.Message + ".");
                        }
                        // Server user probably rejected the connection
                        else if (e is EndOfStreamException)
                        {
                            ImGui.Text("Server disconnected.");
                        }
                        else
                            ImGui.Text(e.GetType().ToString());
                    }
                    if (viewFullException)
                        DisplayException(connectToServerTask.Exception);

                    if (ImGui.Button("OK"))
                    {
                        connectToServerTask = null;
                        viewFullException = false;
                    }
                    if (!viewFullException)
                    {
                        ImGui.SameLine();
                        if (ImGui.Button("View full exception details"))
                        {
                            viewFullException = true;
                        }
                    }
                }
                else if (connectToServerTask.IsCanceled)
                {
                    connectToServerTask = null;
                }
                else if (connectToServerTask.IsCompletedSuccessfully)
                {
                    DisplayInfoMessage("Connection to server established!");
                    cancelSource?.Dispose();
                    return true;
                }
                else
                {
                    if (connectToServerStage == 0)
                        ImGui.Text($"Connecting to server {endPoint}...");
                    else if (connectToServerStage == 1)
                        ImGui.Text($"Connected to server {endPoint}, waiting for them to accept...");
                    else if (connectToServerStage == 2)
                        ImGui.Text($"Connection accepted, synchronizing project...");
                    else if (connectToServerStage == 3)
                        ImGui.Text($"Loading project...");
                    if (ImGui.Button("Abort"))
                    {
                        if (cancelSource != null)
                        {
                            cancelSource.Cancel();
                            cancelSource.Dispose();
                            cancelSource = null;
                        }
                    }
                }
            }
            else
            {
                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
                ImGui.InputText("Server Address", ref serverAddressTextInput, 20);
                ImGui.InputInt("Server Port", ref serverPort);
                ImGui.PopItemWidth();

                string toParse = $"{serverAddressTextInput}:{serverPort}";

                if (!IPEndPoint.TryParse(toParse, out endPoint))
                {
                    ImGui.Text($"Error: Couldn't parse address '{toParse}'.");
                }

                if (ImGui.Button("Connect") && endPoint != null)
                {
                    cancelSource = new();
                    connectToServerTask = ConnectToServer(endPoint, cancelSource.Token);
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    return true;
                }
            }

            return false;
        });
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    // May throw SocketException on failure to connect, maybe throws a NetworkException if the
    // server sends some packets we're not expecting.
    public static async Task ConnectToServer(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        Project project = null;
        ConnectionController clientConn = null;

        connectToServerStage = 0;

        (project, clientConn) = await ConnectionController.CreateForClientAsync(endPoint, Top.DoNextFrameAsync, cancellationToken);

        // Make no assumptions about what thread we're on after above "await".

        connectToServerStage = 1;

        Task clientRunTask = clientConn.RunUntilClosed();

        try
        {
            await clientConn.WaitUntilAcceptedAsync(cancellationToken);

            connectToServerStage = 2;

            await clientConn.WaitUntilSynchronizedAsync();

            connectToServerStage = 3;
        }
        catch (Exception e)
        {
            log.Debug("ConnectToServer: Cancelled/error midway; closing connection.");
            log.Debug(e);
            clientConn.Close();
            throw;
        }

        project.FinalizeLoad();
        await Top.DoNextFrameAsync(() => { Top.SetWorkspace(new ProjectWorkspace(project, "ClientProject", clientConn)); });
    }


    // ================================================================================
    // Nested structs
    // ================================================================================

    class ModalStruct
    {
        public string Name { get; init; }
        public Func<bool> RenderFunc { get; init; }
    }

    class MessageStruct
    {
        public string Message { get; init; }
        public string Title { get; init; }
        public Exception Exception { get; init; }
        public Stopwatch Watch { get; init; }
        public int DisplaySeconds { get; init; }
        public int ID { get; init; }

        public bool DisplayException { get; set; } = false;
    }
}
