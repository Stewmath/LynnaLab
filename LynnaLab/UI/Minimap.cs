using System;
using System.Drawing;
using System.Collections.Generic;
using Util;

namespace LynnaLab
{
    /** A minimap represents a map of either an overworld or a dungeon.
     *
     * Images in the minimap are cached until the "SetMap" function is called. Calling
     * "InvalidateImageCache" will force the image to update.
     *
     * The only external event that causes the images to update is dungeon editing. Room editing
     * does not dynamically update the image since I'm concerned about performance.
     */
    public class Minimap : TileGridViewer
    {
        Map _map;
        double _scale;
        int _floor;

        Dictionary<Room,Cairo.Surface> cachedImageDict = new Dictionary<Room,Cairo.Surface>();

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

                InvalidateImageCache();
            }
        }

        public Minimap()
        {
            Selectable = true;
            SelectedIndex = 0;
        }

        public Minimap(double scale) : this() {
            this._scale = scale;
        }

        public void SetMap(Map m, int index = -1) {
            if (_map != m) {
                if (_map is Dungeon)
                    (_map as Dungeon).RoomChangedEvent -= DungeonRoomChanged;
                if (m is Dungeon)
                    (m as Dungeon).RoomChangedEvent += DungeonRoomChanged;

                _map = m;
                _floor = 0;

                if (m.MapWidth >= 16 && m.RoomWidth >= 15)
                    _scale = 1.0/12; // Draw large indoor groups smaller
                else
                    _scale = 1.0/8;

                Width = Map.MapWidth;
                Height = Map.MapHeight;
                TileWidth = (int)(_map.RoomWidth*16*_scale);
                TileHeight = (int)(_map.RoomHeight*16*_scale);

                base.UpdateSizeRequest();
                InvalidateImageCache();
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

        public void InvalidateImageCache() {
            foreach (var s in cachedImageDict.Values)
                s.Dispose();
            cachedImageDict.Clear();
            QueueDraw();
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

        // TileGridViewer override
        protected override void TileDrawer(int index, Cairo.Context cr) {
            int x = index % _map.MapWidth;
            int y = index / _map.MapWidth;
            var source = GetTileImage(x, y);

            cr.SetSource(source, 0, 0);
            cr.Paint();
        }

        // Overridable function which generates the image for a tile on the map.
        // Child classes could choose override "TileDrawer" instead. The advantage of overriding
        // this function is that the image will be cached.
        protected virtual Cairo.Surface GenerateTileImage(int x, int y) {
            Room room = GetRoom(x, y);
            var tileSurface = this.Window.CreateSimilarSurface(Cairo.Content.Color,
                    (int)(room.Width * 16 * _scale), (int)(room.Height * 16 * _scale));

            // The below line works fine, but doesn't look as good on hidpi monitors
            //_surface = new Cairo.ImageSurface(Cairo.Format.Rgb24,
            //        (int)(room.Width * 16 * _scale), (int)(room.Height * 16 * _scale));

            Bitmap img = room.GetImage();

            using (var cr = new Cairo.Context(tileSurface))
            using (var source = new BitmapSurface(img)) {
                cr.Scale(_scale, _scale);
                cr.SetSource(source, 0, 0);
                cr.Paint();
            }

            return tileSurface;
        }


        // Private methods

        Cairo.Surface GetTileImage(int x, int y) {
            Room room = GetRoom(x, y);

            if (cachedImageDict.ContainsKey(room))
                return cachedImageDict[room];

            var tileSurface = GenerateTileImage(x, y);
            cachedImageDict[room] = tileSurface;
            return tileSurface;
        }

        void DungeonRoomChanged(object sender, DungeonRoomChangedEventArgs args) {
            if (args.floor == Floor)
                QueueDrawTile(args.x, args.y);
        }
    }
}
