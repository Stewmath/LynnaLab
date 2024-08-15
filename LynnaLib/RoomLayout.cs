using System;
using System.Collections.Generic;
using System.IO;
using Util;

namespace LynnaLib
{
    /// Represents a possible tileset/layout combination of a room. Currently, the only reason this
    /// is distinct from the "Room" class is to handle different seasons.
    public class RoomLayout
    {
        private static readonly log4net.ILog log = LogHelper.GetLogger();

        // Actual width and height of room (note that width can differ from Stride, below)
        int width, height;

        MemoryFileStream tileDataFile;
        Tileset loadedTileset;
        Bitmap cachedImage;


        public delegate void LayoutModifiedHandler();
        // Event invoked when the room's image is modified in any way
        public event LayoutModifiedHandler LayoutModifiedEvent;


        internal RoomLayout(Room room, int season)
        {
            Room = room;
            Season = season;
            UpdateTileset();
        }


        public Room Room { get; private set; }
        public int Season { get; private set; }

        // Properties from "Room" class
        public Project Project { get { return Room.Project; } }
        public int Group { get { return Room.Group; } }
        public int ExpectedGroup { get { return Room.ExpectedGroup; } }
        public int ExpectedIndex { get { return Room.ExpectedIndex; } }

        public int Height
        {
            get { return height; }
        }
        public int Width
        {
            get { return width; }
        }
        public Tileset Tileset
        {
            get
            {
                return Room.GetTileset(Season);
            }
        }
        public bool IsSmall
        {
            get { return width == 10; }
        }


        /// # of bytes per row (including unused bytes; this means it has a value of 16 for large
        /// rooms, which is 1 higher than the width, which is 15).
        int Stride
        {
            get { return width == 10 ? 10 : 16; }
        }

