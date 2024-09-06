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
    public ProjectWorkspace(TopLevel topLevel, Project project)
    {
        this.TopLevel = topLevel;
        this.Project = project;

        QuickstartData.x = 0x48;
        QuickstartData.y = 0x48;

        foreach (Tileset tileset in Project.GetAllTilesets())
        {
            tileset.LazyTileRedraw(topLevel.LazyInvoke);
        }

        tilesetImageCacher = new TilesetImageCacher(this);
        roomImageCacher = new RoomImageCacher(this);
        mapImageCacher = new MapImageCacher(this);

        linkImage = TopLevel.ImageFromBitmap(project.LinkBitmap);
        roomEditor = new RoomEditor(this);
        dungeonEditor = new DungeonEditor(this);
        buildDialog = new BuildDialog(this);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    RoomEditor roomEditor;
    DungeonEditor dungeonEditor;
    Image linkImage;
    BuildDialog buildDialog;

    TilesetImageCacher tilesetImageCacher;
    RoomImageCacher roomImageCacher;
    MapImageCacher mapImageCacher;

    Process emulatorProcess;

    // ================================================================================
    // Properties
    // ================================================================================
    public TopLevel TopLevel { get; private set; }
    public Project Project { get; private set; }
    public GlobalConfig GlobalConfig { get { return TopLevel.GlobalConfig; } }
    public QuickstartData QuickstartData { get; set; } = new QuickstartData();

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render()
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
            }
        }

        ImGui.Begin("Room Editor");
        roomEditor.Render();
        ImGui.End();

        ImGui.Begin("Dungeon Editor");
        dungeonEditor.Render();
        ImGui.End();

        if (buildDialog.Visible)
        {
            if (ImGuiX.Begin("Build", () => buildDialog.Close()))
            {
                buildDialog.Render();
            }
            ImGui.End();
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

public class QuickstartData
{
    public byte group, room, season, y, x;
}
