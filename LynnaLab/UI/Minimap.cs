using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Minimap : TileGridViewer
    {
        Bitmap _image;
        Map _map;
        double scale;
        int _floor;

        Dictionary<Tuple<Map,int>,Bitmap> cachedImageDict = new Dictionary<Tuple<Map,int>,Bitmap>();
        GLib.IdleHandler idleHandler;

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
            Selectable = true;
            SelectedIndex = 0;
        }

        public Minimap(double scale) : this() {
            this.scale = scale;
        }

        public void SetMap(Map m) {
            if (_map != m) {
                _map = m;
                _floor = 0;

                if (m.MapWidth >= 16 && m.RoomWidth >= 15)
                    scale = 1.0/12; // Draw large indoor groups smaller
                else
                    scale = 1.0/8;

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

        public virtual void GenerateImage() {
            GLib.Idle.Remove(idleHandler);

            if (_map == null) {
                _image = null;
                return;
            }

            int width = (int)(_map.RoomWidth*_map.MapWidth*16*scale);
            int height = (int)(_map.RoomHeight*_map.MapHeight*16*scale);
            SetSizeRequest(width, height);

            var key = new Tuple<Map,int>(Map, Floor);

            if (cachedImageDict.ContainsKey(key)) {
                _image = cachedImageDict[key];
                QueueDraw();
                return;
            }

            _image = new Bitmap(width,height);

            idleX = 0;
            idleY = 0;
            GLib.Idle.Add(idleHandler = new GLib.IdleHandler(OnIdleGenerateImage));
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

        protected override void OnSizeAllocated(Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated(allocation);
            // Insert layout code here.
        }

        protected void ClearImageCache() {
            cachedImageDict = new Dictionary<Tuple<Map,int>,Bitmap>();
        }


        // Private methods

        int idleX, idleY;
        bool OnIdleGenerateImage() {
            int x = idleX;
            int y = idleY;

            if (idleY >= _map.MapHeight) {
                var key = new Tuple<Map,int>(Map, Floor);
                cachedImageDict[key] = _image;
                return false;
            }

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

            base.QueueDrawTile(x, y);

            idleX++;
            if (idleX >= _map.MapWidth) {
                idleY++;
                idleX = 0;
            }

            return true;
        }
    }
}
