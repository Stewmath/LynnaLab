#nullable disable
namespace LynnaLab;

/// <summary>
/// A widget whose size is precisely defined.
///
/// Most of the properties or methods in this class are not valid in any way unless RenderPrep() has
/// been called beforehand, mainly because it needs to record the value of "origin" to do any
/// further calculations.
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
    // Usually this is (0,0). Specify this in coordinates BEFORE global scaling.
    public Vector2 RenderOffset { get; set; }

    // Following properties are only valid after RenderPrep() is called.

    public Vector2 Origin { get { return origin; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Call this just before rendering begins to set up some helper stuff. ImGui cursor position
    /// should be at the start of the render area when this is called.
    /// </summary>
    public void RenderPrep()
    {
        ImGuiX.ShiftCursorScreenPos(RenderOffset * ImGuiX.ScaleUnit);
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

    // All methods below here are only allowed to be called during rendering, and sometime after
    // RenderPrep() has been called while rendering the current frame.

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
            thickness,
            0);
    }

    public void AddRectFilled(FRect rect, Color color)
    {
        drawList.AddRectFilled(
            origin + new Vector2(rect.X, rect.Y),
            origin + new Vector2(rect.X + rect.Width, rect.Y + rect.Height),
            color.ToUInt());
    }

    public unsafe void AddLine(Vector2 p1, Vector2 p2, Color color, float thickness = 1.0f)
    {
        drawList.AddLine(origin + p1, origin + p2, color.ToUInt(), thickness);
    }

    public unsafe void AddPolyline(Polygon polygon, Color color, float thickness)
    {
        var coords = (polygon + origin).Coordinates.ToArray();
        fixed (Vector2* v = coords)
        {
            drawList.AddPolyline(in *v,
                polygon.Coordinates.Count,
                color.ToUInt(),
                thickness,
                ImDrawFlags.Closed);
        }
    }

    public unsafe void AddConcavePolyFilled(Polygon polygon, Color color)
    {
        var coords = (polygon + origin).Coordinates.ToArray();
        fixed (Vector2* v = coords)
        {
            drawList.AddConcavePolyFilled(in *v, polygon.Coordinates.Count, color.ToUInt());
        }
    }

    // ================================================================================
    // Variables
    // ================================================================================
    protected Vector2 origin;
    protected ImDrawListPtr drawList;
}
