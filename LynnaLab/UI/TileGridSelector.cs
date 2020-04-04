using System;
using System.Drawing;
using System.ComponentModel;
using Gtk;

namespace LynnaLab {

    public abstract class TileGridSelector : TileGridViewer {

        public static readonly Cairo.Color SelectionColor = new Cairo.Color(1.0, 1.0, 1.0);

        [BrowsableAttribute(false)]
        public bool Selectable { get; set; }

        [BrowsableAttribute(false)]
        public bool Draggable { get; set; }

        // This can be set to -1 for "nothing selected".
        [BrowsableAttribute(false)]
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


        // Event for selecting a tile
        public delegate void TileSelectedEventHandler(object sender);
        public event TileSelectedEventHandler TileSelectedEvent;


        int selectedIndex = 0;

        public TileGridSelector() : base() {
            this.ButtonPressEvent += new ButtonPressEventHandler(OnButtonPressEvent);
            this.MotionNotifyEvent += new MotionNotifyEventHandler(OnMotionNotifyEvent);
            this.Events |= Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;
            Selectable = true;
            Draggable = false;
        }

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

        protected override bool OnDrawn(Cairo.Context cr) {
            if (!base.OnDrawn(cr))
                return false;

            if (!Selectable)
                return true;

            var rect = GetTileRectSansPadding(SelectedX, SelectedY);

            if (IsInBounds((int)rect.X, (int)rect.Y)) {
                cr.NewPath();
                cr.SetSourceColor(SelectionColor);
                cr.Rectangle(rect.X + 0.5, rect.Y + 0.5, rect.Width-1, rect.Height-1);
                cr.LineWidth = 1;
                cr.Stroke();
            }

            return true;
        }
    }
}
