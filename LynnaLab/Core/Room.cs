using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab
{
    /* Room class
     * Provides an interface for modifying layout and keeping track of changes
     * to the image.
     * Doesn't keep track of much else, like objects or dungeon data,
     * though it may have getter functions.
     */
    public class Room : ProjectIndexedDataType {

        public delegate void RoomModifiedHandler();
        // Event invoked when the room's image is modified in any way
        public event RoomModifiedHandler RoomModifiedEvent;

        // Actual width and height of room
        int width, height;
        // Actual width of file; large rooms always have an extra 0 at the end of each row
        int fileWidth;

        MemoryFileStream tileDataFile;

        Tileset tileset;
        Bitmap cachedImage;

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
            get { return tileset; }
        }

        internal Room(Project p, int i) : base(p,i) {
            Stream groupTilesetsFile = GetTilesetMappingFile();
            groupTilesetsFile.Position = Index&0xff;
            int areaID = groupTilesetsFile.ReadByte() & 0x7f;

            Tileset t = Project.GetIndexedDataType<Tileset>(areaID);
            SetTileset(t);

        }

        // Returns a stream for the tileset mapping file (256 bytes, one byte per room)
        private Stream GetTilesetMappingFile() {
            Data data = Project.GetData("roomTilesetsGroupTable", 2*(Index>>8));
            data = Project.GetData(data.GetValue(0)); // Follow .dw pointer

            string path = data.GetValue(0);
            path = path.Substring(1, path.Length-2); // Remove quotes

            Stream stream = Project.GetBinaryFile(path);
            return stream;
        }

        public Bitmap GetImage() {
            if (cachedImage != null)
                return cachedImage;

            cachedImage = new Bitmap(width*16, height*16);
            Graphics g = Graphics.FromImage(cachedImage);

            for (int x=0; x<width; x++) {
                for (int y=0; y<height; y++) {
                    g.DrawImageUnscaled(tileset.GetTileImage(GetTile(x,y)), x*16, y*16);
                }
            }

            g.Dispose();

            return cachedImage;
        }

        public int GetTile(int x, int y) {
            tileDataFile.Position = y*fileWidth+x;
            return tileDataFile.ReadByte();
        }
        public void SetTile(int x, int y, int value) {
            if (GetTile(x,y) != value) {
                tileDataFile.Position = y*fileWidth+x;
                tileDataFile.WriteByte((byte)value);
                if (cachedImage != null) {
                    Graphics g = Graphics.FromImage(cachedImage);
                    g.DrawImageUnscaled(tileset.GetTileImage(value), x*16, y*16);
                    g.Dispose();
                }
                Modified = true;
                if (RoomModifiedEvent != null)
                    RoomModifiedEvent();
            }
        }

        // These 2 functions may be deprecated later if I switch to using
        // constant definitions
        public int GetMusicID() {
            Stream file = GetMusicFile();
            file.Position = Index&0xff;
            return file.ReadByte();
        }
        public void SetMusicID(int id) {
            Stream file = GetMusicFile();
            file.Position = Index&0xff;
            file.WriteByte((byte)id);
        }

        private Stream GetMusicFile() {
            Data data = Project.GetData("musicAssignmentGroupTable", 2*(Index>>8));
            data = Project.GetData(data.GetValue(0)); // Follow .dw pointer

            string path = data.GetValue(0);
            path = path.Substring(1, path.Length-2); // Remove quotes

            Stream stream = Project.GetBinaryFile(path);
            return stream;
        }

        public void SetTileset(Tileset t) {
            if (tileset == null || t.Index != tileset.Index) {
                Stream groupTilesetsFile = GetTilesetMappingFile();
                groupTilesetsFile.Position = Index&0xff;
                int lastValue = groupTilesetsFile.ReadByte() & 0x80;
                groupTilesetsFile.Position = Index&0xff;
                groupTilesetsFile.WriteByte((byte)((t.Index&0x7f) | lastValue));

                var handler = new Tileset.TileModifiedHandler(ModifiedTileCallback);
                var layoutHandler = new Tileset.LayoutGroupModifiedHandler(ModifiedLayoutGroupCallback);
                if (tileset != null) {
                    tileset.TileModifiedEvent -= handler;
                    tileset.LayoutGroupModifiedEvent -= layoutHandler;
                }
                t.TileModifiedEvent += handler;
                t.LayoutGroupModifiedEvent += layoutHandler;

                cachedImage = null;

                tileset = t;

                UpdateRoomData();
                if (RoomModifiedEvent != null)
                    RoomModifiedEvent();
            }
        }

        public ObjectGroup GetObjectGroup() {
            string tableLabel = Project.GetData("objectDataGroupTable", 2*(Index>>8)).GetValue(0);
            string label = Project.GetData(tableLabel, 2*(Index&0xff)).GetValue(0);
            return Project.GetObjectGroup(label, ObjectGroupType.Main);
        }

        public WarpGroup GetWarpGroup() {
            return Project.GetIndexedDataType<WarpGroup>(Index);
        }

        void UpdateRoomData() {
            // Get the tileDataFile
            int layoutGroup = tileset.LayoutGroup;
            string label = "room" + ((layoutGroup<<8)+(Index&0xff)).ToString("X4").ToLower();
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

            if (tileDataFile.Length == 80) { // Small map
                width = fileWidth = 10;
                height = 8;
            }
            else if (tileDataFile.Length == 176) { // Large map
                width = 0xf;
                fileWidth = 0x10;
                height = 0xb;
            }
            else
                throw new AssemblyErrorException("Size of file \"" + tileDataFile.Name + "\" was invalid!");
        }

        void ModifiedTileCallback(int tile) {
            Graphics g = null;
            if (cachedImage != null)
                g = Graphics.FromImage(cachedImage);
            for (int x=0; x<Width; x++) {
                for (int y=0; y<Height; y++) {
                    if (GetTile(x, y) == tile) {
                        if (cachedImage != null)
                            g.DrawImageUnscaled(tileset.GetTileImage(GetTile(x,y)), x*16, y*16);
                        if (RoomModifiedEvent != null)
                            RoomModifiedEvent();
                    }
                }
            }
            if (g != null)
                g.Dispose();
        }

        void ModifiedLayoutGroupCallback() {
            UpdateRoomData();
            cachedImage = null;
            if (RoomModifiedEvent != null)
                RoomModifiedEvent();
        }
    }
}
