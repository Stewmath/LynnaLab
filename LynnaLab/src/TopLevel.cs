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
    public static void Load(string path = null, string game = null)
    {
        string versionString = new StreamReader(Helper.GetResourceStream("LynnaLab.version.txt")).ReadToEnd();
        backend = new VeldridBackend.VeldridBackend("LynnaLab " + versionString);

        ImageDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Images/";

        backend.SetIcon(ImageDir + "icon.bmp");
        PegasusSeedImage = backend.ImageFromFile(ImageDir + "Pegasus_Seed_OOX.png");

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

    static IBackend backend;
    static Dictionary<Bitmap, Image> imageDict = new Dictionary<Bitmap, Image>();
    static Queue<Func<bool>> idleFunctions = new Queue<Func<bool>>();
    static Queue<Action> actionsForNextFrame = new();

    // Vars related to modal windows
    static Queue<ModalStruct> modalQueue = new Queue<ModalStruct>();
    static bool rememberGameChoice;

    // ================================================================================
    // Properties
    // ================================================================================

    public static IBackend Backend { get { return backend; } }
    public static GlobalConfig GlobalConfig { get; private set; }
    public static string ImageDir { get; private set; }

    public static ImFontPtr DefaultFont { get; private set; }
    public static ImFontPtr OraclesFont { get; private set; }
    public static ImFontPtr OraclesFont24px { get; private set; }

    public static Image PegasusSeedImage { get; private set; }

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
                if (Workspace == null)
                    backend.Close();
                else
                {
                    CloseProjectModal(() => { backend.Close(); });
                }

                backend.CloseRequested = false;
            }

            if (backend.Exited)
                break;

            {
                var actions = actionsForNextFrame.ToArray();
                actionsForNextFrame.Clear();
                foreach (Action act in actions)
                    act();
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

            // TODO: OK to call this repeatedly?
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
            return;
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
            LoadingModal("Loading project...", () =>
            {
                var project = new Project(path, game, config);
                Workspace = new ProjectWorkspace(project);
            });
        };

        if (config == null)
        {
            DisplayMessageModal("Error", $"Error opening directory: {path}\n\nCouldn't find project config file (config.yaml). Please select the \"oracles-disasm\" folder to open.");
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
            TopLevel.SelectGameModal((game, rememberGameChoice) =>
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

    public static void CloseProjectModal(Action callback = null)
    {
        // Offer to save the project before closing. Pops up when attempting to close the window, or
        // "Project -> Close" (slightly different things)
        OpenModal("Close Project", () =>
        {
            ImGui.Text("Save project before closing?");

            // Close either the project or the entire window, depending on the context
            var close = () =>
            {
                Workspace.Close();
                Workspace = null;
                if (callback != null)
                    callback();
            };

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

    // Not an ImGui modal, but functions similarly.
    public static void OpenProjectModal()
    {
        LoadingModal("Waiting for file dialog...", () =>
        {
            var result = NativeFileDialogSharp.Dialog.FolderPicker();

            if (result.IsOk)
            {
                OpenProject(result.Path);
            }
            else if (result.IsCancelled)
            {
            }
            else if (result.IsError)
            {
                DisplayMessageModal("Error", result.ErrorMessage);
            }
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

    public static void SelectGameModal(Action<string, bool> onSelected)
    {
        // Deciding which game to edit after selecting a project
        OpenModal("Select Game", () =>
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
