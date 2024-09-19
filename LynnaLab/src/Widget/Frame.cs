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
            active = value;
        }
    }

    public string Name { get; private set; }

    public ImGuiWindowFlags WindowFlags;

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Render this as a separate window (not embedded in anything)
    /// </summary>
    public void RenderAsWindow()
    {
        if (Active)
        {
            if (ImGui.Begin(Name, ref active, WindowFlags))
            {
                Render();
            }
            ImGui.End();
        }
    }

    /// <summary>
    /// Render contents of the window, without concern to what context it's being rendered to
    /// </summary>
    public abstract void Render();
}
