#nullable disable
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SDL;
using Backend = VeldridBackend.VeldridBackend;
using Emulation;

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
    private static readonly log4net.ILog log = LogHelper.GetLogger();

    /// <summary>
    /// Called upon initialization. Optionally takes parameters for the project to load.
    /// </summary>
    public static void Load(string path = null, string game = null, bool implicitWindowsOpen = false)
    {
        string versionString = Helper.GetVersionString();
        backend = new VeldridBackend.VeldridBackend("LynnaLab", versionString);

        AudioAvailable = InitAudio();

        ImageDir = Path.GetDirectoryName(System.AppContext.BaseDirectory) + "/Images/";
        FontDir = Path.GetDirectoryName(System.AppContext.BaseDirectory) + "/Fonts/";

        backend.SetIcon(ImageDir + "icon.bmp");
        PegasusSeedTexture = backend.TextureFromFile(ImageDir + "Pegasus_Seed_OOX.png");
        FaroresMemoryTexture = backend.TextureFromFile(ImageDir + "Farores_Memory.png");
        FaroresMemoryOpenTexture = backend.TextureFromFile(ImageDir + "book_open.png");

        Helper.mainThreadInvokeFunction = Top.DoNextFrame;

        CheckAvailableFonts(); // Must do this before loading global config

        GlobalConfig = GlobalConfig.Load();
        if (GlobalConfig == null)
        {
            GlobalConfig = new GlobalConfig();
            if (!GlobalConfig.FileExists())
                GlobalConfig.Save();
        }

        UpdateDefaultFont();

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
        {
            if (implicitWindowsOpen)
            {
                if (!Path.Exists(path))
                {
                    Modal.DisplayMessageModal("Error", $"Directory does not exist: '{path}'.\n\nTo run LynnaLab locally, first download MSYS2, then execute the \"windows-setup.bat\" file. See readme for details.\n\nIf you're just connecting to a server you may ignore this message.");
                    return;
                }
        
                Modal.OpenModal("Confirm open", () =>
                {
                    bool retval = false;
                    ImGui.Text($"Found project at '{path}', open it?");
                    if (ImGui.Button("Open"))
                    {
                        Top.OpenProject(path, game);
                        retval = true;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Don't open"))
                    {
                        retval = true;
                    }
                    return retval;
                });
            }
            else
                Top.OpenProject(path, game);
        }
    }

    // ================================================================================
    // Constants
    // ================================================================================

    public const string RightClickPopupName = "RightClickPopup";

    // Filter to use with Backend.ShowOpenFileDialog for finding programs
    public static readonly (string, string)[] ProgramFileFilter =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? new (string, string)[] {("Executable files (.exe)", "exe"), ("All files", "*")}
        : new (string, string)[] {};


    // ================================================================================
    // Variables
    // ================================================================================

    static Backend backend;
    static Stopwatch stopwatch;

    static Dictionary<Bitmap, RgbaTexture> imageDict = new();
    static Dictionary<string, FontData> fontDict;

    // This must be thread-safe - signals that come from other threads (ie. emulator process exit
    // signal) may want to schedule actions to perform on the main thread.
    static ConcurrentQueue<Action> actionsForNextFrame = new();

    static SettingsDialog settingsDialog;
    static HelpWindow helpWindow = new();
    static OraclesEmulator emulator;
    static EmulatorManager emulatorManager;


    // ================================================================================
    // Properties
    // ================================================================================

    public static bool AudioAvailable { get; private set; }

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
    public static ImFontPtr InfoFont { get { return fontDict[GlobalConfig.InfoFont].normal; } }
    public static ImFontPtr InfoFontBold { get { return fontDict[GlobalConfig.InfoFont].bold; } }

    /// <summary>
    /// Font used for window titles, labels, etc. The default in most contexts.
    /// </summary>
    public static ImFontPtr MenuFont { get { return fontDict[GlobalConfig.MenuFont].normal; } }

    /// <summary>
    /// Monospace font used by sameboy debugger console.
    /// </summary>
    public static ImFontPtr MonoFont { get { return fontDict["ProggyClean"].normal; } }

    public static RgbaTexture PegasusSeedTexture { get; private set; }
    public static RgbaTexture FaroresMemoryTexture { get; private set; }
    public static RgbaTexture FaroresMemoryOpenTexture { get; private set; }

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
            while (actionsForNextFrame.TryDequeue(out Action act))
                act();
        }
    }

    /// <summary>
    /// Draw ImGui interface
    /// </summary>
    public static void Render(float deltaTime)
    {
        ImGui.NewFrame();
        ImGuiLL.PushMenuFont();

        Modal.RenderModalsAndMessages();

        // Don't render the workspace if we're handling an exception right now, because it is
        // probably something in Workspace.Render that caused the exception in the first place.
        bool renderWorkspace = Workspace != null && Program.handlingException == null;

        var viewport = ImGui.GetMainViewport();

        // Coordinates in terms of "working area" for next elements (see
        // ImGuiViewport.WorkPos/WorkSize - this excludes the main menu bar)
        float xpos = 0.0f;
        float ypos = 0.0f;

        if (ImGui.BeginMainMenuBar())
        {
            if (renderWorkspace)
            {
                Workspace.RenderMenuBar();
            }
            else // No project is loaded
            {
                if (ImGui.BeginMenu("Project"))
                {
                    if (ImGui.MenuItem("Open"))
                    {
                        Modal.OpenProjectModal();
                    }
                    if (ImGui.MenuItem("Connect to server..."))
                    {
                        Modal.ConnectToServerModal();
                    }
                    ImGui.EndMenu();
                }
            }
            ImGui.EndMainMenuBar();
        }

        if (ImGuiX.BeginDocked("Toolbar", new Vector2(0, ypos), ImGuiX.Unit(0, 50.0f), scrollbar: false))
        {

            if (ImGui.ImageButton("Help",
                Top.FaroresMemoryTexture.GetBinding(),
                ImGuiX.Unit(ImGuiLL.TOOLBAR_BUTTON_SIZE)))
            {
                helpWindow.Focus();
            }
            ImGuiX.TooltipOnHover("Help (F1)");

            if (renderWorkspace)
            {
                ImGui.SameLine();
                Workspace.RenderToolbar();
            }
            ypos += ImGui.GetWindowHeight();
        }
        ImGui.End();

        xpos += settingsDialog.RenderDockedLeft(ypos, ImGuiX.Unit(100.0f));

        // Begin the "Main DockSpace", occupying the remainder of the window not taken up by the
        // menubar, toolbar, etc. Windows can be docked in this region.
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(xpos, ypos));
        ImGui.SetNextWindowSize(viewport.WorkSize - new Vector2(xpos, ypos));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));

        ImGui.Begin("Main DockSpace", 0
                    | ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoCollapse
                    | ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoBringToFrontOnFocus
                    | ImGuiWindowFlags.NoNavFocus);

        ImGui.PopStyleVar(2);

        // Submit the DockSpace
        if (ImGui.GetIO().ConfigFlags.HasFlag(ImGuiConfigFlags.DockingEnable))
        {
            uint dockspaceID = ImGui.GetID("MyDockspace");
            ImGui.DockSpace(dockspaceID, new Vector2(0.0f, 0.0f), 0);
        }


        if (renderWorkspace)
        {
            Workspace.Render(deltaTime);
        }

        emulatorManager?.RenderAll();
        helpWindow.RenderAsWindow("Help");

        ImGui.End(); // Dockspace window

        // Keybindings
        if (ImGui.IsKeyPressed(ImGuiKey.F1))
            helpWindow.Focus();

        ImGui.PopFont();
    }

    /// <summary>
    /// Mimics the C++ ImGui default constructor for ImFontConfig
    /// </summary>
    static ImFontConfig BasicImFontConfig()
    {
        ImFontConfig config = new();
        config.FontDataOwnedByAtlas = 1;
        config.OversampleH = 0; // Auto == 1 or 2 depending on size
        config.OversampleV = 0; // Auto == 1
        config.ExtraSizeScale = 1.0f;
        config.GlyphMaxAdvanceX = float.MaxValue;
        config.RasterizerMultiply = 1.0f;
        config.RasterizerDensity = 1.0f;
        config.EllipsisChar = 0;
        return config;
    }

    /// <summary>
    /// Scales fonts and styles with the given scale value, for high-DPI displays.
    /// </summary>
    public static unsafe void ReAdjustScale()
    {
        ImGuiX.UpdateStyle();
    }

    public unsafe static void CheckAvailableFonts()
    {
        fontDict = new();

        // Built in ImGui fonts
        AddFont("ProggyClean");
        AddFont("ProggyVector");

        string[] fonts = Directory.GetFiles(FontDir, "*.ttf");
        foreach (string path in fonts)
        {
            string name = Path.GetFileName(path);
            AddFont(name);
        }

        ImFontConfig boldConfig = BasicImFontConfig();
        boldConfig.FontLoaderFlags = (uint)ImGuiFreeTypeLoaderFlags.Bold;

        foreach (FontData font in fontDict.Values)
        {
            if (font.name == "ProggyClean") // Built into ImGui
            {
                font.normal = ImGui.GetIO().Fonts.AddFontDefaultBitmap();
                font.bold = ImGui.GetIO().Fonts.AddFontDefaultBitmap(boldConfig);
            }
            else if (font.name == "ProggyVector") // Built into ImGui
            {
                font.normal = ImGui.GetIO().Fonts.AddFontDefaultVector();
                font.bold = ImGui.GetIO().Fonts.AddFontDefaultVector(boldConfig);
            }
            else
            {
                font.normal = ImGuiX.LoadFont(font.name);
                font.bold = ImGuiX.LoadFont(font.name, boldConfig);
            }
        }
    }

    /// <summary>
    /// The default font generally doesn't matter, because we use PushFont everywhere. However the
    /// one thing we can't use PushFont for is the title string for docked windows; for some reason
    /// this seems to exist outside of anything we can influence except by setting the DefaultFont.
    /// (Not for regular floating windows! Only when docked together.)
    ///
    /// This responds to DPI scaling, but not to user updates to the font size. Fixing that would
    /// require changing FontScaleBase or something? Not really worth it for this edge case...
    /// </summary>
    public static void UpdateDefaultFont()
    {
        ImGui.GetIO().FontDefault = MenuFont;
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
    /// Queues the given function to run later (after imgui rendering is finished).
    /// THIS MAY BE CALLED FROM OTHER THREADS! Ensure that no race conditions occur. The
    /// "actionsForNextFrame" queue is a thread-safe type.
    /// </summary>
    public static void DoNextFrame(Action action)
    {
        actionsForNextFrame.Enqueue(action);
    }

    /// <summary>
    /// Like DoNextFrame but it returns a Task, so can be used with "await".
    /// </summary>
    public static Task DoNextFrameAsync(Action action)
    {
        TaskCompletionSource taskSource = new();

        actionsForNextFrame.Enqueue(() =>
        {
            try
            {
                action();
                taskSource.SetResult();
            }
            catch (Exception e)
            {
                taskSource.SetException(e);
                throw;
            }
        });

        return taskSource.Task;
    }

    /// <summary>
    /// Opens the project at the specified path. If game == null, this either checks the project
    /// config to decide which game to load, or shows a prompt to let the user decide.
    /// </summary>
    public static void OpenProject(string path, string gameOverride = null)
    {
        CloseProject();

        // Try to load project config
        ProjectConfig config = ProjectConfig.Load(path);

        var loadProject = (string game) =>
        {
            Modal.LoadingModal($"Loading project: {path} ({game})...", () =>
            {
                Stopwatch watch = new();
                watch.Start();
                var project = new Project(path, Project.GameFromString(game), config);
                watch.Stop();
                log.Info($"Loaded project in {((float)watch.ElapsedMilliseconds) / 1000:f3}s");
                Workspace = new ProjectWorkspace(project, "MainProject");
                helpWindow.SetProject(project);
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
    /// no data is lost. Calling this directly while rendering can cause exceptions due to Workspace
    /// turning null.
    /// </summary>
    public static void CloseProject()
    {
        Workspace?.Close();
        Workspace = null;
        helpWindow.SetProject(null);
    }

    public static void SetWorkspace(ProjectWorkspace workspace)
    {
        if (Workspace != null)
            throw new Exception("SetWorkspace: Already have a workspace?");

        Workspace = workspace;
    }

    public static void SetClipboardData(string mimeType, string value)
    {
        SDLUtil.SDLHelper.SetClipboardData([mimeType], Encoding.UTF8.GetBytes(value + '\0'));
    }

    public static string GetClipboardData(string mimeType)
    {
        byte[] data = SDLUtil.SDLHelper.GetClipboardData(mimeType);
        if (data == null)
            throw new Exception("GetClipboardData: No data available for mime type " + mimeType);
        return Encoding.UTF8.GetString(data);
    }

    public static bool HasClipboardData(string mimeType)
    {
        return SDLUtil.SDLHelper.HasClipboardData(mimeType);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    static void AddFont(string name)
    {
        FontData font = new();
        font.name = name;
        font.normal = null; // Will be loaded later
        fontDict[name] = font;
    }

    static bool InitAudio()
    {
        bool retval = SDL3.SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO);

        if (!retval)
            log.Warn($"SDL audio failed to intiialize: {SDL3.SDL_GetError()}.");

        return retval;
    }

    public static void StartEmulator(string romFile, EmulatorStartOptions options)
    {
        emulatorManager?.Dispose();
        emulator?.Dispose();

        emulator = new OraclesEmulator(romFile, options);
        emulatorManager = new EmulatorManager(emulator);
    }

    // ================================================================================
    // Nested structs
    // ================================================================================

    public class FontData
    {
        public string name;

        // Variants of the font
        public ImFontPtr normal;
        public ImFontPtr bold;
    }
}
