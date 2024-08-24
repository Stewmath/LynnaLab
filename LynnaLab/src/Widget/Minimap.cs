using System.Numerics;
using ImGuiNET;
using LynnaLib;

using Point = Cairo.Point;

namespace LynnaLab
{
    public class Minimap : TileGridViewer
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public Minimap(TopLevel topLevel)
        {
            this.topLevel = topLevel;

            base.Selectable = true;
        }

        // ================================================================================
        // Variables
        // ================================================================================
        TopLevel topLevel;
        Map map;
        Image image;
        int minimapScale = 10;

        const float MIN_SCALE = 0.1f;
        const float MAX_SCALE = 1.0f;
        const int MAX_SCALE_SLIDER = 100;

        // ================================================================================
        // Properties
        // ================================================================================

        protected override Image Image
        {
            get
            {
                return image;
            }
        }

        public Map Map { get { return map; } }

        public RoomLayout SelectedRoomLayout
        {
            get { return map.GetRoomLayout(SelectedX, SelectedY, 0); }
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public override void Render()
        {
            {
                bool scrollChanged = false;
                if (ImGui.SliderInt("Scale", ref minimapScale, 0, MAX_SCALE_SLIDER))
                    scrollChanged = true;

                base.Scale =
                    MIN_SCALE + (minimapScale / (float)MAX_SCALE_SLIDER) * (MAX_SCALE - MIN_SCALE);

                if (scrollChanged)
                {
                    CenterScroll();
                }
            }

            ImGui.BeginChild("MinimapChild", Vector2.Zero, 0, ImGuiWindowFlags.HorizontalScrollbar);
            base.Render();
            ImGui.EndChild();
        }

        /// <summary>
        /// Sets the map to display
        /// </summary>
        public void SetMap(Map map)
        {
            if (this.map == map)
                return;

            this.map = map;

            if (image != null)
            {
                image.Dispose();
                image = null;
            }

            if (map == null)
                return;

            base.TileWidth = map.RoomWidth * 16;
            base.TileHeight = map.RoomHeight * 16;
            base.Width = map.MapWidth;
            base.Height = map.MapHeight;

            GenerateImage();
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Generate the full minimap image
        /// </summary>
        void GenerateImage()
        {
            image?.Dispose();

            image = topLevel.Backend.CreateImage(
                base.ImageSize.X,
                base.ImageSize.Y,
                Interpolation.Bicubic);

            for (int x=0; x<map.MapWidth; x++)
            {
                for (int y=0; y<map.MapHeight; y++)
                {
                    Room room = map.GetRoom(x, y);
                    int season = map.Season;
                    Image roomImage = topLevel.ImageFromBitmap(room.GetLayout(season).GetImage());

                    roomImage.DrawOn(image,
                                     new Point(0, 0),
                                     new Point(x * TileWidth, y * TileHeight),
                                     new Point(TileWidth, TileHeight));
                }
            }
        }

        /// <summary>
        /// Move the scrollbar such that the selected room is in the center.
        /// Must be called before starting the scroll window.
        /// </summary>
        void CenterScroll()
        {
            var windowSize = ImGui.GetContentRegionAvail();
            var tilePos = new Vector2(
                SelectedX * TileWidth + TileWidth / 2.0f,
                SelectedY * TileHeight + TileHeight / 2.0f) * Scale;

            var scroll = tilePos - windowSize / 2;
            if (scroll.X < 0)
                scroll.X = 0;
            if (scroll.Y < 0)
                scroll.Y = 0;

            ImGui.SetNextWindowScroll(tilePos - windowSize / 2);
        }
    }
}
