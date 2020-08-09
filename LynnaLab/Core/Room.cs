using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using Util;

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

        // Actual width and height of room (note that width can differ from Stride, below)
        int width, height;

        MemoryFileStream tileDataFile;
        MemoryFileStream dungeonFlagStream;

        Tileset tileset;
        Bitmap cachedImage;


        internal Room(Project p, int i) : base(p,i) {
            int tilesetID = GetTilesetByte() & 0x7f;

            Tileset t = Project.GetIndexedDataType<Tileset>(tilesetID);
            SetTileset(t);

            // Get dungeon flag file
            Data data = Project.GetData("dungeonRoomPropertiesGroupTable", (Group % 2) * 2);
            data = Project.GetData(data.GetValue(0));
            dungeonFlagStream = Project.GetBinaryFile("rooms/" + Project.GameString + "/" + data.GetValue(0));

            GenerateValueReferenceGroup();
        }


        public int Group
        {
            get { return Index >> 8; }
        }

        /// Some rooms are "duplicates" (share tile data and object data) but only a specific
        /// version is expected to be used. Most notable in sidescrolling areas. This gets the
        /// group number for the version of the room that's expected to be used.
        public int ExpectedGroup
        {
            get {
                if (Project.GameString == "ages") {
                    if (Group < 4)
                        return Group;
                    else if (Group < 8) {
                        int g = 4 + (Group % 2);
                        if (Tileset.SidescrollFlag)
                            return g + 2;
                        else
                            return g;
                    }
                    else
                        throw new Exception("Group number too high?");
                }
                else { // Seasons
                    if (Group == 0)
                        return Group;
                    else if (Group < 4)
                        return Group; // TODO: subrosia, maku tree, and indoor rooms have different values
                    else if (Group < 8) {
                        int g = 4 + (Group % 2);
                        if (Tileset.SidescrollFlag)
                            return g + 2;
                        else
                            return g;
                    }
                    else
                        throw new Exception("Group number too high?");
                }
            }
        }

        // Like above
        public int ExpectedIndex
        {
            get { return ExpectedGroup << 8 | (Index & 0xff); }
        }

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
        /// If true, tileset graphics are loaded after the screen transition instead of before.
        /// Often used in "buffer" rooms to transition between 2 tilesets.
        public bool GfxLoadAfterTransition {
            get {
                return (GetTilesetByte() & 0x80) != 0;
            }
        }

        public byte DungeonFlags {
            get {
                if (!(Group >= 4 && Group < 8))
                    throw new Exception(string.Format("Room {0:X3} is not in a dungeon group, doesn't have dungeon flags.", Index));
                dungeonFlagStream.Seek(Index & 0xff, SeekOrigin.Begin);
                return (byte)dungeonFlagStream.ReadByte();
            }
            set {
                if (!(Group >= 4 && Group < 8))
                    throw new Exception(string.Format("Room {0:X3} is not in a dungeon group, doesn't have dungeon flags.", Index));
                dungeonFlagStream.Seek(Index & 0xff, SeekOrigin.Begin);
                dungeonFlagStream.WriteByte(value);
            }
        }

        // Dungeon flag individual bits
        public bool DungeonFlagUp {
            get {
                return GetDungeonFlagBit(0);
            }
            set {
                SetDungeonFlagBit(0, value);
            }
        }
        public bool DungeonFlagRight {
            get {
                return GetDungeonFlagBit(1);
            }
            set {
                SetDungeonFlagBit(1, value);
            }
        }
        public bool DungeonFlagDown {
            get {
                return GetDungeonFlagBit(2);
            }
            set {
                SetDungeonFlagBit(2, value);
            }
        }
        public bool DungeonFlagLeft {
            get {
                return GetDungeonFlagBit(3);
            }
            set {
                SetDungeonFlagBit(3, value);
            }
        }
        public bool DungeonFlagKey {
            get {
                return GetDungeonFlagBit(4);
            }
            set {
                SetDungeonFlagBit(4, value);
            }
        }
        public bool DungeonFlagChest {
            get {
                return GetDungeonFlagBit(5);
            }
            set {
                SetDungeonFlagBit(5, value);
            }
        }
        public bool DungeonFlagBoss {
            get {
                return GetDungeonFlagBit(6);
            }
            set {
                SetDungeonFlagBit(6, value);
            }
        }
        public bool DungeonFlagDark {
            get {
                return GetDungeonFlagBit(7);
            }
            set {
                SetDungeonFlagBit(7, value);
            }
        }

        /// Alternative interface for accessing certain properties
        public ValueReferenceGroup ValueReferenceGroup { get; private set; }


        // # of bytes per row (including unused bytes; this means it has a value of 16 for large
        // rooms, which is 1 higher than the width, which is 15).
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
                    g.DrawImageUnscaled(tileset.GetTileImage(GetTile(x,y)), x*16, y*16);
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

        public void SetTileset(Tileset t) {
            if (tileset == null || t.Index != tileset.Index) {
                Stream groupTilesetsFile = GetTilesetMappingFile();
                groupTilesetsFile.Position = Index&0xff;
                int lastValue = groupTilesetsFile.ReadByte() & 0x80;
                groupTilesetsFile.Position = Index&0xff;
                groupTilesetsFile.WriteByte((byte)((t.Index&0x7f) | lastValue));

                var handler = new Tileset.TileModifiedHandler(ModifiedTilesetCallback);
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

        /// Returns true if the rooms are equal, or if they share duplicate room layout data.
        public bool EqualsOrDuplicate(Room room) {
            return tileDataFile == room.tileDataFile;
        }


        // Returns a stream for the tileset mapping file (256 bytes, one byte per room)
        MemoryFileStream GetTilesetMappingFile() {
            Data data = Project.GetData("roomTilesetsGroupTable", 2*(Index>>8));
            data = Project.GetData(data.GetValue(0)); // Follow .dw pointer

            string path = data.GetValue(0);
            path = path.Substring(1, path.Length-2); // Remove quotes

            return Project.GetBinaryFile(path);
        }

        byte GetTilesetByte() {
            return (byte)GetTilesetMappingFile().GetByte(Index & 0xff);
        }

        MemoryFileStream GetMusicFile() {
            Data data = Project.GetData("musicAssignmentGroupTable", 2*(Index>>8));
            data = Project.GetData(data.GetValue(0)); // Follow .dw pointer

            string path = data.GetValue(0);
            path = path.Substring(1, path.Length-2); // Remove quotes

            return Project.GetBinaryFile(path);
        }

        void UpdateRoomData() {
            if (tileDataFile != null) {
                tileDataFile.RemoveModifiedEventHandler(TileDataModified);
                tileDataFile = null;
            }
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
                    g.DrawImageUnscaled(tileset.GetTileImage(GetTile(x, y)), x*16, y*16);
                }
                g.Dispose();
            }
            Modified = true;
            if (RoomModifiedEvent != null)
                RoomModifiedEvent();
        }

        // Tileset modified
        void ModifiedTilesetCallback(int tile) {
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

        bool GetDungeonFlagBit(int bit) {
            return (DungeonFlags & (1 << bit)) != 0;
        }

        void SetDungeonFlagBit(int bit, bool value) {
            DungeonFlags &= (byte)~(1 << bit);
            if (value)
                DungeonFlags |= (byte)(1 << bit);
        }

        void GenerateValueReferenceGroup() {
            var vrs = new ValueReference[] {
                new StreamValueReference(
                        stream: GetTilesetMappingFile(),
                        name: "Gfx Load After Transition",
                        offset: Index & 0xff,
                        type: DataValueType.ByteBit,
                        startBit: 7)
                /*
                new StreamValueReference(
                        stream: GetMusicFile(),
                        name: "Music",
                        offset: Index & 0xff,
                        type: DataValueType.Byte),
                        */
            };

            ValueReferenceGroup = new ValueReferenceGroup(vrs);
        }
    }
}
