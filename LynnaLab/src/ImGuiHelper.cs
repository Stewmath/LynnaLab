using System;
using System.IO;
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

    }
}
