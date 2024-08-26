using LynnaLib;

namespace LynnaLab
{
    /// <summary>
    /// Viewing a tileset & selecting tiles from it
    /// </summary>
    public class TilesetViewer : TileGridViewer
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public TilesetViewer(ProjectWorkspace workspace)
        {
            this.Workspace = workspace;

            base.TileWidth = 16;
            base.TileHeight = 16;
            base.Width = 16;
            base.Height = 16;
            base.Selectable = true;
        }

        // ================================================================================
        // Variables
        // ================================================================================
        Tileset tileset;
        Image image;

        // ================================================================================
        // Properties
        // ================================================================================

        public ProjectWorkspace Workspace { get; private set; }
        public Project Project { get { return Workspace.Project; } }
        public TopLevel TopLevel { get { return Workspace.TopLevel; } }

        protected override Image Image
        {
            get
            {
                return image;
            }
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public override void Render()
        {
            base.Render();
        }

        public void SetTileset(Tileset t)
        {
            if (tileset != t)
            {
                tileset = t;
                OnTilesetChanged();
            }
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Called when the tileset is changed
        /// </summary>
        void OnTilesetChanged()
        {
            image = null;

            if (tileset != null)
            {
                image = TopLevel.ImageFromBitmap(tileset.GetFullImage());
            }
        }
    }
}
