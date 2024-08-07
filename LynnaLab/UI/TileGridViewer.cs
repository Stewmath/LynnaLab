using System;
using System.ComponentModel;
using System.Collections.Generic;
using Gtk;

using LynnaLib;
using Util;
using System.Diagnostics;

namespace LynnaLab
{
    public enum MouseButton
    {
        Any,
        LeftClick,
        RightClick
    }

    [Flags]
    public enum MouseModifier
    {
        // Exact combination of keys must be pressed, but "Any" allows any combination.
        Any = 1,
        None = 2,
        Ctrl = 4 | None,
        Shift = 8 | None,
        // Mouse can be dragged during the operation.
        Drag = 16,
    }

    public enum GridAction
    {
        Select,       // Set the selected tile (bound to button "Any" with modifier "None" by default)
        Callback,     // Invoke a callback whenever the tile is clicked, or (optionally) the drag changes
        SelectRangeCallback,  // Select a range of tiles, takes a callback
    }

    public struct TileGridEventArgs
    {
        // "click", "drag", "release"
        public string mouseAction;

        // For GridAction.Callback (selected one tile)
        public int selectedIndex;

        // For GridAction.SelectRange
        public Cairo.Point topLeft, bottomRight;

        public void Foreach(System.Action<int, int> action)
        {
            for (int x=topLeft.X; x<=bottomRight.X; x++)
            {
                for (int y=topLeft.Y; y<=bottomRight.Y; y++)
                {
                    action(x, y);
                }
            }
        }
    }


    /* Represents any kind of tile-based grid. It can be hovered over with the mouse, and optionally
     * allows one to select tiles by clicking, or define actions to occur with other mouse buttons.
     */
    public abstract class TileGridViewer : Gtk.DrawingArea
    {
        public static readonly Color DefaultHoverColor = Color.Red;
        public static readonly Color DefaultSelectionColor = Color.White;


        public Color HoverColor { get; set; } = DefaultHoverColor;
        public Color SelectionColor { get; set; } = DefaultSelectionColor;

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


        public bool Hoverable
        {
            get { return _hoverable; }
            set
            {
                if (_hoverable != value && HoveringIndex != -1)
                {
                    Cairo.Rectangle rect = GetTileRect(HoveringX, HoveringY);
                    QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                }
                _hoverable = value;
            }
        }

        public bool Selectable
        {
            get { return _selectable; }
            set
            {
                if (_selectable == value)
                    return;
                _selectable = value;
                CanFocus = value;
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
            get
            {
                if (Width == 0) return 0;
                return hoveringIndex % Width;
            }
        }
        public int HoveringY
        {
            get
            {
                if (Height == 0) return 0;
                return hoveringIndex / Width;
            }
        }

        // This can be set to -1 for "nothing selected".
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (selectedIndex != value)
                {
                    var rect = GetTileRect(SelectedX, SelectedY);
                    QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                    selectedIndex = value;
                    rect = GetTileRect(SelectedX, SelectedY);
                    QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                }
                tileSelectedEvent.Invoke(this, SelectedIndex);
            }
        }
        public int SelectedX
        {
            get
            {
                if (Width == 0) return 0;
                return selectedIndex % Width;
            }
        }
        public int SelectedY
        {
            get
            {
                if (Height == 0) return 0;
                return selectedIndex / Width;
            }
        }


        // Size of tile (not including spacing)
        public int ScaledTileWidth
        {
            get { return TileWidth * Scale; }
        }
        public int ScaledTileHeight
        {
            get { return TileHeight * Scale; }
        }

        public int MaxIndex
        {
            get { return Math.Min(_maxIndex, Width * Height - 1); }
            set
            {
                _maxIndex = value;
                hoveringIndex = Math.Min(hoveringIndex, MaxIndex);
            }
        }

        public bool IsSelectingRange
        {
            get { return activeAction?.action == GridAction.SelectRangeCallback; }
        }


        // Subclasses can either override "Image" to set the image, or override the "TileDrawer"
        // function to set the image using a per-tile draw function.
        protected virtual Bitmap Image { get { return null; } }

        // If true, call the TileDrawer function for each tile even if Image is non-null.
        protected virtual bool UseTileDrawer { get; set; }

        public Color BackgroundColor { get; set; } = Color.Black;


        // Event triggered when the hovering tile changes
        public event EventHandler<int> HoverChangedEvent;


