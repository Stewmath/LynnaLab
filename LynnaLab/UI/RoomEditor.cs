using System;
using System.Drawing;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
	[System.ComponentModel.ToolboxItem(true)]
	public class RoomEditor : Gtk.DrawingArea
	{
        public Room Room
        {
            get { return room; }
        }
        Room room;

        public RoomEditor() {
        }

        public void SetRoom(Room r) {
            room = r;
            QueueDraw();
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

            if (room != null) {
                g.DrawImage(room.GetImage(), 0, 0);
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
			requisition.Width = 0xf*16;
			requisition.Height = 0xb*16;
		}
	}
}
