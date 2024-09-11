using System.Diagnostics;

namespace LynnaLab;

/// <summary>
/// TopLevel class contains the main loop and deals with anything that's not project-specific.
/// It could potentially contain multiple ProjectWorkspaces in the future.
/// </summary>
public class TopLevel
{
    public TopLevel(IBackend backend, string path = "", string game = "seasons")
    {
        this.backend = backend;

        Helper.mainThreadInvokeFunction = this.LazyInvoke;

        GlobalConfig = GlobalConfig.Load();
        if (GlobalConfig == null)
        {
            GlobalConfig = new GlobalConfig();
            GlobalConfig.Save();
            // TODO: Save on exit?
        }

        this.DefaultFont = ImGui.GetFont();
        this.OraclesFont = ImGuiX.LoadFont(
            Helper.GetResourceStream("LynnaLab.ZeldaOracles.ttf"), 20);

        backend.RecreateFontTexture();

        if (path != "")
            OpenProject(path, game);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    IBackend backend;
    Dictionary<Bitmap, Image> imageDict = new Dictionary<Bitmap, Image>();
    Queue<Func<bool>> idleFunctions = new Queue<Func<bool>>();

    bool showImGuiDemoWindow = false;

    // ================================================================================
    // Properties
    // ================================================================================

    public IBackend Backend { get { return backend; } }
    public GlobalConfig GlobalConfig { get; private set; }

    public ImFontPtr DefaultFont { get; private set; }
    public ImFontPtr OraclesFont { get; private set; }

    // Private properties
    private ProjectWorkspace Workspace { get; set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Main loop
    /// </summary>
    public void Run()
    {
        var stopwatch = Stopwatch.StartNew();

        // Main application loop
        while (!backend.Exited)
        {
            float lastDeltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
            stopwatch.Restart();

            backend.HandleEvents(lastDeltaTime);

            if (backend.Exited)
                break;

            this.Render(lastDeltaTime);

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
    public void Render(float deltaTime)
    {
        ImGui.PushFont(OraclesFont);

        {
            ImGui.Begin("Control Panel");
            ImGui.Checkbox("Demo Window".AsSpan(), ref showImGuiDemoWindow);
            ImGui.Text("Frametime: " + deltaTime);
            ImGui.End();
        }

        Workspace?.Render();

        ImGui.PopFont();

        if (showImGuiDemoWindow)
        {
            ImGui.ShowDemoWindow(ref showImGuiDemoWindow);
        }
    }

    /// <summary>
    /// Turns a Bitmap (cpu) into an Image (gpu), or looks up the existing Image if one exists
    /// in the cache for that Bitmap already.
    /// </summary>
    public Image ImageFromBitmap(Bitmap bitmap)
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

    public void UnregisterBitmap(Bitmap bitmap)
    {
        if (!imageDict.Remove(bitmap))
            throw new Exception("Bitmap to remove did not exist in imageDict.");
    }

    /// <summary>
    /// Queues the given function to run when some free time is available.
    /// The function will be repeatedly called until it returns false.
    /// Modeled after GLib.IdleHandler.
    /// </summary>
    public void LazyInvoke(Func<bool> function)
    {
        idleFunctions.Enqueue(function);
    }

    public void LazyInvoke(Action function)
    {
        LazyInvoke(() => { function(); return false; });
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    void OpenProject(string path, string game)
    {
        // Try to load project config
        ProjectConfig config = ProjectConfig.Load(path);

        var project = new Project(path, game, config);
        this.Workspace = new ProjectWorkspace(this, project);
    }
}
