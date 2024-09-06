namespace LynnaLab;

/// <summary>
/// A widget whose size is precisely defined.
/// </summary>
public abstract class SizedWidget
{
    // ================================================================================
    // Properties
    // ================================================================================
    public abstract Vector2 WidgetSize { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Call this just before rendering begins to set up some helper stuff.
    /// </summary>
    public void RenderPrep()
    {
        origin = ImGui.GetCursorScreenPos();
        drawList = ImGui.GetWindowDrawList();
    }

    /// <summary>
    /// Get the position of the mouse relative to the widget origin
    /// </summary>
    public Vector2 GetRelativeMousePos()
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
            ImGuiX.ToImGuiColor(color),
            0,
            0,
            thickness);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    protected Vector2 origin;
    protected ImDrawListPtr drawList;
}
