using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

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
        return ImGui.GetColorU32(
            new Vector4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f));
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

    public static void ShiftCursorScreenPos(float x, float y)
    {
        ShiftCursorScreenPos(new Vector2(x, y));
    }

    /// <summary>
    /// Moves the cursor such that the given widget will be centered horizontally if drawn
    /// immediately after this call
    /// </summary>
    public static void CenterHorizontal(SizedWidget widget)
    {
        var cursor = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();

        float x = cursor.X;
        float y = cursor.Y;
        x += (avail.X - widget.WidgetSize.X) / 2;

        ImGui.SetCursorScreenPos(new Vector2(x, y));
    }

    // ================================================================================
    // Custom widgets
    // ================================================================================

    /// <summary>
    /// Convenience method for rendering images
    /// </summary>
    public static void DrawImage(Image image, float scale = 1.0f,
                                 Vector2? topLeft = null, Vector2? bottomRight = null, float alpha = 1.0f)
    {
        if (bottomRight == null)
        {
            topLeft = new Vector2(0, 0);
            bottomRight = new Vector2(image.Width, image.Height);
        }
        Vector2 drawSize = (Vector2)(bottomRight - topLeft);
        Vector2 totalSize = new Vector2(image.Width, image.Height);
        ImGui.Image(image.GetBinding(alpha: alpha), drawSize * scale,
                    (Vector2)topLeft / totalSize, (Vector2)bottomRight / totalSize);
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
    /// A checkbox with a callback
    /// </summary>
    public static void Checkbox(string name, bool initial, Action<bool> onChanged)
    {
        bool value = initial;
        bool changed = ImGui.Checkbox(name, ref value);
        if (changed)
            onChanged(value);
    }

    /// <summary>
    /// Color picker with a callback
    /// </summary>
    public static void ColorEdit(string name, Vector4 initial, Action<Vector4> onChanged)
    {
        if (ImGui.ColorEdit4(
            name,
            ref initial,
            ImGuiColorEditFlags.NoInputs
            | ImGuiColorEditFlags.NoLabel
            | ImGuiColorEditFlags.NoAlpha
        ))
        {
            onChanged(initial);
        }
    }

    /// <summary>
    /// Hex input field, takes a ref int. Returns true if value was changed.
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
    /// Hex input field, takes an initial value + callback function.
    /// </summary>
    public static void InputHex(string name, int initial, Action<int> changed,
                                int digits = 2, int min = 0, int max = int.MaxValue)
    {
        int value = initial;
        if (InputHex(name, ref value, digits, min, max))
            changed(value);
    }

    /// <summary>
    /// Hex input field, takes a property accessor.
    /// </summary>
    public static void InputHex(string name, Accessor<int> accessor,
                                int digits = 2, int min = 0, int max = int.MaxValue)
    {
        int value = accessor.Get();
        bool changed = InputHex(name, ref value, digits, min, max);
        if (changed)
            accessor.Set(value);
    }

    /// <summary>
    /// Menu item that behaves as a checkbox
    /// </summary>
    public static bool MenuItemCheckbox(string name, ref bool value)
    {
        return ImGui.MenuItem(name, null, ref value);
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

    /// <summary>
    /// Set the Drag/Drop payload with an unmanaged type (ie. an int, or a struct which does not
    /// point to any managed types)
    /// </summary>
    public static unsafe void SetDragDropPayload<T>(string type, T payload) where T : unmanaged
    {
        IntPtr ptr = (IntPtr)(&payload);
        ImGui.SetDragDropPayload(type, ptr, (uint)sizeof(T));
    }

    public static unsafe T? AcceptDragDropPayload<T>(string type) where T : unmanaged
    {
        var payload = ImGui.AcceptDragDropPayload(type);
        if (payload.NativePtr == null)
            return null;
        Debug.Assert(payload.DataSize == sizeof(T));

        IntPtr ptr = (IntPtr)payload.Data;
        return Marshal.PtrToStructure<T>(ptr);
    }

    /// <summary>
    /// Same as Text() but centers the text in the available region horizontally. Not for multiline
    /// text.
    /// </summary>
    public static void TextCentered(string text)
    {
        var avail = ImGui.GetContentRegionAvail();
        var size = ImGui.CalcTextSize(text);
        var shiftVector = new Vector2(((avail - size) / 2).X, 0.0f);
        ShiftCursorScreenPos(shiftVector);
        ImGui.Text(text);
        ShiftCursorScreenPos(-shiftVector);
    }

    public const float TOOLTIP_WINDOW_WIDTH = 500.0f;

    /// <summary>
    /// A tooltip with a standardized window size
    /// </summary>
    public static void Tooltip(string text)
    {
        var textSize = ImGui.CalcTextSize(text);

        if (textSize.X > TOOLTIP_WINDOW_WIDTH)
            ImGui.SetNextWindowSize(new Vector2(TOOLTIP_WINDOW_WIDTH, 0.0f));
        else
            ImGui.SetNextWindowSize(new Vector2(textSize.X + 20.0f, 0.0f));
        ImGui.BeginTooltip();
        ImGui.TextWrapped(text);
        ImGui.EndTooltip();
    }

    /// <summary>
    /// Contains the code for checking whether to show the tooltip. Should replace uses of the above
    /// function with this in most cases.
    /// </summary>
    public static void TooltipOnHover(string text)
    {
        if (!ImGui.IsItemHovered())
            return;
        Tooltip(text);
    }
}
