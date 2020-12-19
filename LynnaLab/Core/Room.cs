using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using Util;

namespace LynnaLib
{
    /// Provides an interface for modifying layout and keeping track of changes
    /// to the image.
    /// Can also use this to modify various room properties, or to get related
    /// classes, ie. relating to warps or objects.
    public partial class Room : ProjectIndexedDataType {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Actual width and height of room (note that width can differ from Stride, below)
        int width, height;

        MemoryFileStream tileDataFile;
        MemoryFileStream dungeonFlagStream;

        Tileset loadedTileset;
        Bitmap cachedImage;


        public delegate void RoomModifiedHandler();
        // Event invoked when the room's image is modified in any way
        public event RoomModifiedHandler RoomModifiedEvent;

        // Event invoked upon adding a chest. For removing a chest, use the event in the Chest
        // class.
        public event EventHandler<EventArgs> ChestAddedEvent;


        internal Room(Project p, int i) : base(p,i) {
            // Get dungeon flag file
            Data data = Project.GetData("dungeonRoomPropertiesGroupTable", (Group % 2) * 2);
            data = Project.GetData(data.GetValue(0));
            dungeonFlagStream = Project.GetBinaryFile("rooms/" + Project.GameString + "/" + data.GetValue(0));

            GenerateValueReferenceGroup();

            UpdateTileset();
            InitializeChest();
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
            get {
                return Project.GetIndexedDataType<Tileset>(ValueReferenceGroup.GetIntValue("Tileset"));
            }
            set {
                ValueReferenceGroup.SetValue("Tileset", value.Index);
            }
        }
        public Chest Chest { get; private set; }

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


        /// # of bytes per row (including unused bytes; this means it has a value of 16 for large
        /// rooms, which is 1 higher than the width, which is 15).
        int Stride {
            get { return width == 10 ? 10 : 16; }

        }

        /// Whether a room pack value exists or not (only exists for the main overworld groups)
        bool HasRoomPack {
            get {
                return GetRoomPackFile() != null;
            }
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


        // Chest-related stuff

        public void AddChest() {
            if (Chest != null) {
                log.Warn(string.Format("Tried to add chest data to room {0:x3} which already has chest data.", Index));
                return;
            }

            int room = Index;

            int group = room>>8;
            room &= 0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestDataGroupTable");
            Data chestPointer = chestFileParser.GetData("chestDataGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            Data newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, new List<string>{"\t"});
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {Wla.ToByte((byte)room)}, -1, null, null);
            newData.PrintCommand = false;
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, null);
            newData.PrintCommand = false;
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, null);
            newData.PrintCommand = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            InitializeChest();

            ChestAddedEvent?.Invoke(this, null);
        }

        public void DeleteChest() {
            if (Chest == null) {
                log.Warn(string.Format("Tried to remove chest data to room {0:x3} which doesn't have chest data.", Index));
                return;
            }

            Chest.Delete();
            // Deletion handler will set "chest" to null
        }



        // Private methods

        void UpdateTileset() {
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
                if (RoomModifiedEvent != null)
                    RoomModifiedEvent();
            }
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

        MemoryFileStream GetRoomPackFile() {
            string s;
            if (Project.GameString == "seasons") {
                if (Group != 0)
                    return null;
                s = "rooms/seasons/roomPacks.bin";
            }
            else { // ages
                if (Group == 0)
                    s = "rooms/ages/roomPacksPresent.bin";
                else if (Group == 1)
                    s = "rooms/ages/roomPacksPast.bin";
                else
                    return null;
            }
            return Project.GetBinaryFile(s);
        }

        void UpdateRoomData() {
            if (tileDataFile != null) {
                tileDataFile.RemoveModifiedEventHandler(TileDataModified);
                tileDataFile = null;
            }
            // Get the tileDataFile
            int layoutGroup = Tileset.LayoutGroup;
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
                    g.DrawImageUnscaled(Tileset.GetTileImage(GetTile(x, y)), x*16, y*16);
                }
                g.Dispose();
            }
            Modified = true;
            if (RoomModifiedEvent != null)
                RoomModifiedEvent();
        }

        // Tileset modified
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

            if (changed && RoomModifiedEvent != null)
                RoomModifiedEvent();
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
            ValueReference roomPackVr;
            if (HasRoomPack) {
                roomPackVr = new StreamValueReference(Project,
                        stream: GetRoomPackFile(),
                        name: "Room Pack",
                        offset: Index & 0xff,
                        type: DataValueType.Byte);
            }
            else {
                // Placeholder (need to keep elements the same at all times to make the UI simpler)
                roomPackVr = new AbstractIntValueReference(Project,
                        name: "Room Pack",
                        getter: () => 0,
                        setter: (v) => {},
                        maxValue: 255,
                        editable: false);
            }
            roomPackVr.Tooltip = "Seasons: Forces a season recheck when transitioning between rooms with different values."
                + "\nBoth games: Specific values force different tilesets to load in animal companion regions."
                + "\nAges: Forces seasons-like screen reloads only if bit 7 is set (value $80 or above).";


            var vrs = new ValueReference[] {
                new StreamValueReference(Project,
                        stream: GetMusicFile(),
                        name: "Music",
                        offset: Index & 0xff,
                        type: DataValueType.Byte,
                        constantsMappingString: "MusicMapping"),
                new StreamValueReference(Project,
                        stream: GetTilesetMappingFile(),
                        name: "Tileset",
                        offset: Index & 0xff,
                        type: DataValueType.ByteBits,
                        maxValue: Project.NumTilesets-1,
                        startBit: 0,
                        endBit: 6),
                roomPackVr,
                new StreamValueReference(Project,
                        stream: GetTilesetMappingFile(),
                        name: "Gfx Load After Transition",
                        offset: Index & 0xff,
                        type: DataValueType.ByteBit,
                        startBit: 7,
                        tooltip: "Check to load this screen's tileset graphics after the screen transition, instead of before."),
            };

            ValueReferenceGroup = new ValueReferenceGroup(vrs);

            ValueReferenceGroup["Tileset"].AddValueModifiedHandler((sender, args) => {
                UpdateTileset();
            });
        }

        // Find the data corresponding to the chest for this room, or "null" if it doesn't exist.
        Data GetChestData() {
            int room = Index;

            int group = room>>8;
            room &= 0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestDataGroupTable");
            Data chestPointer = chestFileParser.GetData("chestDataGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            while (chestGroupData.GetIntValue(0) != 0xff) {
                if (chestGroupData.NextData.GetIntValue(0) == room)
                    return chestGroupData;
                for (int i=0;i<4;i++)
                    chestGroupData = chestGroupData.NextData;
            }

            return null;
        }

        void InitializeChest() {
            if (Chest != null)
                throw new Exception("Internal error");
            Data d = GetChestData();
            if (d == null)
                return;
            Chest = new Chest(d);
            Chest.DeletedEvent += (sender, args) => { Chest = null; };
        }
    }
}
