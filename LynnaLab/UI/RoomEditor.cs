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
        InteractionGroupEditor interactionEditor;

        public RoomEditor() {
            TileWidth = 16;
            TileHeight = 16;

            this.ButtonPressEvent += delegate(object o, ButtonPressEventArgs args)
            {
                if (client == null)
                    return;
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
                if (client == null)
                    return;
                int x, y;
                Gdk.ModifierType state;
                args.Event.Window.GetPointer(out x, out y, out state);
                if (IsInBounds(x, y)) {
                    if (state.HasFlag(Gdk.ModifierType.Button1Mask))
                        OnClicked(x, y);
                }
            };
        }

        public void SetClient(TileGridSelector client) {
            this.client = client;
        }

        public void SetInteractionGroupEditor(InteractionGroupEditor editor) {
            interactionEditor = editor;
        }

        public void SetRoom(Room r) {
            var handler = new Room.RoomModifiedHandler(OnRoomModified);
            if (room != null)
                room.RoomModifiedEvent -= handler;
            r.RoomModifiedEvent += handler;

            room = r;
            Width = room.Width;
            Height = room.Height;
            QueueDraw();
        }

        void OnRoomModified() {
            QueueDraw();
        }

        void OnClicked(int x, int y) {
            x /= TileWidth;
            y /= TileHeight;
            room.SetTile(x, y, client.SelectedIndex);
            this.QueueDrawArea(x * TileWidth, y * TileWidth, TileWidth - 1, TileHeight - 1);
        }

		protected override bool OnButtonPressEvent(Gdk.EventButton ev)
		{
			// Insert button press handling code here.
			return base.OnButtonPressEvent(ev);
		}

		protected override bool OnExposeEvent(Gdk.EventExpose ev)
		{
			base.OnExposeEvent(ev);

            // Draw interactions
            if (interactionEditor == null) return true;
            InteractionGroup group = interactionEditor.InteractionGroup;

            for (int i=0; i<group.GetNumInteractions(); i++) {
                InteractionData data = group.GetInteractionData(i);
                Color color = data.GetColor();
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