        public delegate void TileGridEventHandler(object sender, TileGridEventArgs args);


        // Protected variables

        protected LockableEvent<int> tileSelectedEvent = new LockableEvent<int>();


        // Private variables

        int hoveringIndex = -1, selectedIndex = -1;
        int _maxIndex = int.MaxValue;
        bool _selectable = false, _hoverable = true;
        bool keyDown = false;

        Cairo.Point rangeSelectionStart, rangeSelectionEnd;

        List<TileGridAction> actionList = new List<TileGridAction>();
        TileGridAction activeAction = null;


        // Constructors

        public TileGridViewer() : base()
        {
            this.ButtonPressEvent += new ButtonPressEventHandler(OnButtonPressEvent);
            this.ButtonReleaseEvent += new ButtonReleaseEventHandler(OnButtonReleaseEvent);
            this.MotionNotifyEvent += new MotionNotifyEventHandler(OnMoveMouse);
            this.LeaveNotifyEvent += new LeaveNotifyEventHandler(OnMouseLeave);
            this.KeyPressEvent += new KeyPressEventHandler(OnKeyPressEvent);
            this.KeyReleaseEvent += new KeyReleaseEventHandler(OnKeyReleaseEvent);
            this.Events = Gdk.EventMask.PointerMotionMask | Gdk.EventMask.LeaveNotifyMask |
                Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask
                | Gdk.EventMask.KeyPressMask | Gdk.EventMask.KeyReleaseMask;

            // This doesn't work by itself?
            //base.FocusOnClick = true;

            // Really stupid hack to prevent focus from being lost when arrow keys are used.
            // This isn't ideal because it will still attempt to focus on other widgets, with
            // visible effects (ie. spin button contents are highlighted).
            this.FocusOutEvent += (a, b) =>
            {
                if (keyDown)
                    GrabFocus();
            };

            Scale = 1;
        }


        // Methods

        // Check if the given "real" position (in pixels) is in bounds. This ALSO checks that the
        // index that it's hovering over does not exceed MaxIndex.
        public bool IsInBounds(int x, int y, bool scale = true, bool offset = true)
        {
            Cairo.Rectangle p = GetTotalBounds(scale: scale, offset: offset);
            if (!(x >= p.X && y >= p.Y && x < p.X + p.Width && y < p.Y + p.Height))
                return false;
            return GetGridIndex(x, y) <= MaxIndex;
        }

        // Convert a "real" position (in pixels) into a grid position (in tiles).
        public Cairo.Point GetGridPosition(int x, int y, bool scale = true, bool offset = true)
        {
            int s = Scale;
            int xoffset = XOffset;
            int yoffset = YOffset;
            if (!scale)
                s = 1;
            if (!offset)
            {
                xoffset = 0;
                yoffset = 0;
            }
            int x2 = (x - xoffset) / ((TileWidth * s) + (TilePaddingX * s * 2));
            int y2 = (y - yoffset) / ((TileHeight * s) + (TilePaddingY * s * 2));
            return new Cairo.Point(x2, y2);
        }

        // Convert a "real" position (in pixels) to an index.
        public int GetGridIndex(int x, int y)
        {
            Cairo.Point p = GetGridPosition(x, y);
            return p.X + p.Y * Width;
        }

        public void AddMouseAction(MouseButton button, MouseModifier mod, GridAction action, TileGridEventHandler callback = null)
        {
            TileGridAction act;
            if (action == GridAction.Callback || action == GridAction.SelectRangeCallback)
            {
                if (callback == null)
                    throw new Exception("Need to specify a callback.");
                act = new TileGridAction(button, mod, action, callback);
            }
            else
            {
                if (callback != null)
                    throw new Exception("This action doesn't take a callback.");
                act = new TileGridAction(button, mod, action);
            }

            // Insert at front of list; newest actions get highest priority
            actionList.Insert(0, act);
        }

        public void RemoveMouseAction(MouseButton button, MouseModifier mod)
        {
            for (int i = 0; i < actionList.Count; i++)
            {
                var act = actionList[i];
                if (act.button == button && act.mod == mod)
                {
                    actionList.Remove(act);
                    i--;
                }
            }
        }

        public void AddTileSelectedHandler(EventHandler<int> handler)
        {
            tileSelectedEvent += handler;
        }

        public void RemoveTileSelectedHandler(EventHandler<int> handler)
        {
            tileSelectedEvent -= handler;
        }

