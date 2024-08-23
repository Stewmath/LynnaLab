namespace LynnaLab
{
    /// <summary>
    /// Represents any kind of tile-based grid. It can be hovered over with the mouse, and
    /// optionally allows one to select tiles by clicking, or define actions to occur with other
    /// mouse buttons.
    /// </summary>
    public class TileGridViewer : Widget
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public TileGridViewer()
        {

        }

        // ================================================================================
        // Variables
        // ================================================================================

        // ================================================================================
        // Properties
        // ================================================================================
        public int Scale { get; set; } = 1;

        protected virtual Image Image { get { return null; } }

        // ================================================================================
        // Public methods
        // ================================================================================
        public override void Render()
        {
            if (Image != null)
            {
                Widget.Image(Image, scale: Scale);
            }
        }

        // ================================================================================
        // Private methods
        // ================================================================================
    }
}
