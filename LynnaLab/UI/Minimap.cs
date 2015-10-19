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
                GenerateImage();
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
                GenerateImage();
            }
        }

        protected override Bitmap Image {
            get {
                if (_image == null)
                    GenerateImage();
                return _image;
            }
        }

        Bitmap _image;
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
            if (dungeon != d) {
                worldGroup = -1;
                dungeon = d;
                roomWidth = 15;
                roomHeight = 11;
                GenerateImage();
            }
        }

        public void SetWorld(int group) {
            if (worldGroup != group) {
                worldGroup = group;
                dungeon = null;
                roomWidth = 10;
                roomHeight = 8;
                GenerateImage();
            }
        }

        public Room GetRoom(int x, int y) {
            if (dungeon != null)
                return dungeon.GetRoom(Floor, x, y);
            else if (worldGroup != -1)
                return Project.GetIndexedDataType<Room>(worldGroup*0x100+y*16+x);
            else
                return null;
        }
        public Room GetRoom() {
            return GetRoom(SelectedX, SelectedY);
        }

        public void GenerateImage() {
            const double scale = 1.0/8;
            if (dungeon != null) {
                _image = new Bitmap((int)(roomWidth*16*8*scale), (int)(roomHeight*16*8*scale));
            }
            else if (worldGroup != -1) {
                _image = new Bitmap((int)(roomWidth*16*16*scale), (int)(roomHeight*16*16*scale));

                for (int x=0; x<16; x++) {
                    for (int y=0; y<16; y++) {
                    }
                }
            }
            else
                _image = null;

            if (_image != null) {
                idleX = 0;
                idleY = 0;
                GLib.IdleHandler handler = new GLib.IdleHandler(OnIdleGenerateImage);
                GLib.Idle.Remove(handler);
                GLib.Idle.Add(handler);
            }
        }

        int idleX, idleY;
        bool OnIdleGenerateImage() {
            int x = idleX;
            int y = idleY;

            const System.Drawing.Drawing2D.InterpolationMode interpolationMode =
                System.Drawing.Drawing2D.InterpolationMode.Default;
            const double scale = 1.0/8;

            int width = (int)(roomWidth*16*scale);
            int height = (int)(roomHeight*16*scale);

            Graphics g = Graphics.FromImage(_image);
            g.InterpolationMode = interpolationMode;

            Room room = GetRoom(x, y);
            Bitmap img = room.GetImage();
            g.DrawImage(img, (int)(x*width), (int)(y*height),
                    (int)(width), (int)(height));

            g.Dispose();

            this.QueueDrawArea(x*width, y*height, width, height);

            idleX++;
            if (idleX >= (worldGroup != -1 ? 16 : 8)) {
                idleY++;
                idleX = 0;
                if (idleY >= (worldGroup != -1 ? 16 : 8))
                    return false;
            }
            return true;
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
