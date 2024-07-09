using System;
using System.Collections.Generic;
using Gtk;

using Util;
using LynnaLib;

namespace LynnaLab
{
    public class RoomChangedEventArgs
    {
        public Room room;
        public bool fromFollowWarp;
    }

    [System.ComponentModel.ToolboxItem(true)]
    public class RoomEditor : TileGridViewer
    {
        public static readonly Color ObjectHoverColor = Color.Cyan;


        // Variables

        Room room;
        int season;

        ObjectGroupEditor _objectEditor;
        WarpEditor _warpEditor;
        Warp _editingWarpDestination;

        WeakEventWrapper<Room> roomEventWrapper = new WeakEventWrapper<Room>();
        WeakEventWrapper<Chest> chestEventWrapper = new WeakEventWrapper<Chest>();

        List<RoomComponent> roomComponents = new List<RoomComponent>();

        int mouseX = -1, mouseY = -1;

        bool _enableTileEditing, _viewObjects, _viewWarps, _viewChests;
        bool draggingTile, draggingObject;

        RoomComponent hoveringComponent;
        RoomComponent selectedComponent;

        Gdk.ModifierType gdkState;

        // Events

        public event EventHandler<RoomChangedEventArgs> RoomChangedEvent;
        public event EventHandler<bool> WarpDestEditModeChangedEvent;


        // Constructors

        public RoomEditor()
        {
            base.TileWidth = 16;
            base.TileHeight = 16;
            base.Halign = Gtk.Align.Start;
            base.Valign = Gtk.Align.Start;

            EnableTileEditing = true;

            this.ButtonPressEvent += delegate (object o, ButtonPressEventArgs args)
            {
                if (TilesetViewer == null)
                    return;
                int x, y;
                args.Event.Window.GetDevicePosition(args.Event.Device, out x, out y, out gdkState);
                UpdateMouse(x, y);
                OnClicked(mouseX, mouseY, args.Event, args.Event.Button);
            };
            this.ButtonReleaseEvent += delegate (object o, ButtonReleaseEventArgs args)
            {
                if (args.Event.Button == 1)
                {
                    draggingObject = false;
                    draggingTile = false;
                }
            };
            this.MotionNotifyEvent += delegate (object o, MotionNotifyEventArgs args)
            {
                if (TilesetViewer == null)
                    return;
                int x, y;
                args.Event.Window.GetDevicePosition(args.Event.Device, out x, out y, out gdkState);
                UpdateMouse(x, y);
                if (gdkState.HasFlag(Gdk.ModifierType.Button1Mask))
                    OnDragged(mouseX, mouseY, args.Event);
            };

            roomEventWrapper.Bind<EventArgs>("ChestAddedEvent", OnChestAdded);

            chestEventWrapper.Bind<EventArgs>("DeletedEvent", OnChestDeleted);
            chestEventWrapper.Bind<ValueModifiedEventArgs>("ModifiedEvent", OnChestModified);
            chestEventWrapper.Bind<ValueModifiedEventArgs>("TreasureModifiedEvent", OnTreasureModified);
        }


        // Properties

        public Room Room
        {
            get { return room; }
        }
        public int Season
        {
            get { return season; }
        }
        public RoomLayout RoomLayout
        {
            get
            {
                if (room == null)
                    return null;
                if (room.HasSeasons)
                    return room.GetLayout(season);
                else
                    return room.GetLayout(-1);
            }
        }

        public QuickstartData QuickstartData { get; } = new QuickstartData();

        public bool EnableTileEditing
        {
            get
            {
                return _enableTileEditing && EditingWarpDestination == null;
            }
            set
            {
                if (value != _enableTileEditing)
                {
                    _enableTileEditing = value;
                    base.Hoverable = value;
                    if (value)
                        EnableDragFillAction();
                    else
                        DisableDragFillAction();
                }
            }
        }

        public bool ViewObjects
        {
            get
            {
                return _viewObjects;
            }
            set
            {
                _viewObjects = value;
                GenerateRoomComponents();
            }
        }

