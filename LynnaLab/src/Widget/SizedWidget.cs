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

    // Whether to center the widget within the available region
    public bool CenterX { get; set; }
    public bool CenterY { get; set; }

    // Leave this much space above and to the left of the start of the render area.
    // Usually this is (0,0).
    public Vector2 RenderOffset { get; set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Call this just before rendering begins to set up some helper stuff. ImGui cursor position
    /// should be at the start of the render area when this is called.
    /// </summary>
    public void RenderPrep()
    {
        ImGuiX.ShiftCursorScreenPos(RenderOffset);
        var cursor = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();

        float x = cursor.X;
        float y = cursor.Y;
        if (CenterX)
            x += (avail.X - WidgetSize.X) / 2;
        if (CenterY)
            y += (avail.Y - WidgetSize.Y) / 2;

        origin = new Vector2(x, y);
        ImGui.SetCursorScreenPos(origin);
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
            color.ToUInt(),
            0,
            0,
            thickness);
    }

    public void AddRectFilled(FRect rect, Color color)
    {
        drawList.AddRectFilled(
            origin + new Vector2(rect.X, rect.Y),
            origin + new Vector2(rect.X + rect.Width, rect.Y + rect.Height),
            color.ToUInt());
    }

    // ================================================================================
    // Variables
    // ================================================================================
    protected Vector2 origin;
    protected ImDrawListPtr drawList;
}
