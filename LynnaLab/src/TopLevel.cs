using System;
using ImGuiNET;
using LynnaLib;

namespace LynnaLab
{
    public class TopLevel
    {
        public TopLevel(string path = "", string game = "seasons")
        {
            if (path != "")
                OpenProject(path, game);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        bool showImGuiDemoWindow = true;
        byte room;


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
        }
    }
}
