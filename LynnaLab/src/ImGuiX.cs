using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace LynnaLab;

/// <summary>
/// Helper functions for ImGui usage
/// </summary>
public static class ImGuiX
{
    // ================================================================================
    // Variables
    // ================================================================================

    static ImGuiStyle backupStyle;

    static Stack<float> alphaStack = new Stack<float>();
    static Stack<Interpolation> interpolationStack = new Stack<Interpolation>();

    // ================================================================================
    // Properties
    // ================================================================================

    /// <summary>
    /// Use this for sizing windows such that they can be scaled by changing this value.
    /// </summary>
    public static float ScaleUnit
    {
        get
        {
            if (Top.GlobalConfig.OverrideSystemScaling)
                return Math.Max(Top.GlobalConfig.DisplayScaleFactor, 1.0f);
            else
                return Top.Backend.WindowDisplayScale;
        }
    }


    public static float TOOLTIP_WINDOW_WIDTH { get { return Unit(500.0f); } }


    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Multiply input value by the ScaleUnit, for DPI-independent positioning.
    /// </summary>
    public static float Unit(float value)
    {
        return value * ScaleUnit;
    }

    /// <summary>
    /// Multiply input vector by the ScaleUnit, for DPI-independent positioning.
    /// </summary>
    public static Vector2 Unit(Vector2 value)
    {
        return value * ScaleUnit;
    }

    /// <summary>
    /// Multiply input vector by the ScaleUnit, for DPI-independent positioning.
    /// </summary>
    public static Vector2 Unit(float x, float y)
    {
        return new Vector2(x, y) * ScaleUnit;
    }

    /// <summary>
    /// Save initial style information so that it can be reset and rescaled later.
    /// </summary>
    public unsafe static void BackupStyle()
    {
        ImGuiStyle* stylePtr = ImGui.GetStyle();
        backupStyle = *stylePtr;
    }

    /// <summary>
    /// Call this after updating scale ratio or style.
    /// </summary>
    public unsafe static void UpdateStyle()
    {
        ImGuiStylePtr stylePtr = ImGui.GetStyle();
        *(ImGuiStyle*)stylePtr = backupStyle;
        stylePtr.ScaleAllSizes(ScaleUnit);

        if (Top.GlobalConfig.LightMode)
            ImGui.StyleColorsLight();
        else
            ImGui.StyleColorsDark();
    }

