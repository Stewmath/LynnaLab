using System;
using Bitmap = System.Drawing.Bitmap;
using System.ComponentModel;
using Cairo;
using Gtk;

namespace LynnaLab
{
    enum MouseButton {
        LEFT_CLICK,
        RIGHT_CLICK // [PLANNED]
    }

    [Flags]
    enum MouseModifier {
        ANY = 0,
        NONE = 1,
        CTRL = 2,  // [PLANNED]
        SHIFT = 4, // [PLANNED]
        DRAG = 8   // [PLANNED]
    }

    enum GridAction {
        SELECT,       // Set the selected tile (bound to LEFT_CLICK with ANY modifier by default)
        SELECTRANGE,  // [PLANNED] Select a range of tiles
        CALLBACK      // [PLANNED] Invoke a callback whenever the tile is clicked, or the drag changes
    }

    public abstract class TileGridViewer : Gtk.DrawingArea {
        public static readonly Cairo.Color HoverColor = new Cairo.Color(1.0,0,0);
        public static readonly Cairo.Color SelectionColor = new Cairo.Color(1.0, 1.0, 1.0);


        public int Width { get; set; }
        public int Height { get; set; }

        public int TileWidth { get; set; }
        public int TileHeight { get; set; }

        public int Scale { get; set; }

        // Pixel offset where grid starts (TODO: replace with padding)
        public int XOffset { get; set; }
        public int YOffset { get; set; }

        // Padding on each side of each tile on the grid, before scaling (default = 0)
        public int PaddingX { get; set; }
        public int PaddingY { get; set; }

        public bool Selectable { get; set; }
        public bool Draggable { get; set; }


        public int HoveringIndex
        {
            get { return hoveringIndex; }
        }
        public int HoveringX
        {
            get {
                if (Width == 0) return 0;
                return hoveringIndex%Width;
            }
        }
        public int HoveringY
        {
            get {
                if (Height == 0) return 0;
                return hoveringIndex/Width;
            }
        }

        // This can be set to -1 for "nothing selected".
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set {
                if (selectedIndex != value) {
                    var rect = GetTileRectSansPadding(SelectedX, SelectedY);
                    QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                    selectedIndex = value;
                    rect = GetTileRectSansPadding(SelectedX, SelectedY);
                    QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                }
                if (TileSelectedEvent != null)
                    TileSelectedEvent(this);
            }
        }
        public int SelectedX
        {
            get {
                if (Width == 0) return 0;
                return selectedIndex%Width;
            }
        }
        public int SelectedY
        {
            get {
                if (Height == 0) return 0;
                return selectedIndex/Width;
            }
        }


        // Size of tile (not including spacing)
        public int ScaledTileWidth {
            get { return TileWidth*Scale; }
        }
        public int ScaledTileHeight {
            get { return TileHeight*Scale; }
        }

        public int MaxIndex {
            get { return Math.Min(_maxIndex, Width * Height); }
            set {
                _maxIndex = value;
                hoveringIndex = Math.Min(hoveringIndex, MaxIndex);
            }
        }


        // Subclasses can either override "Image" to set the image, or call the "DrawImageWithTiles"
        // function to set the image using a per-tile draw function.
        protected virtual Bitmap Image { get { return null; } }

        protected virtual Surface Surface { get; }


        // Event triggered when the hovering tile changes
        public event System.Action HoverChangedEvent;

        // Event trigger when a tile is selected
        public delegate void TileSelectedEventHandler(object sender);
        public event TileSelectedEventHandler TileSelectedEvent;


        // Private variables

        int hoveringIndex = -1, selectedIndex = -1;
        int _maxIndex = int.MaxValue;
        Bitmap _image;


        // Constructors

        public TileGridViewer() : base() {
            this.ButtonPressEvent += new ButtonPressEventHandler(OnButtonPressEvent);
            this.MotionNotifyEvent += new MotionNotifyEventHandler(OnMoveMouse);
            this.LeaveNotifyEvent += new LeaveNotifyEventHandler(OnMouseLeave);
            this.Events = Gdk.EventMask.PointerMotionMask | Gdk.EventMask.LeaveNotifyMask |
                Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;

            Scale = 1;
        }


        // Methods

        // Check if the given "real" position (in pixels) is in bounds. This ALSO check that the
        // index that it's hovering over does not exceet MaxIndex.
        public bool IsInBounds(int x, int y) {
            Cairo.Rectangle p = GetTotalBounds();
            if (!(x >= p.X && y >= p.Y && x < p.X + p.Width && y < p.Y + p.Height))
                return false;
            return GetGridIndex(x, y) <= MaxIndex;
        }

        // Convert a "real" position (in pixels) into a grid position (in tiles).
        public Cairo.Point GetGridPosition(int x, int y) {
            int x2 = (x - XOffset) / ((TileWidth  * Scale) + (PaddingX * 2));
            int y2 = (y - YOffset) / ((TileHeight * Scale) + (PaddingY * 2));
            return new Cairo.Point(x2, y2);
        }

        // Convert a "real" position (in pixels) to an index.
        public int GetGridIndex(int x, int y) {
            Cairo.Point p = GetGridPosition(x, y);
            return p.X + p.Y * Width;
        }


        // Protected methods

        protected void OnButtonPressEvent(object o, ButtonPressEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            if (IsInBounds(x, y)) {
                Cairo.Point pos = GetGridPosition(x, y);
                SelectedIndex = pos.X + pos.Y * Width;
            }
        }

