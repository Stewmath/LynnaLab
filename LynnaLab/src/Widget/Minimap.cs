namespace LynnaLab;

public class Minimap : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public Minimap(ProjectWorkspace workspace)
        : base("Minimap")
    {
        this.Workspace = workspace;

        base.Selectable = true;
    }

    // ================================================================================
    // Variables
    // ================================================================================
    Map map;
    int floor;
    Image image;
    Vector2? lastMousePos = null;

    // Used for scrolling operations to keep zoom focused around point of interest
    Vector2? centerScaledPos = null; // Position relative to internal window (Scaling + scroll applied)
    Vector2? centerUnscaledPos = null; // No scaling/scroll applied
    Vector2 lastFrameScroll;

    // User-controllable options
    int minimapScale = 10;
    int interpolation = (int)Interpolation.Bicubic;
    bool scrollToZoom = true;

    // Constants
    const float MIN_SCALE = 0.1f;
    const float MAX_SCALE = 1.0f;
    const int MAX_SCALE_SLIDER = 100;
    const int MIN_SCALE_SLIDER = 0;

    // ================================================================================
    // Properties
    // ================================================================================

    protected override Image Image
    {
        get
        {
            return image;
        }
    }

    public ProjectWorkspace Workspace { get; private set; }

    public Map Map { get { return map; } }

    public RoomLayout SelectedRoomLayout
    {
        get { return map.GetRoomLayout(SelectedX, SelectedY, 0); }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        // Top bar
        {
            bool scaleChangedFromUI = false;

            ImGui.PushItemWidth(200);
            scaleChangedFromUI = ImGui.SliderInt("Scale", ref minimapScale, 0, MAX_SCALE_SLIDER);

            if (scaleChangedFromUI)
            {
                // Keep the view centered around the selected tile
                this.centerUnscaledPos = new Vector2(
                    SelectedX * TileWidth + TileWidth / 2.0f,
                    SelectedY * TileHeight + TileHeight / 2.0f);
                this.centerScaledPos = centerUnscaledPos * Scale - lastFrameScroll;
            }

            base.Scale =
                MIN_SCALE + (minimapScale / (float)MAX_SCALE_SLIDER) * (MAX_SCALE - MIN_SCALE);

            ImGui.SameLine();
            int newInterpolation = interpolation;
            if (ImGui.Combo("Interpolation", ref newInterpolation,
                            new string[] { "Nearest", "Bicubic" }, (int)Interpolation.Count))
            {
                if (newInterpolation >= 0 && newInterpolation < (int)Interpolation.Count)
                {
                    interpolation = newInterpolation;
                    image.SetInterpolation((Interpolation)interpolation);
                }
            }

            ImGui.SameLine();
            ImGui.Checkbox("Scroll To Zoom".AsSpan(), ref scrollToZoom);

            ImGui.PopItemWidth();
        }

        // Start position of window containing the scrollbars
        var scrollOrigin = ImGui.GetCursorScreenPos();
        // Mouse position "above" the scroll widget (not affected by scrolling)
        var topLevelMousePos = ImGui.GetIO().MousePos - scrollOrigin;

        UpdateScroll();

        ImGuiWindowFlags flags = ImGuiWindowFlags.HorizontalScrollbar;
        if (scrollToZoom)
            flags |= ImGuiWindowFlags.NoScrollWithMouse;
        ImGui.BeginChild("MinimapChild", Vector2.Zero, 0, flags);

        base.Render();

        if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            if (lastMousePos != null)
            {
                Vector2 delta = topLevelMousePos - (Vector2)lastMousePos;
                var scroll = ImGuiX.GetScroll();
                scroll -= delta;
                ImGuiX.SetScroll(scroll);
            }

            this.lastMousePos = topLevelMousePos;
        }
        else
        {
            this.lastMousePos = null;
        }

        if (scrollToZoom && ImGui.IsItemHovered())
        {
            int offset = (int)(ImGui.GetIO().MouseWheel * 5);
            if (offset != 0)
            {
                // Keep the view centered around the mouse cursor
                this.centerScaledPos = topLevelMousePos;
                this.centerUnscaledPos = (centerScaledPos + ImGuiX.GetScroll()) / Scale;

                // base.Scale will be updated next frame based on this
                minimapScale += offset;
                minimapScale = Math.Min(minimapScale, MAX_SCALE_SLIDER);
                minimapScale = Math.Max(minimapScale, MIN_SCALE_SLIDER);
            }
        }

        lastFrameScroll = ImGuiX.GetScroll();

        ImGui.EndChild();
    }

    /// <summary>
    /// Sets the map to display
    /// </summary>
    public void SetMap(Map map, int floor = 0)
    {
        if (this.map == map && this.floor == floor)
            return;

        this.map = map;
        this.floor = floor;

        this.image = null;

        if (map == null)
            return;

        base.TileWidth = map.RoomWidth * 16;
        base.TileHeight = map.RoomHeight * 16;
        base.Width = map.MapWidth;
        base.Height = map.MapHeight;

        this.image = Workspace.GetCachedMapImage((Map, floor));
        this.image.SetInterpolation((Interpolation)interpolation);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Updates the scrollbar. Deals with smoothly zooming in and out relative to a particular
    /// point.
    /// Must be called just before the BeginChild containing the scrollable area.
    /// </summary>
    void UpdateScroll()
    {
        ImGui.SetNextWindowContentSize(new Vector2(CanvasWidth, CanvasHeight));

        if (centerScaledPos != null)
        {
            // centerUnscaledPos is the unscaled position within the TileGridViewer that must be
            // placed at centerScaledPos (above the scrollbar window) in order for the zooming
            // to focus on the point of interest.
            // These variables should have been calculated before the Scale was updated. We now
            // calculate the new scroll value using the updated Scale.
            var scroll = centerUnscaledPos * Scale - centerScaledPos;

            ImGui.SetNextWindowScroll((Vector2)scroll);
            centerScaledPos = null;
            centerUnscaledPos = null;
        }
    }
}
