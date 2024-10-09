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
        scratchpad = new ScratchPad("Scratchpad", roomEditor.TilesetViewer, Brush);

        frames.AddRange(new Frame[] {
                roomEditor,
                dungeonEditor,
                tilesetEditor,
                tilesetCloner,
                scratchpad,
                buildDialog,
            });

        // Default active windows
        roomEditor.Active = true;
        scratchpad.Active = true;
    }

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

    Image linkImage;
    BuildDialog buildDialog;

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

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render(float deltaTime)
    {
        if (Project == null)
            return;

        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Open"))
                {
                }
                if (ImGui.MenuItem("Save"))
                {
                    Project.Save();
                }
                if (ImGui.MenuItem("Run"))
                {
                    buildDialog.BeginCompile();
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
                ImGuiX.MenuItemCheckbox(
                    "Quickstart",
                    QuickstartData.Enabled,
                    (value) =>
                    {
                        QuickstartData.group = (byte)roomEditor.Room.Group;
                        QuickstartData.room = (byte)(roomEditor.Room.Index & 0xff);
                        QuickstartData.season = (byte)roomEditor.Season;
                        QuickstartData.x = 0x48;
                        QuickstartData.y = 0x48;
                        QuickstartData.Enabled = value;
                    });
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Debug"))
            {
                ImGuiX.MenuItemCheckbox("Debug Window", ref showDebugWindow);
                ImGuiX.MenuItemCheckbox("ImGui Demo Window", ref showImGuiDemoWindow);
                ImGui.EndMenu();
            }
        }


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

    // ================================================================================
    // Private methods
    // ================================================================================
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
