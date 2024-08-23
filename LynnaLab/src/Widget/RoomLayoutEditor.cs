using LynnaLib;

namespace LynnaLab
{
    public class RoomLayoutEditor : TileGridViewer
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public RoomLayoutEditor(TopLevel topLevel)
        {
            _topLevel = topLevel;

            base.TileWidth = 16;
            base.TileHeight = 16;
            base.Scale = 2;
        }

        // ================================================================================
        // Variables
        // ================================================================================
        Image _image;
        TopLevel _topLevel;

        // ================================================================================
        // Properties
        // ================================================================================
        public Project Project { get { return _topLevel.Project; } }
        public Room Room { get { return RoomLayout?.Room; } }
        public RoomLayout RoomLayout { get; private set; }


        // TileGridViewer overrides

        protected override Image Image { get { return _image; } }


        // ================================================================================
        // Public methods
        // ================================================================================

        /// <summary>
        /// Set the room layout
        /// </summary>
        public void SetRoomLayout(RoomLayout layout)
        {
            if (layout != RoomLayout)
            {
                RoomLayout = layout;
                RoomChanged();
            }
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Called when room is changed
        /// </summary>
        void RoomChanged()
        {
            // Choosing not to dispose the image here, leave it in the TopLevel cache
            _image = null;

            if (RoomLayout != null)
            {
                // TODO: Watch for changes
                _image = _topLevel.ImageFromBitmap(RoomLayout.GetImage());

                base.Width = RoomLayout.Width;
                base.Height = RoomLayout.Height;
            }
        }
    }
}
