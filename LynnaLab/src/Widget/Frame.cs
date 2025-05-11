namespace LynnaLab;

/// <summary>
/// A Frame is an imgui widget that can be displayed as a separate window, or potentially embedded
/// in another window if the Render() function is used by itself
/// </summary>
public abstract class Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public Frame(string name)
    {
        Name = name;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    bool active;
    bool grabFocus;

    Vector2 lastWindowSize = Vector2.Zero;
    float lastScaleFactor = 0.0f;

    // ================================================================================
    // Events
    // ================================================================================

    public event EventHandler ClosedEvent;

    // ================================================================================
    // Properties
    // ================================================================================

    /// <summary>
    /// True if window is active (not closed)
    /// </summary>
    public bool Active
    {
        get
        {
            return active;
        }
        set
        {
            bool old = active;
            active = value;
            if (old && !active)
            {
                ClosedEvent?.Invoke(this, null);
            }
        }
    }

    /// <summary>
    /// Default size the first time this window is opened. Don't use ImGuiX.Unit() when setting
    /// this; that scaling happens later.
    /// </summary>
    public Vector2 DefaultSize { get; set; } = Vector2.Zero;

    // Name is the name used for ImGui's internal window tracking. DisplayName, if set, changes only
    // the name as displayed to the user.
    public string Name { get; private set; }
    public string DisplayName { get; set; }

    public ImGuiWindowFlags WindowFlags;

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Render this as a separate window (not embedded in anything)
    /// </summary>
    public void RenderAsWindow()
    {
        if (grabFocus)
            Active = true;

        if (Active)
        {
            if (grabFocus)
                ImGui.SetNextWindowFocus();

            // Set initial window size (only for the first time ever using LynnaLab; normally,
            // saved window sizes are retrieved from a .ini file)
            ImGui.SetNextWindowSize(DefaultSize * ImGuiX.ScaleUnit, ImGuiCond.FirstUseEver);

            // Auto-adjust window size when scale factor is changed
            if (lastScaleFactor != 0.0f && lastScaleFactor != ImGuiX.ScaleUnit)
            {
                float factor = ImGuiX.ScaleUnit / lastScaleFactor;
                ImGui.SetNextWindowSize(lastWindowSize * factor);
            }

            string name = DisplayName == null ? Name : $"{DisplayName}###{Name}";
            if (ImGui.Begin(name, ref active, WindowFlags))
            {
                Render();

                lastWindowSize = ImGui.GetWindowSize();
                lastScaleFactor = ImGuiX.ScaleUnit;
            }
            ImGui.End();
        }

        grabFocus = false;
    }

    /// <summary>
    /// Makes this window grab focus
    /// </summary>
    public void Focus()
    {
        grabFocus = true;
    }

    /// <summary>
    /// Render contents of the window, without concern to what context it's being rendered to
    /// </summary>
    public abstract void Render();
}
