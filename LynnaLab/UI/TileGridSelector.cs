using System;
using System.Drawing;
using System.ComponentModel;
using Gtk;

namespace LynnaLab {

    public abstract class TileGridSelector : TileGridViewer {

        public static readonly Cairo.Color SelectionColor = new Cairo.Color(255,255,255);

        [BrowsableAttribute(false)]
        public bool Selectable { get; set; }

        [BrowsableAttribute(false)]
        public bool Draggable { get; set; }

        [BrowsableAttribute(false)]
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set {
                if (selectedIndex != value) {
                    QueueDrawArea(SelectedX*TileWidth*Scale, SelectedY*TileHeight*Scale, TileWidth*Scale, TileHeight*Scale);
                    selectedIndex = value;
                    QueueDrawArea(SelectedX*TileWidth*Scale, SelectedY*TileHeight*Scale, TileWidth*Scale, TileHeight*Scale);
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

            if (x >= 0 && y >= 0 && x<Width*TileWidth*Scale && y<Height*TileHeight*Scale) {
                SelectedIndex = (x/TileWidth/Scale) + (y/TileHeight/Scale)*Width;
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

            if (x >= 0 && y >= 0 && x<Width*TileWidth*Scale && y<Height*TileHeight*Scale) {
                int newIndex = (x/TileWidth/Scale) + (y/TileHeight/Scale)*Width;
                // When dragging, only fire the event when a different square
                // is reached
                if (SelectedIndex != newIndex)
                    SelectedIndex = newIndex;
            }
        }

        protected override bool OnDrawn(Cairo.Context cr) {
            base.OnDrawn(cr);

            if (!Selectable)
                return true;

            cr.NewPath();
            cr.SetSourceColor(SelectionColor);
            cr.Rectangle(XOffset+SelectedX*TileWidth*Scale+0.5, SelectedY*TileHeight*Scale+0.5, TileWidth*Scale-1, TileHeight*Scale-1);
            cr.LineWidth = 1;
            cr.Stroke();

            return true;
        }
        /*
        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);

            if (!Selectable)
                return true;

            Gdk.Window win = ev.Window;
            System.Drawing.Graphics g = Gtk.DotNet.Graphics.FromDrawable(win);

            if (SelectedIndex != -1) {
                g.DrawRectangle(new Pen(Color.White),
                        SelectedX*TileWidth*Scale, SelectedY*TileHeight*Scale,
                        TileWidth*Scale-1, TileHeight*Scale-1);
            }

            g.Dispose();

            return true;
        }
        */
    }
}
