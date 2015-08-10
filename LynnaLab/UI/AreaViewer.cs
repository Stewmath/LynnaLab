using System;
using System.Drawing;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
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
