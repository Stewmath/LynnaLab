using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

using Backend = VeldridBackend.VeldridBackend;

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
    public static void Load(string path = null, string game = null)
    {
        string versionString = new StreamReader(Helper.GetResourceStream("LynnaLab.version.txt")).ReadToEnd().Trim();
        backend = new VeldridBackend.VeldridBackend("LynnaLab", versionString);

        ImageDir = Path.GetDirectoryName(System.AppContext.BaseDirectory) + "/Images/";

        backend.SetIcon(ImageDir + "icon.bmp");
        PegasusSeedTexture = backend.TextureFromFile(ImageDir + "Pegasus_Seed_OOX.png");

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

        ImGui.StyleColorsDark(); // Default style
        //ImGui.StyleColorsClassic();
        //ImGui.StyleColorsLight();

        backend.RecreateFontTexture();

        stopwatch = Stopwatch.StartNew();

        if (path != null)
            TopLevel.OpenProject(path, game);
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

    static Dictionary<Bitmap, RgbaTexture> imageDict = new Dictionary<Bitmap, RgbaTexture>();

    static Queue<Action> actionsForNextFrame = new();

    // This must be thread-safe - signals that come from other threads (ie. emulator process exit
    // signal) may want to schedule actions to perform on the main thread.
    static ConcurrentQueue<Func<bool>> idleFunctions = new();

    // Vars related to modal windows
    static Queue<ModalStruct> modalQueue = new Queue<ModalStruct>();
    static bool rememberGameChoice;

    // ================================================================================
    // Properties
    // ================================================================================

    public static Backend Backend { get { return backend; } }
    public static GlobalConfig GlobalConfig { get; private set; }
    public static string ImageDir { get; private set; }

    public static ImFontPtr DefaultFont { get; private set; }
    public static ImFontPtr OraclesFont { get; private set; }
    public static ImFontPtr OraclesFont24px { get; private set; }

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
        TopLevel.Render(lastDeltaTime);

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
                CloseProjectModal(() => { backend.Close(); });

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
        ImGui.PushFont(OraclesFont);

        RenderModals();

        if (Workspace == null)
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("Project"))
                {
                    if (ImGui.MenuItem("Open"))
                    {
                        OpenProjectModal();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
        }
        else
        {
            // Don't render the workspace if we're handling an exception right now, because it is
            // probably something in Workspace.Render that caused the exception in the first place.
            if (Program.handlingException == null)
                Workspace.Render(deltaTime);
        }

        ImGui.PopFont();
    }

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
    /// This is in the TopLevel class because some modals are needed even when no Workspace is loaded.
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
            LoadingModal($"Loading project: {path} ({game})...", () =>
            {
                var project = new Project(path, game, config);
                Workspace = new ProjectWorkspace(project);
            });
        };

        if (config == null)
        {
            DisplayMessageModal("Error", $"Error opening directory: {path}\n\nCouldn't find project config file (config.yaml). Please select the \"oracles-disasm\" folder to open.\n\nIf you're on Windows, you may need to execute the \"windows-setup.bat\" file first.");
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
            TopLevel.SelectGameModal(path, (game, rememberGameChoice) =>
            {
                if (rememberGameChoice)
                    config.SetEditingGame(game);
                loadProject(game);
            });
        }
    }

    public static void DoNextFrame(Action action)
    {
        actionsForNextFrame.Enqueue(action);
    }

    // ================================================================================
    // Modal windows
    // ================================================================================

    // Offer to save the project before closing. Pops up when attempting to close the window, or
    // "Project -> Close" (slightly different things).
    // If the project is not modified then it is immediately closed.
    public static void CloseProjectModal(Action callback = null)
    {
        Debug.Assert(Workspace != null);

        var close = () =>
        {
            Workspace.Close();
            Workspace = null;
            if (callback != null)
                callback();
        };

        if (!Workspace.Project.Modified)
        {
            DoNextFrame(close);
            return;
        }

        OpenModal("Close Project", () =>
        {
            ImGui.Text("Save project before closing?");

            if (ImGui.Button("Save"))
            {
                Workspace.Project.Save();
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

        Backend.ShowOpenFolderDialog(null, (folder) =>
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
                    OpenProject(selectedFolder);
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

    public static void DisplayExceptionModal(Exception e)
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

            if (Workspace != null)
            {
                if (ImGui.Button("Save project and quit"))
                {
                    Program.handlingException = null;
                    Workspace.Project.Save();
                    Environment.Exit(1);
                    retval = true;
                }
                ImGui.SameLine();
            }

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
