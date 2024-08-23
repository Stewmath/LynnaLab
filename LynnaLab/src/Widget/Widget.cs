using System;
using System.Numerics;
using ImGuiNET;

using Point = Cairo.Point;
using Rect = Cairo.Rectangle;
using Color = LynnaLib.Color;

namespace LynnaLab
{
    public abstract class Widget
    {
        // ================================================================================
        // Public methods
        // ================================================================================
        public virtual void Render()
        {
            origin = ImGui.GetCursorScreenPos();
            drawList = ImGui.GetWindowDrawList();
        }

        /// <summary>
        /// Get the position of the mouse relative to the widget origin
        /// </summary>
        public Point GetMousePos()
        {
            var io = ImGui.GetIO();
            var mousePos = io.MousePos - origin;
            return new Point((int)mousePos.X, (int)mousePos.Y);
        }

        public void AddRect(Rect rect, Color color, float thickness = 1.0f)
        {
            drawList.AddRect(
                origin + new Vector2((float)rect.X, (float)rect.Y),
                origin + new Vector2((float)(rect.X + rect.Width), (float)(rect.Y + rect.Height)),
                ImGuiHelper.ToImGuiColor(color),
                0,
                0,
                thickness);
        }

        // ================================================================================
        // Variables
        // ================================================================================
        protected Vector2 origin;
        protected ImDrawListPtr drawList;

        // ================================================================================
        // Static methods
        // ================================================================================

        /// <summary>
        /// Byte input field
        /// </summary>
        public static unsafe void InputByte(string name, ref byte value)
        {
            byte v = value;
            byte step = 1;
            ImGui.InputScalar("Room", ImGuiDataType.U8, (IntPtr)(&v), (IntPtr)(&step));

            if (v < 0)
                v = 0;
            if (v > 255)
                v = 255;

            value = v;
        }

        /// <summary>
        /// Convenience method for rendering images
        /// </summary>
        public static void DrawImage(Image image, int scale = 1)
        {
            ImGui.Image(image.GetBinding(), new Vector2(image.Width * scale, image.Height * scale));
        }
    }
}
