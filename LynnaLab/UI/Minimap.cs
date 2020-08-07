using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Minimap : TileGridViewer
    {
        Cairo.Surface _surface;

        Map _map;
        double scale;
        int _floor;

        Dictionary<Tuple<Map,int>,Cairo.Surface> cachedImageDict = new Dictionary<Tuple<Map,int>,Cairo.Surface>();
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
                if (value >= (_map as Dungeon).NumFloors)
                    throw new ArgumentException(string.Format("Floor {0} too high.", value));
                _floor = value;
                GenerateImage();
            }
        }

        protected override Cairo.Surface Surface {
            get {
                if (_surface == null)
                    GenerateImage();
                return _surface;
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

        public void SetMap(Map m, int index = -1) {
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

            if (index != -1) {
                SelectedIndex = index;
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
                _surface = null;
                return;
            }

            if (this.Window == null) // Need the Window for the "CreateSimilarSurface" function
                return;

            int width = (int)(_map.RoomWidth*_map.MapWidth*16*scale);
            int height = (int)(_map.RoomHeight*_map.MapHeight*16*scale);
            SetSizeRequest(width, height);

            var key = new Tuple<Map,int>(Map, Floor);

            if (cachedImageDict.ContainsKey(key)) {
                _surface = cachedImageDict[key];
                QueueDraw();
                return;
            }

            _surface = this.Window.CreateSimilarSurface(Cairo.Content.Color, width, height);

            // The below line works fine, but doesn't look as good on hidpi monitors
            //_surface = new Cairo.ImageSurface(Cairo.Format.Rgb24, width, height);

            idleX = 0;
            idleY = 0;
            GLib.Idle.Add(idleHandler = new GLib.IdleHandler(OnIdleGenerateImage));
        }

        protected virtual Bitmap GenerateTileImage(int x, int y) {
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
            cachedImageDict = new Dictionary<Tuple<Map,int>,Cairo.Surface>();
        }


        // Private methods

        int idleX, idleY;
        bool OnIdleGenerateImage() {
            int x = idleX;
            int y = idleY;

            if (idleY >= _map.MapHeight) {
                var key = new Tuple<Map,int>(Map, Floor);
                cachedImageDict[key] = _surface;
                return false;
            }

            int drawX = (int)(_map.RoomWidth * 16 * scale) * x;
            int drawY = (int)(_map.RoomHeight * 16 * scale) * y;

            Bitmap img = GenerateTileImage(x,y);
            using (var cr = new Cairo.Context(_surface)) {
                using (var source = new BitmapSurface(img)) {
                    cr.Translate(drawX, drawY);
                    cr.Scale(scale, scale);
                    cr.SetSource(source, 0, 0);
                    cr.Paint();
                }
            }

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
