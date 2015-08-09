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

        public int HoveringIndex
        {
            get { return hoveringIndex; }
        }
        public int HoveringX
        {
            get { return hoveringIndex%Width; }
        }
        public int HoveringY
        {
            get { return hoveringIndex/Width; }
        }

        protected abstract Bitmap Image { get; }

        int hoveringIndex = -1;

        public TileGridViewer() : base() {
            this.MotionNotifyEvent += new MotionNotifyEventHandler(OnMoveMouse);
            this.LeaveNotifyEvent += new LeaveNotifyEventHandler(OnMouseLeave);
            this.Events = Gdk.EventMask.PointerMotionMask | Gdk.EventMask.LeaveNotifyMask | Gdk.EventMask.ButtonPressMask;
        }

        public bool IsInBounds(int x, int y) {
            return (x >= 0 && y >= 0 && x < Width * TileWidth && y < Height * TileHeight);
        }

        protected void OnMoveMouse(object o, MotionNotifyEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            int nextHoveringIndex;
            if (x >= 0 && y >= 0 && x<Width*TileWidth && y<Height*TileHeight) {
                nextHoveringIndex = (x/TileWidth) + (y/TileHeight)*Width;
            }
            else {
                nextHoveringIndex = -1;
            }

            if (nextHoveringIndex != hoveringIndex) {
                this.QueueDrawArea(HoveringX*TileWidth, HoveringY*TileHeight, TileWidth, TileHeight);
                hoveringIndex = nextHoveringIndex;
                this.QueueDrawArea(HoveringX*TileWidth, HoveringY*TileHeight, TileWidth, TileHeight);
            }
        }

        protected void OnMouseLeave(object o, LeaveNotifyEventArgs args) {
            if (hoveringIndex != -1)
                this.QueueDrawArea(HoveringX*TileWidth, HoveringY*TileHeight, TileWidth, TileHeight);
            hoveringIndex = -1;
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);

            Gdk.Window win = ev.Window;
            Graphics g = Gtk.DotNet.Graphics.FromDrawable(win);

            if (Image != null)
                g.DrawImage(Image, 0, 0);
            if (hoveringIndex != -1) {
                g.DrawRectangle(new Pen(Color.Red), HoveringX*TileWidth, HoveringY*TileHeight, TileWidth-1, TileHeight-1);
            }

            g.Dispose();

            return true;
        }
    }

    public abstract class TileGridSelector : TileGridViewer {

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set {
                if (selectedIndex != value) {
                    QueueDrawArea(SelectedX*TileWidth, SelectedY*TileHeight, TileWidth, TileHeight);
                    selectedIndex = value;
                    QueueDrawArea(SelectedX*TileWidth, SelectedY*TileHeight, TileWidth, TileHeight);
                }
                if (TileSelectedEvent != null)
                    TileSelectedEvent(this);
            }
        }
        public int SelectedX
        {
            get { return selectedIndex%Width; }
        }
        public int SelectedY
        {
            get { return selectedIndex/Width; }
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

            if (x >= 0 && y >= 0 && x<Width*TileWidth&& y<Height*TileHeight) {
                SelectedIndex = (x/TileWidth) + (y/TileHeight)*Width;
            }
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);

            Gdk.Window win = ev.Window;
            System.Drawing.Graphics g = Gtk.DotNet.Graphics.FromDrawable(win);

            if (SelectedIndex != -1) {
                g.DrawRectangle(new Pen(Color.White), SelectedX*TileWidth, SelectedY*TileHeight, TileWidth-1, TileHeight-1);
            }

            g.Dispose();

            return true;
        }
    }

    [System.ComponentModel.ToolboxItem(true)]
    public class AreaViewer : TileGridSelector
    {
        public Project Project {
            get {
                if (area == null)
                    return null;
                return area.Project;
            }
        }

        public Area Area
        {
            get { return area; }
        }

        protected override Bitmap Image {
            get {
                if (Area == null)
                    return null;
                return Area.GetFullCachedImage();
            }
        }

        Area area;

        public AreaViewer() : base()
        {
            TileWidth = 16;
            TileHeight = 16;
            Width = 16;
            Height = 16;
        }

        public void SetArea(Area a) {
            Area.TileModifiedHandler handler = new Area.TileModifiedHandler(ModifiedTileCallback);
            if (area != null)
                area.TileModifiedEvent -= handler;
            a.TileModifiedEvent += handler;

            area = a;

            area.DrawAllTiles();

            this.QueueDraw();
        }

        void ModifiedTileCallback(int tile) {
            QueueDraw();
        }

        protected override bool OnButtonPressEvent(Gdk.EventButton ev)
        {
            // Insert button press handling code here.
            return base.OnButtonPressEvent(ev);
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            return base.OnExposeEvent(ev);
        }

        protected override void OnSizeAllocated(Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated(allocation);
            // Insert layout code here.
        }

        protected override void OnSizeRequested(ref Gtk.Requisition requisition)
        {
            // Calculate desired size here.
            requisition.Height = 16*16;
            requisition.Width = 16*16;
        }
    }
}
