using System.Diagnostics;

namespace LynnaLab;

/// <summary>
/// Class containing all project-specific information.
/// Keeping this separate from the TopLevel class just in case I want to make a way to open
/// multiple projects at once.
/// </summary>
public class ProjectWorkspace
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ProjectWorkspace(Project project)
    {
        this.Project = project;

        Project.LazyInvoke = TopLevel.LazyInvoke;

        tilesetImageCacher = new TilesetImageCacher(this);
        roomImageCacher = new RoomImageCacher(this);
        mapImageCacher = new MapImageCacher(this);

        this.Brush = new Brush<int>(0);

        linkImage = TopLevel.ImageFromBitmap(project.LinkBitmap);

        roomEditor = new RoomEditor(this);
        dungeonEditor = new DungeonEditor(this, "Dungeon Editor");
        tilesetEditor = new TilesetEditor(this, "Tileset Editor");
        tilesetCloner = new TilesetCloner(this, "Tileset Cloner");
        buildDialog = new BuildDialog(this, "Build");
        documentationDialog = new DocumentationDialog(this, "Documentation Dialog");
        scratchpad = new ScratchPad(this, "Scratchpad", roomEditor.TilesetViewer, Brush);
        undoDialog = new UndoDialog(this, "Undo History");

        roomEditor.SetInterpolation(bicubicScaling ? Interpolation.Bicubic : Interpolation.Nearest);
        roomEditor.SetScrollToZoom(scrollToZoom);

        frames.AddRange(new Frame[] {
                roomEditor,
                dungeonEditor,
                tilesetEditor,
                tilesetCloner,
                scratchpad,
                buildDialog,
                documentationDialog,
                undoDialog,
            });
        frames.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));

        // Default active windows
        roomEditor.Active = true;
        undoDialog.Active = true;
    }

    // ================================================================================
    // Constants
    // ================================================================================

    readonly Vector2 TOOLBAR_BUTTON_SIZE = new Vector2(30.0f, 30.0f);

    // ================================================================================
    // Variables
    // ================================================================================

    RoomEditor roomEditor;
    DungeonEditor dungeonEditor;
    TilesetEditor tilesetEditor;
    TilesetCloner tilesetCloner;
    ScratchPad scratchpad;
    BuildDialog buildDialog;
    DocumentationDialog documentationDialog;
    UndoDialog undoDialog;

    List<Frame> frames = new List<Frame>();
    Queue<Action> actionsForNextFrame = new();
    bool showDebugWindow;
    bool showImGuiDemoWindow;
    bool lightMode, scrollToZoom = true, darkenUsedDungeonRooms = true, bicubicScaling = true;
    bool autoAdjustGroupNumber = true;

    Image linkImage;

    TilesetImageCacher tilesetImageCacher;
    RoomImageCacher roomImageCacher;
    MapImageCacher mapImageCacher;

    Process emulatorProcess;

    // ================================================================================
    // Properties
    // ================================================================================
    public Project Project { get; private set; }
    public QuickstartData QuickstartData { get; set; } = new QuickstartData();
    public Brush<int> Brush { get; private set; }
    public bool ShowBrushPreview { get; private set; } = true;

    // For copy/pasting. This may or may not exist in the game (it may have been deleted), but it
    // should be copiable regardless.
    public ObjectDefinition CopiedObject { get; set; }
    public Color? CopiedColor { get; set; }

    // Togglable settings that affect other modules (really just minimaps right now)
    public bool DarkenUsedDungeonRooms { get { return darkenUsedDungeonRooms; } }
    public bool AutoAdjustGroupNumber { get { return autoAdjustGroupNumber; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render(float deltaTime)
    {
        if (Project == null)
            return;

        foreach (Action act in actionsForNextFrame)
            act();
        actionsForNextFrame.Clear();

        float menuBarHeight = 0.0f;
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Project"))
            {
                if (ImGui.MenuItem("Open"))
                {
                    TopLevel.CloseProjectModal(() => TopLevel.OpenProjectModal());
                }
                if (ImGui.MenuItem("Save"))
                {
                    Project.Save();
                }
                if (ImGui.MenuItem("Close"))
                {
                    TopLevel.CloseProjectModal();
                }
                if (ImGui.MenuItem("Reload"))
                {
                    TopLevel.CloseProjectModal(() => TopLevel.OpenProject(Project.BaseDirectory, Project.GameString));
                }
                if (ImGui.MenuItem("Switch Game"))
                {
                    TopLevel.CloseProjectModal(() =>
                    {
                        string gameString = Project.Game == Game.Seasons ? "ages" : "seasons";
                        TopLevel.OpenProject(Project.BaseDirectory, gameString);
                    });
                }
                if (ImGui.MenuItem("Run"))
                {
                    RunGame();
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Edit"))
            {
                Func<bool> renderUndoButton;
                if (Project.UndoState.UndoAvailable)
                    renderUndoButton = () => ImGui.Selectable("Undo: " + Project.UndoState.GetUndoDescription());
                else
                    renderUndoButton = () => ImGui.Selectable("Undo", false, ImGuiSelectableFlags.Disabled);

                Func<bool> renderRedoButton;
                if (Project.UndoState.RedoAvailable)
                    renderRedoButton = () => ImGui.Selectable("Redo: " + Project.UndoState.GetRedoDescription());
                else
                    renderRedoButton = () => ImGui.Selectable("Redo", false, ImGuiSelectableFlags.Disabled);

                if (renderUndoButton())
                {
                    Project.UndoState.Undo();
                }
                if (renderRedoButton())
                {
                    Project.UndoState.Redo();
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Windows"))
            {
                foreach (Frame frame in frames)
                {
                    ImGuiX.MenuItemCheckbox(
                        frame.Name,
                        frame.Active,
                        (active) =>
                        {
                            frame.Active = active;
                        });
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Minimap"))
            {
                if (ImGuiX.MenuItemCheckbox("Scroll to Zoom", ref scrollToZoom))
                {
                    roomEditor.SetScrollToZoom(scrollToZoom);
                }

                ImGuiX.MenuItemCheckbox("Darken used dungeon rooms & duplicate rooms", ref darkenUsedDungeonRooms);
                ImGuiX.TooltipOnHover("Rooms which are darkened have a more \"canonical\" version somewhere else, either on the dungeon tab or in a different world index. Duplicate rooms may be missing their warp data.");

                if (ImGuiX.MenuItemCheckbox("Bicubic scaling", ref bicubicScaling))
                {
                    Interpolation interp = bicubicScaling ? Interpolation.Bicubic : Interpolation.Nearest;
                    roomEditor.SetInterpolation(interp);
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Misc"))
            {
                ImGuiX.MenuItemCheckbox("Auto-Adjust World Number", ref autoAdjustGroupNumber);
                ImGuiX.TooltipOnHover("The subrosia map & dungeons have duplicates in the World tab. Check this box to auto-adjust the group number to its expected value when selecting these rooms.");

                if (ImGuiX.MenuItemCheckbox("Light Mode", ref lightMode))
                {
                    if (lightMode)
                        ImGui.StyleColorsLight();
                    else
                        ImGui.StyleColorsDark();
                }
                ImGuiX.MenuItemCheckbox(
                    "Hover preview",
                    new Accessor<bool>(() => ShowBrushPreview
                    ));

                if (ImGui.MenuItem("Choose emulator path..."))
                {
                    string cmd = BuildDialog.SelectEmulator();
                    if (cmd != null)
                        TopLevel.GlobalConfig.EmulatorCommand = cmd;
                }

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Debug"))
            {
                ImGuiX.MenuItemCheckbox("Debug Window", ref showDebugWindow);
                ImGuiX.MenuItemCheckbox("ImGui Demo Window", ref showImGuiDemoWindow);
                ImGui.EndMenu();
            }
            menuBarHeight = ImGui.GetWindowHeight();
            ImGui.EndMainMenuBar();
        }

        if (ImGuiX.BeginToolbar("Toolbar", menuBarHeight, 50.0f))
        {
            if (ImGui.ImageButton("Run", TopLevel.PegasusSeedImage.GetBinding(), TOOLBAR_BUTTON_SIZE))
            {
                RunGame();
            }
            ImGuiX.TooltipOnHover("Run (F5)");

            ImGui.SameLine();
            ImGuiX.ToggleImageButton("Quickstart", linkImage.GetBinding(), TOOLBAR_BUTTON_SIZE,
                    QuickstartData.Enabled, ToggleQuickstart);
            ImGuiX.TooltipOnHover("Toggle Quickstart (F4)");
        }
        ImGui.End();

        if (showDebugWindow)
        {
            ImGui.Begin("Debug", ref showDebugWindow);
            ImGui.Text("Frametime: " + deltaTime);
            ImGui.End();
        }

        if (showImGuiDemoWindow)
        {
            ImGui.PushFont(TopLevel.DefaultFont);
            ImGui.ShowDemoWindow(ref showImGuiDemoWindow);
            ImGui.PopFont();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.F4))
            ToggleQuickstart(!QuickstartData.Enabled);
        if (ImGui.IsKeyPressed(ImGuiKey.F5))
            RunGame();
        if (ImGui.IsKeyChordPressed(ImGuiKey.ModCtrl | ImGuiKey.Z))
            Project.UndoState.Undo();
        if (ImGui.IsKeyChordPressed(ImGuiKey.ModCtrl | ImGuiKey.ModShift | ImGuiKey.Z))
            Project.UndoState.Redo();

        // Rendering frames should be the last thing done. In particular, undo/redo operations
        // should happen before this. They may delete resources (ImGui images) that the frames have
        // requested to use already if the frames are rendered first.
        foreach (var frame in frames)
        {
            frame.RenderAsWindow();
        }
    }

    /// <summary>
    /// This is called when the Project should be closed.
    /// </summary>
    public void Close()
    {
        Project.Close();
        linkImage.Dispose();
        tilesetImageCacher.Dispose();
        roomImageCacher.Dispose();
        mapImageCacher.Dispose();
    }

    public Image GetCachedTilesetImage(Tileset tileset)
    {
        return tilesetImageCacher.GetImage(tileset);
    }

    public Image GetCachedRoomImage(RoomLayout layout)
    {
        return roomImageCacher.GetImage(layout);
    }

    public Image GetCachedMapImage((Map map, int floor) key)
    {
        return mapImageCacher.GetImage(key);
    }

    public void OpenTilesetCloner(RealTileset source, RealTileset dest)
    {
        tilesetCloner.SetSourceTileset(source);
        tilesetCloner.SetDestTileset(dest);
        tilesetCloner.Focus();
    }

    /// <summary>
    /// Set the current active emulator process, killing the previous process if it exists.
    /// </summary>
    public void RegisterEmulatorProcess(Process process)
    {
        if (emulatorProcess != null)
        {
            emulatorProcess.Kill(true); // Pass true to kill whole process tree
        }
        emulatorProcess = process;
    }

    public void ShowDocumentation(Documentation documentation)
    {
        documentationDialog.SetDocumentation(documentation);
        documentationDialog.Active = true;
        documentationDialog.Focus();
    }

    public void OpenTilesetEditor(RealTileset tileset)
    {
        tilesetEditor.SetTileset(tileset);
        tilesetEditor.Focus();
    }

    public void DoNextFrame(Action action)
    {
        actionsForNextFrame.Enqueue(action);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void ToggleQuickstart(bool enabled)
    {
        QuickstartData.group = (byte)roomEditor.Room.Group;
        QuickstartData.room = (byte)(roomEditor.Room.Index & 0xff);
        QuickstartData.season = (byte)roomEditor.Season;
        QuickstartData.x = 0x48;
        QuickstartData.y = 0x48;
        QuickstartData.Enabled = enabled;
    }

    void RunGame()
    {
        Project.Save();
        buildDialog.BeginCompile();
        buildDialog.Focus();
    }
}

/// <summary>
/// Little class to hold quickstart state
/// </summary>
public class QuickstartData
{
    // ================================================================================
    // Variables
    // ================================================================================
    public byte group, room, season, y, x;
    bool _enabled;

    // ================================================================================
    // Properties
    // ================================================================================
    public bool Enabled
    {
        get { return _enabled; }
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                enableToggledEvent?.Invoke(this, value);
            }
        }
    }

    // ================================================================================
    // Events
    // ================================================================================
    public event EventHandler<bool> enableToggledEvent;
}
