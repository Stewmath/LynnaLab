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
            if (interactionEditor != editor) {
                interactionEditor = editor;
                interactionEditor.RoomEditor = this;
            }
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

        public void OnRoomModified() {
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

            Graphics g = Gtk.DotNet.Graphics.FromDrawable(ev.Window);

            // Draw interactions

            if (interactionEditor == null) return true;

            InteractionGroup group = interactionEditor.InteractionGroup;
            DrawInteractionGroup(g, 0, group, interactionEditor);

            g.Dispose();

            return true;
		}

        int DrawInteractionGroup(Graphics g, int index, InteractionGroup group, InteractionGroupEditor editor) {
            if (group == null) return index;

            int cursorX=-1,cursorY=-1;

            for (int i=0; i<group.GetNumInteractions(); i++) {
                InteractionData data = group.GetInteractionData(i);
                if (data.GetInteractionType() >= InteractionType.Pointer &&
                        data.GetInteractionType() <= InteractionType.Conditional) {
                    InteractionGroup nextGroup = data.GetPointedInteractionGroup();
                    if (nextGroup != null) {
                        if (editor != null && i == editor.SelectedIndex)
                            index = DrawInteractionGroup(g, index, nextGroup, editor.SubEditor);
                        else
                            index = DrawInteractionGroup(g, index, nextGroup, null);
                    }
                }
                else {
                    Color color = data.GetColor();
                    int x,y;
                    int width;
                    try {
                        x = data.GetIntValue("X");
                        y = data.GetIntValue("Y");
                        width = 16;
                        if (data.HasShortenedXY()) {
                            x = x*16+8;
                            y = y*16+8;
                        }
                        // Interactions with specific positions get
                        // transparency
                        color = Color.FromArgb(0xd0,color.R,color.G,color.B);
                    }
                    catch (NotFoundException e) {
                        // No X/Y values exist
                        x = index;
                        y = 0;
                        while (x >= 0xf) {
                            x -= 0xf;
                            y++;
                        }
                        x *= 16;
                        y *= 16;
                        x += 8;
                        y += 8;
                        width = 8;
                        index++;
                    }

                    if (editor != null && i == editor.SelectedIndex) {
                        cursorX = x-8;
                        cursorY = y-8;
                    }
                    x -= width/2;
                    y -= width/2;

                    g.FillRectangle(new SolidBrush(color), x, y, width, width);
                }
            }

            if (cursorX != -1)
                g.DrawRectangle(new Pen(Color.Red), cursorX, cursorY, 15, 15);
            return index;
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
