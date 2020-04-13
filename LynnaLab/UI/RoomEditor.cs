using System;
using Bitmap = System.Drawing.Bitmap;
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

        public bool ViewObjects {
            get {
                return _viewObjects;
            }
            set {
                _viewObjects = value;
                base.Hoverable = !DrawRoomComponents;
                GenerateRoomComponents();
            }
        }

        public bool ViewWarps {
            get { return _viewWarps; }
            set {
                _viewWarps = value;
                base.Hoverable = !DrawRoomComponents;
                GenerateRoomComponents();
            }
        }

        public TileGridViewer TilesetViewer { get; set; }

        public ObjectGroupEditor ObjectGroupEditor {
            get { return _objectEditor; }
            set {
                if (_objectEditor != value) {
                    _objectEditor = value;
                    _objectEditor.RoomEditor = this;
                }
            }
        }

        public WarpEditor WarpEditor {
            get { return _warpEditor; }
            set {
                if (_warpEditor != null)
                    _warpEditor.SelectedWarpEvent -= OnWarpSelected;
                _warpEditor = value;
                _warpEditor.SelectedWarpEvent += OnWarpSelected;
            }
        }


        protected override Bitmap Image {
            get {
                if (room == null)
                    return null;
                return room.GetImage();
            }
        }

        Project Project {
            get { return room.Project; }
        }

        bool DrawRoomComponents {
            get { return ViewObjects || ViewWarps; }
        }


        // Variables

        Room room;
        ObjectGroupEditor _objectEditor;
        WarpEditor _warpEditor;

        List<RoomComponent> roomComponents;

        int mouseX=-1,mouseY=-1;

        bool _viewObjects, _viewWarps;
        bool draggingObject;

        RoomComponent hoveringComponent;
        RoomComponent selectedComponent;

        Gdk.ModifierType gdkState;

        public RoomEditor() {
            base.TileWidth = 16;
            base.TileHeight = 16;
            base.Halign = Gtk.Align.Start;
            base.Valign = Gtk.Align.Start;

            ViewWarps = true;

            this.ButtonPressEvent += delegate(object o, ButtonPressEventArgs args)
            {
                if (TilesetViewer == null)
                    return;
                int x,y;
                args.Event.Window.GetPointer(out x, out y, out gdkState);
                UpdateMouse(x,y);
                if (gdkState.HasFlag(Gdk.ModifierType.Button1Mask))
                    OnClicked(x, y);
                if (IsInBounds(x, y)) {
                    Cairo.Point p = GetGridPosition(x, y);
                    if (gdkState.HasFlag(Gdk.ModifierType.Button3Mask))
                        TilesetViewer.SelectedIndex = room.GetTile(p.X, p.Y);
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
                if (TilesetViewer == null)
                    return;
                int x,y;
                args.Event.Window.GetPointer(out x, out y, out gdkState);
                UpdateMouse(x,y);
                if (gdkState.HasFlag(Gdk.ModifierType.Button1Mask))
                    OnDragged(x, y);
            };
        }

        public void SetRoom(Room r) {
            if (room != null) {
                room.RoomModifiedEvent -= OnRoomModified;
                room.GetObjectGroup().AddModifiedHandler(OnObjectModified);
                room.GetWarpSourceGroup().RemoveModifiedHandler(OnWarpSourceModified);
            }
            r.RoomModifiedEvent += OnRoomModified;
            r.GetObjectGroup().AddModifiedHandler(OnObjectModified);
            r.GetWarpSourceGroup().AddModifiedHandler(OnWarpSourceModified);

            room = r;
            Width = room.Width;
            Height = room.Height;

            ObjectGroupEditor.SetObjectGroup(room.GetObjectGroup());

            GenerateRoomComponents();
            selectedComponent = null;
            QueueDraw();
        }

        public void OnRoomModified() {
            QueueDraw();
        }

        // Called when an object is selected in the ObjectGroupEditor
        public void OnObjectSelected() {
            if ((selectedComponent as ObjectRoomComponent)?.obj != ObjectGroupEditor.SelectedObject) {
                foreach (RoomComponent com in roomComponents) {
                    if (!(com is ObjectRoomComponent))
                        continue;
                    var objCom = com as ObjectRoomComponent;
                    if (objCom.obj.ObjectGroup == ObjectGroupEditor.SelectedObjectGroup
                            && objCom.obj.Index == ObjectGroupEditor.SelectedIndex) {
                        selectedComponent = objCom;
                        break;
                    }
                }
                QueueDraw();
            }
        }

        // Called when a warp is selected from the WarpEditor
        void OnWarpSelected(object sender, EventArgs args) {
            foreach (RoomComponent com in roomComponents) {
                if (!(com is WarpSourceRoomComponent))
                    continue;
                var warpCom = com as WarpSourceRoomComponent;
                if (WarpEditor.SelectedIndex == warpCom.index) {
                    selectedComponent = warpCom;
                    break;
                }
            }
            QueueDraw();
        }

        // Called when the ObjectGroup is modified
        void OnObjectModified(object sender, EventArgs args) {
            GenerateRoomComponents();
            QueueDraw();
        }

        // Called when the WarpSourceGroup is modified
        void OnWarpSourceModified(object sender, EventArgs args) {
            GenerateRoomComponents();
            QueueDraw();
        }

        void UpdateMouse(int x, int y) {
            int newX = (x - XOffset) / Scale;
            int newY = (y - YOffset) / Scale;
            if (mouseX != newX || mouseY != newY) {
                mouseX = newX;
                mouseY = newY;

                if (DrawRoomComponents) // Laziness; not checking where the mouse is
                    QueueDraw();
            }
        }

        void OnClicked(int posX, int posY) {
            Cairo.Point p = GetGridPosition(posX, posY);
            if (!DrawRoomComponents) {
                if (!IsInBounds(posX, posY))
                    return;
                room.SetTile(p.X, p.Y, TilesetViewer.SelectedIndex);
            }
            else {
                if (hoveringComponent != null) {
                    selectedComponent = hoveringComponent;
                    hoveringComponent.Select();
                    draggingObject = true;
                }
            }
            QueueDraw();
        }

        void OnDragged(int x, int y) {
            if (!DrawRoomComponents)
                OnClicked(x,y);
            else {
                if (!draggingObject)
                    return;

                RoomComponent com = selectedComponent;
                if (com != null && com.HasXY) {
                    int newX,newY;
                    if (gdkState.HasFlag(Gdk.ModifierType.ControlMask) || com.HasShortenedXY) {
                        newX = x-XOffset;
                        newY = y-YOffset;
                    }
                    else {
                        // Move comects in increments of 8 pixels
                        int unit = 8;
                        int unitLog = (int)Math.Log(unit, 2);

                        int dataX = com.X+unit/2;
                        int dataY = com.Y+unit/2;
                        int alignX = (dataX)%unit;
                        int alignY = (dataY)%unit;
                        newX = (x-XOffset-alignX)>>unitLog;
                        newY = (y-YOffset-alignY)>>unitLog;
                        newX = newX*unit+alignX+unit/2;
                        newY = newY*unit+alignY+unit/2;
                    }

                    if (newX >= 0 && newX < 256 && newY >= 0 && newY < 256) {
                        com.X = (byte)newX;
                        com.Y = (byte)newY;
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

        protected override bool OnDrawn(Cairo.Context cr) {
            base.OnDrawn(cr);

            cr.Translate(XOffset, YOffset);
            cr.Scale(Scale, Scale);

            hoveringComponent = null;

            foreach (RoomComponent com in roomComponents) {
                cr.SetSourceColor(com.BoxColor);
                cr.Rectangle(com.BoxRectangle);
                cr.Fill();
                com.Draw(cr);

                if (CairoHelper.PointInRect(mouseX, mouseY, com.BoxRectangle)) {
                    hoveringComponent = com;
                }
            }

            // Object hovering over
            if (hoveringComponent != null) {
                cr.SetSourceColor(TileGridViewer.HoverColor);
                CairoHelper.DrawRectOutline(cr, 2, hoveringComponent.BoxRectangle);

            }
            // Object selected
            if (selectedComponent != null) {
                cr.SetSourceColor(TileGridViewer.SelectionColor);
                CairoHelper.DrawRectOutline(cr, 1, selectedComponent.BoxRectangle);
            }

            return true;
        }

        void GenerateRoomComponents() {
            if (Room == null)
                return;

            roomComponents = new List<RoomComponent>();
            hoveringComponent = null;

            if (ViewObjects && ObjectGroupEditor.TopObjectGroup != null) {
                foreach (ObjectGroup group in ObjectGroupEditor.TopObjectGroup.GetAllGroups()) {
                    for (int i=0; i<group.GetNumObjects(); i++) {
                        ObjectDefinition obj = group.GetObject(i);
                        if (!obj.HasXY())
                            continue;
                        ObjectRoomComponent com = new ObjectRoomComponent(obj);
                        com.SelectedEvent += (sender, args) => {
                            ObjectGroupEditor.SelectObject(obj.ObjectGroup, obj.Index);
                        };
                        roomComponents.Add(com);
                    }
                }
            }

            if (ViewWarps) {
                int index = 0;

                foreach (WarpSourceData warp in room.GetWarpSourceGroup().GetWarpSources()) {
                    Action<int,int,int,int> drawWarpBox = (x, y, width, height) => {
                        var rect = new Cairo.Rectangle(x, y, width, height);
                        var com = new WarpSourceRoomComponent(warp, index, rect);
                        com.SelectedEvent += (sender, args) => {
                            WarpEditor.SetWarpIndex(com.index);
                        };
                        roomComponents.Add(com);
                    };

                    if (warp.WarpSourceType == WarpSourceType.Standard) {
                        int middle = ((Room.Width + 1) / 2) * 16;
                        int right = Room.Width * 16;
                        int bottom = Room.Height * 16 - 8;

                        if (warp.TopLeft)
                            drawWarpBox(0, -8, middle, 16);
                        if (warp.TopRight)
                            drawWarpBox(middle, -8, right - middle, 16);
                        if (warp.BottomLeft)
                            drawWarpBox(0, bottom, middle, 16);
                        if (warp.BottomRight)
                            drawWarpBox(middle, bottom, right - middle, 16);

                        if (!warp.TopLeft && !warp.TopRight && !warp.BottomLeft && !warp.BottomRight) {
                            drawWarpBox(0, 16 * 13, Room.Width * 16, 32);
                        }
                    }
                    else if (warp.WarpSourceType == WarpSourceType.Pointed) {
                        drawWarpBox(warp.X * TileWidth, warp.Y * TileHeight, TileWidth, TileHeight);
                    }
                    index++;
                }
            }


            // The "selectedComponent" now refers to an old object. Look for the corresponding new
            // object.
            RoomComponent newSelectedComponent = null;

            foreach (RoomComponent com in roomComponents) {
                if (com.Compare(selectedComponent)) {
                    newSelectedComponent = com;
                    break;
                }
            }

            selectedComponent = newSelectedComponent;
        }

        protected override void OnSizeAllocated(Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated(allocation);

            // Offset the image so that objects at position (0,0) can be fully drawn
            base.XOffset = Math.Max(base.XOffset, 8);
            base.YOffset = Math.Max(base.YOffset, 8);
        }

        // Override preferred width/height so that objects can be drawn even outside normal room
        // boundaries.
        protected override void OnGetPreferredHeight(out int minimum_height, out int natural_height) {
            minimum_height = 17*16*Scale;
            natural_height = minimum_height;
        }        
        protected override void OnGetPreferredWidth(out int minimum_width, out int natural_width) {
            minimum_width = 17*16*Scale;
            natural_width = minimum_width;
        }        


        // "Room Component" classes: things drawn on top of the room (like objects and warps) which
        // can be selected and moved around.

        abstract class RoomComponent {
            public event EventHandler<EventArgs> SelectedEvent;

            public abstract Cairo.Color BoxColor { get; }

            public abstract bool HasXY { get; }
            public abstract bool HasShortenedXY { get; }
            public abstract int X { get; set; } // These are always in the full range even if "ShortPosition" is true
            public abstract int Y { get; set; }
            public abstract int BoxWidth { get; }
            public abstract int BoxHeight { get; }

            public virtual Cairo.Rectangle BoxRectangle {
                get { return new Cairo.Rectangle(X - BoxWidth / 2.0, Y - BoxHeight / 2.0, BoxWidth, BoxHeight); }
            }

            public abstract void Draw(Cairo.Context cr);

            public virtual void Select() {
                if (SelectedEvent != null)
                    SelectedEvent(this, null);
            }

            public abstract bool Compare(RoomComponent com); // Returns true if the underlying data is the same
            public abstract void Delete(); // Right-click, select "delete", or press "Delete" key while selected
        }

        class ObjectRoomComponent : RoomComponent {
            public ObjectDefinition obj;


            public ObjectRoomComponent(ObjectDefinition obj) {
                this.obj = obj;
            }


            public override Cairo.Color BoxColor {
                get {
                    Cairo.Color color = ObjectGroupEditor.GetObjectColor(obj.GetObjectType());
                    return new Cairo.Color(color.R, color.G, color.B, 0.75);
                }
            }

            public override bool HasXY {
                get { return obj.HasXY(); }
            }
            public override bool HasShortenedXY {
                get { return obj.HasShortenedXY(); }
            }
            public override int X {
                get { return obj.GetX(); }
                set { obj.SetX((byte)value); }
            }
            public override int Y {
                get { return obj.GetY(); }
                set { obj.SetY((byte)value); }
            }
            public override int BoxWidth { get { return 16; } }
            public override int BoxHeight { get { return 16; } }


            public override void Draw(Cairo.Context cr) {
                int x = X;
                int y = Y;

                if (obj.GetGameObject() != null) {
                    try {
                        ObjectAnimationFrame o = obj.GetGameObject().DefaultAnimation.GetFrame(0);
                        o.Draw(cr, x, y);
                    }
                    catch(NoAnimationException) {
                        // No animation defined
                    }
                    catch(InvalidAnimationException) {
                        // Error parsing an animation; draw a blue X to indicate the error
                        double xPos = x - BoxHeight / 2 + 0.5;
                        double yPos = y - BoxHeight / 2 + 0.5;

                        cr.SetSourceColor(new Cairo.Color(1.0, 0, 0));
                        cr.MoveTo(xPos, yPos);
                        cr.LineTo(xPos + BoxWidth - 1, yPos + BoxHeight - 1);
                        cr.MoveTo(xPos + BoxWidth - 1, yPos);
                        cr.LineTo(xPos, yPos + BoxHeight - 1);
                        cr.Stroke();
                    }
                }
            }

            public override bool Compare(RoomComponent com) {
                return obj == (com as ObjectRoomComponent)?.obj;
            }

            public override void Delete() {
                throw new NotImplementedException();
            }
        }

        class WarpSourceRoomComponent : RoomComponent {
            public WarpSourceData data;
            public int index;

            Cairo.Rectangle rect;


            public WarpSourceRoomComponent(WarpSourceData data, int index, Cairo.Rectangle rect) {
                this.data = data;
                this.index = index;
                this.rect = rect;
            }


            public override Cairo.Color BoxColor {
                get {
                    return CairoHelper.ConvertColor(186, 8, 206, 0xc0);
                }
            }

            public override bool HasXY {
                get { return data.WarpSourceType == WarpSourceType.Pointed; }
            }
            public override bool HasShortenedXY {
                get { return true; }
            }
            public override int X {
                get { return data.X * 16 + 8; }
                set {
                    data.X = value / 16;
                    UpdateRect();
                }
            }
            public override int Y {
                get { return data.Y * 16 + 8; }
                set {
                    data.Y = value / 16;
                    UpdateRect();
                }
            }

            public override int BoxWidth { get { return (int)rect.Width; } }
            public override int BoxHeight { get { return (int)rect.Height; } }

            public override Cairo.Rectangle BoxRectangle { get { return rect; } }


            public override void Draw(Cairo.Context cr) {
                cr.SetSourceColor(new Cairo.Color(1, 1, 1));
                CairoHelper.DrawText(cr, index.ToString("X"), 9, BoxRectangle);
            }

            public override bool Compare(RoomComponent com) {
                return data == (com as WarpSourceRoomComponent)?.data;
            }

            public override void Delete() {
                throw new NotImplementedException();
            }


            void UpdateRect() {
                rect = new Cairo.Rectangle(X - rect.Width / 2.0, Y - rect.Height / 2.0, rect.Width, rect.Height);
            }
        }
    }

}
