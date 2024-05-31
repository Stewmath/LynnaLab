using System;
using System.Collections.Generic;

using LynnaLib;
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
        bool scaleSetInConstructor;
        int _floor;

        WeakEventWrapper<Dungeon> dungeonEventWrapper = new WeakEventWrapper<Dungeon>();

        Dictionary<Tuple<Room, int>, Cairo.Surface> cachedImageDict
            = new Dictionary<Tuple<Room, int>, Cairo.Surface>();

        public Project Project
        {
            get
            {
                if (_map == null)
                    return null;
                return _map.Project;
            }
        }
        public new Map Map
        {
            get
            {
                return _map;
            }
        }

        public int Floor
        {
            get
            {
                return _floor;
            }
            set
            {
                if (value >= (_map as Dungeon).NumFloors)
                    throw new ArgumentException(string.Format("Floor {0} too high.", value));
                _floor = value;
                QueueDraw();
            }
        }

        public Minimap()
        {
            Selectable = true;
            SelectedIndex = 0;

            dungeonEventWrapper.Bind<DungeonRoomChangedEventArgs>("RoomChangedEvent", OnDungeonRoomChanged);
            dungeonEventWrapper.Bind<EventArgs>("FloorsChangedEvent", OnDungeonFloorsChanged);
        }

        public Minimap(double scale) : this()
        {
            this._scale = scale;
            scaleSetInConstructor = true;
        }

        public void SetMap(Map m, int index = -1)
        {
            if (_map != m)
            {
                _map = m;
                _floor = 0;

                dungeonEventWrapper.ReplaceEventSource(_map as Dungeon); // May be null, that's fine

                if (_map != null)
                {
                    if (!scaleSetInConstructor)
                    {
                        if (m.MapWidth >= 16 && m.RoomWidth >= 15)
                            _scale = 1.0 / 12; // Draw large indoor groups smaller
                        else
                            _scale = 1.0 / 8;
                    }

                    Width = Map.MapWidth;
                    Height = Map.MapHeight;
                    TileWidth = (int)(_map.RoomWidth * 16 * _scale);
                    TileHeight = (int)(_map.RoomHeight * 16 * _scale);
                }

                base.UpdateSizeRequest();
                QueueDraw();
            }

            if (index != -1)
            {
                SelectedIndex = index;
            }
        }

        public Room GetRoom(int x, int y)
        {
            return _map?.GetRoom(x, y, Floor);
        }
        public Room GetRoom()
        {
            return GetRoom(SelectedX, SelectedY);
        }
        public RoomLayout GetRoomLayout(int x, int y)
        {
            return _map?.GetRoomLayout(x, y, Floor);
        }

        public void InvalidateImageCache()
        {
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
        protected override void TileDrawer(int index, Cairo.Context cr)
        {
            int x = index % _map.MapWidth;
            int y = index / _map.MapWidth;
            var source = GetTileImage(x, y);

            cr.SetSource(source, 0, 0);
            cr.Paint();
        }

        // Overridable function which generates the image for a tile on the map.
        // Child classes could choose override "TileDrawer" instead. The advantage of overriding
        // this function is that the image will be cached.
        protected virtual Cairo.Surface GenerateTileImage(int x, int y)
        {
            RoomLayout layout = GetRoomLayout(x, y);
            var tileSurface = this.Window.CreateSimilarSurface(Cairo.Content.Color,
                    (int)(layout.Width * 16 * _scale), (int)(layout.Height * 16 * _scale));

            // The below line works fine, but doesn't look as good on hidpi monitors
            //_surface = new Cairo.ImageSurface(Cairo.Format.Rgb24,
            //        (int)(layout.Width * 16 * _scale), (int)(layout.Height * 16 * _scale));

            Bitmap img = layout.GetImage();

            using (var cr = new Cairo.Context(tileSurface))
            {
                cr.Scale(_scale, _scale);
                cr.SetSource(img, 0, 0);
                cr.Paint();
            }

            return tileSurface;
        }

        protected override void OnDestroyed()
        {
            base.OnDestroyed();
            Dispose();
        }

        protected override void Dispose(bool disposeAll)
        {
            base.Dispose(disposeAll);
            foreach (var img in cachedImageDict.Values)
                img.Dispose();
            cachedImageDict.Clear();
        }


        // Private methods

        Cairo.Surface GetTileImage(int x, int y)
        {
            var key = new Tuple<Room, int>(GetRoom(x, y), _map.Season);

            if (cachedImageDict.ContainsKey(key))
                return cachedImageDict[key];

            var tileSurface = GenerateTileImage(x, y);
            cachedImageDict[key] = tileSurface;
            return tileSurface;
        }

        void OnDungeonRoomChanged(object sender, DungeonRoomChangedEventArgs args)
        {
            if (args.all)
                QueueDraw();
            else if (args.floor == Floor)
                QueueDrawTile(args.x, args.y);
        }

        void OnDungeonFloorsChanged(object sender, EventArgs args)
        {
            Dungeon d = _map as Dungeon;
            if (_floor >= d.NumFloors)
                _floor = d.NumFloors - 1;
            QueueDraw();
        }
    }
}
