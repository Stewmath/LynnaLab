using System;
using System.IO;
using ImGuiNET;
using LynnaLib;

namespace LynnaLab
{
    public class TopLevel
    {
        public TopLevel(IBackend backend, string path = "", string game = "seasons")
        {
            this.backend = backend;

            oraclesFont = ImGuiHelper.LoadFont(
                Util.Helper.GetResourceStream("LynnaLab.ZeldaOracles.ttf"), 20);

            backend.RecreateFontTexture();

            if (path != "")
                OpenProject(path, game);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        IBackend backend;
        ImFontPtr oraclesFont;

        Image linkImage;
        RoomLayoutEditor viewer;

        bool showImGuiDemoWindow = true;
        byte room;

        // ================================================================================
        // Properties
        // ================================================================================

        public Project Project
        {
            get; private set;
        }

        public IBackend Backend { get { return backend; } }

        // ================================================================================
        // Public methods
        // ================================================================================

        public void Render()
        {
            ImGui.PushFont(oraclesFont);

            Widget.Image(linkImage, scale:3);

            viewer.Render();

            ImGui.Text("Hello, world!");

            Widget.InputByte("Room", ref room);
            if (room < 0)
                room = 0;

            ImGui.PopFont();

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
            viewer = new RoomLayoutEditor(this);
            viewer.SetRoomLayout(Project.GetIndexedDataType<Room>(0x100).GetLayout(-1));
        }
    }
}