        public Bitmap GetImage()
        {
            if (cachedImage != null)
                return cachedImage;

            cachedImage = new Bitmap(width * 16, height * 16);

            using (var cr = cachedImage.CreateContext())
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        cr.SetSource(Tileset.GetTileImage(GetTile(x, y)), x * 16, y * 16);
                        cr.Paint();
                    }
                }
            }

            return cachedImage;
        }

        public byte[] GetLayout()
        {
            tileDataFile.Position = 0;
            byte[] output = new byte[Stride * Height];
            tileDataFile.Read(output, 0, Stride * Height);
            return output;
        }

        public void SetLayout(byte[] layout)
        {
            if (layout.Length != Stride * Height)
                throw new Exception($"Tried to write a layout of invalid size to room {Room.Index:x3}");
            tileDataFile.Position = 0;
            tileDataFile.Write(layout, 0, Stride * Height);
            // Modifying the data will trigger the callback to the TileDataModified function
        }

        public int GetTile(int x, int y)
        {
            tileDataFile.Position = y * Stride + x;
            return tileDataFile.ReadByte();
        }
        public void SetTile(int x, int y, int value)
        {
            if (GetTile(x, y) != value)
            {
                tileDataFile.Position = y * Stride + x;
                tileDataFile.WriteByte((byte)value);
                // Modifying the data will trigger the callback to the TileDataModified function
            }
        }

        // Shift room tiles by given values
        public void ShiftTiles(int xshift, int yshift)
        {
            byte[] oldLayout = GetLayout();
            byte[] newLayout = (byte[])oldLayout.Clone();

            for (int x=0; x<Width; x++)
            {
                for (int y=0; y<Height; y++)
                {
                    Func<int, int, int> normalize = (val, max) =>
                    {
                        while (val < 0)
                            val += max;
                        while (val >= max)
                            val -= max;
                        return val;
                    };
                    int oldX = normalize(x - xshift, Width);
                    int oldY = normalize(y - yshift, Height);

                    newLayout[y * Stride + x] = oldLayout[oldY * Stride + oldX];
                }
            }

            SetLayout(newLayout);
        }


        internal void UpdateTileset()
        {
            if (loadedTileset != Tileset)
            {
                if (loadedTileset != null)
                {
                    loadedTileset.TileModifiedEvent -= ModifiedTilesetCallback;
                    loadedTileset.LayoutGroupModifiedEvent -= ModifiedLayoutGroupCallback;
                }
                Tileset.TileModifiedEvent += ModifiedTilesetCallback;
                Tileset.LayoutGroupModifiedEvent += ModifiedLayoutGroupCallback;

                cachedImage?.Dispose();
                cachedImage = null;
                loadedTileset = Tileset;

                UpdateRoomData();
                if (LayoutModifiedEvent != null)
                    LayoutModifiedEvent();
            }
        }


        // Private methods

        void UpdateRoomData()
        {
            if (tileDataFile != null)
            {
                tileDataFile.RemoveModifiedEventHandler(TileDataModified);
                tileDataFile = null;
            }


            int layoutGroup;
            if (Project.Config.ExpandedTilesets)
                layoutGroup = Project.GetCanonicalLayoutGroup(Group, Season);
            else
                layoutGroup = Tileset.LayoutGroup;

            // Get the tileDataFile
            string label = "room" + ((layoutGroup << 8) + (Room.Index & 0xff)).ToString("X4").ToLower();
            FileParser parserFile = Project.GetFileWithLabel(label);
            Data data = parserFile.GetData(label);
            if (data.CommandLowerCase != "m_roomlayoutdata")
            {
                throw new AssemblyErrorException("Expected label \"" + label + "\" to be followed by the m_RoomLayoutData macro.");
            }
            string roomString = data.GetValue(0) + ".bin";
            try
            {
                tileDataFile = Project.GetBinaryFile(
                        "rooms/" + Project.GameString + "/small/" + roomString);
            }
            catch (FileNotFoundException)
            {
                try
                {
                    tileDataFile = Project.GetBinaryFile(
                            "rooms/" + Project.GameString + "/large/" + roomString);
                }
                catch (FileNotFoundException)
                {
                    throw new AssemblyErrorException("Couldn't find \"" + roomString + "\" in \"rooms/small\" or \"rooms/large\".");
                }
            }

            tileDataFile.AddModifiedEventHandler(TileDataModified);

            if (tileDataFile.Length == 80)
            { // Small map
                width = 10;
                height = 8;
            }
            else if (tileDataFile.Length == 176)
            { // Large map
                width = 0xf;
                height = 0xb;
            }
            else
                throw new AssemblyErrorException("Size of file \"" + tileDataFile.Name + "\" was invalid!");
        }

        // Room layout modified
        void TileDataModified(object sender, MemoryFileStream.ModifiedEventArgs args)
        {
            if (cachedImage != null)
            {
                using (var cr = cachedImage.CreateContext())
                {

                    for (long i = args.modifiedRangeStart; i < args.modifiedRangeEnd; i++)
                    {
                        int x = (int)(i % Stride);
                        int y = (int)(i / Stride);
                        if (x >= Width)
                            continue;
                        cr.SetSource(Tileset.GetTileImage(GetTile(x, y)), x * 16, y * 16);
                        cr.Paint();
                    }
                }
            }
            if (LayoutModifiedEvent != null)
                LayoutModifiedEvent();
        }

        // Tileset data modified
        void ModifiedTilesetCallback(object sender, int tile)
        {
            Cairo.Context cr = cachedImage?.CreateContext();

            bool changed = false;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (GetTile(x, y) == tile)
                    {
                        if (cachedImage != null) {
                            cr.SetSource(Tileset.GetTileImage(GetTile(x, y)), x * 16, y * 16);
                            cr.Paint();
                        }
                        changed = true;
                    }
                }
            }

            cr?.Dispose();

            if (changed && LayoutModifiedEvent != null)
                LayoutModifiedEvent();
        }

        void ModifiedLayoutGroupCallback()
        {
            UpdateRoomData();
            cachedImage?.Dispose();
            cachedImage = null;
            if (LayoutModifiedEvent != null)
                LayoutModifiedEvent();
        }
    }
}
