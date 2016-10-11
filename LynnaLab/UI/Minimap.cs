using System;
using System.Drawing;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public class Minimap : TileGridSelector
    {
        Bitmap _image;
        Map _map;
        double scale;

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
            scale = 1.0/8;
        }

        public Minimap(double scale) {
            this.scale = scale;
        }

        public void SetMap(Map m) {
            if (_map != m) {
                _map = m;
                _floor = 0;

                Width = Map.MapWidth;
                Height = Map.MapHeight;
                TileWidth = (int)(_map.RoomWidth*16*scale);
                TileHeight = (int)(_map.RoomHeight*16*scale);

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

        int idleX, idleY;
        public void GenerateImage() {
            if (_map == null) {
                _image = null;
                return;
            }

            idleX = 0;
            idleY = 0;

            Func<bool> OnIdleGenerateImage = () => {
                int x = idleX;
                int y = idleY;

                if (idleY >= _map.MapHeight)
                    return false;

                int roomWidth = (int)(_map.RoomWidth*16*scale);
                int roomHeight = (int)(_map.RoomHeight*16*scale);

                const System.Drawing.Drawing2D.InterpolationMode interpolationMode =
                                System.Drawing.Drawing2D.InterpolationMode.High;

                Graphics g = Graphics.FromImage(_image);
                g.InterpolationMode = interpolationMode;

                Image img = GenerateTileImage(x,y);
                g.DrawImage(img, (int)(x*roomWidth), (int)(y*roomHeight),
                        (int)(roomWidth), (int)(roomHeight));

                g.Dispose();

                QueueDrawArea(x*roomWidth, y*roomHeight, roomWidth, roomHeight);

                idleX++;
                if (idleX >= _map.MapWidth) {
                    idleY++;
                    idleX = 0;
                }

                return true;
            };

            int width = (int)(_map.RoomWidth*_map.MapWidth*16*scale);
            int height = (int)(_map.RoomHeight*_map.MapHeight*16*scale);
            _image = new Bitmap(width,height);

            SetSizeRequest(width, height);
            idleX = 0;
            idleY = 0;
            var handler = new GLib.IdleHandler(OnIdleGenerateImage);
            GLib.Idle.Remove(handler);
            GLib.Idle.Add(handler);
        }

        protected virtual Image GenerateTileImage(int x, int y) {
            Room room = GetRoom(x, y);
            Bitmap img = room.GetImage();
            return img;
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
