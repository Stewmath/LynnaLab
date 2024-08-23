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

        Image linkImage;
        RoomLayoutEditor roomLayoutEditor;
        TilesetViewer tilesetViewer;

        bool showImGuiDemoWindow = true;

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
            ImGui.PushFont(oraclesFont);

            {
                ImGui.Begin("Room Layout");
                roomLayoutEditor.Render();
                ImGui.End();
            }

            {
                ImGui.Begin("Tileset");
                tilesetViewer.Render();
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
            roomLayoutEditor = new RoomLayoutEditor(this);
            roomLayoutEditor.SetRoomLayout(Project.GetIndexedDataType<Room>(0x100).GetLayout(-1));

            tilesetViewer = new TilesetViewer(this);
            tilesetViewer.SetTileset(roomLayoutEditor.Room.GetTileset(-1));

            roomLayoutEditor.AddMouseAction(MouseButton.Any, MouseModifier.Any, GridAction.Callback,
            (_, args) =>
            {
                int x = args.selectedIndex % roomLayoutEditor.Width;
                int y = args.selectedIndex / roomLayoutEditor.Width;
                roomLayoutEditor.RoomLayout.SetTile(x, y, tilesetViewer.SelectedIndex);
            });
        }
    }
}
