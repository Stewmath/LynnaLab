using System.Diagnostics;

namespace LynnaLab;

public static class Modal
{
    // ================================================================================
    // Variables
    // ================================================================================

    static Queue<ModalStruct> modalQueue = new Queue<ModalStruct>();
    static bool rememberGameChoice;

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
        modalQueue.Enqueue(new ModalStruct { name = name, renderFunc = renderFunc });
    }

    /// <summary>
    /// Render code for modal windows.
    /// </summary>
    public static void RenderModals()
    {
        if (modalQueue.Count != 0)
        {
            ModalStruct modal = (ModalStruct)modalQueue.Peek();

            // There seems to be no harm in calling this repeatedly instead of just once
            ImGui.OpenPopup(modal.name);

            // Put it in the center of the screen
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), 0, new Vector2(0.5f, 0.5f));

            if (ImGui.BeginPopupModal(modal.name, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            {
                if (modal.renderFunc())
                {
                    modalQueue.Dequeue();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }
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
            TopLevel.CloseProject();
            if (callback != null)
                callback();
        };

        if (!workspace.Project.Modified)
        {
            TopLevel.DoNextFrame(close);
            return;
        }

        OpenModal("Close Project", () =>
        {
            ImGui.Text("Save project before closing?");

            if (ImGui.Button("Save"))
            {
                workspace.Project.Save();
                close();
                return true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Don't save"))
            {
                close();
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

        TopLevel.Backend.ShowOpenFolderDialog(null, (folder) =>
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
                    TopLevel.OpenProject(selectedFolder);
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

    public static void DisplayExceptionModal(Exception e, Action saveProject)
    {
        modalQueue.Clear();
        OpenModal("Exception", () =>
        {
            ImGui.PushFont(TopLevel.DefaultFont);

            ImGui.Text("An unhandled exception occurred!\n\nYou can attempt to resume LynnaLab, but the program may be in an invalid state.\n\nException details:");

            Vector2 exceptionViewSize = ImGui.GetMainViewport().Size * 0.8f;

            if (ImGui.BeginChild("Exception details", exceptionViewSize, ImGuiChildFlags.FrameStyle))
            {
                ImGui.Text(e.ToString());
            }
            ImGui.EndChild();

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

            ImGui.PopFont();
            return retval;
        });
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

    // ================================================================================
    // Nested structs
    // ================================================================================

    struct ModalStruct
    {
        public string name;
        public Func<bool> renderFunc;
    }

}
