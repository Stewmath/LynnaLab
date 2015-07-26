using System;
using System.Drawing;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
	[System.ComponentModel.ToolboxItem(true)]
	public class AreaViewer : Gtk.DrawingArea
	{
        Area area;

        Bitmap tilesetImage;

        public Project Project {
            get {
                if (area == null)
                    return null;
                return area.Project;
            }
        }

		public AreaViewer()
		{
		}

        public void SetArea(Area a) {
            area = a;

            tilesetImage = new Bitmap(0x10*16,0x10*16);
            Graphics g = Graphics.FromImage(tilesetImage);

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
			base.OnExposeEvent(ev);

			Gdk.Window win = ev.Window;
			System.Drawing.Graphics g = Gtk.DotNet.Graphics.FromDrawable(win);

            if (area != null) {
                GraphicsState state = area.GraphicsState;

                g.DrawImage(tilesetImage, 0, 0);
            }

			return true;
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

