using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

using Backend = VeldridBackend.VeldridBackend;

namespace LynnaLab;

/// <summary>
/// Top class (the "top level") contains the main loop and deals with anything that's not
/// project-specific.
///
/// It could potentially contain multiple ProjectWorkspaces in the future.
///
/// Try not to put things in here unless you're sure that the program only needs one of it, since
/// everything is static. Most things should go in ProjectWorkspace instead.
/// </summary>
public static class Top
{
    /// <summary>
    /// Called upon initialization. Optionally takes parameters for the project to load.
    /// </summary>
    public static void Load(string path = null, string game = null)
    {
        string versionString = Helper.ReadResourceFile("LynnaLab.version.txt").Trim();
        backend = new VeldridBackend.VeldridBackend("LynnaLab", versionString);

        ImageDir = Path.GetDirectoryName(System.AppContext.BaseDirectory) + "/Images/";
        FontDir = Path.GetDirectoryName(System.AppContext.BaseDirectory) + "/Fonts/";

        backend.SetIcon(ImageDir + "icon.bmp");
        PegasusSeedTexture = backend.TextureFromFile(ImageDir + "Pegasus_Seed_OOX.png");

        Helper.mainThreadInvokeFunction = Top.LazyInvoke;

        CheckAvailableFonts(); // Must do this before loading global config

        GlobalConfig = GlobalConfig.Load();
        if (GlobalConfig == null)
        {
            GlobalConfig = new GlobalConfig();
            if (!GlobalConfig.FileExists())
                GlobalConfig.Save();
        }

        stopwatch = Stopwatch.StartNew();

        ImGuiX.BackupStyle();
        ReAdjustScale();

        backend.DisplayScaleChanged += () =>
        {
            if (!GlobalConfig.OverrideSystemScaling)
                ReAdjustScale();
        };

        settingsDialog = new SettingsDialog("Settings Dialog");
        settingsDialog.AutoAdjustWidth = true;

        if (path != null)
            Top.OpenProject(path, game);
    }

    // ================================================================================
    // Constants
    // ================================================================================

    public const string RightClickPopupName = "RightClickPopup";

    // ================================================================================
    // Variables
    // ================================================================================

    static Backend backend;
    static Stopwatch stopwatch;

    static Dictionary<Bitmap, RgbaTexture> imageDict = new();
    static Dictionary<string, FontData> fontDict;

    static Queue<Action> actionsForNextFrame = new();

    // This must be thread-safe - signals that come from other threads (ie. emulator process exit
    // signal) may want to schedule actions to perform on the main thread.
    static ConcurrentQueue<Func<bool>> idleFunctions = new();

    static SettingsDialog settingsDialog;

    // ================================================================================
    // Properties
    // ================================================================================

    public static Backend Backend { get { return backend; } }
    public static GlobalConfig GlobalConfig { get; private set; }
    public static string ImageDir { get; private set; }
    public static string FontDir { get; private set; }

    /// <summary>
    /// List of available fonts from the "Fonts/" directory.
    /// </summary>
    public static IEnumerable<string> AvailableFonts { get { return fontDict.Values.Select((v) => v.name); } }

    /// <summary>
    /// Font used for detailed text displays. (It can be a bit difficult to read text dumps in the
    /// oracles font.)
    /// </summary>
    public static ImFontPtr InfoFont { get { return fontDict[GlobalConfig.InfoFont].infoSize; } }

    /// <summary>
    /// Font used for window titles, labels, etc. The default in most contexts.
    /// </summary>
    public static ImFontPtr MenuFont { get { return fontDict[GlobalConfig.MenuFont].menuSize; } }
    public static ImFontPtr MenuFontLarge { get { return fontDict[GlobalConfig.MenuFont].menuSizeLarge; } }

    public static RgbaTexture PegasusSeedTexture { get; private set; }

