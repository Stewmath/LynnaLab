using System;
using System.Drawing;
using System.ComponentModel;
using Cairo;
using Gtk;

namespace LynnaLab
{
    public abstract class TileGridViewer : Gtk.DrawingArea {
        public static readonly Cairo.Color HoverColor = new Cairo.Color(255,0,0);

        [BrowsableAttribute(false)]
        public int Width { get; set; }

        [BrowsableAttribute(false)]
        public int Height { get; set; }

        [BrowsableAttribute(false)]
        public int TileWidth { get; set; }

        [BrowsableAttribute(false)]
        public int TileHeight { get; set; }

        [BrowsableAttribute(false)]
        public int Scale { get; set; }

        [BrowsableAttribute(false)]
        // Pixel offset where grid starts
        public int XOffset { get; set; }

        [BrowsableAttribute(false)]
        public int YOffset { get; set; }

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

        public int ScaledTileWidth {
            get { return TileWidth*Scale; }
        }
        public int ScaledTileHeight {
            get { return TileHeight; }
        }

        protected abstract Bitmap Image { get; }
        protected virtual Surface Surface { get; }

        public event System.Action HoverChangedEvent;

        int hoveringIndex = -1;

        public TileGridViewer() : base() {
            this.MotionNotifyEvent += new MotionNotifyEventHandler(OnMoveMouse);
            this.LeaveNotifyEvent += new LeaveNotifyEventHandler(OnMouseLeave);
            this.Events = Gdk.EventMask.PointerMotionMask | Gdk.EventMask.LeaveNotifyMask |
                Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;

            Scale = 1;
        }

        public bool IsInBounds(int x, int y) {
            return (x >= XOffset && y >= YOffset && x < Width * TileWidth * Scale + XOffset &&
                    y < Height * TileHeight * Scale + YOffset);
        }

        protected void OnMoveMouse(object o, MotionNotifyEventArgs args) {
            int x,y;
            Gdk.ModifierType state;
            args.Event.Window.GetPointer(out x, out y, out state);

            int nextHoveringIndex;
            if (IsInBounds(x,y)) {
                nextHoveringIndex = ((x-XOffset)/TileWidth/Scale) + ((y-YOffset)/TileHeight/Scale)*Width;
            }
            else {
                nextHoveringIndex = -1;
            }

            if (nextHoveringIndex != hoveringIndex) {
                this.QueueDrawArea(XOffset+HoveringX*TileWidth*Scale, YOffset+HoveringY*TileHeight*Scale,
                        TileWidth*Scale, TileHeight*Scale);
                hoveringIndex = nextHoveringIndex;
                this.QueueDrawArea(XOffset+HoveringX*TileWidth*Scale, YOffset+HoveringY*TileHeight*Scale,
                        TileWidth*Scale, TileHeight*Scale);

                if (HoverChangedEvent != null)
                    HoverChangedEvent();
            }
        }

        protected void OnMouseLeave(object o, LeaveNotifyEventArgs args) {
            bool changed = false;
            if (hoveringIndex != -1) {
                this.QueueDrawArea(XOffset + HoveringX * TileWidth * Scale, YOffset + HoveringY * TileHeight * Scale,
                    TileWidth * Scale, TileHeight * Scale);

                changed = true;
            }
            hoveringIndex = -1;

            if (changed && HoverChangedEvent != null)
                HoverChangedEvent();
        }

        protected override bool OnDrawn(Cairo.Context cr) {
            base.OnDrawn(cr);

            if (Surface != null) {
                cr.SetSourceSurface(Surface, XOffset, YOffset);
                cr.Paint();
            }
            else if (Image != null) {
                using (Surface source = CairoHelper.LockBitmap(Image)) {
                    cr.SetSourceSurface(source, XOffset, YOffset);
                    cr.Paint();
                    CairoHelper.UnlockBitmap(Image);
                }
            }

            if (hoveringIndex != -1) {
                cr.NewPath();
                cr.SetSourceColor(HoverColor);
                cr.Rectangle(new Cairo.Rectangle(XOffset+HoveringX*TileWidth*Scale+0.5, YOffset+HoveringY*TileHeight*Scale+0.5, ScaledTileWidth-1, ScaledTileHeight-1));
                cr.LineWidth = 1;
                cr.LineJoin = LineJoin.Bevel;
                cr.Stroke();
            }

            return true;
        }

        /*
        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);

            Gdk.Window win = ev.Window;
            Graphics g = Gtk.DotNet.Graphics.FromDrawable(win);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            if (Image != null)
                g.DrawImage(Image, XOffset, YOffset, Image.Width*Scale, Image.Height*Scale);
            if (hoveringIndex != -1) {
                g.DrawRectangle(new Pen(Color.Red), XOffset+HoveringX*TileWidth*Scale, YOffset+HoveringY*TileHeight*Scale,
                        TileWidth*Scale-1, TileHeight*Scale-1);
            }

            g.Dispose();

            return true;
        }
        */

        protected override void OnGetPreferredWidth(out int minimum_width, out int natural_width) {
            minimum_width = XOffset + Width * TileWidth * Scale;
            natural_width = minimum_width;
        }
        protected override void OnGetPreferredHeight(out int minimum_height, out int natural_height) {
            minimum_height = YOffset + Height * TileHeight * Scale;
            natural_height = minimum_height;
        }
    }

}
