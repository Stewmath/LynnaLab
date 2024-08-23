using System;
using System.Numerics;
using ImGuiNET;

using Point = Cairo.Point;
using Rect = Cairo.Rectangle;
using Color = LynnaLib.Color;

namespace LynnaLab
{
    /// <summary>
    /// Represents any kind of tile-based grid. It can be hovered over with the mouse, and
    /// optionally allows one to select tiles by clicking, or define actions to occur with other
    /// mouse buttons.
    /// </summary>
    public class TileGridViewer : Widget
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public TileGridViewer()
        {

        }

        // ================================================================================
        // Variables
        // ================================================================================

        // ================================================================================
        // Properties
        // ================================================================================

        // Number of tiles on each axis
        public int Width { get; protected set; }
        public int Height { get; protected set; }

        // Size of tiles on each axis
        public int TileWidth { get; protected set; }
        public int TileHeight { get; protected set; }

        public int HoveringIndex { get; private set; }
        public int Scale { get; set; } = 1;

        public Color HoverColor { get; protected set; } = Color.Red;

        public int CanvasWidth
        {
            get
            {
                return TileWidth * Width * Scale;
            }
        }
        public int CanvasHeight
        {
            get
            {
                return TileHeight * Height * Scale;
            }
        }

        protected virtual Image Image { get { return null; } }

        // ================================================================================
        // Public methods
        // ================================================================================
        public override void Render()
        {
            base.Render();

            Point mousePos = base.GetMousePos();

            if (Image != null)
            {
                Widget.DrawImage(Image, scale: Scale);

                if (ImGui.IsItemHovered())
                {
                    int pos = CoordToTile(mousePos);
                    if (pos != -1)
                    {
                        Rect r = TileRect(pos);

                        base.AddRect(r, HoverColor, 3);
                    }
                }
            }
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Converts a mouse position in pixels to a tile index.
        /// </summary>
        int CoordToTile(Point pos)
        {
            if (pos.X < 0 || pos.Y < 0 || pos.X >= CanvasWidth || pos.Y >= CanvasHeight)
                return -1;

            return (pos.Y / (TileHeight * Scale)) * Width + (pos.X / (TileWidth * Scale));
        }

        /// <summary>
        /// Gets the bounds of a tile in a rectangle.
        /// </summary>
        Rect TileRect(int tileIndex)
        {
            int x = tileIndex % Width;
            int y = tileIndex / Width;

            Point tl = new Point(x * TileWidth * Scale, y * TileHeight * Scale);
            return new Rect(tl.X, tl.Y, TileWidth * Scale, TileHeight * Scale);
        }
    }
}