        // May need to call this after updating Width, Height, TileWidth, TileHeight
        public void UpdateSizeRequest()
        {
            Cairo.Rectangle r = GetTotalBounds(scale: true, offset: false);
            SetSizeRequest((int)r.Width, (int)r.Height);
        }


        // Protected methods

        protected void OnButtonPressEvent(object o, ButtonPressEventArgs args)
        {
            int x, y;
            Gdk.ModifierType state;
            args.Event.Window.GetDevicePosition(args.Event.Device, out x, out y, out state);

            if (activeAction == null && IsInBounds(x, y))
            {
                foreach (TileGridAction act in actionList)
                {
                    if (act.MatchesState(state))
                    {
                        HandleTileGridAction(act, GetGridIndex(x, y), "click");

                        if (act.mod.HasFlag(MouseModifier.Drag) && activeAction == null)
                        {
                            activeAction = act;
                        }
                    }
                }

                if (Selectable)
                    GrabFocus();
            }
        }

        protected void OnButtonReleaseEvent(object o, ButtonReleaseEventArgs args)
        {
            int x, y;
            Gdk.ModifierType state;
            args.Event.Window.GetDevicePosition(args.Event.Device, out x, out y, out state);

            if (activeAction != null && !activeAction.ButtonMatchesState(state))
            {
                if (activeAction.mod.HasFlag(MouseModifier.Drag))
                {
                    HandleTileGridAction(activeAction, GetGridIndex(x, y), "release");
                    // Probably will add a "button release" callback here later
                    activeAction = null;
                }
            }
        }

        protected void OnMoveMouse(object o, MotionNotifyEventArgs args)
        {
            int x, y;
            Gdk.ModifierType state;
            args.Event.Window.GetDevicePosition(args.Event.Device, out x, out y, out state);

            int nextHoveringIndex;
            if (IsInBounds(x, y))
            {
                Cairo.Point p = GetGridPosition(x, y);
                nextHoveringIndex = p.X + p.Y * Width;
                Debug.Assert(nextHoveringIndex <= MaxIndex);
            }
            else
            {
                nextHoveringIndex = -1;
            }

            if (nextHoveringIndex != hoveringIndex)
            {
                // Update hovering cursor
                this.QueueDrawArea(GetTileRect(HoveringX, HoveringY));
                hoveringIndex = nextHoveringIndex;
                this.QueueDrawArea(GetTileRect(HoveringX, HoveringY));

                if (HoverChangedEvent != null)
                    HoverChangedEvent(this, hoveringIndex);

                // Drag actions
                if (activeAction != null && activeAction.mod.HasFlag(MouseModifier.Drag)
                    && IsInBounds(x, y))
                {
                    HandleTileGridAction(activeAction, nextHoveringIndex, "drag");
                }
            }
        }

        protected void OnMouseLeave(object o, LeaveNotifyEventArgs args)
        {
            bool changed = false;
            if (hoveringIndex != -1)
            {
                Cairo.Rectangle rect = GetTileRect(HoveringX, HoveringY);
                this.QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

                changed = true;
            }
            hoveringIndex = -1;

            if (changed && HoverChangedEvent != null)
                HoverChangedEvent(this, hoveringIndex);

            // Don't check for drag actions because we'll ignore out-of-bounds events?
        }

        protected void OnKeyPressEvent(object o, KeyPressEventArgs args)
        {
            if (Selectable)
            {
                int newIndex = -1;
                if (args.Event.Key == Gdk.Key.Up)
                {
                    newIndex = SelectedIndex - Width;
                }
                else if (args.Event.Key == Gdk.Key.Down)
                {
                    newIndex = SelectedIndex + Width;
                }
                else if (args.Event.Key == Gdk.Key.Left)
                {
                    newIndex = SelectedIndex - 1;
                }
                else if (args.Event.Key == Gdk.Key.Right)
                {
                    newIndex = SelectedIndex + 1;
                }

                if (newIndex >= 0 && newIndex <= MaxIndex)
                    SelectedIndex = newIndex;

            }

            if (args.Event.Key != Gdk.Key.Tab)
                keyDown = true;
        }

        protected void OnKeyReleaseEvent(object o, KeyReleaseEventArgs args)
        {
            keyDown = false;
        }

