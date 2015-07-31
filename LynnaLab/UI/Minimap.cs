using System;
using System.Drawing;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public class Minimap : TileGridSelector
    {
        public Project Project {
            get {
                if (_project != null)
                    return _project;
                if (dungeon != null)
                    return dungeon.Project;
                return null;
            }
            set {
                _project = value;
            }
        }
        public Dungeon Dungeon {
            get {
                return dungeon;
            }
        }

        public int Floor
        {
            get {
                return floor;
            }
            set {
                floor = value;
                QueueDraw();
            }
        }

        protected override Bitmap Image {
            get {
                const double scale = 1.0/8;
                if (dungeon != null) {
                    Bitmap image = new Bitmap((int)(roomWidth*16*8*scale), (int)(roomHeight*16*8*scale));
                    Graphics g = Graphics.FromImage(image);

                    for (int x=0; x<8; x++) {
                        for (int y=0; y<8; y++) {
                            Room room = GetRoom(x, y);
                            Bitmap img = room.GetImage();
                            g.DrawImage(img, (int)(x*roomWidth*16*scale), (int)(y*roomHeight*16*scale),
                                    (int)(roomWidth*16*scale), (int)(roomHeight*16*scale));
                        }
                    }
                    g.Dispose();
                    return image;
                }
                else if (worldGroup != -1) {
                    Bitmap image = new Bitmap((int)(roomWidth*16*16*scale), (int)(roomHeight*16*16*scale));
                    Graphics g = Graphics.FromImage(image);

                    for (int x=0; x<16; x++) {
                        for (int y=0; y<16; y++) {
                            Room room = GetRoom(x, y);
                            Bitmap img = room.GetImage();
                            g.DrawImage(img, (int)(x*roomWidth*16*scale), (int)(y*roomHeight*16*scale),
                                    (int)(roomWidth*16*scale), (int)(roomHeight*16*scale));
                        }
                    }
                    g.Dispose();
                    return image;
                }
                else
                    return null;
            }
        }

        Project _project;
        int roomWidth, roomHeight;

        // For dungeons
        Dungeon dungeon;
        int floor;

        // For worlds
        int worldGroup = -1;

        public Minimap()
        {
        }

        public void SetDungeon(Dungeon d) {
            worldGroup = -1;
            dungeon = d;
            roomWidth = 15;
            roomHeight = 11;
            QueueDraw();
        }

        public void SetWorld(int group) {
            worldGroup = group;
            dungeon = null;
            roomWidth = 10;
            roomHeight = 8;
            QueueDraw();
        }

        public Room GetRoom(int x, int y) {
            if (dungeon != null)
                return dungeon.GetRoom(Floor, x, y);
            else if (worldGroup != -1)
                return Project.GetDataType<Room>(worldGroup*0x100+y*16+x);
            else
                return null;
        }
        public Room GetRoom() {
            return GetRoom(SelectedX, SelectedY);
        }

        protected override bool OnButtonPressEvent(Gdk.EventButton ev)
        {
            // Insert button press handling code here.
            return base.OnButtonPressEvent(ev);
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);
            
            return true;
        }

        protected override void OnSizeAllocated(Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated(allocation);
            // Insert layout code here.
        }

        protected override void OnSizeRequested(ref Gtk.Requisition requisition)
        {
            base.OnSizeRequested(ref requisition);
        }
    }
}
