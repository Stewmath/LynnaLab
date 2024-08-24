using System;
using System.Numerics;
using ImGuiNET;

using FRect = Util.FRect;
using Color = LynnaLib.Color;

namespace LynnaLab
{
    public abstract class Widget
    {
        // ================================================================================
        // Properties
        // ================================================================================
        public abstract Vector2 WidgetSize { get; }

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
        public Vector2 GetMousePos()
        {
            var io = ImGui.GetIO();
            var mousePos = io.MousePos - origin;
            return new Vector2((int)mousePos.X, (int)mousePos.Y);
        }

        public void AddRect(FRect rect, Color color, float thickness = 1.0f)
        {
            drawList.AddRect(
                origin + new Vector2(rect.X, rect.Y),
                origin + new Vector2(rect.X + rect.Width, rect.Y + rect.Height),
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
        /// Hex input field. Returns true if value was changed.
        /// </summary>
        public static unsafe bool InputHex(string name, ref int value, int digits = 2)
        {
            int v = value;
            int step = 1;
            int stepFast = 16;
            ImGui.InputScalar(name, ImGuiDataType.S32, (IntPtr)(&v),
                              (IntPtr)(&step), (IntPtr)(&stepFast), $"%0{digits}X",
                              ImGuiInputTextFlags.CharsHexadecimal);

            if (value == v)
                return false;
            value = v;
            return true;
        }

        /// <summary>
        /// Convenience method for rendering images
        /// </summary>
        public static void DrawImage(Image image, float scale = 1.0f)
        {
            ImGui.Image(image.GetBinding(), new Vector2(image.Width * scale, image.Height * scale));
        }
    }
}
