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
            this.Events |= Gdk.EventMask.PointerMotionMask | Gdk.EventMask.LeaveNotifyMask | Gdk.EventMask.ButtonPressMask;
        }

        public bool IsInBounds(int x, int y) {
            return (x >= 0 && y >= 0 && x < Width * TileWidth && y < Height * TileHeight);
        }

        protected void OnMoveMouse(object o, MotionNotifyEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            this.QueueDrawArea(HoveringX*TileWidth, HoveringY*TileHeight, TileWidth, TileHeight);
            if (x >= 0 && y >= 0 && x<Width*TileWidth && y<Height*TileHeight) {
                hoveringIndex = (x/TileWidth) + (y/TileHeight)*Width;
                this.QueueDrawArea(HoveringX*TileWidth, HoveringY*TileHeight, TileWidth, TileHeight);
            }
            else {
                hoveringIndex = -1;
            }
        }

        protected void OnMouseLeave(object o, LeaveNotifyEventArgs args) {
            this.QueueDrawArea(HoveringX*TileWidth, HoveringY*TileHeight, TileWidth, TileHeight);
            hoveringIndex = -1;
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);

            Gdk.Window win = ev.Window;
            System.Drawing.Graphics g = Gtk.DotNet.Graphics.FromDrawable(win);

            if (Image != null)
                g.DrawImage(Image, 0, 0);
            if (hoveringIndex != -1) {
                g.DrawRectangle(new Pen(Color.Red), HoveringX*TileWidth, HoveringY*TileHeight, TileWidth-1, TileHeight-1);
            }

            return true;
        }
    }

    public abstract class TileGridSelector : TileGridViewer {

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set {
                QueueDrawArea(SelectedX*16, SelectedY*16, TileWidth, TileHeight);
                selectedIndex = value;
                QueueDrawArea(SelectedX*16, SelectedY*16, TileWidth, TileHeight);
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
                this.QueueDrawArea(SelectedX*TileWidth, SelectedY*TileHeight, TileWidth, TileHeight);
                selectedIndex = (x/TileWidth) + (y/TileHeight)*Width;
                this.QueueDrawArea(SelectedX*TileWidth, SelectedY*TileHeight, TileWidth, TileHeight);
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
            get { return image; }
        }

        Area area;
        Bitmap image;

        public AreaViewer() : base()
        {
            TileWidth = 16;
            TileHeight = 16;
            Width = 16;
            Height = 16;
        }

        public void SetArea(Area a) {
            area = a;

            image = new Bitmap(0x10*16,0x10*16);
            Graphics g = Graphics.FromImage(image);

            for (int i=0; i<256; i++) {
                int x = i%16;
                int y = i/16;
                g.DrawImage(area.GetTileImage(i), x*16, y*16);
            }

            this.QueueDraw();
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
