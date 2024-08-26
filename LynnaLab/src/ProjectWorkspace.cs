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

            linkImage = TopLevel.ImageFromBitmap(project.LinkBitmap);
            roomEditor = new RoomEditor(this);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        RoomEditor roomEditor;
        Image linkImage;

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
            }
        }

        // ================================================================================
        // Private methods
        // ================================================================================
    }
}