        /// Function called when the mouse is clicked, dragged, or released with the appropriate
        /// modifiers for the given action
        void HandleTileGridAction(TileGridAction act, int index, string mouseAction)
        {
            var eventArgs = new TileGridEventArgs() { mouseAction = mouseAction };
            eventArgs.selectedIndex = index;

            int x = index % Width;
            int y = index / Width;

            if (act.action == GridAction.Select)
            {
                if (mouseAction != "release")
                    SelectedIndex = index;
            }
            else if (act.action == GridAction.SelectRangeCallback)
            {
                if (mouseAction == "click")
                {
                    Debug.Assert(index != -1);
                    rangeSelectionStart = new Cairo.Point(x, y);
                    rangeSelectionEnd = rangeSelectionStart;
                }
                else if (mouseAction == "drag")
                {
                    if (index == -1)
                        return;
                    QueueDrawArea(GetRangeRect(rangeSelectionStart, rangeSelectionEnd));
                    rangeSelectionEnd = new Cairo.Point(x, y);
                    QueueDrawArea(GetRangeRect(rangeSelectionStart, rangeSelectionEnd));
                }
                else if (mouseAction == "release")
                {
                    var (topLeft, bottomRight) = GetTopLeftAndBottomRight(rangeSelectionStart, rangeSelectionEnd);
                    eventArgs.topLeft = topLeft;
                    eventArgs.bottomRight = bottomRight;
                    act.callback(this, eventArgs);

                    var rect = GetRangeRect(topLeft, bottomRight);
                    QueueDrawArea(rect);
                }
            }
            else if (act.action == GridAction.Callback)
            {
                if (mouseAction != "release")
                {
                    act.callback(this, eventArgs);
                }
            }
        }

        protected void QueueDrawArea(Cairo.Rectangle rect)
        {
            QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        }

        protected override void OnSizeAllocated(Gdk.Rectangle allocation)
        {
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

        protected override bool OnDrawn(Cairo.Context cr)
        {
            DrawBackground(cr);
            DrawHoverAndSelection(cr);

            return true;
        }

        protected void DrawBackground(Cairo.Context cr)
        {
            var totalRect = GetTotalBounds();
            cr.SetSourceColor(BackgroundColor);
            cr.Rectangle(totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);
            cr.Fill();

            cr.Save();
            cr.Translate(XOffset, YOffset);
            cr.Scale(Scale, Scale);

            if (Image != null)
            {
                cr.SetSource(Image, 0, 0);

                using (Cairo.SurfacePattern pattern = (Cairo.SurfacePattern)cr.GetSource())
                {
                    pattern.Filter = Cairo.Filter.Nearest;
                }

                cr.Paint();
            }

            if (Image == null || UseTileDrawer)
            {
                Cairo.Rectangle extents = cr.ClipExtents();
                for (int i = 0; i < Width * Height; i++)
                {
                    int tileX = i % Width;
                    int tileY = i / Width;
                    Cairo.Rectangle rect = GetTileRect(tileX, tileY, scale: false, offset: false);

                    if (!CairoHelper.RectsOverlap(extents, rect))
                        continue;

                    cr.Save();
                    cr.Translate(rect.X, rect.Y);
                    cr.Rectangle(0, 0, TileWidth, TileHeight);
                    cr.Clip();
                    TileDrawer(i, cr);
                    cr.Restore();
                }
            }

            cr.Restore(); // Undo scale, offset
        }

        protected void DrawHoverAndSelection(Cairo.Context cr)
        {
            cr.Save();

            cr.Translate(XOffset, YOffset);
            cr.Scale(Scale, Scale);

            if (Hoverable && hoveringIndex != -1 && !IsSelectingRange)
            {
                Cairo.Rectangle rect = GetTileRect(HoveringX, HoveringY, scale: false, offset: false);
                cr.NewPath();
                cr.SetSourceColor(HoverColor);
                cr.Rectangle(new Cairo.Rectangle(rect.X + 0.5, rect.Y + 0.5, rect.Width - 1, rect.Height - 1));
                cr.LineWidth = 1;
                cr.LineJoin = Cairo.LineJoin.Bevel;
                cr.Stroke();
            }

            if (Selectable || IsSelectingRange)
            {
                Cairo.Rectangle rect;

                if (IsSelectingRange)
                {
                    rect = GetRangeRect(rangeSelectionStart, rangeSelectionEnd, scale: false, offset: false);
                }
                else
                    rect = GetTileRect(SelectedX, SelectedY, scale: false, offset: false);

                if (IsInBounds((int)rect.X, (int)rect.Y, scale: false, offset: false))
                {
                    cr.NewPath();
                    cr.SetSourceColor(SelectionColor);
                    cr.Rectangle(rect.X + 0.5, rect.Y + 0.5, rect.Width - 1, rect.Height - 1);
                    cr.LineWidth = 1;
                    cr.Stroke();
                }
            }

            cr.Restore();
        }

        protected override void OnGetPreferredWidth(out int minimum_width, out int natural_width)
        {
            var rect = GetTotalBounds();
            minimum_width = (int)rect.Width;
            natural_width = (int)rect.Width;
        }
        protected override void OnGetPreferredHeight(out int minimum_height, out int natural_height)
        {
            var rect = GetTotalBounds();
            minimum_height = (int)rect.Height;
            natural_height = (int)rect.Height;
        }

        protected Cairo.Rectangle GetTileRect(int x, int y, bool scale = true, bool offset = true)
        {
            var p = new Cairo.Point(x, y);
            return GetRangeRect(p, p, scale, offset);
        }

        protected Cairo.Rectangle GetRangeRect(Cairo.Point p1, Cairo.Point p2, bool scale = true, bool offset = true)
        {
            var (topleft, bottomright) = GetTopLeftAndBottomRight(p1, p2);

            int width = (bottomright.X - topleft.X) + 1;
            int height = (bottomright.Y - topleft.Y) + 1;

            int s = Scale;
            int xoffset = XOffset;
            int yoffset = YOffset;
            if (!scale)
                s = 1;
            if (!offset)
            {
                xoffset = 0;
                yoffset = 0;
            }
            return new Cairo.Rectangle(
                    xoffset + topleft.X * (TilePaddingX * 2 + TileWidth) * s + TilePaddingX * s,
                    yoffset + topleft.Y * (TilePaddingY * 2 + TileHeight) * s + TilePaddingY * s,
                    width * TileWidth * s,
                    height * TileHeight * s);
        }

        (Cairo.Point, Cairo.Point) GetTopLeftAndBottomRight(Cairo.Point p1, Cairo.Point p2)
        {
            Cairo.Point topLeft = new Cairo.Point(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y)
            );
            Cairo.Point bottomRight = new Cairo.Point(
                Math.Max(p1.X, p2.X),
                Math.Max(p1.Y, p2.Y)
            );
            return (topLeft, bottomRight);
        }

