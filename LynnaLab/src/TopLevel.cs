using System.Diagnostics;

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
                    ImGui.OpenPopup("Save Project");
                else
                    backend.Close();
            }
            else if (Workspace?.CloseRequested ?? false)
            {
                ImGui.OpenPopup("Save Project");
            }

            if (backend.Exited)
                break;

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
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
        }
        else
        {
            // Modal that pops up when attempting to close the window, or "Project -> Close"
            // (slightly different things)
            if (ImGui.BeginPopupModal("Save Project", ImGuiWindowFlags.AlwaysAutoResize))
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
                        Workspace.Close();
                        Workspace = null;
                    }
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
                    ImGui.CloseCurrentPopup();
                }
            }

            // Workspace may be null here
            Workspace?.Render(deltaTime);
        }

        ImGui.PopFont();
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

        var project = new Project(path, game, config);
        Workspace = new ProjectWorkspace(project);
    }
}