        public bool ViewWarps
        {
            get { return _viewWarps; }
            set
            {
                _viewWarps = value;
                GenerateRoomComponents();
            }
        }

        public bool ViewChests
        {
            get
            {
                return _viewChests;
            }
            set
            {
                _viewChests = value;
                GenerateRoomComponents();
            }
        }

        public TileGridViewer TilesetViewer { get; set; }

        public ObjectGroupEditor ObjectGroupEditor
        {
            get { return _objectEditor; }
            set
            {
                if (_objectEditor != value)
                {
                    _objectEditor = value;
                    _objectEditor.RoomEditor = this;
                }
            }
        }

        public WarpEditor WarpEditor
        {
            get { return _warpEditor; }
            set
            {
                if (_warpEditor != null)
                    _warpEditor.SelectedWarpEvent -= OnWarpSelected;
                _warpEditor = value;
                _warpEditor.SelectedWarpEvent += OnWarpSelected;
            }
        }

        // This is set to a warp when we're in "warp destination editing mode".
        public Warp EditingWarpDestination
        {
            get
            {
                return _editingWarpDestination;
            }
            private set
            {
                if (_editingWarpDestination != null)
                    _editingWarpDestination.RemoveModifiedHandler(OnDestinationWarpModified);

                _editingWarpDestination = value;

                if (_editingWarpDestination != null)
                    _editingWarpDestination.AddModifiedHandler(OnDestinationWarpModified);

                GenerateRoomComponents();
                WarpDestEditModeChangedEvent?.Invoke(this, _editingWarpDestination != null);
            }
        }

        protected override Bitmap Image
        {
            get
            {
                return RoomLayout?.GetImage();
            }
        }

        Project Project
        {
            get { return room.Project; }
        }

        // Refers to the (one and only) chest in the room, or null.
        Chest Chest
        {
            get; set;
        }

        // Should we draw the room components (objects, warps)?
        bool DrawRoomComponents
        {
            get
            {
                return ViewObjects
                    || ViewWarps
                    || ViewChests
                    || EditingWarpDestination != null
                    || QuickstartInCurrentRoom;
            }
        }

        // Should we be able to select & drag the room components?
        bool SelectRoomComponents
        {
            get { return DrawRoomComponents; }
        }

        bool DrawTileHover
        {
            get { return EnableTileEditing && (draggingTile || (hoveringComponent == null && !draggingObject)); }
        }

        bool DrawRoomComponentHover
        {
            get { return DrawRoomComponents && !draggingObject && !draggingTile; }
        }

        bool QuickstartInCurrentRoom
        {
            get
            {
                return QuickstartData.enabled
                    && Room.Group == QuickstartData.group
                    && (Room.Index & 0xff) == QuickstartData.room;
            }
        }



        // Methods

        public void SetRoom(Room r, int season, bool changedFromWarpFollow = false)
        {
            if (r == Room && this.season == season)
                return;

            if (room != null)
            {
                RoomLayout.LayoutModifiedEvent -= OnLayoutModified;
                room.GetObjectGroup().RemoveModifiedHandler(OnObjectModified);
                room.GetWarpGroup().RemoveModifiedHandler(OnWarpModified);
            }

            room = r;
            if (season != -1)
            {
                this.season = season;
                if (QuickstartInCurrentRoom)
                    QuickstartData.season = (byte)season;
            }

            if (room != null)
            {
                RoomLayout.LayoutModifiedEvent += OnLayoutModified;
                room.GetObjectGroup().AddModifiedHandler(OnObjectModified);
                room.GetWarpGroup().AddModifiedHandler(OnWarpModified);

                Width = room.Width;
                Height = room.Height;

                ObjectGroupEditor.SetObjectGroup(room.GetObjectGroup());
            }

            if (r == null)
                EditingWarpDestination = null;

            GenerateRoomComponents();
            selectedComponent = null;

            if (EditingWarpDestination != null)
                EditingWarpDestination.DestRoom = r;

            roomEventWrapper.ReplaceEventSource(room);
            UpdateChestEvents();

            RoomChangedEvent?.Invoke(this,
                    new RoomChangedEventArgs { room = r, fromFollowWarp = changedFromWarpFollow });

            QueueDraw();
        }

