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

        public bool ViewInteractions {get; set;}

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
        int mouseX=-1,mouseY=-1;

        bool draggingInteraction;
        // List of indices of interactions, each entry is one "depth" into the
        // interaction pointers
        List<int> hoveringInteractionIndices = new List<int>();

        public RoomEditor() {
            TileWidth = 16;
            TileHeight = 16;

            this.ButtonPressEvent += delegate(object o, ButtonPressEventArgs args)
            {
                if (client == null)
                    return;
                int x,y;
                Gdk.ModifierType state;
                args.Event.Window.GetPointer(out x, out y, out state);
                UpdateMouse(x,y);
                if (IsInBounds(x, y)) {
                    if (state.HasFlag(Gdk.ModifierType.Button1Mask))
                        OnClicked(x, y);
                    else if (state.HasFlag(Gdk.ModifierType.Button3Mask))
                        client.SelectedIndex = room.GetTile(x/TileWidth, y/TileWidth);
                }
            };
            this.ButtonReleaseEvent += delegate(object o, ButtonReleaseEventArgs args) {
                int x,y;
                Gdk.ModifierType state;
                args.Event.Window.GetPointer(out x, out y, out state);
                if (!state.HasFlag(Gdk.ModifierType.Button1Mask)) {
                    draggingInteraction = false;
                }
            };
            this.MotionNotifyEvent += delegate(object o, MotionNotifyEventArgs args) {
                if (client == null)
                    return;
                int x,y;
                Gdk.ModifierType state;
                args.Event.Window.GetPointer(out x, out y, out state);
                UpdateMouse(x,y);
                if (IsInBounds(x, y)) {
                    if (state.HasFlag(Gdk.ModifierType.Button1Mask))
                        OnDragged(x, y);
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

        // Called when a new set of interactions is loaded or interactions are
        // modified or whatever
        public void OnInteractionsModified() {
        }

        void UpdateMouse(int x, int y) {
            if (mouseX != x || mouseY != y) {
                mouseX = x;
                mouseY = y;

                if (ViewInteractions) // Laziness
                    QueueDraw();
            }
        }

        void OnClicked(int x, int y) {
            x /= TileWidth;
            y /= TileHeight;
            if (!ViewInteractions) {
                room.SetTile(x, y, client.SelectedIndex);
                this.QueueDrawArea(x * TileWidth, y * TileWidth, TileWidth - 1, TileHeight - 1);
            }
            else {
                if (interactionEditor != null) {
                    InteractionGroupEditor editor = interactionEditor;
                    while (hoveringInteractionIndices.Count > 1) {
                        editor.SelectedIndex = hoveringInteractionIndices[0];
                        hoveringInteractionIndices.RemoveAt(0);
                        editor = editor.SubEditor;
                    }
                    if (hoveringInteractionIndices.Count == 1) {
                        editor.SelectedIndex = hoveringInteractionIndices[0];
                        draggingInteraction = true;
                    }
                }
            }
        }

        void OnDragged(int x, int y) {
            if (!ViewInteractions)
                OnClicked(x,y);
            else {
                if (!IsInBounds(x,y)) return;
                if (!draggingInteraction) return;

                InteractionData data = interactionEditor.SelectedInteractionData;
                if (data != null && data.HasXY()) {
                    // Move interactions in increments of 16 pixels
                    int dataX = data.GetX()+8;
                    int dataY = data.GetY()+8;
                    int alignX = (dataX)%16;
                    int alignY = (dataY)%16;
                    int newX = (x-alignX)/16;
                    int newY = (y-alignY)/16;
                    newX = (newX*16+alignX+8)%256;
                    newY = (newY*16+alignY+8)%256;

                    data.SetX(newX);
                    data.SetY(newY);

                    QueueDraw();
                }
            }
        }

        protected override bool OnButtonPressEvent(Gdk.EventButton ev)
        {
            // Insert button press handling code here.
            return base.OnButtonPressEvent(ev);
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            Graphics g = Gtk.DotNet.Graphics.FromDrawable(ev.Window);

            if (ViewInteractions)
                g.DrawImage(Image, 0, 0, Image.Width*Scale, Image.Height*Scale);
            else
                base.OnExposeEvent(ev);

            if (ViewInteractions && interactionEditor != null) {
                // Draw interactions

                int cursorX=-1,cursorY=-1;
                int selectedX=-1,selectedY=-1;
                hoveringInteractionIndices = new List<int>();

                InteractionGroup group = interactionEditor.InteractionGroup;
                DrawInteractionGroup(g, 0, ref cursorX, ref cursorY, ref selectedX, ref selectedY, group, interactionEditor);

                // Interaction hovering over
                if (cursorX != -1)
                    g.DrawRectangle(new Pen(Color.Red), cursorX, cursorY, 15, 15);
                // Interaction selected
                if (selectedX != -1)
                    g.DrawRectangle(new Pen(Color.White), selectedX, selectedY, 15, 15);
            }


            g.Dispose();

            return true;
        }

        int DrawInteractionGroup(Graphics g, int index, ref int cursorX, ref int cursorY, ref int selectedX, ref int selectedY, InteractionGroup group, InteractionGroupEditor editor) {
            if (group == null) return index;

            bool foundHoveringMatch = false;

            for (int i=0; i<group.GetNumInteractions(); i++) {
                InteractionData data = group.GetInteractionData(i);
                if (data.GetInteractionType() >= InteractionType.Pointer &&
                        data.GetInteractionType() <= InteractionType.Conditional) {
                    InteractionGroup nextGroup = data.GetPointedInteractionGroup();
                    if (nextGroup != null) {
                        List<int> oldHoveringInteractionIndices =
                            new List<int>(hoveringInteractionIndices);
                        hoveringInteractionIndices.Add(i);
                        if (editor != null && i == editor.SelectedIndex)
                            index = DrawInteractionGroup(g, index, ref cursorX, ref cursorY,
                                    ref selectedX, ref selectedY, nextGroup, editor.SubEditor);
                        else
                            index = DrawInteractionGroup(g, index, ref cursorX, ref cursorY,
                                    ref selectedX, ref selectedY, nextGroup, null);
                        if (hoveringInteractionIndices.Count == oldHoveringInteractionIndices.Count+1)
                            hoveringInteractionIndices = oldHoveringInteractionIndices;
                    }
                }
                else {
                    Color color = data.GetColor();
                    int x,y;
                    int width;
                    if (data.HasXY()) {
                        x = data.GetX();
                        y = data.GetY();
                        width = 16;
                        // Interactions with specific positions get
                        // transparency
                        color = Color.FromArgb(0xd0,color.R,color.G,color.B);
                    }
                    else {
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
                        selectedX = x-8;
                        selectedY = y-8;
                    }
                    if (mouseX >= x-8 && mouseX < x+8 &&
                            mouseY >= y-8 && mouseY < y+8) {
                        if (foundHoveringMatch)
                            hoveringInteractionIndices[hoveringInteractionIndices.Count-1] = i;
                        else
                            hoveringInteractionIndices.Add(i);
                        cursorX = x-8;
                        cursorY = y-8;
                        foundHoveringMatch = true;
                    }

                    x -= width/2;
                    y -= width/2;


                    g.FillRectangle(new SolidBrush(color), x, y, width, width);
                }
            }

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
