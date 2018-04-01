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

        public bool ViewObjects {get; set;}
        public bool ViewObjectBoxes {get; set;}

        protected override Bitmap Image {
            get {
                if (room == null)
                    return null;
                return room.GetImage();
            }
        }

        Room room;
        TileGridSelector client;
        ObjectGroupEditor objectEditor;
        int mouseX=-1,mouseY=-1;

        bool draggingObject;
        // List of indices of objects, each entry is one "depth" into the
        // object pointers
        List<int> hoveringObjectIndices = new List<int>();

        Gdk.ModifierType gdkState;

        public RoomEditor() {
            TileWidth = 16;
            TileHeight = 16;
            XOffset = 8;
            YOffset = 8;

            ViewObjectBoxes = true;

            this.ButtonPressEvent += delegate(object o, ButtonPressEventArgs args)
            {
                if (client == null)
                    return;
                int x,y;
                args.Event.Window.GetPointer(out x, out y, out gdkState);
                UpdateMouse(x,y);
                if (gdkState.HasFlag(Gdk.ModifierType.Button1Mask))
                    OnClicked(x, y);
                if (IsInBounds(x, y)) {
                    if (gdkState.HasFlag(Gdk.ModifierType.Button3Mask))
                        client.SelectedIndex = room.GetTile(HoveringX, HoveringY);
                }
            };
            this.ButtonReleaseEvent += delegate(object o, ButtonReleaseEventArgs args) {
                int x,y;
                args.Event.Window.GetPointer(out x, out y, out gdkState);
                if (!gdkState.HasFlag(Gdk.ModifierType.Button1Mask)) {
                    draggingObject = false;
                }
            };
            this.MotionNotifyEvent += delegate(object o, MotionNotifyEventArgs args) {
                if (client == null)
                    return;
                int x,y;
                args.Event.Window.GetPointer(out x, out y, out gdkState);
                UpdateMouse(x,y);
                if (gdkState.HasFlag(Gdk.ModifierType.Button1Mask))
                    OnDragged(x, y);
            };
        }

        public void SetClient(TileGridSelector client) {
            this.client = client;
        }

        public void SetObjectGroupEditor(ObjectGroupEditor editor) {
            if (objectEditor != editor) {
                objectEditor = editor;
                objectEditor.RoomEditor = this;
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

        // Called when a new set of objects is loaded or objects are
        // modified or whatever
        public void OnObjectsModified() {
            QueueDraw();
        }

        void UpdateMouse(int x, int y) {
            if (mouseX != x || mouseY != y) {
                mouseX = x;
                mouseY = y;

                if (ViewObjects) // Laziness
                    QueueDraw();
            }
        }

        void OnClicked(int posX, int posY) {
            int x = (posX - XOffset) / TileWidth;
            int y = (posY - YOffset) / TileHeight;
            if (!ViewObjects) {
                if (!IsInBounds(posX,posY))
                    return;
                room.SetTile(x, y, client.SelectedIndex);
                this.QueueDrawArea(x * TileWidth + XOffset, y * TileWidth + YOffset, TileWidth - 1, TileHeight - 1);
            }
            else {
                if (objectEditor != null) {
                    ObjectGroupEditor editor = objectEditor;
                    while (hoveringObjectIndices.Count > 1) {
                        editor.SelectedIndex = hoveringObjectIndices[0];
                        hoveringObjectIndices.RemoveAt(0);
                        editor = editor.SubEditor;
                    }
                    if (hoveringObjectIndices.Count == 1) {
                        editor.SelectedIndex = hoveringObjectIndices[0];
                        draggingObject = true;
                    }
                }
            }
        }

        void OnDragged(int x, int y) {
            if (!ViewObjects)
                OnClicked(x,y);
            else {
                if (!draggingObject) return;

                ObjectData data = objectEditor.SelectedObjectData;
                if (data != null && data.HasXY()) {
                    int newX,newY;
                    if (gdkState.HasFlag(Gdk.ModifierType.ControlMask) || data.HasShortenedXY()) {
                        newX = x-XOffset;
                        newY = y-XOffset;
                    }
                    else {
                        // Move objects in increments of 8 pixels
                        int unit = 8;
                        int unitLog = (int)Math.Log(unit, 2);

                        int dataX = data.GetX()+unit/2;
                        int dataY = data.GetY()+unit/2;
                        int alignX = (dataX)%unit;
                        int alignY = (dataY)%unit;
                        newX = (x-XOffset-alignX)>>unitLog;
                        newY = (y-YOffset-alignY)>>unitLog;
                        newX = newX*unit+alignX+unit/2;
                        newY = newY*unit+alignY+unit/2;
                    }

                    if (newX >= 0 && newX < 256 && newY >= 0 && newY < 256) {
                        data.SetX((byte)newX);
                        data.SetY((byte)newY);
                    }

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

            if (ViewObjects)
                g.DrawImage(Image, XOffset, YOffset, Image.Width*Scale, Image.Height*Scale);
            else
                base.OnExposeEvent(ev);

            if (ViewObjects && objectEditor != null) {
                // Draw objects

                int cursorX=-1,cursorY=-1;
                int selectedX=-1,selectedY=-1;
                hoveringObjectIndices = new List<int>();

                ObjectGroup group = objectEditor.ObjectGroup;
                DrawObjectGroup(g, 0, ref cursorX, ref cursorY, ref selectedX, ref selectedY, group, objectEditor, ref hoveringObjectIndices);

                // Object hovering over
                if (cursorX != -1)
                    g.DrawRectangle(new Pen(Color.Red), cursorX, cursorY, 15, 15);
                // Object selected
                if (selectedX != -1)
                    g.DrawRectangle(new Pen(Color.White), selectedX, selectedY, 15, 15);
            }


            g.Dispose();

            return true;
        }

        int DrawObjectGroup(Graphics g, int index, ref int cursorX, ref int cursorY, ref int selectedX, ref int selectedY, ObjectGroup group, ObjectGroupEditor editor, ref List<int> objectIndices) {
            if (group == null) return index;

            List<int> localObjectIndices = new List<int>(objectIndices);

            bool foundHoveringMatch = false;

            for (int i=0; i<group.GetNumObjects(); i++) {
                ObjectData data = group.GetObjectData(i);
                if (data.GetObjectType() >= ObjectType.Pointer &&
                        data.GetObjectType() <= ObjectType.AntiBossPointer) {
                    ObjectGroup nextGroup = data.GetPointedObjectGroup();
                    if (nextGroup != null) {
                        List<int> pointerObjectIndices = new List<int>(objectIndices);
                        pointerObjectIndices.Add(i);
                        if (editor != null && i == editor.SelectedIndex)
                            index = DrawObjectGroup(g, index, ref cursorX, ref cursorY,
                                    ref selectedX, ref selectedY, nextGroup, editor.SubEditor, ref pointerObjectIndices);
                        else
                            index = DrawObjectGroup(g, index, ref cursorX, ref cursorY,
                                    ref selectedX, ref selectedY, nextGroup, null, ref pointerObjectIndices);
                        if (pointerObjectIndices.Count > objectIndices.Count+1)
                            localObjectIndices = pointerObjectIndices;
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
                        // Objects with specific positions get
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
                        selectedX = x-8 + XOffset;
                        selectedY = y-8 + YOffset;
                    }
                    if (mouseX-XOffset >= x-8 && mouseX-XOffset < x+8 &&
                            mouseY-YOffset >= y-8 && mouseY-YOffset < y+8) {
                        if (localObjectIndices.Count == objectIndices.Count) {
                            if (foundHoveringMatch)
                                localObjectIndices[localObjectIndices.Count-1] = i;
                            else
                                localObjectIndices.Add(i);
                            cursorX = x-8 + XOffset;
                            cursorY = y-8 + YOffset;
                            foundHoveringMatch = true;
                        }
                    }

                    // x and y are the center coordinates for the object

                    if (ViewObjectBoxes)
                        g.FillRectangle(new SolidBrush(color), x-width/2 + XOffset, y-width/2 + YOffset, width, width);

                    if (data.GetGameObject() != null) {
                        try {
                            ObjectAnimationFrame o = data.GetGameObject().DefaultAnimation.GetFrame(0);
                            o.Draw(g, x+XOffset, y+YOffset);
                        }
                        catch(NoAnimationException) {
                            // No animation defined
                        }
                        catch(InvalidAnimationException) {
                            // Error parsing an animation; draw a blue X to indicate the error
                            int xPos = x-width/2 + XOffset;
                            int yPos = y-width/2 + YOffset;
                            g.DrawLine(new Pen(Color.Blue), xPos, yPos, xPos+width-1, yPos+width-1);
                            g.DrawLine(new Pen(Color.Blue), xPos+width-1, yPos, xPos, yPos+width-1);
                        }
                    }
                }
            }

            objectIndices = localObjectIndices;
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
            requisition.Width = 0x10*16;
            requisition.Height = 0x10*16;
        }
    }
}