        public void OnLayoutModified()
        {
            QueueDraw();
        }

        // Called when an object is selected in the ObjectGroupEditor
        public void OnObjectSelected()
        {
            if ((selectedComponent as ObjectRoomComponent)?.obj != ObjectGroupEditor.SelectedObject)
            {
                selectedComponent = null;
                foreach (RoomComponent com in roomComponents)
                {
                    if (!(com is ObjectRoomComponent))
                        continue;
                    var objCom = com as ObjectRoomComponent;
                    if (objCom.obj.ObjectGroup == ObjectGroupEditor.SelectedObjectGroup
                            && objCom.obj.Index == ObjectGroupEditor.SelectedIndex)
                    {
                        selectedComponent = objCom;
                        break;
                    }
                }
                QueueDraw();
            }
        }

        public void OnQuickstartModified()
        {
            GenerateRoomComponents();
        }

        // Called when a warp is selected from the WarpEditor
        void OnWarpSelected(object sender, EventArgs args)
        {
            foreach (RoomComponent com in roomComponents)
            {
                Warp warp;
                if (com is WarpSourceRoomComponent)
                    warp = (com as WarpSourceRoomComponent).warp;
                else if (com is WarpDestRoomComponent)
                    warp = (com as WarpDestRoomComponent).warp;
                else
                    continue;
                if (WarpEditor.SelectedWarp == warp)
                {
                    selectedComponent = com;
                    break;
                }
            }
            QueueDraw();
        }

        // Called when the ObjectGroup is modified
        void OnObjectModified(object sender, EventArgs args)
        {
            GenerateRoomComponents();
        }

        // Called when the "EditingWarpDestination" is modified
        void OnDestinationWarpModified(object sender, EventArgs args)
        {
            if (EditingWarpDestination.DestRoom != Room)
                SetRoom(EditingWarpDestination.DestRoom, season);
            else
            {
                GenerateRoomComponents();
            }
        }

        // Called when the WarpGroup is modified (not the warp destination if in that mode)
        void OnWarpModified(object sender, EventArgs args)
        {
            GenerateRoomComponents();
        }

        void OnChestAdded(object sender, EventArgs args)
        {
            UpdateChestEvents();
            GenerateRoomComponents();
        }
        void OnChestDeleted(object sender, EventArgs args)
        {
            UpdateChestEvents();
            GenerateRoomComponents();
        }
        // Chest variables modified (NOT contents of treasure itself)
        void OnChestModified(object sender, ValueModifiedEventArgs args)
        {
            QueueDraw();
        }
        // Treasure contents modified
        void OnTreasureModified(object sender, ValueModifiedEventArgs args)
        {
            QueueDraw();
        }

        void UpdateChestEvents()
        {
            chestEventWrapper.ReplaceEventSource(room?.Chest);
        }

        void UpdateMouse(int x, int y)
        {
            int newX = (x - XOffset) / Scale;
            int newY = (y - YOffset) / Scale;
            if (mouseX != newX || mouseY != newY)
            {
                mouseX = newX;
                mouseY = newY;

                if (DrawRoomComponents) // Laziness; not checking where the mouse is
                    QueueDraw();
            }
        }

