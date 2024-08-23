using System;
using System.Collections.Generic;
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
        Dictionary<Bitmap, Image> imageDict = new Dictionary<Bitmap, Image>();

        RoomEditor roomEditor;

        Image linkImage;

        bool showImGuiDemoWindow = false;

        // ================================================================================
        // Properties
        // ================================================================================

        public Project Project
        {
            get; private set;
        }

        public IBackend Backend
        {
            get
            {
                return backend;
            }
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public void Render()
        {
            if (Project == null)
                return;

            ImGui.PushFont(oraclesFont);

            {
                ImGui.Begin("Control Panel");

                ImGui.Checkbox("Demo Window".AsSpan(), ref showImGuiDemoWindow);

                int roomIndex = roomEditor.Room.Index;
                ImGui.InputInt("Room", ref roomIndex);
                if (roomIndex >= 0 && roomIndex < Project.NumRooms &&
                    roomIndex != roomEditor.Room.Index)
                {
                    var room = Project.GetIndexedDataType<Room>(roomIndex);
                    roomEditor.SetRoom(room);
                }

                ImGui.End();
            }

            {
                ImGui.Begin("Room Editor");
                roomEditor.Render();
                ImGui.End();
            }

            ImGui.PopFont();

            if (showImGuiDemoWindow)
            {
                ImGui.ShowDemoWindow(ref showImGuiDemoWindow);
            }
        }

        /// <summary>
        /// Turns a Bitmap (cpu) into an Image (gpu), or looks up the existing Image if one exists
        /// in the cache for that Bitmap already.
        /// </summary>
        public Image ImageFromBitmap(Bitmap bitmap)
        {
            Image image;
            if (imageDict.TryGetValue(bitmap, out image))
                return image;

            image = backend.ImageFromBitmap(bitmap);
            imageDict[bitmap] = image;
            return image;
        }

        public void UnregisterBitmap(Bitmap bitmap)
        {
            if (!imageDict.Remove(bitmap))
                throw new Exception("Bitmap to remove did not exist in imageDict.");
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
            roomEditor = new RoomEditor(this);
        }
    }
}
