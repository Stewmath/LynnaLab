using System;
using Bitmap = System.Drawing.Bitmap;
using System.ComponentModel;
using System.Collections.Generic;
using Cairo;
using Gtk;

namespace LynnaLab
{
    public enum MouseButton {
        Any,
        LeftClick,
        RightClick
    }

    [Flags]
    public enum MouseModifier {
        // Exact combination of keys must be pressed, but "Any" allows any combination.
        Any = 1,
        None = 2,
        Ctrl = 4 | None,
        Shift = 8 | None,
        // Mouse can be dragged during the operation.
        Drag = 16,
    }

    public enum GridAction {
        Select,       // Set the selected tile (bound to button "Any" with modifier "None" by default)
        SelectRange,  // [PLANNED] Select a range of tiles
        Callback      // Invoke a callback whenever the tile is clicked, or the drag changes
    }

    public abstract class TileGridViewer : Gtk.DrawingArea {
        public static readonly Cairo.Color HoverColor = new Cairo.Color(1.0, 0, 0);
        public static readonly Cairo.Color SelectionColor = new Cairo.Color(1.0, 1.0, 1.0);


        public int Width { get; set; }
        public int Height { get; set; }

        public int TileWidth { get; set; }
        public int TileHeight { get; set; }

        public int Scale { get; set; }

        // Pixel offset where grid starts.
        // This is updated with the "OnSizeAllocated" function. Don't directly set this otherwise.
        public int XOffset { get; protected set; }
        public int YOffset { get; protected set; }

        // Padding on each side of each tile on the grid, before scaling (default = 0)
        public int TilePaddingX { get; set; }
        public int TilePaddingY { get; set; }


        public bool Hoverable {
            get { return _hoverable; }
            set {
                if (_hoverable != value && HoveringIndex != -1) {
                    Cairo.Rectangle rect = GetTileRectWithPadding(HoveringX, HoveringY);
                    QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                }
                _hoverable = value;
            }
        }

        public bool Selectable {
            get { return _selectable; }
            set {
                if (_selectable == value)
                    return;
                _selectable = value;
                if (value)
                    AddMouseAction(MouseButton.Any, MouseModifier.Any, GridAction.Select);
                else
                    RemoveMouseAction(MouseButton.Any, MouseModifier.Any);
            }
        }


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
                tileSelectedEvent.Invoke(SelectedIndex);
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


        // Event triggered when the hovering tile changes
        public event System.Action HoverChangedEvent;


        public delegate void TileGridEventHandler(object sender, int tileIndex);


        // Protected variables

        protected LockableEvent<int> tileSelectedEvent = new LockableEvent<int>();


        // Private variables

        int hoveringIndex = -1, selectedIndex = -1;
        int _maxIndex = int.MaxValue;
        bool _selectable = false, _hoverable = true;
        Bitmap _image;

        IList<TileGridAction> actionList = new List<TileGridAction>();
        TileGridAction activeAction = null;


        // Constructors

        public TileGridViewer() : base() {
            this.ButtonPressEvent += new ButtonPressEventHandler(OnButtonPressEvent);
            this.ButtonReleaseEvent += new ButtonReleaseEventHandler(OnButtonReleaseEvent);
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
            int x2 = (x - XOffset) / ((TileWidth  * Scale) + (TilePaddingX * 2));
            int y2 = (y - YOffset) / ((TileHeight * Scale) + (TilePaddingY * 2));
            return new Cairo.Point(x2, y2);
        }

        // Convert a "real" position (in pixels) to an index.
        public int GetGridIndex(int x, int y) {
            Cairo.Point p = GetGridPosition(x, y);
            return p.X + p.Y * Width;
        }

        public void AddMouseAction(MouseButton button, MouseModifier mod, GridAction action, TileGridEventHandler callback = null) {
            TileGridAction act;
            if (action == GridAction.Callback) {
                if (callback == null)
                    throw new Exception("Need to specify a callback.");
                act = new TileGridAction(button, mod, action, callback);
            }
            else {
                if (callback != null)
                    throw new Exception("This action doesn't take a callback.");
                act = new TileGridAction(button, mod, action);
            }

            actionList.Add(act);
        }

        public void RemoveMouseAction(MouseButton button, MouseModifier mod) {
            foreach (var act in actionList) {
                if (act.button == button && act.mod == mod)
                    actionList.Remove(act);
            }
        }

        public void AddTileSelectedHandler(LockableEvent<int>.Handler handler) {
            tileSelectedEvent += handler;
        }

        public void RemoveTileSelectedHandler(LockableEvent<int>.Handler handler) {
            tileSelectedEvent -= handler;
        }


        // Protected methods

        protected void OnButtonPressEvent(object o, ButtonPressEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            if (activeAction == null && IsInBounds(x, y)) {
                foreach (TileGridAction act in actionList) {
                    if (act.MatchesState(state)) {
                        HandleTileGridAction(act, GetGridIndex(x, y));

                        if (act.mod.HasFlag(MouseModifier.Drag))
                            activeAction = act;
                    }
                }
            }
        }

