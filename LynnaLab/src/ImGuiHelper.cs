using System;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace LynnaLab
{
    public static class ImGuiHelper
    {
        public static unsafe ImFontPtr LoadFont(Stream stream, int size)
        {
            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);

            fixed (byte *ptr = data)
            {
                return ImGui.GetIO().Fonts.AddFontFromMemoryTTF((IntPtr)ptr, data.Length, size);
            }
        }

        public static uint ToImGuiColor(LynnaLib.Color color)
        {
            return ImGui.GetColorU32(new Vector4(color.R, color.G, color.B, color.A));
        }
    }
}
