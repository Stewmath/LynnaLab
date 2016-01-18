using System;
using System.Drawing;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public class Minimap : TileGridSelector
    {
        const double IMAGE_SCALE = 1.0/8;

        Bitmap _image;
        Map _map;

        int _floor;

        public Project Project {
            get {
                if (_map == null)
                    return null;
                return _map.Project;
            }
        }
        public new Map Map {
            get {
                return _map;
            }
        }

        public int Floor
        {
            get {
                return _floor;
            }
            set {
                _floor = value;
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

        public Minimap()
        {
        }

        public void SetMap(Map m) {
            if (_map != m) {
                _map = m;
                _floor = 0;
                GenerateImage();
            }
        }

        public Room GetRoom(int x, int y) {
            if (_map != null)
                return _map.GetRoom(x, y, Floor);
            return null;
        }
        public Room GetRoom() {
            return GetRoom(SelectedX, SelectedY);
        }

        public void GenerateImage() {
            if (_map == null) {
                _image = null;
                return;
            }
            int width = (int)(_map.RoomWidth*_map.MapWidth*16*IMAGE_SCALE);
            int height = (int)(_map.RoomHeight*_map.MapHeight*16*IMAGE_SCALE);
            _image = new Bitmap(width,height);

            idleX = 0;
            idleY = 0;
            GLib.IdleHandler handler = new GLib.IdleHandler(OnIdleGenerateImage);
            GLib.Idle.Remove(handler);
            GLib.Idle.Add(handler);

            SetSizeRequest(width, height);
        }

        int idleX, idleY;
        bool OnIdleGenerateImage() {
            int x = idleX;
            int y = idleY;

            int width = (int)(_map.RoomWidth*16*IMAGE_SCALE);
            int height = (int)(_map.RoomHeight*16*IMAGE_SCALE);

            const System.Drawing.Drawing2D.InterpolationMode interpolationMode =
                System.Drawing.Drawing2D.InterpolationMode.Default;

            Graphics g = Graphics.FromImage(_image);
            g.InterpolationMode = interpolationMode;

            Room room = GetRoom(x, y);
            Bitmap img = room.GetImage();
            g.DrawImage(img, (int)(x*width), (int)(y*height),
                    (int)(width), (int)(height));

            g.Dispose();

            this.QueueDrawArea(x*width, y*height, width, height);

            idleX++;
            if (idleX >= _map.MapWidth) {
                idleY++;
                idleX = 0;
                if (idleY >= _map.MapHeight)
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