        void OnClicked(int posX, int posY, Gdk.Event triggerEvent, uint button)
        {
            if (base.IsSelectingRange)
                return;

            Cairo.Point p = GetGridPosition(posX, posY, scale: false, offset: false);
            if (EnableTileEditing && hoveringComponent == null)
            {
                if (!IsInBounds(posX, posY, scale: false, offset: false))
                    return;
                if (button == 1)
                { // Left-click
                    RoomLayout.SetTile(p.X, p.Y, TilesetViewer.SelectedIndex);
                    draggingTile = true;
                }
                else if (button == 3)
                { // Right-click
                    TilesetViewer.SelectedIndex = RoomLayout.GetTile(p.X, p.Y);
                    if (selectedComponent != null)
                    {
                        selectedComponent = null;
                        GenerateRoomComponents();
                    }
                }
            }
            if (DrawRoomComponents)
            {
                if (hoveringComponent != null)
                {
                    selectedComponent = hoveringComponent;
                    hoveringComponent.Select();

                    if (button == 1)
                    { // Left click
                        draggingObject = true;
                    }
                    else if (button == 3)
                    { // Right click
                        Gtk.Menu menu = new Gtk.Menu();

                        foreach (Gtk.MenuItem item in selectedComponent.GetRightClickMenuItems())
                        {
                            menu.Add(item);
                        }

                        RoomComponent comp = selectedComponent;

                        if (comp.Deletable)
                        {
                            if (menu.Children.Length != 0)
                                menu.Add(new Gtk.SeparatorMenuItem());

                            var deleteButton = new Gtk.MenuItem("Delete");
                            deleteButton.Activated += (sender, args) =>
                            {
                                comp.Delete();
                            };
                            menu.Add(deleteButton);
                        }

                        menu.AttachToWidget(this, null);
                        menu.ShowAll();
                        menu.PopupAtPointer(triggerEvent);
                    }
                }
            }
            QueueDraw();
        }

