using System;
using System.Collections.Generic;
using System.IO;
using ImGuiNET;
using LynnaLib;

namespace LynnaLab
{
    /// <summary>
    /// TopLevel class could potentially contain multiple ProjectWorkspaces in the future.
    /// </summary>
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

        bool showImGuiDemoWindow = false;

        // ================================================================================
        // Properties
        // ================================================================================

        public IBackend Backend
        {
            get
            {
                return backend;
            }
        }

        private ProjectWorkspace Workspace
        {
            get; set;
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public void Render()
        {
            ImGui.PushFont(oraclesFont);

            {
                ImGui.Begin("Control Panel");
                ImGui.Checkbox("Demo Window".AsSpan(), ref showImGuiDemoWindow);
                ImGui.End();
            }

            Workspace?.Render();

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

            var project = new Project(path, game, config);
            this.Workspace = new ProjectWorkspace(this, project);
        }
    }
}
