using System;
using ImGuiNET;

namespace LynnaLab
{
    public class Widget
    {
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
    }
}
