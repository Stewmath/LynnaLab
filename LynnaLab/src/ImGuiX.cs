using System.IO;

namespace LynnaLab;

/// <summary>
/// Helper functions for ImGui usage
/// </summary>
public static class ImGuiX
{
    public static unsafe ImFontPtr LoadFont(Stream stream, int size)
    {
        byte[] data = new byte[stream.Length];
        stream.Read(data, 0, (int)stream.Length);

        fixed (byte* ptr = data)
        {
            return ImGui.GetIO().Fonts.AddFontFromMemoryTTF((IntPtr)ptr, data.Length, size);
        }
    }

    public static uint ToImGuiColor(LynnaLib.Color color)
    {
        return ImGui.GetColorU32(new Vector4(color.R, color.G, color.B, color.A));
    }

    public static Vector2 GetScroll()
    {
        return new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
    }

    public static void SetScroll(Vector2 scroll)
    {
        ImGui.SetScrollX(scroll.X);
        ImGui.SetScrollY(scroll.Y);
    }

    public static void ShiftCursorScreenPos(Vector2 delta)
    {
        var pos = ImGui.GetCursorScreenPos() + delta;
        ImGui.SetCursorScreenPos(pos);
    }

    // ================================================================================
    // Custom widgets
    // ================================================================================

    /// <summary>
    /// Convenience method for rendering images
    /// </summary>
    public static void DrawImage(Image image, float scale = 1.0f)
    {
        ImGui.Image(image.GetBinding(), new Vector2(image.Width, image.Height) * scale);
    }

    // ================================================================================
    // Function wrappers
    // ================================================================================

    /// <summary>
    /// Like ImGui.Begin but takes a callback for something to do when window is closed
    /// </summary>
    public static bool Begin(string name, Action onClose)
    {
        bool value = true;
        bool retval = ImGui.Begin(name, ref value);
        if (!value)
            onClose();
        return retval;
    }

    /// <summary>
    /// A checkbox which takes a boolean property as a parameter, wrapped through the Accessor
    /// class
    /// </summary>
    public static bool Checkbox(string name, Accessor<bool> accessor)
    {
        bool value = accessor.Get();
        bool changed = ImGui.Checkbox(name, ref value);
        if (changed)
            accessor.Set(value);
        return changed;
    }

    /// <summary>
    /// Hex input field. Returns true if value was changed.
    /// </summary>
    public static unsafe bool InputHex(string name, ref int value,
                                       int digits = 2, int min = 0, int max = int.MaxValue)
    {
        int v = value;
        int step = 1;
        int stepFast = 16;
        ImGui.InputScalar(name, ImGuiDataType.S32, (IntPtr)(&v),
                          (IntPtr)(&step), (IntPtr)(&stepFast), $"%0{digits}X",
                          ImGuiInputTextFlags.CharsHexadecimal);

        if (value == v || v < min || v > max)
            return false;
        value = v;
        return true;
    }

    /// <summary>
    /// Like above, but takes an initial value + callback function instead of a ref int
    /// </summary>
    public static void InputHex(string name, int initial, Action<int> changed,
                                int digits = 2, int min = 0, int max = int.MaxValue)
    {
        int value = initial;
        if (InputHex(name, ref value, digits, min, max))
            changed(value);
    }

    /// <summary>
    /// Menu item that behaves as a checkbox, with an accessor for the value
    /// </summary>
    public static void MenuItemCheckbox(string name, Accessor<bool> accessor)
    {
        bool value = accessor.Get();
        bool changed = ImGui.MenuItem(name, null, ref value);
        if (changed)
            accessor.Set(value);
    }

    /// <summary>
    /// Menu item that behaves as a checkbox, with a callback
    /// </summary>
    public static void MenuItemCheckbox(string name, bool initial, Action<bool> onChanged)
    {
        bool value = initial;
        bool changed = ImGui.MenuItem(name, null, ref value);
        if (changed)
            onChanged(value);
    }
}