        protected Cairo.Rectangle GetTotalBounds(bool scale = true, bool offset = true)
        {
            int s = Scale;
            int xoffset = XOffset;
            int yoffset = YOffset;
            if (!scale)
                s = 1;
            if (!offset)
            {
                xoffset = 0;
                yoffset = 0;
            }
            return new Cairo.Rectangle(
                    xoffset,
                    yoffset,
                    Width * (TilePaddingX * 2 + TileWidth) * s,
                    Height * (TilePaddingY * 2 + TileHeight) * s);
        }

        protected void QueueDrawTile(int x, int y)
        {
            Cairo.Rectangle rect = GetTileRect(x, y);
            QueueDrawArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        }

        protected virtual void TileDrawer(int index, Cairo.Context cr) { }


        // Nested class

        class TileGridAction
        {
            public readonly MouseButton button;
            public readonly MouseModifier mod;
            public readonly GridAction action;
            public readonly TileGridEventHandler callback;

            public int lastIndex = -1;

            public TileGridAction(MouseButton button, MouseModifier mod, GridAction action, TileGridEventHandler callback = null)
            {
                this.button = button;
                this.mod = mod;
                this.action = action;
                this.callback = callback;
            }

            public bool MatchesState(Gdk.ModifierType state)
            {
                return ButtonMatchesState(state) && ModifierMatchesState(state);
            }

            public bool ButtonMatchesState(Gdk.ModifierType state)
            {
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

            public bool ModifierMatchesState(Gdk.ModifierType state)
            {
                if (mod.HasFlag(MouseModifier.Any))
                    return true;
                MouseModifier flags = MouseModifier.None;
                if (state.HasFlag(Gdk.ModifierType.ControlMask))
                    flags |= MouseModifier.Ctrl;
                if (state.HasFlag(Gdk.ModifierType.ShiftMask))
                    flags |= MouseModifier.Shift;

                return (mod & ~MouseModifier.Drag) == flags;
            }
        }
    }

}
