using System.Diagnostics;
using System.IO;

namespace LynnaLab;

/// <summary>
/// TopLevel class contains the main loop and deals with anything that's not project-specific.
///
/// It could potentially contain multiple ProjectWorkspaces in the future.
///
/// Try not to put things in here unless you're sure that the program only needs one of it, since
/// everything is static. Most things should go in ProjectWorkspace instead.
/// </summary>
public static class TopLevel
{
    public static void Load(IBackend backend, string path = "", string game = "seasons")
    {
        TopLevel.backend = backend;

        Helper.mainThreadInvokeFunction = TopLevel.LazyInvoke;

        GlobalConfig = GlobalConfig.Load();
        if (GlobalConfig == null)
        {
            GlobalConfig = new GlobalConfig();
            GlobalConfig.Save();
            // TODO: Save on exit?
        }

        TopLevel.DefaultFont = ImGui.GetFont();
        TopLevel.OraclesFont = ImGuiX.LoadFont(
            Helper.GetResourceStream("LynnaLab.ZeldaOracles.ttf"), 18);
        TopLevel.OraclesFont24px = ImGuiX.LoadFont(
            Helper.GetResourceStream("LynnaLab.ZeldaOracles.ttf"), 24);

        backend.RecreateFontTexture();

        if (path != "")
            TopLevel.OpenProject(path, game);
    }

    // ================================================================================
    // Constants
    // ================================================================================

    public const string RightClickPopupName = "RightClickPopup";

    // ================================================================================
    // Variables
    // ================================================================================

    static IBackend backend;
    static Dictionary<Bitmap, Image> imageDict = new Dictionary<Bitmap, Image>();
    static Queue<Func<bool>> idleFunctions = new Queue<Func<bool>>();

    // Vars related to modal windows
    static string activeModal;
    static string nextPopup;
    static bool willCloseWorkspace;
    static bool rememberGameChoice;
    static string projectDirectoryToOpen;

    // ================================================================================
    // Properties
    // ================================================================================

    public static IBackend Backend { get { return backend; } }
    public static GlobalConfig GlobalConfig { get; private set; }

    public static ImFontPtr DefaultFont { get; private set; }
    public static ImFontPtr OraclesFont { get; private set; }
    public static ImFontPtr OraclesFont24px { get; private set; }

