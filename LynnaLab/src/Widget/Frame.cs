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

            string name = DisplayName == null ? Name : $"{DisplayName}###{Name}";
            if (ImGui.Begin(name, ref active, WindowFlags))
            {
                Render();
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
