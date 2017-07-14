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

        Area area;
        Bitmap cachedImage;

        public int Height
        {
            get { return height; }
        }
        public int Width
        {
            get { return width; }
        }
        public Area Area
        {
            get { return area; }
        }

        internal Room(Project p, int i) : base(p,i) {
            int areaID;
            Stream groupAreasFile = Project.GetBinaryFile(
                    "rooms/" + Project.GetGameString() + "/group" + (Index>>8) + "Areas.bin");
            groupAreasFile.Position = Index&0xff;
            areaID = groupAreasFile.ReadByte() & 0x7f;

            Area a = Project.GetIndexedDataType<Area>(areaID);
            SetArea(a);

        }

        public Bitmap GetImage() {
            if (cachedImage != null)
                return cachedImage;

            cachedImage = new Bitmap(width*16, height*16);
            Graphics g = Graphics.FromImage(cachedImage);

            for (int x=0; x<width; x++) {
                for (int y=0; y<height; y++) {
                    g.DrawImageUnscaled(area.GetTileImage(GetTile(x,y)), x*16, y*16);
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
                    g.DrawImageUnscaled(area.GetTileImage(value), x*16, y*16);
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
            Stream file = Project.GetBinaryFile("audio/group" + (Index>>8) + "IDs.bin");
            file.Position = Index&0xff;
            return file.ReadByte();
        }
        public void SetMusicID(int id) {
            Stream file = Project.GetBinaryFile("audio/group" + (Index>>8) + "IDs.bin");
            file.Position = Index&0xff;
            file.WriteByte((byte)id);
        }

        public void SetArea(Area a) {
            if (area == null || a.Index != area.Index) {
                Stream groupAreasFile = Project.GetBinaryFile(
                    "rooms/" + Project.GetGameString() + "/group" + (Index>>8) + "Areas.bin");
                groupAreasFile.Position = Index&0xff;
                int lastValue = groupAreasFile.ReadByte() & 0x80;
                groupAreasFile.Position = Index&0xff;
                groupAreasFile.WriteByte((byte)((a.Index&0x7f) | lastValue));

                var handler = new Area.TileModifiedHandler(ModifiedTileCallback);
                var layoutHandler = new Area.LayoutGroupModifiedHandler(ModifiedLayoutGroupCallback);
                if (area != null) {
                    area.TileModifiedEvent -= handler;
                    area.LayoutGroupModifiedEvent -= layoutHandler;
                }
                a.TileModifiedEvent += handler;
                a.LayoutGroupModifiedEvent += layoutHandler;

                cachedImage = null;

                area = a;

                UpdateRoomData();
            }
        }

        public ObjectGroup GetObjectGroup() {
            String label = "group" + (Index/0x100).ToString("x") + "Map" + (Index%0x100).ToString("x2") + "ObjectData";
            return Project.GetDataType<ObjectGroup>(label);
        }

        void UpdateRoomData() {
            // Get the tileDataFile
            int layoutGroup = area.LayoutGroup;
            string label = "room" + ((layoutGroup<<8)+(Index&0xff)).ToString("X4").ToLower();
            FileParser parserFile = Project.GetFileWithLabel(label);
            Data data = parserFile.GetData(label);
            if (data.CommandLowerCase != "m_roomlayoutdata") {
                throw new Exception("Expected label \"" + label + "\" to be followed by the m_RoomLayoutData macro.");
            }
            string roomString = data.GetValue(0) + ".bin";
            try {
                tileDataFile = Project.GetBinaryFile(
                        "rooms/" + Project.GetGameString() + "/small/" + roomString);
            }
            catch (FileNotFoundException) {
                try {
                    tileDataFile = Project.GetBinaryFile(
                            "rooms/" + Project.GetGameString() + "/large/" + roomString);
                }
                catch (FileNotFoundException) {
                    throw new FileNotFoundException("Couldn't find \"" + roomString + "\" in \"rooms/small\" or \"rooms/large\".");
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
                throw new Exception("Size of file \"" + tileDataFile.Name + "\" was invalid!");
        }

        void ModifiedTileCallback(int tile) {
            Graphics g = null;
            if (cachedImage != null)
                g = Graphics.FromImage(cachedImage);
            for (int x=0; x<Width; x++) {
                for (int y=0; y<Height; y++) {
                    if (GetTile(x, y) == tile) {
                        if (cachedImage != null)
                            g.DrawImageUnscaled(area.GetTileImage(GetTile(x,y)), x*16, y*16);
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

        public override void Save() {
        }
    }
}