    // Private properties
    private static ProjectWorkspace Workspace { get; set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Main loop
    /// </summary>
    public static void Run()
    {
        var stopwatch = Stopwatch.StartNew();

        // Main application loop
        while (!backend.Exited)
        {
            float lastDeltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
            stopwatch.Restart();

            backend.HandleEvents(lastDeltaTime);

            if (backend.CloseRequested)
            {
                if (Workspace != null)
                    OpenModal("Close Project");
                else
                    backend.Close();
            }

            if (backend.Exited)
                break;

            if (willCloseWorkspace)
            {
                Workspace.Close();
                Workspace = null;
                willCloseWorkspace = false;
            }

            TopLevel.Render(lastDeltaTime);

            // Call the "idle functions", used for lazy drawing.
            // Do this before backend.Render() as that triggers vsync and messes up our timer.
            while (idleFunctions.Count != 0)
            {
                var func = idleFunctions.Peek();
                if (!func())
                    idleFunctions.Dequeue();

                // Maximum frame time up to which we can run another idle function.
                // At 60fps, 1 frame = 0.016 seconds. So we keep this a bit under that.
                const float MAX_FRAME_TIME = 0.01f;

                float deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
                if (deltaTime >= MAX_FRAME_TIME)
                    break;
            }

            backend.Render();
        }
    }

    /// <summary>
    /// Draw ImGui interface
    /// </summary>
    public static void Render(float deltaTime)
    {
        ImGui.PushFont(OraclesFont);

        if (Workspace == null)
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open"))
                    {
                        OpenModal("Open Project");
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
        }
        else
        {
            Workspace.Render(deltaTime);
        }

        RenderModals();

        ImGui.PopFont();
    }

    /// <summary>
    /// If another modal is active when this is called it won't open until that one gets closed, if
    /// at all
    /// </summary>
    public static void OpenModal(string name)
    {
        nextPopup = name;
    }

    /// <summary>
    /// Render code for modal windows.
    /// This is in the TopLevel class because some modals are needed even when no Workspace is loaded.
    /// </summary>
    public static void RenderModals()
    {
        Func<string, (string, string)> splitPopupString = (string s) =>
        {
            int splitIndex = s.IndexOf('|');
            string first = splitIndex == -1 ? s : s.Substring(0, splitIndex);
            string rest = splitIndex == -1 ? null : s.Substring(splitIndex + 1);
            return (first, rest);
        };

        Action closeCurrentModal = () =>
        {
            ImGui.CloseCurrentPopup();
            activeModal = null;
        };

        if (activeModal == null && nextPopup != null)
        {
            var (first, rest) = splitPopupString(nextPopup);
            ImGui.OpenPopup(first);
            activeModal = first;
            nextPopup = rest;
        }

        var beginModal = (string name) =>
        {
            bool ret = ImGui.BeginPopupModal(name, ImGuiWindowFlags.AlwaysAutoResize);
            return ret;
        };

        // Offer to save the project before closing. Pops up when attempting to close the window, or
        // "Project -> Close" (slightly different things)
        if (ImGui.BeginPopupModal("Close Project", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Save project before closing?");

            // Close either the project or the entire window, depending on the context
            var close = () =>
            {
                if (backend.CloseRequested)
                {
                    backend.Close();
                    backend.CloseRequested = false;
                }
                else if (Workspace.CloseRequested)
                {
                    // Don't close the workspace right away because we may have rendered some of it
                    // already, and this will cause some null pointer exceptions if we free its
                    // resources now.
                    willCloseWorkspace = true;
                    Workspace.CloseRequested = false;
                }
                closeCurrentModal();
            };

            if (ImGui.Button("Save"))
            {
                Workspace.Project.Save();
                close();
            }
            ImGui.SameLine();
            if (ImGui.Button("Don't save"))
            {
                close();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                backend.CloseRequested = false;
                Workspace.CloseRequested = false;
                nextPopup = null; // Don't show subsequent popup windows, if any
                closeCurrentModal();
            }
            ImGui.EndPopup();
        }

        // File chooser to open project
        if (ImGui.BeginPopupModal("Open Project", ImGuiWindowFlags.AlwaysAutoResize))
        {
            var picker = FilePicker.GetFolderPicker("Open", Path.Combine(Environment.CurrentDirectory));
            picker.OnlyAllowFolders = true;
            picker.RootFolder = "/";
            if (picker.Draw())
            {
                projectDirectoryToOpen = picker.SelectedFile;
                ProjectConfig config = ProjectConfig.Load(projectDirectoryToOpen);

                if (config == null)
                {
                    nextPopup = "Invalid Project";
                }
                else if (config.EditingGameIsValid())
                {
                    OpenProject(picker.SelectedFile, config.EditingGame);
                }
                else
                {
                    nextPopup = "Select Game";
                }
                FilePicker.RemoveFilePicker("Open");
                closeCurrentModal();
            }
            ImGui.EndPopup();
        }

        // Notification that the directory selected was not a valid project
        if (ImGui.BeginPopupModal("Invalid Project", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Couldn't open project config file. Select the root of the oracles-disasm folder.");
            if (ImGui.Button("OK"))
                closeCurrentModal();
            ImGui.EndPopup();
        }

        // Deciding which game to edit after selecting a project
        if (ImGui.BeginPopupModal("Select Game", ImGuiWindowFlags.AlwaysAutoResize))
        {
            string gameChoice = null;

            ImGui.Text("Which game to edit?");
            if (ImGui.Button("Ages"))
                gameChoice = "ages";
            ImGui.SameLine();
            if (ImGui.Button("Seasons"))
                gameChoice = "seasons";
            ImGui.Checkbox("Remember my choice", ref rememberGameChoice);

            if (gameChoice != null)
            {
                closeCurrentModal();

                if (rememberGameChoice)
                {
                    ProjectConfig config = ProjectConfig.Load(projectDirectoryToOpen);
                    config.SetEditingGame(gameChoice);
                }
                OpenProject(projectDirectoryToOpen, gameChoice);

                projectDirectoryToOpen = null;
                rememberGameChoice = false;
            }

            ImGui.EndPopup();
        }
    }

    /// <summary>
    /// Turns a Bitmap (cpu) into an Image (gpu), or looks up the existing Image if one exists
    /// in the cache for that Bitmap already.
    /// </summary>
    public static Image ImageFromBitmap(Bitmap bitmap)
    {
        Image image;
        if (imageDict.TryGetValue(bitmap, out image))
            return image;

        image = backend.ImageFromBitmap(bitmap);
        imageDict[bitmap] = image;

        bitmap.DisposedEvent += (sender) =>
        {
            var image = imageDict[sender as Bitmap];
            imageDict.Remove(sender as Bitmap);
            image.Dispose();
        };
        return image;
    }

    public static void UnregisterBitmap(Bitmap bitmap)
    {
        if (!imageDict.Remove(bitmap))
            throw new Exception("Bitmap to remove did not exist in imageDict.");
    }

    /// <summary>
    /// Queues the given function to run when some free time is available.
    /// The function will be repeatedly called until it returns false.
    /// Modeled after GLib.IdleHandler.
    /// </summary>
    public static void LazyInvoke(Func<bool> function)
    {
        idleFunctions.Enqueue(function);
    }

    public static void LazyInvoke(Action function)
    {
        LazyInvoke(() => { function(); return false; });
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    static void OpenProject(string path, string game)
    {
        if (Workspace != null)
        {
            Workspace.Close();
        }

        // Try to load project config
        ProjectConfig config = ProjectConfig.Load(path);

        if (config == null)
            return;

        var project = new Project(path, game, config);
        Workspace = new ProjectWorkspace(project);
    }
}
