using System.IO;
using System.Text.Json.Serialization;

namespace LynnaLib
{
    public enum Season
    {
        None = -1,
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3,
    }

    /// Provides an interface for accessing various room properties, or to get related classes, ie.
    /// relating to room layout variants, warps or objects.
    public partial class Room : TrackedIndexedProjectData, IndexedProjectDataInstantiator
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // ================================================================================
        // Constructors
        // ================================================================================

        /// <summary>
        /// Access rooms with "Project.GetIndexedDataType" instead of this constructor to avoid
        /// duplicate instances.
        /// </summary>
        private Room(Project p, int index) : base(p, index)
        {
            if (!p.IsInConstructor)
                throw new Exception("Rooms should not be loaded outside of the Project constructor.");

            if (index < 0 || index >= Project.NumRooms)
                throw new Exception($"Invalid room: {index:X3}");

            UpdateChestRef(true);
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private Room(Project p, string id, TransactionState s)
            : base(p, int.Parse(id))
        {
            this.state = (State)s;
        }

        void CreateRoomLayouts()
        {
            var layouts = new List<RoomLayout>();
            if (HasSeasons)
            {
                for (int i = 0; i < 4; i++)
                {
                    layouts.Add(new RoomLayout(this, (Season)i));
                }
            }
            else
                layouts.Add(new RoomLayout(this, Season.None));
            _layouts = layouts;
            UpdateTileset();
        }

        static ProjectDataType IndexedProjectDataInstantiator.Instantiate(Project p, int index)
        {
            return new Room(p, index);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        class State : TransactionState
        {
            [JsonInclude, JsonRequired]
            public InstanceResolver<Data> _chestDataStart; // Can be null

            [JsonIgnore]
            public Data ChestDataStart
            {
                get { return _chestDataStart?.Instance; }
                set
                {
                    if (value == null)
                        _chestDataStart = null;
                    else
                        _chestDataStart = new(value);
                }
            }
        }

        State state = new();

        // These are not tracked, but are created once when needed and then never changed.
        MemoryFileStream _dungeonFlagStream;
        List<RoomLayout> _layouts; // Different season layouts
        ValueReferenceGroup _vrg;

        // This is not directly tracked, but is kept up to date through events triggered by undos.
        Chest chest;

        // ================================================================================
        // Events
        // ================================================================================

        // Event invoked upon adding a chest. For removing a chest, use the event in the Chest
        // class.
        public event EventHandler<EventArgs> ChestAddedEvent;

        // ================================================================================
        // Properties
        // ================================================================================

        public int Group
        {
            get { return Index >> 8; }
        }

        /// Some rooms are "duplicates" (share tile data and object data) but only a specific
        /// version is expected to be used. Most notable in sidescrolling areas. This gets the
        /// group number for the version of the room that's expected to be used.
        public int ExpectedGroup
        {
            get
            {
                if (Project.GameString == "ages")
                {
                    if (Group < 4)
                        return Group;
                    else if (Group < 8)
                    {
                        int g = 4 + (Group % 2);
                        if (GetTileset(Season.None).SidescrollFlag)
                            return g + 2;
                        else
                            return g;
                    }
                    else
                        throw new Exception("Group number too high?");
                }
                else
                { // Seasons
                    if (Group == 0)
                        return Group;
                    else if (Group < 4)
                    {
                        if (GetTileset(Season.None).SubrosiaFlag)
                            return 1;
                        else if (GetTileset(Season.None).MakuTreeFlag)
                            return 2;
                        else if (GetTileset(Season.None).SmallIndoorFlag)
                            return 3;
                        else
                            return Group;
                    }
                    else if (Group < 8)
                    {
                        int g = 4 + (Group % 2);
                        if (GetTileset(Season.None).SidescrollFlag)
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

        public int Width { get { return GetLayout(Season.None, true).Width; } }
        public int Height { get { return GetLayout(Season.None, true).Height; } }

        /// <summary>
        /// If "HasSeasons" is true then the tileset should also be seasonal, although since it's
        /// easy to change the tileset, there's no guarantee of that.
        /// </summary>
        public bool HasSeasons
        {
            get { return Project.Game == Game.Seasons && Group == 0; }
        }

        public Chest Chest
        {
            get { return chest; }
        }

        /// If true, tileset graphics are loaded after the screen transition instead of before.
        /// Often used in "buffer" rooms to transition between 2 tilesets.
        public bool GfxLoadAfterTransition
        {
            get
            {
                return (GetTilesetByte() & 0x80) != 0;
            }
        }

        public int TilesetIndex
        {
            get
            {
                return ValueReferenceGroup.GetIntValue("Tileset");
            }
            set
            {
                Project.BeginTransaction($"Change Tileset#{Index:X3}", true);
                ValueReferenceGroup.SetValue("Tileset", value);
                Project.EndTransaction();
            }
        }

        public byte DungeonFlags
        {
            get
            {
                if (!(Group >= 4 && Group < 8))
                    throw new Exception(string.Format("Room {0:X3} is not in a dungeon group, doesn't have dungeon flags.", Index));
                DungeonFlagStream.Seek(Index & 0xff, SeekOrigin.Begin);
                return (byte)DungeonFlagStream.ReadByte();
            }
            set
            {
                if (!(Group >= 4 && Group < 8))
                    throw new Exception(string.Format("Room {0:X3} is not in a dungeon group, doesn't have dungeon flags.", Index));
                DungeonFlagStream.Seek(Index & 0xff, SeekOrigin.Begin);
                DungeonFlagStream.WriteByte(value);
            }
        }

        // Dungeon flag individual bits
        public bool DungeonFlagUp
        {
            get
            {
                return GetDungeonFlagBit(0);
            }
            set
            {
                SetDungeonFlagBit(0, value);
            }
        }
        public bool DungeonFlagRight
        {
            get
            {
                return GetDungeonFlagBit(1);
            }
            set
            {
                SetDungeonFlagBit(1, value);
            }
        }
        public bool DungeonFlagDown
        {
            get
            {
                return GetDungeonFlagBit(2);
            }
            set
            {
                SetDungeonFlagBit(2, value);
            }
        }
        public bool DungeonFlagLeft
        {
            get
            {
                return GetDungeonFlagBit(3);
            }
            set
            {
                SetDungeonFlagBit(3, value);
            }
        }
        public bool DungeonFlagKey
        {
            get
            {
                return GetDungeonFlagBit(4);
            }
            set
            {
                SetDungeonFlagBit(4, value);
            }
        }
        public bool DungeonFlagChest
        {
            get
            {
                return GetDungeonFlagBit(5);
            }
            set
            {
                SetDungeonFlagBit(5, value);
            }
        }
        public bool DungeonFlagBoss
        {
            get
            {
                return GetDungeonFlagBit(6);
            }
            set
            {
                SetDungeonFlagBit(6, value);
            }
        }
        public bool DungeonFlagDark
        {
            get
            {
                return GetDungeonFlagBit(7);
            }
            set
            {
                SetDungeonFlagBit(7, value);
            }
        }

        /// Alternative interface for accessing certain properties
        public ValueReferenceGroup ValueReferenceGroup
        {
            get
            {
                if (_vrg == null)
                    GenerateValueReferenceGroup();
                return _vrg;
            }
        }

        public string TransactionIdentifier { get { return $"room-{Index:X3}"; } }

        /// Whether a room pack value exists or not (only exists for the main overworld groups)
        bool HasRoomPack
        {
            get
            {
                return GetRoomPackFile() != null;
            }
        }

        MemoryFileStream DungeonFlagStream
        {
            get
            {
                if (_dungeonFlagStream == null)
                {
                    // Get dungeon flag file
                    Data data = Project.GetData("dungeonRoomPropertiesGroupTable", (Group % 2) * 2);
                    data = Project.GetData(data.GetValue(0));
                    _dungeonFlagStream = Project.GetFileStream("rooms/" + Project.GameString + "/" + data.GetValue(0));
                }
                return _dungeonFlagStream;
            }
        }

        List<RoomLayout> Layouts
        {
            get
            {
                if (_layouts == null)
                    CreateRoomLayouts();
                return _layouts;
            }
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        /// Gets a RoomLayout object corresponding to the season. Pass -1 if seasons are not
        /// expected to exist.
        public RoomLayout GetLayout(Season season, bool autoCorrect = false)
        {
            season = ValidateSeason(season, autoCorrect);
            if (season == Season.None)
                return Layouts[0];
            else
                return Layouts[(int)season];
        }

        public RealTileset GetTileset(Season season, bool autoCorrect = false)
        {
            season = ValidateSeason(season, autoCorrect);
            // Always autocorrect the season when fetching the tileset, just in case the room is
            // considered "seasonal" while the tileset isn't. (See Samasa Desert!)
            return Project.GetTileset(TilesetIndex, season, autoCorrect: true);
        }

        // These 2 functions may be deprecated later if I switch to using
        // constant definitions
        public int GetMusicID()
        {
            TrackedStream file = GetMusicFile();
            file.Position = Index & 0xff;
            return file.ReadByte();
        }
        public void SetMusicID(int id)
        {
            TrackedStream file = GetMusicFile();
            file.Position = Index & 0xff;
            file.WriteByte((byte)id);
        }

        public ObjectGroup GetObjectGroup()
        {
            string tableLabel = Project.GetData("objectDataGroupTable", 2 * (Index >> 8)).GetValue(0);
            string label = Project.GetData(tableLabel, 2 * (Index & 0xff)).GetValue(0);
            return Project.GetObjectGroup(label, ObjectGroupType.Main);
        }

        public WarpGroup GetWarpGroup()
        {
            return Project.GetIndexedDataType<WarpGroup>(Index);
        }


        // Chest-related stuff

        public void AddChest()
        {
            Project.BeginTransaction("Add chest");

            if (Chest != null)
            {
                log.Warn(string.Format("Tried to add chest data to room {0:x3} which already has chest data.", Index));
                return;
            }

            int room = Index;

            int group = room >> 8;
            room &= 0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestDataGroupTable");
            Data chestPointer = chestFileParser.GetData("chestDataGroupTable", group * 2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            chestFileParser.InsertParseableTextBefore(chestGroupData, new string[] {
                string.Format("\tm_ChestData $00, ${0:x2}, $0000", Index & 0xff)
            });

            UpdateChestRef(false);

            Debug.Assert(state.ChestDataStart != null);
            Debug.Assert(Chest != null);

            Project.EndTransaction();
        }

        public bool IsValidSeason(Season season)
        {
            if (HasSeasons)
                return (int)season >= 0 && (int)season <= 3;
            else
                return season == Season.None;
        }


        // Private methods

        void UpdateTileset()
        {
            foreach (var layout in Layouts)
                layout.UpdateTileset();
        }

        // Returns a stream for the tileset mapping file (256 bytes, one byte per room)
        MemoryFileStream GetTilesetMappingFile()
        {
            Data data = Project.GetData("roomTilesetsGroupTable", 2 * (Index >> 8));
            data = Project.GetData(data.GetValue(0)); // Follow .dw pointer

            string path = data.GetValue(0);
            path = path.Substring(1, path.Length - 2); // Remove quotes

            return Project.GetFileStream(path);
        }

        byte GetTilesetByte()
        {
            return (byte)GetTilesetMappingFile().GetByte(Index & 0xff);
        }

        MemoryFileStream GetMusicFile()
        {
            Data data = Project.GetData("musicAssignmentGroupTable", 2 * (Index >> 8));
            data = Project.GetData(data.GetValue(0)); // Follow .dw pointer

            string path = data.GetValue(0);
            path = path.Substring(1, path.Length - 2); // Remove quotes

            return Project.GetFileStream(path);
        }

        MemoryFileStream GetRoomPackFile()
        {
            string s;
            if (Project.GameString == "seasons")
            {
                if (Group != 0)
                    return null;
                s = "rooms/seasons/roomPacks.bin";
            }
            else
            { // ages
                if (Group == 0)
                    s = "rooms/ages/roomPacksPresent.bin";
                else if (Group == 1)
                    s = "rooms/ages/roomPacksPast.bin";
                else
                    return null;
            }
            return Project.GetFileStream(s);
        }

        bool GetDungeonFlagBit(int bit)
        {
            return (DungeonFlags & (1 << bit)) != 0;
        }

        void SetDungeonFlagBit(int bit, bool value)
        {
            DungeonFlags &= (byte)~(1 << bit);
            if (value)
                DungeonFlags |= (byte)(1 << bit);
        }

        void GenerateValueReferenceGroup()
        {
            ValueReferenceDescriptor roomPackDesc;

            var tooltip = "Seasons: Forces a season recheck when transitioning between rooms with different values."
                + "\nBoth games: Specific values force different tilesets to load in animal companion regions."
                + "\nAges: Forces seasons-like screen reloads only if bit 7 is set (value $80 or above).";

            if (HasRoomPack)
            {
                roomPackDesc = StreamValueReference.Descriptor(
                    Project,
                    stream: GetRoomPackFile(),
                    name: "Room Pack",
                    offset: Index & 0xff,
                    type: DataValueType.Byte,
                    tooltip: tooltip);
            }
            else
            {
                // Placeholder (need to keep elements the same at all times to make the UI simpler)
                roomPackDesc = AbstractIntValueReference.Descriptor(
                    Project,
                    name: "Room Pack",
                    getter: () => 0,
                    setter: (v) => { },
                    maxValue: 255,
                    editable: false,
                    tooltip: tooltip);
            }

            var descriptors = new ValueReferenceDescriptor[] {
                StreamValueReference.Descriptor(Project,
                        stream: GetTilesetMappingFile(),
                        name: "Tileset",
                        offset: Index & 0xff,
                        type: DataValueType.ByteBits,
                        maxValue: Project.NumTilesets-1,
                        startBit: 0,
                        endBit: 6),
                StreamValueReference.Descriptor(Project,
                        stream: GetMusicFile(),
                        name: "Music",
                        offset: Index & 0xff,
                        type: DataValueType.Byte,
                        constantsMappingString: "MusicMapping"),
                roomPackDesc,
                StreamValueReference.Descriptor(Project,
                        stream: GetTilesetMappingFile(),
                        name: "Gfx Load After Transition",
                        offset: Index & 0xff,
                        type: DataValueType.ByteBit,
                        startBit: 7,
                        tooltip: "Check to load this screen's tileset graphics after the screen transition, instead of before."),
            };

            _vrg = new ValueReferenceGroup(descriptors);

            _vrg["Tileset"].ValueReference.AddValueModifiedHandler((sender, args) =>
            {
                UpdateTileset();
            });

            _vrg.EnableTransactions($"Edit room property#{TransactionIdentifier}", true);
        }

        // Find the data corresponding to the chest for this room, or "null" if it doesn't exist.
        Data GetChestData()
        {
            int room = Index;

            int group = room >> 8;
            room &= 0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestDataGroupTable");
            Data chestPointer = chestFileParser.GetData("chestDataGroupTable", group * 2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            while (chestGroupData.Command == "m_ChestData")
            {
                if (chestGroupData.GetIntValue(1) == room)
                    return chestGroupData;
                chestGroupData = chestGroupData.NextData;
            }

            return null;
        }

        /// <summary>
        /// Called by chest class when it is removed
        /// </summary>
        internal void OnChestDeleted(Chest c)
        {
            Debug.Assert(Chest == c);
            UpdateChestRef(false);
            Debug.Assert(state.ChestDataStart == null);
            Debug.Assert(Chest == null);
        }

        /// <summary>
        /// Called when chests are added or removed.
        /// </summary>
        void UpdateChestRef(bool inConstructor)
        {
            Data d = GetChestData();
            if (d == state.ChestDataStart)
                return;

            // Don't capture initial state in constructor - since we're not actually changing
            // anything. (Don't want to register undo events that would undo the initialization!)
            if (!inConstructor)
                Project.UndoState.CaptureInitialState<State>(this);

            state.ChestDataStart = d;
            OnChestDataChanged(true);
        }

        /// <summary>
        /// Call this when ChestDataStart is changed to ensure that the corresponding Chest is
        /// updated. This is called on undo/redo.
        /// </summary>
        void OnChestDataChanged(bool invokeEvents)
        {
            Chest oldChest = chest;

            if (state.ChestDataStart == null)
                chest = null;
            else
                chest = new Chest(this, state.ChestDataStart);

            if (!invokeEvents)
                return;

            if (oldChest != null)
                oldChest.InvokeDeletedEvent();
            if (chest != null)
                ChestAddedEvent?.Invoke(this, null);
        }

        /// <summary>
        /// Checks if the given season is valid for this room. If it's not, this either produces an
        /// exception or returns a valid season (Season.None or Season.Spring).
        /// </summary>
        Season ValidateSeason(Season season, bool autoCorrect)
        {
            var handleError = () =>
            {
                if (autoCorrect)
                    return HasSeasons ? Season.Spring : Season.None;
                else
                    throw new ProjectErrorException($"Invalid season '{season}' in room {Index:X3}");
            };

            if (!Enum.IsDefined<Season>(season))
                return handleError();
            else if (season == Season.None && HasSeasons)
                return handleError();
            else if (season != Season.None && !HasSeasons)
                return handleError();

            return season;
        }

        // ================================================================================
        // TrackedProjectData implementation
        // ================================================================================

        public override TransactionState GetState()
        {
            return state;
        }

        public override void SetState(TransactionState state)
        {
            this.state = (State)state;
        }

        public override void InvokeUndoEvents(TransactionState oldState)
        {
            State old = (State)oldState;
            if (state._chestDataStart != old._chestDataStart)
                OnChestDataChanged(true);
        }

        public override void OnInitializedFromTransfer()
        {
            OnChestDataChanged(false);
        }
    }
}
