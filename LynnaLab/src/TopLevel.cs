using System;
using ImGuiNET;
using LynnaLib;

namespace LynnaLab
{
    public class TopLevel
    {
        public TopLevel(IBackend backend, string path = "", string game = "seasons")
        {
            this.backend = backend;
            if (path != "")
                OpenProject(path, game);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        IBackend backend;
        bool showImGuiDemoWindow = true;
        byte room;

        Image linkImage;

        // ================================================================================
        // Properties
        // ================================================================================

        public Project Project
        {
            get; private set;
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public void Render()
        {
            Widget.Image(linkImage);

            ImGui.Text("Hello, world!");

            Widget.InputByte("Room", ref room);
            if (room < 0)
                room = 0;

            if (showImGuiDemoWindow)
            {
                ImGui.ShowDemoWindow(ref showImGuiDemoWindow);
            }
        }


        // ================================================================================
        // Private methods
        // ================================================================================

        void OpenProject(string path, string game)
        {
            // Try to load project config
            ProjectConfig config = ProjectConfig.Load(path);

            Project = new Project(path, game, config);

            linkImage = backend.ImageFromBitmap(Project.LinkBitmap);
        }
    }
}
