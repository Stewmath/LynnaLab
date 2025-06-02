namespace LynnaLib
{
    /// <summary>
    /// Invoked when something changes the layout of rooms, or number of floors, in a dungeon.
    /// </summary>
    public class DungeonChangedEventArgs
    {
        public bool AllRoomsChanged { get; init; } // True if all rooms are potentially modified
        public bool FloorsChanged { get; init; } // True if number of floors has potentially changed

        public bool RoomPosValid { get; init; } // True if a single room is modified.
        public (int x, int y, int floor) RoomPos { get; init; } // position in grid of changed room

        private DungeonChangedEventArgs() {}

        public static DungeonChangedEventArgs CreateFloorsChanged()
        {
            return new DungeonChangedEventArgs() { FloorsChanged = true, AllRoomsChanged = true };
        }
        public static DungeonChangedEventArgs CreateAllRoomsChanged()
        {
            return new DungeonChangedEventArgs() { AllRoomsChanged = true };
        }
        public static DungeonChangedEventArgs CreateRoomChanged(int x, int y, int floor)
        {
            return new DungeonChangedEventArgs() { RoomPosValid = true, RoomPos = (x, y, floor) };
        }
    }

    /// <summary>
    /// Represents a dungeon, which is really just an organized layout of rooms. Some "dungeons" are
    /// just collections of miscellaneous large rooms (like Ambi's Palace).
    ///
    /// This class assumes that each dungeon uniquely controls its own dungeon layout data. This is
    /// technically not true in vanilla Ages, as dungeons 0 and 9 reference the same layout data
    /// (maku path). This has been changed in the hack-base branch (fresh duplicate data created).
    ///
    /// The above assumption could also break down if there is overlap between dungeons in the
    /// "dungeonLayouts.s" file, but that should only occur if there is a misconfiguration.
    ///
    /// Utmost flexibility to overcome the above limitations would involve installing handlers on
    /// the underlying data itself, rather than doing things when the "SetRoom" function, etc. is
    /// called. That's annoying to do when floors can be added and deleted though.
    /// </summary>
    public class Dungeon : Map, IndexedProjectDataInstantiator
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        private Dungeon(Project p, int i) : base(p, i.ToString())
        {
            if (!p.IsInConstructor)
                throw new Exception("Dungeons should not be loaded outside of the Project constructor.");

            this.index = i;
            this.state = new();

            this.state.numFloors = DataStart.GetIntValue(3);

            DetermineGroup();
            DetermineRoomsUsed();
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private Dungeon(Project p, string id, TransactionState s)
            : base(p, id)
        {
            this.state = (State)s;
            this.index = int.Parse(id);
        }

        static ProjectDataType IndexedProjectDataInstantiator.Instantiate(Project p, int index)
        {
            return new Dungeon(p, index);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        class State : TransactionState
        {
            // roomsLookup[i] = a list of positions in the map where room "i" is used.
            public List<(int x, int y, int floor)>[] roomLookup;
            public int _mainGroup;
            public int numFloors;
        }

        readonly int index;

        State state;
        Data _dataStart;
        ValueReferenceGroup _vrg;

        // ================================================================================
        // Properties
        // ================================================================================

        public string TransactionIdentifier { get { return $"dungeon-{Index:X2}"; } }

        // The start of the data, at the "dungeonDataXX" label
        Data DataStart
        {
            get
            {
                if (_dataStart == null)
                {
                    FileParser dungeonDataFile = Project.GetFileWithLabel("dungeonDataTable");
                    Data pointerData = dungeonDataFile.GetData("dungeonDataTable", Index * 2);
                    string label = pointerData.GetValue(0);
                    _dataStart = dungeonDataFile.GetData(label);
                }
                return _dataStart;
            }
        }

        // ================================================================================
        // Events
        // ================================================================================

        /// <summary>
        /// Event invoked when:
        /// - Number of floors change
        /// - Room layout changes
        /// </summary>
        public event EventHandler<DungeonChangedEventArgs> ChangedEvent;

        // ================================================================================
        // Properties
        // ================================================================================

        public ValueReferenceGroup ValueReferenceGroup
        {
            get
            {
                if (_vrg == null)
                    GenerateValueReferenceGroup();
                return _vrg;
            }
        }

        public int NumFloors
        {
            get
            {
                Debug.Assert(state.numFloors == GetDataIndex(3));
                return state.numFloors;
            }
            private set
            {
                Project.TransactionManager.CaptureInitialState<State>(this);
                state.numFloors = value;
                SetDataIndex(3, value);
            }
        }

        // Map properties

        /// <summary>
        /// Group management is confusing. Most dungeon rooms use groups 4/5, but sidescrolling rooms
        /// use groups 6/7 instead. So there is no one single "group number" that any given dungeon
        /// uses, there are actually two: "MainGroup" and "SidescrollGroup".
        ///
        /// This means there is a "fake" version of each dungeon room, the one with the wrong group
        /// number. It can be recognized by the fact that warp data is usually missing. LynnaLab
        /// tries to hide it by auto-adjusting the group number of the room to show.
        /// </summary>
        public override int MainGroup
        {
            get
            {
                return state._mainGroup;
            }
        }

        public int SidescrollGroup
        {
            get
            {
                return MainGroup + 2;
            }
        }

        public override int MapWidth
        {
            get
            {
                return 8;
            }
        }
        public override int MapHeight
        {
            get
            {
                return 8;
            }
        }
        public override int RoomWidth
        {
            get
            {
                return GetRoom(0, 0).GetLayout(Season.None, autoCorrect: true).Width;
            }
        }
        public override int RoomHeight
        {
            get
            {
                return GetRoom(0, 0).GetLayout(Season.None, autoCorrect: true).Height;
            }
        }
        public override Season Season
        {
            get { return Season.None; }
        }

        // Other properties

        public int Index { get { return index; } }


        public void SetRoom(int x, int y, int floor, int room)
        {
            Project.BeginTransaction($"Change dungeon room#d{Index}x{x}y{y}f{floor}", true);
            Project.TransactionManager.CaptureInitialState<State>(this);

            int pos = y * 8 + x;
            if (pos >= 0x40)
                throw new ArgumentException(string.Format("Arguments {0:X},{1:X} to 'SetRoom' too high.", x, y));
            if (floor >= NumFloors)
                throw new ArgumentException(string.Format("Floor {0} too high.", floor));

            Data d = GetFloorLayoutData(floor).GetDataAtOffset(y * 8 + x);
            int oldRoom = d.GetIntValue(0);

            d.SetByteValue(0, (byte)room);

            if (!state.roomLookup[oldRoom].Remove((x, y, floor)))
                throw new Exception("Internal error: Dungeon room lookup table invalid");
            state.roomLookup[(byte)room].Add((x, y, floor));

            DetermineRoomsUsed();

            ChangedEvent?.Invoke(this, DungeonChangedEventArgs.CreateRoomChanged(x, y, floor));

            Project.EndTransaction();
        }

        public bool RoomUsed(int roomIndex)
        {
            int group = roomIndex >> 8;
            if (group != MainGroup && group != SidescrollGroup)
                return false;
            return state.roomLookup[roomIndex & 0xff].Count > 0;
        }

        // Map methods

        public override Room GetRoom(int x, int y, int floor = 0)
        {
            int pos = y * 8 + x;
            if (pos >= 0x40)
                throw new ArgumentException(string.Format("Arguments {0:X},{1:X} to 'GetRoom' too high.", x, y));
            if (floor >= NumFloors)
                throw new ArgumentException(string.Format("Floor {0} too high.", floor));

            int roomIndex = GetFloorLayoutData(floor).GetDataAtOffset(y * 8 + x).GetIntValue(0);
            Room room = Project.GetIndexedDataType<Room>(roomIndex + MainGroup * 0x100);

            // Change the group if the room is sidescrolling. As a result, the group number of
            // sidescrolling rooms in a dungeon will be different from other rooms, which will look
            // weird, but that's the way it works apparently...
            if (room.GetTileset(Season.None).SidescrollFlag)
                room = Project.GetIndexedDataType<Room>(roomIndex + SidescrollGroup * 0x100);

            return room;
        }

        public override IEnumerable<(int x, int y, int floor)> GetRoomPositions(Room room)
        {
            if (room.Group != MainGroup && room.Group != SidescrollGroup)
                return new List<(int, int, int)>();
            return state.roomLookup[room.Index & 0xff];
        }

        public override bool GetRoomPosition(Room room, out int x, out int y, out int floor)
        {
            x = -1;
            y = -1;
            floor = -1;

            if (room.Group != MainGroup && room.Group != SidescrollGroup)
                return false;
            if (state.roomLookup[room.Index & 0xff].Count == 0)
                return false;

            var tup = state.roomLookup[room.Index & 0xff][0];
            x = tup.x;
            y = tup.y;
            floor = tup.floor;

            return true;
        }

        /// Insert a floor below "floorIndex". If "floorIndex == NumFloors" then the floor is
        /// inserted at the top.
        public void InsertFloor(int floorIndex)
        {
            Project.BeginTransaction("Add dungeon floor");
            Project.TransactionManager.CaptureInitialState<State>(this);

            if (floorIndex < 0 || floorIndex > NumFloors)
                throw new ArgumentException("Can't insert floor " + floorIndex + ".");

            FileComponent component;
            string dungeonLayoutLabel = DataStart.GetValue(2);

            if (floorIndex == 0)
                component = Project.GetLabel(dungeonLayoutLabel);
            else
                component = Project.GetData(dungeonLayoutLabel, floorIndex * 64 - 1);

            // Insert the blank floor data
            List<string> textToInsert = new List<string>();
            for (int y = 0; y < 8; y++)
                textToInsert.Add("\t.db $00 $00 $00 $00 $00 $00 $00 $00");

            // Location to add extra newline differs if this is the first floor
            if (floorIndex == 0)
            {
                component.FileParser.InsertParseableTextAfter(component, new string[] { "" });
                component.FileParser.InsertParseableTextAfter(component, textToInsert.ToArray());
            }
            else
            {
                component.FileParser.InsertParseableTextAfter(component, textToInsert.ToArray());
                component.FileParser.InsertParseableTextAfter(component, new string[] { "" });
            }

            NumFloors++;

            DetermineRoomsUsed();
            ChangedEvent?.Invoke(this, DungeonChangedEventArgs.CreateFloorsChanged());

            Project.EndTransaction();
        }

        public void RemoveFloor(int floorIndex)
        {
            Project.BeginTransaction("Remove dungeon floor");
            Project.TransactionManager.CaptureInitialState<State>(this);

            if (floorIndex < 0 || floorIndex >= NumFloors)
                throw new ArgumentException("Can't remove floor " + floorIndex + ": doesn't exist.");
            if (NumFloors <= 1)
                throw new ArgumentException("Can't remove last remaining floor in a dungeon. (Or at least it's not a good idea.)");

            FileComponent component = GetFloorLayoutData(floorIndex);
            FileParser parser = component.FileParser;
            int count = 0;
            while (count < 64)
            {
                if (component is Data)
                    count++;
                FileComponent next = component.Next;
                parser.RemoveFileComponent(component);
                component = next;
            }
            // Remove ending newline, but don't want to accidentally remove data
            if (component is StringFileComponent)
                parser.RemoveFileComponent(component);

            NumFloors--;

            DetermineRoomsUsed();
            ChangedEvent?.Invoke(this, DungeonChangedEventArgs.CreateFloorsChanged());

            Project.EndTransaction();
        }


        void GenerateValueReferenceGroup()
        {
            var groupDescriptor = AbstractIntValueReference.Descriptor(
                Project,
                name: "Group",
                getter: () => MainGroup,
                setter: (v) => SetGroup(v),
                minValue: 4,
                maxValue: 5,
                tooltip: "Also known as the high byte of the room index.");

            var list = new ValueReferenceDescriptor[] {
                groupDescriptor,
                DataValueReference.Descriptor(
                        data: DataStart,
                        name: "Wallmaster dest room",
                        index: 1,
                        type: DataValueType.Byte,
                        tooltip: "The low byte of the room index wallmasters will send you to."),
                DataValueReference.Descriptor(
                        data: DataStart,
                        name: "Base floor name",
                        index: 4,
                        type: DataValueType.Byte,
                        tooltip: "Determines what the game will call the bottom floor. For a value of:\n$00: The bottom floor is 'B3'.\n$01: The bottom floor is 'B2'.\n$02: The bottom floor is 'B1'.\n$03: The bottom floor is 'F1'."),
                DataValueReference.Descriptor(
                        data: DataStart,
                        name: "Floors unlocked with compass",
                        index: 5,
                        type: DataValueType.Byte,
                        tooltip: "A bitset of floors that will appear on the map when the compass is obtained.\n\nEg. If this is $05, then floors 0 and 2 will be unlocked (bits 0 and 2 are set)."),
            };

            _vrg = new ValueReferenceGroup(list);
            _vrg.EnableTransactions($"Edit dungeon property#{TransactionIdentifier}", true);
        }

        /// <summary>
        /// Ensure that CaptureInitialState() is called if necessary when invoking this (except from
        /// constructor)
        /// </summary>
        void DetermineRoomsUsed()
        {
            state.roomLookup = new List<(int, int, int)>[256];
            for (int i=0; i<256; i++)
                state.roomLookup[i] = new List<(int,int,int)>();
            for (int f = 0; f < NumFloors; f++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    for (int y = 0; y < MapHeight; y++)
                    {
                        int room = GetRoom(x, y, f).Index & 0xff;
                        state.roomLookup[room].Add((x, y, f));
                    }
                }
            }
        }

        /// <summary>
        /// Ensure that CaptureInitialState() is called if necessary when invoking this (except from
        /// constructor)
        /// </summary>
        void DetermineGroup()
        {
            state._mainGroup = GetDataIndex(0) - Project.Eval(">wGroup4RoomFlags") + 4;
        }

        int GetDataIndex(int i)
        {
            return DataStart.GetIntValue(i);
        }

        Data GetFloorLayoutData(int floor)
        {
            return Project.GetData(DataStart.GetValue(2), floor * 64);
        }

        void SetDataIndex(int i, int val)
        {
            DataStart.SetByteValue(i, (byte)val);
        }

        void SetGroup(int g)
        {
            if (!(g == 4 || g == 5))
                throw new ArgumentException("Invalid group '" + g + "' for dungeon.");

            Project.TransactionManager.CaptureInitialState<State>(this);

            DataStart.SetValue(0, ">wGroup" + g.ToString() + "RoomFlags");

            DetermineGroup();
            DetermineRoomsUsed();
            ChangedEvent?.Invoke(this, DungeonChangedEventArgs.CreateAllRoomsChanged());
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
            bool floorsChanged = old.numFloors != state.numFloors;

            DungeonChangedEventArgs args;

            if (floorsChanged)
            {
                args = DungeonChangedEventArgs.CreateFloorsChanged();
            }
            else
            {
                // Assume all rooms are potentially changed (too lazy to implement comparison)
                args = DungeonChangedEventArgs.CreateAllRoomsChanged();
            }

            ChangedEvent?.Invoke(this, args);
        }
    }
}