        protected void OnButtonReleaseEvent(object o, ButtonReleaseEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            if (activeAction != null && !activeAction.ButtonMatchesState(state)) {
                if (activeAction.mod.HasFlag(MouseModifier.Drag)) {
                    // Probably will add a "button release" callback here later
                    activeAction = null;
                }
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
                // Update hovering cursor
                Cairo.Rectangle rect = GetTileRectWithPadding(HoveringX, HoveringY);
                this.QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                hoveringIndex = nextHoveringIndex;
                rect = GetTileRectWithPadding(HoveringX, HoveringY);
                this.QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

                if (HoverChangedEvent != null)
                    HoverChangedEvent();

                // Drag actions
                if (activeAction != null && activeAction.mod.HasFlag(MouseModifier.Drag) && IsInBounds(x, y)) {
                    HandleTileGridAction(activeAction, nextHoveringIndex);
                }
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

            // Don't check for drag actions because we'll ignore out-of-bounds events?
        }

        void HandleTileGridAction(TileGridAction act, int index) {
            if (act.action == GridAction.Select) {
                SelectedIndex = index;
            }
            else if (act.action == GridAction.Callback) {
                act.callback(this, index);
            }
        }


        protected override void OnSizeAllocated(Gdk.Rectangle allocation) {
            base.OnSizeAllocated(allocation);

            Cairo.Rectangle desiredRect = GetTotalBounds();

            if (Halign == Gtk.Align.Center || Halign == Gtk.Align.Fill)
                XOffset = (int)(((allocation.Width) - desiredRect.Width) / 2);
            else if (Halign == Gtk.Align.Start)
                XOffset = 0;
            else
                throw new NotImplementedException();

            if (Valign == Gtk.Align.Center || Valign == Gtk.Align.Fill)
                YOffset = (int)(((allocation.Height) - desiredRect.Height) / 2);
            else if (Valign == Gtk.Align.Start)
                YOffset = 0;
            else
                throw new NotImplementedException();
        }

        protected override bool OnDrawn(Cairo.Context cr) {
            /*
            if (!base.OnDrawn(cr))
                return false;
                */

            Bitmap image = _image;
            if (_image == null)
                image = Image;

            if (image != null) {
                using (Surface source = new BitmapSurface(image)) {
                    cr.Scale(Scale, Scale);
                    cr.SetSource(source, XOffset, YOffset);

                    if (Scale != 1) {
                        // NOTE: Don't know how or why, but this line causes memory leaks to be
                        // reported...
                        ((SurfacePattern)cr.Source).Filter = Filter.Nearest;

                        cr.Scale(1.0/Scale, 1.0/Scale);
                    }
                    cr.Paint();
                }
            }

            if (Hoverable && hoveringIndex != -1) {
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
                    XOffset + x * (TilePaddingX * 2 + TileWidth)  * Scale,
                    YOffset + y * (TilePaddingY * 2 + TileHeight) * Scale,
                    TileWidth  * Scale + TilePaddingX * Scale * 2,
                    TileHeight * Scale + TilePaddingY * Scale * 2);
        }

        protected Cairo.Rectangle GetTileRectSansPadding(int x, int y) {
            return new Cairo.Rectangle(
                    XOffset + x * (TilePaddingX * 2 + TileWidth)  * Scale + TilePaddingX * Scale,
                    YOffset + y * (TilePaddingY * 2 + TileHeight) * Scale + TilePaddingY * Scale,
                    TileWidth  * Scale,
                    TileHeight * Scale);
        }

        protected Cairo.Rectangle GetTotalBounds() {
            return new Cairo.Rectangle(
                    XOffset,
                    YOffset,
                    Width  * (TilePaddingX * 2 + TileWidth)  * Scale,
                    Height * (TilePaddingY * 2 + TileHeight) * Scale);
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

            QueueDraw();
        }


        // Nested class

        class TileGridAction {
            public readonly MouseButton button;
            public readonly MouseModifier mod;
            public readonly GridAction action;
            public readonly TileGridEventHandler callback;

            public int lastIndex = -1;

            public TileGridAction(MouseButton button, MouseModifier mod, GridAction action, TileGridEventHandler callback=null) {
                this.button = button;
                this.mod = mod;
                this.action = action;
                this.callback = callback;
            }

            public bool MatchesState(Gdk.ModifierType state) {
                return ButtonMatchesState(state) && ModifierMatchesState(state);
            }

            public bool ButtonMatchesState(Gdk.ModifierType state) {
                bool left = state.HasFlag(Gdk.ModifierType.Button1Mask);
                bool right = state.HasFlag(Gdk.ModifierType.Button3Mask);
                if (button == MouseButton.Any && (left || right))
                    return true;
                if (button == MouseButton.LeftClick && left)
                    return true;
                if (button == MouseButton.RightClick && right)
                    return true;
                return false;
            }

            public bool ModifierMatchesState(Gdk.ModifierType state) {
                if (mod.HasFlag(MouseModifier.Any))
                    return true;
                MouseModifier flags = MouseModifier.None;
                if (state.HasFlag(Gdk.ModifierType.ControlMask))
                    flags |= MouseModifier.Ctrl;
                if (state.HasFlag(Gdk.ModifierType.ShiftMask))
                    flags |= MouseModifier.Shift;

                return mod == flags;
            }
        }
    }

}
