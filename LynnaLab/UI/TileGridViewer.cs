using System;
using System.Drawing;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    public abstract class TileGridViewer : Gtk.DrawingArea {
        public int Height { get; set; }
        public int Width { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public int Scale { get; set; }

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

        protected abstract Bitmap Image { get; }

        int hoveringIndex = -1;

        public TileGridViewer() : base() {
            this.MotionNotifyEvent += new MotionNotifyEventHandler(OnMoveMouse);
            this.LeaveNotifyEvent += new LeaveNotifyEventHandler(OnMouseLeave);
            this.Events = Gdk.EventMask.PointerMotionMask | Gdk.EventMask.LeaveNotifyMask | Gdk.EventMask.ButtonPressMask;

            Scale = 1;
        }

        public bool IsInBounds(int x, int y) {
            return (x >= 0 && y >= 0 && x < Width * TileWidth * Scale &&
                    y < Height * TileHeight * Scale);
        }

        protected void OnMoveMouse(object o, MotionNotifyEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            int nextHoveringIndex;
            if (x >= 0 && y >= 0 && x<Width*TileWidth*Scale && y<Height*TileHeight*Scale) {
                nextHoveringIndex = (x/TileWidth/Scale) + (y/TileHeight/Scale)*Width;
            }
            else {
                nextHoveringIndex = -1;
            }

            if (nextHoveringIndex != hoveringIndex) {
                this.QueueDrawArea(HoveringX*TileWidth*Scale, HoveringY*TileHeight*Scale,
                        TileWidth*Scale, TileHeight*Scale);
                hoveringIndex = nextHoveringIndex;
                this.QueueDrawArea(HoveringX*TileWidth*Scale, HoveringY*TileHeight*Scale,
                        TileWidth*Scale, TileHeight*Scale);
            }
        }

        protected void OnMouseLeave(object o, LeaveNotifyEventArgs args) {
            if (hoveringIndex != -1)
                this.QueueDrawArea(HoveringX*TileWidth*Scale, HoveringY*TileHeight*Scale,
                        TileWidth*Scale, TileHeight*Scale);
            hoveringIndex = -1;
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);

            Gdk.Window win = ev.Window;
            Graphics g = Gtk.DotNet.Graphics.FromDrawable(win);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            if (Image != null)
                g.DrawImage(Image, 0, 0, Image.Width*Scale, Image.Height*Scale);
            if (hoveringIndex != -1) {
                g.DrawRectangle(new Pen(Color.Red), HoveringX*TileWidth*Scale, HoveringY*TileHeight*Scale,
                        TileWidth*Scale-1, TileHeight*Scale-1);
            }

            g.Dispose();

            return true;
        }

        protected override void OnSizeRequested(ref Requisition req) {
            req.Width = Width*TileWidth*Scale;
            req.Height = Height*TileHeight*Scale;
        }
    }

    public abstract class TileGridSelector : TileGridViewer {

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
            this.Events |= Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;
        }

        protected void OnButtonPressEvent(object o, ButtonPressEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            if (x >= 0 && y >= 0 && x<Width*TileWidth*Scale && y<Height*TileHeight*Scale) {
                SelectedIndex = (x/TileWidth/Scale) + (y/TileHeight/Scale)*Width;
            }
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);

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
    }
}
