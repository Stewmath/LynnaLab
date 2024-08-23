using System;
using System.Numerics;
using ImGuiNET;

namespace LynnaLab
{
    public abstract class Widget
    {
        // ================================================================================
        // Public methods
        // ================================================================================
        public abstract void Render();

        // ================================================================================
        // Static methods
        // ================================================================================

        /// <summary>
        ///  Byte input field
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
        public static void Image(Image image, int scale = 1)
        {
            ImGui.Image(image.GetBinding(), new Vector2(image.Width * scale, image.Height * scale));
        }
    }
}