        protected void OnMotionNotifyEvent(object o, MotionNotifyEventArgs args) {
            if (!Draggable)
                return;

            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            if (!state.HasFlag(Gdk.ModifierType.Button1Mask)
                    || state.HasFlag(Gdk.ModifierType.Button3Mask))
                return;

            if (IsInBounds(x, y)) {
                Cairo.Point pos = GetGridPosition(x, y);
                int newIndex = pos.X + pos.Y * Width;
                // When dragging, only fire the event when a different square
                // is reached
                if (SelectedIndex != newIndex)
                    SelectedIndex = newIndex;
            }
        }


        protected void OnMoveMouse(object o, MotionNotifyEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            int nextHoveringIndex;
            if (IsInBounds(x,y)) {
                Cairo.Point p = GetGridPosition(x, y);
                nextHoveringIndex = p.X + p.Y * Width;
            }
            else {
                nextHoveringIndex = -1;
            }

            if (nextHoveringIndex != hoveringIndex) {
                Cairo.Rectangle rect = GetTileRectWithPadding(HoveringX, HoveringY);
                this.QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                hoveringIndex = nextHoveringIndex;
                rect = GetTileRectWithPadding(HoveringX, HoveringY);
                this.QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

                if (HoverChangedEvent != null)
                    HoverChangedEvent();
            }
        }

        protected void OnMouseLeave(object o, LeaveNotifyEventArgs args) {
            bool changed = false;
            if (hoveringIndex != -1) {
                Cairo.Rectangle rect = GetTileRectWithPadding(HoveringX, HoveringY);
                this.QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

                changed = true;
            }
            hoveringIndex = -1;

            if (changed && HoverChangedEvent != null)
                HoverChangedEvent();
        }

        protected override bool OnDrawn(Cairo.Context cr) {
            /*
            if (!base.OnDrawn(cr))
                return false;
                */

            Bitmap image = _image;
            if (_image == null)
                image = Image;

            if (Surface != null) {
                cr.SetSourceSurface(Surface, XOffset, YOffset);
                cr.Paint();
            }
            else if (image != null) {
                Bitmap orig = image;
                using (Surface source = new BitmapSurface(orig)) {
                    cr.Scale(Scale, Scale);
                    cr.SetSource(source, XOffset, YOffset);
                    ((SurfacePattern)cr.Source).Filter = Filter.Nearest;
                    cr.Scale(1.0/Scale, 1.0/Scale);
                    cr.Paint();
                }
            }

            if (hoveringIndex != -1) {
                Cairo.Rectangle rect = GetTileRectSansPadding(HoveringX, HoveringY);
                cr.NewPath();
                cr.SetSourceColor(HoverColor);
                cr.Rectangle(new Cairo.Rectangle(rect.X + 0.5, rect.Y + 0.5, rect.Width-1, rect.Height-1));
                cr.LineWidth = 1;
                cr.LineJoin = LineJoin.Bevel;
                cr.Stroke();
            }

            if (Selectable) {
                var rect = GetTileRectSansPadding(SelectedX, SelectedY);

                if (IsInBounds((int)rect.X, (int)rect.Y)) {
                    cr.NewPath();
                    cr.SetSourceColor(SelectionColor);
                    cr.Rectangle(rect.X + 0.5, rect.Y + 0.5, rect.Width-1, rect.Height-1);
                    cr.LineWidth = 1;
                    cr.Stroke();
                }
            }

            return true;
        }

        protected override void OnGetPreferredWidth(out int minimum_width, out int natural_width) {
            var rect = GetTotalBounds();
            minimum_width = (int)rect.Width;
            natural_width = (int)rect.Width;
        }
        protected override void OnGetPreferredHeight(out int minimum_height, out int natural_height) {
            var rect = GetTotalBounds();
            minimum_height = (int)rect.Height;
            natural_height = (int)rect.Height;
        }

        protected Cairo.Rectangle GetTileRectWithPadding(int x, int y) {
            return new Cairo.Rectangle(
                    XOffset + x * (PaddingX * 2 + TileWidth)  * Scale,
                    YOffset + y * (PaddingY * 2 + TileHeight) * Scale,
                    TileWidth  * Scale + PaddingX * Scale * 2,
                    TileHeight * Scale + PaddingY * Scale * 2);
        }

        protected Cairo.Rectangle GetTileRectSansPadding(int x, int y) {
            return new Cairo.Rectangle(
                    XOffset + x * (PaddingX * 2 + TileWidth)  * Scale + PaddingX * Scale,
                    YOffset + y * (PaddingY * 2 + TileHeight) * Scale + PaddingY * Scale,
                    TileWidth  * Scale,
                    TileHeight * Scale);
        }

        protected Cairo.Rectangle GetTotalBounds() {
            return new Cairo.Rectangle(
                    XOffset,
                    YOffset,
                    Width  * (PaddingX * 2 + TileWidth)  * Scale,
                    Height * (PaddingY * 2 + TileHeight) * Scale);
        }


        // Subclasses can either override "Image" to set the image, or call the "DrawImageWithTiles"
        // function to set the image using a per-tile draw function.
        protected void DrawImageWithTiles(Func<int,Bitmap> tileDrawer) {
            var totalRect = GetTotalBounds();

            _image = new Bitmap((int)totalRect.Width, (int)totalRect.Height);

            using (Cairo.Context cr = new BitmapContext(_image)) {
                cr.SetSourceColor(new Cairo.Color(0.8, 0.8, 0.8));
                cr.Rectangle(totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);
                cr.Fill();

                for (int i=0; i<Width*Height; i++) {
                    int tileX = i%Width;
                    int tileY = i/Width;
                    Cairo.Rectangle rect = GetTileRectSansPadding(tileX, tileY);
                    Bitmap tileImage = tileDrawer(i);
                    if (tileImage == null)
                        continue;
                    using (Surface src = new BitmapSurface(tileImage)) {
                        cr.SetSource(src, rect.X, rect.Y);
                        cr.Paint();
                    }
                }
            }
        }
    }

}