        void OnDragged(int x, int y, Gdk.Event triggerEvent)
        {
            if (EnableTileEditing && draggingTile)
            {
                if (IsInBounds(x, y, scale: false, offset: false))
                {
                    Cairo.Point p = GetGridPosition(x, y, scale: false, offset: false);
                    RoomLayout.SetTile(p.X, p.Y, TilesetViewer.SelectedIndex);
                }
            }
            if (DrawRoomComponents && draggingObject)
            {
                RoomComponent com = selectedComponent;
                if (com != null && com.HasXY)
                {
                    int newX, newY;
                    if (gdkState.HasFlag(Gdk.ModifierType.ControlMask) || com.HasShortenedXY)
                    {
                        newX = x;
                        newY = y;
                    }
                    else
                    {
                        // Move comects in increments of 8 pixels
                        int unit = 8;
                        int unitLog = (int)Math.Log(unit, 2);

                        int dataX = com.X + unit / 2;
                        int dataY = com.Y + unit / 2;
                        int alignX = (dataX) % unit;
                        int alignY = (dataY) % unit;
                        newX = (x - alignX) >> unitLog;
                        newY = (y - alignY) >> unitLog;
                        newX = newX * unit + alignX + unit / 2;
                        newY = newY * unit + alignY + unit / 2;
                    }

                    if (newX >= 0 && newX < 256 && newY >= 0 && newY < 256)
                    {
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

        protected override bool OnDrawn(Cairo.Context cr)
        {
            base.DrawBackground(cr);

            if (room == null)
                return true;

            cr.Save();

            cr.Translate(XOffset, YOffset);
            cr.Scale(Scale, Scale);

            hoveringComponent = null;

            foreach (RoomComponent com in roomComponents)
            {
                if (com.BoxColor != null)
                {
                    cr.SetSourceColor(com.BoxColor);
                    cr.Rectangle(com.BoxRectangle);
                    cr.Fill();
                }

                com.Draw(cr);

                if (DrawRoomComponentHover)
                {
                    if (CairoHelper.PointInRect(mouseX, mouseY, com.BoxRectangle))
                    {
                        hoveringComponent = com;
                    }
                }
            }

            if (SelectRoomComponents)
            {
                // Object hovering over
                if (hoveringComponent != null)
                {
                    cr.SetSourceColor(ObjectHoverColor);
                    CairoHelper.DrawRectOutline(cr, 1, hoveringComponent.BoxRectangle);

                }
                // Object selected
                if (selectedComponent != null)
                {
                    cr.SetSourceColor(TileGridViewer.DefaultSelectionColor);
                    CairoHelper.DrawRectOutline(cr, 1, selectedComponent.BoxRectangle);
                }
            }

            cr.Restore();

            if (DrawTileHover)
                base.DrawHoverAndSelection(cr);

            return true;
        }

        void GenerateRoomComponents()
        {
            if (Room == null)
                return;

            roomComponents = new List<RoomComponent>();
            hoveringComponent = null;

            // We only draw the 1 component if we're editing a warp destination
            if (EditingWarpDestination != null)
            {
                WarpDestRoomComponent com = new WarpDestRoomComponent(this, EditingWarpDestination);
                com.SelectedEvent += (sender, args) =>
                {
                    WarpEditor.SetSelectedWarp(com.warp);
                };
                roomComponents.Add(com);
                goto addedAllComponents; // I love being evil
            }

            if (ViewObjects && ObjectGroupEditor.TopObjectGroup != null)
            {
                foreach (ObjectGroup group in ObjectGroupEditor.TopObjectGroup.GetAllGroups())
                {
                    for (int i = 0; i < group.GetNumObjects(); i++)
                    {
                        ObjectDefinition obj = group.GetObject(i);
                        if (!obj.HasXY())
                            continue;
                        ObjectRoomComponent com = new ObjectRoomComponent(obj);
                        com.SelectedEvent += (sender, args) =>
                        {
                            ObjectGroupEditor.SelectObject(obj.ObjectGroup, obj.Index);
                        };
                        roomComponents.Add(com);
                    }
                }
            }

            if (ViewWarps)
            {
                int index = 0;
                WarpGroup group = room.GetWarpGroup();

                foreach (Warp warp in group.GetWarps())
                {
                    Action<int, int, int, int> addWarpComponent = (x, y, width, height) =>
                    {
                        var rect = new Cairo.Rectangle(x, y, width, height);
                        var com = new WarpSourceRoomComponent(this, warp, index, rect);
                        com.SelectedEvent += (sender, args) =>
                        {
                            WarpEditor.SetWarpIndex(com.index);
                        };
                        roomComponents.Add(com);
                    };

                    if (warp.WarpSourceType == WarpSourceType.Standard)
                    {
                        int middle;
                        if (Room.Width == 15) // Large room
                            middle = ((Room.Width + 1) / 2) * 16;
                        else // Small room
                            middle = ((Room.Width + 1) / 2) * 16 + 8;
                        int right = Room.Width * 16;
                        int bottom = Room.Height * 16 - 8;

                        if (warp.TopLeft)
                            addWarpComponent(0, -8, middle, 16);
                        if (warp.TopRight)
                            addWarpComponent(middle, -8, right - middle, 16);
                        if (warp.BottomLeft)
                            addWarpComponent(0, bottom, middle, 16);
                        if (warp.BottomRight)
                            addWarpComponent(middle, bottom, right - middle, 16);

                        if (!warp.TopLeft && !warp.TopRight && !warp.BottomLeft && !warp.BottomRight)
                        {
                            addWarpComponent(0, 16 * 13, Room.Width * 16, 32);
                        }
                    }
                    else if (warp.WarpSourceType == WarpSourceType.Position)
                    {
                        addWarpComponent(warp.SourceX * TileWidth, warp.SourceY * TileHeight, TileWidth, TileHeight);
                    }
                    index++;
                }
            }

            if (ViewChests)
            {
                if (Room.Chest != null)
                {
                    ChestRoomComponent com = new ChestRoomComponent(Room.Chest);
                    roomComponents.Add(com);
                }
            }

            if (QuickstartInCurrentRoom)
            {
                QuickstartRoomComponent com = new QuickstartRoomComponent(this, QuickstartData);
                roomComponents.Add(com);
            }


        addedAllComponents:
            // The "selectedComponent" now refers to an old object. Look for the corresponding new
            // object.
            RoomComponent newSelectedComponent = null;

            foreach (RoomComponent com in roomComponents)
            {
                if (com.Compare(selectedComponent))
                {
                    newSelectedComponent = com;
                    break;
                }
            }

            selectedComponent = newSelectedComponent;

            QueueDraw();
        }

        void EnableDragFillAction()
        {
            // Define Click & Drag action with Ctrl key
            base.AddMouseAction(
                MouseButton.LeftClick,
                MouseModifier.Ctrl | MouseModifier.Drag,
                GridAction.SelectRangeCallback,
                (sender, args) =>
                {
                    args.Foreach((x, y) =>
                    {
                        RoomLayout.SetTile(x, y, TilesetViewer.SelectedIndex);
                    });
                });
        }

        void DisableDragFillAction()
        {
            base.RemoveMouseAction(
                MouseButton.LeftClick,
                MouseModifier.Ctrl | MouseModifier.Drag);
        }


        protected override void OnSizeAllocated(Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated(allocation);

            // Offset the image so that objects at position (0,0) can be fully drawn
            base.XOffset = Math.Max(base.XOffset, 8 * Scale);
            base.YOffset = Math.Max(base.YOffset, 8 * Scale);
        }

        // Override preferred width/height so that objects can be drawn even outside normal room
        // boundaries.
        protected override void OnGetPreferredHeight(out int minimum_height, out int natural_height)
        {
            minimum_height = 17 * 16 * Scale;
            natural_height = minimum_height;
        }
        protected override void OnGetPreferredWidth(out int minimum_width, out int natural_width)
        {
            minimum_width = 17 * 16 * Scale;
            natural_width = minimum_width;
        }


        // "Room Component" classes: things drawn on top of the room (like objects and warps) which
        // can be selected and moved around.

        abstract class RoomComponent
        {
            public event EventHandler<EventArgs> SelectedEvent;

            public abstract Color BoxColor { get; }

            public abstract bool Deletable { get; }
            public abstract bool HasXY { get; }
            public abstract bool HasShortenedXY { get; }
            public abstract int X { get; set; } // These are always in the full range even if "ShortPosition" is true
            public abstract int Y { get; set; }
            public abstract int BoxWidth { get; }
            public abstract int BoxHeight { get; }

            public virtual Cairo.Rectangle BoxRectangle
            {
                get { return new Cairo.Rectangle(X - BoxWidth / 2.0, Y - BoxHeight / 2.0, BoxWidth, BoxHeight); }
            }

            public abstract void Draw(Cairo.Context cr);

            public virtual void Select()
            {
                if (SelectedEvent != null)
                    SelectedEvent(this, null);
            }

            public virtual IList<Gtk.MenuItem> GetRightClickMenuItems()
            {
                return new List<Gtk.MenuItem>();
            }

            /// Returns true if the underlying data is the same
            public abstract bool Compare(RoomComponent com);

            /// Right-click, select "delete", or press "Delete" key while selected
            public virtual void Delete() {}
        }

        class QuickstartRoomComponent : RoomComponent
        {
            QuickstartData quickstart;
            RoomEditor parent;

            public QuickstartRoomComponent(RoomEditor parent, QuickstartData data)
            {
                this.parent = parent;
                this.quickstart = data;
            }

            public override Color BoxColor
            {
                get { return null; }
            }

            public override bool Deletable
            {
                get { return false; }
            }
            public override bool HasXY
            {
                get { return true; }
            }
            public override bool HasShortenedXY
            {
                get { return false; }
            }
            public override int X
            {
                get { return quickstart.x; }
                set { quickstart.x = (byte)value; }
            }
            public override int Y
            {
                get { return quickstart.y; }
                set { quickstart.y = (byte)value; }
            }
            public override int BoxWidth { get { return 16; } }
            public override int BoxHeight { get { return 16; } }

            public override void Draw(Cairo.Context cr)
            {
                cr.SetSourceSurface(parent.Project.LinkBitmap, X - 8, Y - 8);
                using (Cairo.SurfacePattern pattern = (Cairo.SurfacePattern)cr.GetSource())
                {
                    pattern.Filter = Cairo.Filter.Nearest;
                }
                cr.Paint();
            }

            public override bool Compare(RoomComponent com)
            {
                return quickstart == (com as QuickstartRoomComponent)?.quickstart;
            }
        }

        class ObjectRoomComponent : RoomComponent
        {
            public ObjectDefinition obj;


            public ObjectRoomComponent(ObjectDefinition obj)
            {
                this.obj = obj;
            }


            public override Color BoxColor
            {
                get
                {
                    Color color = ObjectGroupEditor.GetObjectColor(obj.GetObjectType());
                    return Color.FromRgba(color.R, color.G, color.B, (int)(0.75 * 255));
                }
            }

            public override bool Deletable
            {
                get { return true; }
            }
            public override bool HasXY
            {
                get { return obj.HasXY(); }
            }
            public override bool HasShortenedXY
            {
                get { return obj.HasShortenedXY(); }
            }
            public override int X
            {
                get { return obj.GetX(); }
                set { obj.SetX((byte)value); }
            }
            public override int Y
            {
                get { return obj.GetY(); }
                set { obj.SetY((byte)value); }
            }
            public override int BoxWidth { get { return 16; } }
            public override int BoxHeight { get { return 16; } }


            public override void Draw(Cairo.Context cr)
            {
                int x = X;
                int y = Y;

                if (obj.GetGameObject() != null)
                {
                    try
                    {
                        ObjectAnimationFrame o = obj.GetGameObject().DefaultAnimation.GetFrame(0);
                        o.Draw(cr, x, y);
                    }
                    catch (NoAnimationException)
                    {
                        // No animation defined
                    }
                    catch (InvalidAnimationException)
                    {
                        // Error parsing an animation; draw a blue X to indicate the error
                        double xPos = x - BoxHeight / 2 + 0.5;
                        double yPos = y - BoxHeight / 2 + 0.5;

                        cr.SetSourceColor(Color.Red);
                        cr.MoveTo(xPos, yPos);
                        cr.LineTo(xPos + BoxWidth - 1, yPos + BoxHeight - 1);
                        cr.MoveTo(xPos + BoxWidth - 1, yPos);
                        cr.LineTo(xPos, yPos + BoxHeight - 1);
                        cr.Stroke();
                    }
                }
            }

            public override bool Compare(RoomComponent com)
            {
                return obj == (com as ObjectRoomComponent)?.obj;
            }

            public override void Delete()
            {
                obj.Remove();
            }
        }

        class WarpSourceRoomComponent : RoomComponent
        {
            RoomEditor parent;
            public Warp warp;
            public int index;

            Cairo.Rectangle rect;


            public WarpSourceRoomComponent(RoomEditor parent, Warp warp, int index, Cairo.Rectangle rect)
            {
                this.parent = parent;
                this.warp = warp;
                this.index = index;
                this.rect = rect;
            }


            public override Color BoxColor
            {
                get
                {
                    return WarpEditor.WarpSourceColor;
                }
            }

            public override bool Deletable
            {
                get { return true; }
            }
            public override bool HasXY
            {
                get { return warp.WarpSourceType == WarpSourceType.Position; }
            }
            public override bool HasShortenedXY
            {
                get { return true; }
            }
            public override int X
            {
                get { return warp.SourceX * 16 + 8; }
                set
                {
                    warp.SourceX = value / 16;
                    UpdateRect();
                }
            }
            public override int Y
            {
                get { return warp.SourceY * 16 + 8; }
                set
                {
                    warp.SourceY = value / 16;
                    UpdateRect();
                }
            }

            public override int BoxWidth { get { return (int)rect.Width; } }
            public override int BoxHeight { get { return (int)rect.Height; } }

            public override Cairo.Rectangle BoxRectangle { get { return rect; } }


            public override void Draw(Cairo.Context cr)
            {
                cr.SetSourceColor(Color.White);
                CairoHelper.DrawText(cr, index.ToString("X"), 9, BoxRectangle);
            }

            public override IList<Gtk.MenuItem> GetRightClickMenuItems()
            {
                var list = new List<Gtk.MenuItem>();

                {
                    Gtk.MenuItem followButton = new Gtk.MenuItem("Follow");
                    followButton.Activated += (sender, args) =>
                    {
                        parent.SetRoom(warp.DestRoom, parent.season, true);
                    };
                    list.Add(followButton);
                }
                {
                    Gtk.MenuItem setDestButton = new Gtk.MenuItem("Edit Destination");
                    setDestButton.Activated += (sender, args) =>
                    {
                        parent.EditingWarpDestination = warp;
                        parent.SetRoom(warp.DestRoom, parent.season, true);
                        parent.WarpEditor.SetSelectedWarp(warp);
                    };
                    list.Add(setDestButton);
                }

                return list;
            }

            public override bool Compare(RoomComponent com)
            {
                return warp == (com as WarpSourceRoomComponent)?.warp;
            }

            public override void Delete()
            {
                warp.Remove();
            }


            void UpdateRect()
            {
                rect = new Cairo.Rectangle(X - rect.Width / 2.0, Y - rect.Height / 2.0, rect.Width, rect.Height);
            }
        }

        /// The singular room component that's drawn when editing a warp destination.
        class WarpDestRoomComponent : RoomComponent
        {
            RoomEditor parent;
            public Warp warp;


            public WarpDestRoomComponent(RoomEditor parent, Warp warp)
            {
                this.parent = parent;
                this.warp = warp;
            }


            public override Color BoxColor
            {
                get
                {
                    return WarpEditor.WarpSourceColor;
                }
            }

            public override bool Deletable
            {
                get { return false; }
            }
            public override bool HasXY
            {
                get { return true; }
            }
            public override bool HasShortenedXY
            {
                get { return true; }
            }
            public override int X
            {
                get { return warp.DestX * 16 + 8; }
                set
                {
                    warp.DestX = value / 16;
                }
            }
            public override int Y
            {
                get { return warp.DestY * 16 + 8; }
                set
                {
                    warp.DestY = value / 16;
                }
            }

            public override int BoxWidth { get { return 16; } }
            public override int BoxHeight { get { return 16; } }


            public override void Draw(Cairo.Context cr)
            {
                cr.SetSourceColor(Color.White);
                CairoHelper.DrawText(cr, "W", 9, BoxRectangle);
            }

            public override IList<Gtk.MenuItem> GetRightClickMenuItems()
            {
                var list = new List<Gtk.MenuItem>();

                {
                    Gtk.MenuItem doneButton = new Gtk.MenuItem("Done");
                    doneButton.Activated += (sender, args) =>
                    {
                        parent.EditingWarpDestination = null;
                        parent.SetRoom(warp.SourceRoom, parent.season, true);
                    };
                    list.Add(doneButton);
                }

                return list;
            }

            public override bool Compare(RoomComponent com)
            {
                return warp == (com as WarpDestRoomComponent)?.warp;
            }
        }

        /// Draggable chest
        class ChestRoomComponent : RoomComponent
        {
            Chest chest;

            public ChestRoomComponent(Chest chest)
            {
                this.chest = chest;
            }

            Project Project { get { return chest.Project; } }

            public override Color BoxColor { get {
                    return Color.FromRgba(204, 51, 153, 0xc0);
                }
            }

            public override bool Deletable { get { return true; } }
            public override bool HasXY { get { return true; } }
            public override bool HasShortenedXY { get { return true; } }
            public override int X
            {
                get { return chest.ValueReferenceGroup.GetIntValue("X") * 16 + 8; }
                set { chest.ValueReferenceGroup.SetValue("X", value / 16); }
            }
            public override int Y
            {
                get { return chest.ValueReferenceGroup.GetIntValue("Y") * 16 + 8; }
                set { chest.ValueReferenceGroup.SetValue("Y", value / 16); }
            }
            public override int BoxWidth { get { return 18; } }
            public override int BoxHeight { get { return 18; } }

            public override void Draw(Cairo.Context cr)
            {
                if (chest.Treasure == null)
                    return;
                GameObject obj = Project.GetIndexedDataType<InteractionObject>(
                        Project.EvalToInt("INTERAC_TREASURE") * 256 + chest.Treasure.Graphics);
                try
                {
                    obj.DefaultAnimation.GetFrame(0).Draw(cr, X, Y);
                }
                catch (InvalidAnimationException)
                {
                }
            }

            public override void Select()
            {
                base.Select();
            }

            public override bool Compare(RoomComponent com)
            {
                return chest == (com as ChestRoomComponent)?.chest;
            }

            public override void Delete()
            {
                chest.Delete();
            }
        }
    }
}