    public static unsafe ImFontPtr LoadFont(string filename, int size)
    {
        string path = Top.FontDir + filename;
        return ImGui.GetIO().Fonts.AddFontFromFileTTF(path, size);
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

    /// <summary>
    /// Shift cursor position by the specified amount. Should remain within the current window
    /// boundaries (if not, you must draw something there like "ImGui.Dummy()" to force the window
    /// to extend in size or ImGui will error out)
    /// </summary>
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
    /// All subsequent images will have this alpha (transparency) applied to them when drawn
    /// </summary>
    public static void PushAlpha(float alpha)
    {
        alphaStack.Push(alpha);
        AddCallback<float>(ImGuiXCallback.SetAlpha, alpha);
    }

    public static void PopAlpha()
    {
        alphaStack.Pop();
        float alpha = 1.0f;
        if (alphaStack.Count != 0)
            alpha = alphaStack.Peek();
        AddCallback<float>(ImGuiXCallback.SetAlpha, alpha);
    }

    /// <summary>
    /// All subsequent images will be rendered with this interpolation mode
    /// </summary>
    public static void PushInterpolation(Interpolation mode)
    {
        interpolationStack.Push(mode);
        AddCallback<int>(ImGuiXCallback.SetInterpolation, (int)mode);
    }

    public static void PopInterpolation()
    {
        interpolationStack.Pop();
        Interpolation mode = Interpolation.Nearest; // Default interpolation mode
        if (interpolationStack.Count != 0)
            mode = interpolationStack.Peek();
        AddCallback<int>(ImGuiXCallback.SetInterpolation, (int)mode);
    }

    // ================================================================================
    // Function wrappers and widgets
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
    /// ImGui.NET's wrapper for BeginTabItem doesn't support passing "null" in the p_open parameter,
    /// so we must have a custom implementation to support that.
    /// See: https://github.com/ImGuiNET/ImGui.NET/issues/495
    /// </summary>
    public static unsafe bool BeginTabItem(string label, ImGuiTabItemFlags flags)
    {
        byte* native_label = (byte*)Marshal.StringToCoTaskMemUTF8(label);
        byte ret;
        ret = ImGuiNative.igBeginTabItem(native_label, (byte*)0, flags);
        Marshal.FreeCoTaskMem((nint)native_label);
        return ret != 0;
    }

    /// <summary>
    /// Alternate version of ImGui.Combo() which takes the name of the selected value, rather than
    /// just an index.
    /// </summary>
    public static bool Combo(string name, Accessor<string> selection, IEnumerable<string> options)
    {
        bool retval = false;
        string oldSelection = selection.Get();
        if (ImGui.BeginCombo(name, oldSelection))
        {
            foreach (string v in options)
            {
                bool selected = v == oldSelection;
                if (ImGui.Selectable(v, selected))
                {
                    selection.Set(v);
                    retval = true;
                }
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        return retval;
    }

    public static bool SliderInt(string name, Accessor<int> accessor, int min, int max)
    {
        int value = accessor.Get();
        bool retval = ImGui.SliderInt(name, ref value, min, max);
        if (retval)
            accessor.Set(value);
        return retval;
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
        ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload(type);
        if (payload.NativePtr == null)
            return null;
        Debug.Assert(payload.DataSize == sizeof(T));

        IntPtr ptr = (IntPtr)payload.Data;
        return Marshal.PtrToStructure<T>(ptr);
    }

    public static unsafe void AddCallback<T>(ImGuiXCallback callbackType, T payload) where T : unmanaged
    {
        IntPtr ptr = Marshal.AllocHGlobal(sizeof(T));
        Marshal.StructureToPtr(payload, ptr, false);
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        drawList.AddCallback((nint)callbackType, ptr);
    }

    /// <summary>
    /// Convenience method for rendering images
    /// </summary>
    public static void DrawImage(TextureBase texture, float scale = 1.0f, Vector2? topLeft = null, Vector2? bottomRight = null)
    {
        if (bottomRight == null)
        {
            topLeft = new Vector2(0, 0);
            bottomRight = new Vector2(texture.Width, texture.Height);
        }
        Vector2 drawSize = (Vector2)(bottomRight - topLeft);
        Vector2 totalSize = new Vector2(texture.Width, texture.Height);
        ImGui.Image(texture.GetBinding(), drawSize * scale,
                    (Vector2)topLeft / totalSize, (Vector2)bottomRight / totalSize);
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
    public static void MenuItemCheckbox(string name, Accessor<bool> accessor, Action<bool> onChanged = null)
    {
        bool value = accessor.Get();
        bool changed = ImGui.MenuItem(name, null, ref value);
        if (changed)
        {
            accessor.Set(value);
            onChanged?.Invoke(value);
        }
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
    /// Same as Text() but centers the text in the available region horizontally. Not for multiline
    /// text.
    /// </summary>
    public static void TextCentered(string text)
    {
        var avail = ImGui.GetContentRegionAvail();
        var size = ImGui.CalcTextSize(text);

        if (size.X < avail.X)
        {
            var shiftVector = new Vector2(((avail - size) / 2).X, 0.0f);
            ShiftCursorScreenPos(shiftVector);
        }

        ImGui.Text(text);
    }

    /// <summary>
    /// Draws a line of text centered at the given position.
    /// As this is meant to be used for rendering stuff, it always renders text in white, rather
    /// than respecting the current style.
    /// </summary>
    public static void DrawTextAt(string text, Vector2 pos)
    {
        Vector2 textSize = ImGui.CalcTextSize(text);
        ImGui.SetCursorScreenPos(pos - textSize / 2 + new Vector2(1, 0));
        ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), text);
    }

    /// <summary>
    /// A tooltip with a standardized window size
    /// </summary>
    public static void Tooltip(string text)
    {
        ImGui.PushFont(Top.InfoFont);

        var textSize = ImGui.CalcTextSize(text);

        if (textSize.X > TOOLTIP_WINDOW_WIDTH)
            ImGui.SetNextWindowSize(new Vector2(TOOLTIP_WINDOW_WIDTH, 0.0f));
        else
            ImGui.SetNextWindowSize(new Vector2(textSize.X + Unit(20.0f), 0.0f));
        ImGui.BeginTooltip();
        ImGui.TextWrapped(text);
        ImGui.EndTooltip();
        ImGui.PopFont();
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

    /// <summary>
    /// Begin a docked window. Must manually specify the position and size of the window. Used for
    /// toolbars and other things.
    /// </summary>
    public static bool BeginDocked(string name, Vector2 offset, Vector2 size, bool scrollbar=true, bool front=false)
    {
        var mainViewport = ImGui.GetMainViewport();

        if (size.X == 0)
            size.X = mainViewport.Size.X - offset.X;
        if (size.Y == 0)
            size.Y = mainViewport.Size.Y - offset.Y;

        ImGui.SetNextWindowPos(mainViewport.Pos + offset);
        ImGui.SetNextWindowSize(size);
        ImGui.SetNextWindowViewport(mainViewport.ID);

        ImGuiWindowFlags window_flags = 0
            | ImGuiWindowFlags.NoDocking
            | ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoNavFocus
            ;

        if (!scrollbar)
            window_flags |= ImGuiWindowFlags.NoScrollbar;
        if (!front)
            window_flags |= ImGuiWindowFlags.NoBringToFrontOnFocus;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        bool retval = ImGui.Begin(name, window_flags);
        ImGui.PopStyleVar();
        return retval;
    }

    /// <summary>
    /// A button which stays pressed down. Imitates the style of builtin ImGui buttons as closely as possible.
    /// </summary>
    public static bool ToggleImageButton(string name, nint textureID, Vector2 size, ref bool toggled)
    {
        // ImageButtons add FramePadding * 2 to size, so we do that too
        Vector2 framePadding = ImGui.GetStyle().FramePadding;
        Vector2 buttonSize = size + framePadding * 2;

        ImGui.BeginGroup();

        Vector2 startPos = ImGui.GetCursorScreenPos();
        bool clicked = ImGui.InvisibleButton("Btn: " + name, buttonSize);
        Vector2 finalPos = ImGui.GetCursorScreenPos();

        if (clicked)
            toggled = !toggled;
        bool hovered = ImGui.IsItemHovered();
        bool mouseDown = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);

        // Draw rectangle background, imitating colors used by built-in buttons
        uint rectColor;
        if (hovered && !mouseDown)
            rectColor = ImGui.GetColorU32(ImGuiCol.ButtonHovered);
        else if (toggled || mouseDown)
            rectColor = ImGui.GetColorU32(ImGuiCol.ButtonActive);
        else
            rectColor = ImGui.GetColorU32(ImGuiCol.Button);

        ImGui.GetWindowDrawList().AddRectFilled(startPos, startPos + buttonSize, rectColor);

        ImGui.SetCursorScreenPos(startPos + framePadding);
        ImGui.Image(textureID, size);

        ImGui.EndGroup();
        return clicked;
    }

    public static void ToggleImageButton(string name, nint textureID, Vector2 size, bool toggled, Action<bool> onToggled)
    {
        if (ToggleImageButton(name, textureID, size, ref toggled))
            onToggled(toggled);
    }
}

public enum ImGuiXCallback
{
    SetAlpha = 1,
    SetInterpolation = 2,
}
