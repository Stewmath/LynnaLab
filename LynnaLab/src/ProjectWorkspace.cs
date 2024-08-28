using ImGuiNET;
using LynnaLib;

namespace LynnaLab
{
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
        }

        // ================================================================================
        // Variables
        // ================================================================================

        RoomEditor roomEditor;
        DungeonEditor dungeonEditor;
        Image linkImage;

        TilesetImageCacher tilesetImageCacher;
        RoomImageCacher roomImageCacher;
        MapImageCacher mapImageCacher;

        // ================================================================================
        // Properties
        // ================================================================================
        public TopLevel TopLevel { get; private set; }
        public Project Project { get; private set; }

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
                }
            }

            {
                ImGui.Begin("Room Editor");
                roomEditor.Render();
                ImGui.End();

                ImGui.Begin("Dungeon Editor");
                dungeonEditor.Render();
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

        // ================================================================================
        // Private methods
        // ================================================================================
    }
}
