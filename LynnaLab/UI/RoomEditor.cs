using System;
using System.Drawing;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
	[System.ComponentModel.ToolboxItem(true)]
	public class RoomEditor : TileGridViewer
	{
        public Room Room
        {
            get { return room; }
        }

        protected override Bitmap Image {
            get {
                if (room == null)
                    return null;
                return room.GetImage();
            }
        }

        Room room;
        TileGridSelector client;

        public RoomEditor(TileGridSelector client) {
            TileWidth = 16;
            TileHeight = 16;
            this.client = client;

            this.ButtonPressEvent += delegate(object o, ButtonPressEventArgs args)
            {
                int x, y;
                Gdk.ModifierType state;
                args.Event.Window.GetPointer(out x, out y, out state);
                if (IsInBounds(x, y)) {
                    if (state.HasFlag(Gdk.ModifierType.Button1Mask))
                        OnClicked(x, y);
                    else if (state.HasFlag(Gdk.ModifierType.Button3Mask))
                        client.SelectedIndex = room.GetTile(x/TileWidth, y/TileWidth);
                }
            };
            this.MotionNotifyEvent += delegate(object o, MotionNotifyEventArgs args) {
                int x, y;
                Gdk.ModifierType state;
                args.Event.Window.GetPointer(out x, out y, out state);
                if (IsInBounds(x, y)) {
                    if (state.HasFlag(Gdk.ModifierType.Button1Mask))
                    OnClicked(x, y);
                }
            };
        }

        void OnClicked(int x, int y) {
            x /= TileWidth;
            y /= TileHeight;
            room.SetTile(x, y, client.SelectedIndex);
            this.QueueDrawArea(x * TileWidth, y * TileWidth, TileWidth - 1, TileHeight - 1);
        }

        public void SetRoom(Room r) {
            room = r;
            Width = room.Width;
            Height = room.Height;
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
			requisition.Width = 0xf*16;
			requisition.Height = 0xb*16;
		}
	}
}