    // Private properties
    private static ProjectWorkspace Workspace { get; set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Main loop (called repeatedly)
    /// </summary>
    public static void Run()
    {
        float lastDeltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
        stopwatch.Restart();

        backend.HandleEvents(lastDeltaTime);
        Top.Render(lastDeltaTime);

        // Call the "idle functions", used for lazy drawing.
        // Do this before backend.Render() as that triggers vsync and messes up our timer.
        while (Program.handlingException == null)
        {
            if (!idleFunctions.TryDequeue(out Func<bool> func))
                break;
            if (func())
                idleFunctions.Enqueue(func);

            // Maximum frame time up to which we can run another idle function.
            // At 60fps, 1 frame = 0.016 seconds. So we keep this a bit under that.
            const float MAX_FRAME_TIME = 0.01f;

            float deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
            if (deltaTime >= MAX_FRAME_TIME)
                break;
        }

        backend.Render();

        if (backend.CloseRequested)
        {
            if (Workspace == null)
                backend.Close();
            else
                Modal.CloseProjectModal(Workspace, () => { backend.Close(); });

            backend.CloseRequested = false;
        }

        if (Program.handlingException == null)
        {
            var actions = actionsForNextFrame.ToArray();
            actionsForNextFrame.Clear();
            foreach (Action act in actions)
                act();
        }
    }

    /// <summary>
    /// Draw ImGui interface
    /// </summary>
    public static void Render(float deltaTime)
    {
        ImGui.NewFrame();
        ImGui.PushFont(MenuFont);

        Modal.RenderModals();

        // Don't render the workspace if we're handling an exception right now, because it is
        // probably something in Workspace.Render that caused the exception in the first place.
        bool renderWorkspace = Workspace != null && Program.handlingException == null;

        float ypos = 0.0f;

        if (ImGui.BeginMainMenuBar())
        {
            if (renderWorkspace)
            {
                Workspace.RenderMenuBar();
            }
            else
            {
                if (ImGui.BeginMenu("Project"))
                {
                    if (ImGui.MenuItem("Open"))
                    {
                        Modal.OpenProjectModal();
                    }
                    ImGui.EndMenu();
                }
            }
            ypos += ImGui.GetWindowHeight();
            ImGui.EndMainMenuBar();
        }

        if (ImGuiX.BeginDocked("Toolbar", new Vector2(0, ypos), ImGuiX.Unit(0, 50.0f), scrollbar: false))
        {
            if (renderWorkspace)
                Workspace.RenderToolbar();
            ypos += ImGui.GetWindowHeight();
        }
        ImGui.End();

        settingsDialog.RenderDockedLeft(ypos, ImGuiX.Unit(100.0f));

        if (renderWorkspace)
            Workspace.Render(deltaTime);

        ImGui.PopFont();
    }

    /// <summary>
    /// Scales fonts and styles with the given scale value, for high-DPI displays.
    /// </summary>
    public static unsafe void ReAdjustScale()
    {
        ImGui.GetIO().Fonts.Clear();

        float largeFontScale = 1.333f;

        foreach (FontData font in fontDict.Values)
        {
            if (font.name == "ProggyClean")
            {
                ImFontConfig config;
                config.FontDataOwnedByAtlas = 1;
                config.GlyphMaxAdvanceX = float.MaxValue;
                config.RasterizerMultiply = 1.0f;
                config.RasterizerDensity = 1.0f;
                config.OversampleH = config.OversampleV = 1;

                config.SizePixels = (int)(GlobalConfig.InfoFontSize * ImGuiX.ScaleUnit);
                font.infoSize = ImGui.GetIO().Fonts.AddFontDefault(new ImFontConfigPtr(&config));

                config.SizePixels = (int)(GlobalConfig.MenuFontSize * ImGuiX.ScaleUnit);
                font.menuSize = ImGui.GetIO().Fonts.AddFontDefault(new ImFontConfigPtr(&config));

                config.SizePixels = (int)(GlobalConfig.MenuFontSize * ImGuiX.ScaleUnit * largeFontScale);
                font.menuSizeLarge = ImGui.GetIO().Fonts.AddFontDefault(new ImFontConfigPtr(&config));
            }
            else
            {
                font.infoSize = ImGuiX.LoadFont(font.name, (int)(GlobalConfig.InfoFontSize * ImGuiX.ScaleUnit));
                font.menuSize = ImGuiX.LoadFont(font.name, (int)(GlobalConfig.MenuFontSize * ImGuiX.ScaleUnit));
                font.menuSizeLarge = ImGuiX.LoadFont(font.name, (int)(GlobalConfig.MenuFontSize * ImGuiX.ScaleUnit * largeFontScale));
            }
        }

        ImGuiX.UpdateStyle();
        Backend.RecreateFontTexture();
    }

    public static void CheckAvailableFonts()
    {
        fontDict = new();

        AddFont("ProggyClean"); // Default, built-in font

        string[] fonts = Directory.GetFiles(FontDir, "*.ttf");
        foreach (string path in fonts)
        {
            string name = Path.GetFileName(path);
            AddFont(name);
        }
    }

    public static FontData GetFont(string name)
    {
        return fontDict[name];
    }

    /// <summary>
    /// Turns a Bitmap (cpu) into a Texture (gpu), or looks up the existing Texture if one exists in
    /// the cache for that Bitmap already.
    /// The resulting Texture is tied to the Bitmap, updating when the bitmap updates and being
    /// deleted when the bitmap is deleted.
    /// </summary>
    public static RgbaTexture TextureFromBitmapTracked(Bitmap bitmap)
    {
        RgbaTexture texture;
        if (imageDict.TryGetValue(bitmap, out texture))
            return texture;

        texture = backend.TextureFromBitmap(bitmap);
        imageDict[bitmap] = texture;

        bitmap.DisposedEvent += (sender) =>
        {
            var image = imageDict[sender as Bitmap];
            imageDict.Remove(sender as Bitmap);
            image.Dispose();
        };
        return texture;
    }

    public static void UnregisterBitmap(Bitmap bitmap)
    {
        if (!imageDict.Remove(bitmap))
            throw new Exception("Bitmap to remove did not exist in imageDict.");
    }

    /// <summary>
    /// Queues the given function to run when some free time is available.
    /// The function will be repeatedly called until it returns false.
    /// THIS MAY BE CALLED FROM OTHER THREADS! Ensure that no race conditions occur. The
    /// "idleFunctions" queue is a thread-safe type.
    /// </summary>
    public static void LazyInvoke(Func<bool> function)
    {
        idleFunctions.Enqueue(function);
    }

    public static void LazyInvoke(Action function)
    {
        LazyInvoke(() => { function(); return false; });
    }

    /// <summary>
    /// Opens the project at the specified path. If game == null, this either checks the project
    /// config to decide which game to load, or shows a prompt to let the user decide.
    /// </summary>
    public static void OpenProject(string path, string gameOverride = null)
    {
        if (Workspace != null)
        {
            Workspace.Close();
        }

        // Try to load project config
        ProjectConfig config = ProjectConfig.Load(path);

        var loadProject = (string game) =>
        {
            Modal.LoadingModal($"Loading project: {path} ({game})...", () =>
            {
                var project = new Project(path, game, config);
                Workspace = new ProjectWorkspace(project);
            });
        };

        if (config == null)
        {
            Modal.DisplayMessageModal("Error", $"Error opening directory: {path}\n\nCouldn't find project config file (config.yaml). Please select the \"oracles-disasm\" folder to open.\n\nIf you're on Windows, you may need to execute the \"windows-setup.bat\" file first.");
        }
        else if (gameOverride != null)
        {
            loadProject(gameOverride);
        }
        else if (config.EditingGameIsValid())
        {
            loadProject(config.EditingGame);
        }
        else
        {
            Modal.SelectGameModal(path, (game, rememberGameChoice) =>
            {
                if (rememberGameChoice)
                    config.SetEditingGame(game);
                loadProject(game);
            });
        }
    }

    public static void SaveProject()
    {
        Workspace?.Project.Save();
    }

    /// <summary>
    /// Close the project. Normally, this should be called through Modal.CloseProjectModal to ensure
    /// no data is lost.
    /// </summary>
    public static void CloseProject()
    {
        Workspace?.Close();
        Workspace = null;
    }

    public static void DoNextFrame(Action action)
    {
        actionsForNextFrame.Enqueue(action);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    static void AddFont(string name)
    {
        FontData font = new();
        font.name = name;
        font.infoSize = null; // Will be loaded later
        font.menuSize = null;
        font.menuSizeLarge = null;
        fontDict[name] = font;
    }


    // ================================================================================
    // Nested structs
    // ================================================================================

    public class FontData
    {
        public string name;

        // Variants of the font loaded at different sizes
        public ImFontPtr infoSize;
        public ImFontPtr menuSize;
        public ImFontPtr menuSizeLarge;
    }
}
