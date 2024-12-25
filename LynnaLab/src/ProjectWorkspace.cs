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

        this.Brush = new Brush();

        linkImage = TopLevel.ImageFromBitmap(project.LinkBitmap);

        roomEditor = new RoomEditor(this);
        dungeonEditor = new DungeonEditor(this, "Dungeon Editor");
        tilesetEditor = new TilesetEditor(this, "Tileset Editor");
        tilesetCloner = new TilesetCloner(this, "Tileset Cloner");
        buildDialog = new BuildDialog(this, "Build");
        documentationDialog = new DocumentationDialog(this, "Documentation Dialog");
        scratchpad = new ScratchPad(this, "Scratchpad", roomEditor.TilesetViewer, Brush);

        frames.AddRange(new Frame[] {
                roomEditor,
                dungeonEditor,
                tilesetEditor,
                tilesetCloner,
                scratchpad,
                buildDialog,
                documentationDialog,
            });
        frames.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));

        // Default active windows
        roomEditor.Active = true;
        scratchpad.Active = true;
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
    List<Frame> frames = new List<Frame>();
    bool showDebugWindow;
    bool showImGuiDemoWindow;
    bool lightMode, darkenUsedDungeonRooms = true;

    Image linkImage;
    BuildDialog buildDialog;
    DocumentationDialog documentationDialog;

    TilesetImageCacher tilesetImageCacher;
    RoomImageCacher roomImageCacher;
    MapImageCacher mapImageCacher;

    Process emulatorProcess;

    // ================================================================================
    // Properties
    // ================================================================================
    public Project Project { get; private set; }
    public QuickstartData QuickstartData { get; set; } = new QuickstartData();
    public Brush Brush { get; private set; }
    public bool ShowBrushPreview { get; private set; } = true;

    public bool DarkenUsedDungeonRooms { get { return darkenUsedDungeonRooms; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render(float deltaTime)
    {
        if (Project == null)
            return;

        float menuBarHeight = 0.0f;
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Project"))
            {
                if (ImGui.MenuItem("Open"))
                {
                    TopLevel.OpenModal("Close Project|Open Project");
                }
                if (ImGui.MenuItem("Save"))
                {
                    Project.Save();
                }
                if (ImGui.MenuItem("Close"))
                {
                    TopLevel.OpenModal("Close Project");
                }
                if (ImGui.MenuItem("Reload"))
                {
                    TopLevel.OpenModal("Close Project|Reload Project");
                }
                if (ImGui.MenuItem("Switch Game"))
                {
                    TopLevel.OpenModal("Close Project|Switch Game");
                }
                if (ImGui.MenuItem("Run"))
                {
                    RunGame();
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
            if (ImGui.BeginMenu("Misc"))
            {
                if (ImGuiX.MenuItemCheckbox("Light Mode", ref lightMode))
                {
                    if (lightMode)
                        ImGui.StyleColorsLight();
                    else
                        ImGui.StyleColorsDark();
                }
                ImGuiX.MenuItemCheckbox("Darken used dungeon rooms (overworld tab)", ref darkenUsedDungeonRooms);
                ImGuiX.MenuItemCheckbox(
                    "Hover preview",
                    new Accessor<bool>(() => ShowBrushPreview
                    ));
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

        foreach (var frame in frames)
        {
            frame.RenderAsWindow();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.F4))
            ToggleQuickstart(!QuickstartData.Enabled);
        else if (ImGui.IsKeyPressed(ImGuiKey.F5))
            RunGame();
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
        tilesetCloner.Active = true;
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
