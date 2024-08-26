using System.Numerics;
using LynnaLib;

namespace LynnaLab
{
    public class RoomLayoutEditor : TileGridViewer
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public RoomLayoutEditor(ProjectWorkspace workspace)
        {
            this.Workspace = workspace;

            base.TileWidth = 16;
            base.TileHeight = 16;
            base.Scale = 2;
        }

        // ================================================================================
        // Variables
        // ================================================================================
        Image _image;

        // ================================================================================
        // Properties
        // ================================================================================
        public ProjectWorkspace Workspace { get; private set; }
        public Project Project { get { return Workspace.Project; } }
        public TopLevel TopLevel { get { return Workspace.TopLevel; } }
        public Room Room { get { return RoomLayout?.Room; } }
        public RoomLayout RoomLayout { get; private set; }


        // TileGridViewer overrides

        public override Vector2 WidgetSize
        {
            get
            {
                // Always keep it 256x256 so that objects can be rendered even out of bounds, and
                // also so that it doesn't fluctuate when moving between small & large rooms
                return new Vector2(256.0f * Scale, 256.0f * Scale);
            }
        }

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
                _image = TopLevel.ImageFromBitmap(RoomLayout.GetImage());

                base.Width = RoomLayout.Width;
                base.Height = RoomLayout.Height;
            }
        }
    }
}
