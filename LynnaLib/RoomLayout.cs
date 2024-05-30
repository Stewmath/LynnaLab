using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using Util;

namespace LynnaLib
{
    /// Represents a possible tileset/layout combination of a room. Currently, the only reason this
    /// is distinct from the "Room" class is to handle different seasons.
    public class RoomLayout {
        private static readonly log4net.ILog log = LogHelper.GetLogger();

        // Actual width and height of room (note that width can differ from Stride, below)
        int width, height;

        MemoryFileStream tileDataFile;
        Tileset loadedTileset;
        Bitmap cachedImage;


        public delegate void LayoutModifiedHandler();
        // Event invoked when the room's image is modified in any way
        public event LayoutModifiedHandler LayoutModifiedEvent;


        internal RoomLayout(Room room, int season) {
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
            get {
                return Room.GetTileset(Season);
            }
        }


        /// # of bytes per row (including unused bytes; this means it has a value of 16 for large
        /// rooms, which is 1 higher than the width, which is 15).
        int Stride {
            get { return width == 10 ? 10 : 16; }
        }

        public Bitmap GetImage() {
            if (cachedImage != null)
                return cachedImage;

            cachedImage = new Bitmap(width*16, height*16);
            Graphics g = Graphics.FromImage(cachedImage);

            for (int x=0; x<width; x++) {
                for (int y=0; y<height; y++) {
                    g.DrawImageUnscaled(Tileset.GetTileImage(GetTile(x,y)), x*16, y*16);
                }
            }

            g.Dispose();

            return cachedImage;
        }

        public int GetTile(int x, int y) {
            tileDataFile.Position = y*Stride+x;
            return tileDataFile.ReadByte();
        }
        public void SetTile(int x, int y, int value) {
            if (GetTile(x,y) != value) {
                tileDataFile.Position = y*Stride+x;
                tileDataFile.WriteByte((byte)value);
                // Modifying the data will trigger the callback to the TileDataModified function
            }
        }


        internal void UpdateTileset() {
            if (loadedTileset != Tileset) {
                if (loadedTileset != null) {
                    loadedTileset.TileModifiedEvent -= ModifiedTilesetCallback;
                    loadedTileset.LayoutGroupModifiedEvent -= ModifiedLayoutGroupCallback;
                }
                Tileset.TileModifiedEvent += ModifiedTilesetCallback;
                Tileset.LayoutGroupModifiedEvent += ModifiedLayoutGroupCallback;

                cachedImage = null;
                loadedTileset = Tileset;

                UpdateRoomData();
                if (LayoutModifiedEvent != null)
                    LayoutModifiedEvent();
            }
        }


        // Private methods

        void UpdateRoomData() {
            if (tileDataFile != null) {
                tileDataFile.RemoveModifiedEventHandler(TileDataModified);
                tileDataFile = null;
            }
            // Get the tileDataFile
            int layoutGroup = Tileset.LayoutGroup;
            string label = "room" + ((layoutGroup<<8)+(Room.Index&0xff)).ToString("X4").ToLower();
            FileParser parserFile = Project.GetFileWithLabel(label);
            Data data = parserFile.GetData(label);
            if (data.CommandLowerCase != "m_roomlayoutdata") {
                throw new AssemblyErrorException("Expected label \"" + label + "\" to be followed by the m_RoomLayoutData macro.");
            }
            string roomString = data.GetValue(0) + ".bin";
            try {
                tileDataFile = Project.GetBinaryFile(
                        "rooms/" + Project.GameString + "/small/" + roomString);
            }
            catch (FileNotFoundException) {
                try {
                    tileDataFile = Project.GetBinaryFile(
                            "rooms/" + Project.GameString + "/large/" + roomString);
                }
                catch (FileNotFoundException) {
                    throw new AssemblyErrorException("Couldn't find \"" + roomString + "\" in \"rooms/small\" or \"rooms/large\".");
                }
            }

            tileDataFile.AddModifiedEventHandler(TileDataModified);

            if (tileDataFile.Length == 80) { // Small map
                width = 10;
                height = 8;
            }
            else if (tileDataFile.Length == 176) { // Large map
                width = 0xf;
                height = 0xb;
            }
            else
                throw new AssemblyErrorException("Size of file \"" + tileDataFile.Name + "\" was invalid!");
        }

        // Room layout modified
        void TileDataModified(object sender, MemoryFileStream.ModifiedEventArgs args) {
            if (cachedImage != null) {
                Graphics g = Graphics.FromImage(cachedImage);

                for (long i=args.modifiedRangeStart; i<args.modifiedRangeEnd; i++) {
                    int x = (int)(i % Stride);
                    int y = (int)(i / Stride);
                    if (x >= Width)
                        continue;
                    g.DrawImageUnscaled(Tileset.GetTileImage(GetTile(x, y)), x*16, y*16);
                }
                g.Dispose();
            }
            if (LayoutModifiedEvent != null)
                LayoutModifiedEvent();
        }

        // Tileset data modified
        void ModifiedTilesetCallback(object sender, int tile) {
            Graphics g = null;
            if (cachedImage != null)
                g = Graphics.FromImage(cachedImage);

            bool changed = false;
            for (int x=0; x<Width; x++) {
                for (int y=0; y<Height; y++) {
                    if (GetTile(x, y) == tile) {
                        if (cachedImage != null)
                            g.DrawImageUnscaled(Tileset.GetTileImage(GetTile(x,y)), x*16, y*16);
                        changed = true;
                    }
                }
            }

            if (g != null)
                g.Dispose();

            if (changed && LayoutModifiedEvent != null)
                LayoutModifiedEvent();
        }

        void ModifiedLayoutGroupCallback() {
            UpdateRoomData();
            cachedImage = null;
            if (LayoutModifiedEvent != null)
                LayoutModifiedEvent();
        }
    }
}
