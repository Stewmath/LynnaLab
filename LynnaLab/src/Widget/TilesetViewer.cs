using LynnaLib;

namespace LynnaLab
{
    public class TilesetViewer : TileGridViewer
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public TilesetViewer(TopLevel topLevel)
        {
            this.topLevel = topLevel;

            base.TileWidth = 16;
            base.TileHeight = 16;
            base.Width = 16;
            base.Height = 16;
            base.Selectable = true;
        }

        // ================================================================================
        // Variables
        // ================================================================================
        TopLevel topLevel;
        Tileset tileset;
        Image image;

        // ================================================================================
        // Properties
        // ================================================================================

        public Project Project { get { return topLevel.Project; } }

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
                image = topLevel.ImageFromBitmap(tileset.GetFullImage());
            }
        }
    }
}
