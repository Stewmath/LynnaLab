namespace LynnaLib
{
    /// <summary>
    /// Invoked when something changes the layout of rooms, or number of floors, in a dungeon.
    /// </summary>
    public class DungeonChangedEventArgs
    {
        // Exactly one of the following will be set.
        public bool AllRoomsChanged { get; init; } // True if all room assignments are potentially modified
        public bool FloorsChanged { get; init; } // True if number of floors has potentially changed
        public bool RoomPosValid { get; init; } // True if a single room is modified.
        public Dungeon.Floor SingleFloorChanged { get; init; } // A full floor that must be redrawn, or null

        // If RoomPosValid is true, this is the position in the grid of the changed room.
        public (int x, int y, Dungeon.Floor floorPlan) RoomPos { get; init; }

        private DungeonChangedEventArgs() {}

        public static DungeonChangedEventArgs CreateFloorsChanged()
        {
            return new DungeonChangedEventArgs() { FloorsChanged = true };
        }
        public static DungeonChangedEventArgs CreateAllRoomsChanged()
        {
            return new DungeonChangedEventArgs() { AllRoomsChanged = true };
        }
        public static DungeonChangedEventArgs CreateRoomChanged(Dungeon.Floor floor, int x, int y)
        {
            return new DungeonChangedEventArgs() { RoomPosValid = true, RoomPos = (x, y, floor) };
        }
        public static DungeonChangedEventArgs CreateSingleFloorChanged(Dungeon.Floor floor)
        {
            return new DungeonChangedEventArgs() { SingleFloorChanged = floor };
        }
    }

    /// <summary>
    /// Represents a dungeon, which is really just an organized layout of rooms. Some "dungeons" are
    /// just collections of miscellaneous large rooms (like Ambi's Palace).
    ///
    /// You must access rooms through a Dungeon.Floor object (see GetFloor()).
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
    public class Dungeon : TrackedProjectData, IndexedProjectDataInstantiator
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        private Dungeon(Project p, int i)
            : base(p, i.ToString())
        {
            if (!p.IsInConstructor)
                throw new Exception("Dungeons should not be loaded outside of the Project constructor.");

            this.index = i;
            this.state = new();

            DetermineGroup();

            int numFloors = DataStart.GetIntValue(3);

            for (int f=0; f<numFloors; f++)
            {
                Floor floor = new Floor(this, GenFloorID(), GetFloorLayoutData(f));
                state.floors.Add(new(floor));
            }
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
            public int _mainGroup;
            public int floorIDCounter;
            public List<InstanceResolver<Floor>> floors = new();
        }

        readonly int index;

        State state;
        Data _dataStart;
        ValueReferenceGroup _vrg;

        // ================================================================================
        // Properties
        // ================================================================================

        public string TransactionIdentifier { get { return $"dungeon-{Index:X2}"; } }

        public IEnumerable<Floor> FloorPlans
        {
            get
            {
                return state.floors.Select((f) => f.Instance);
            }
        }

        public int MainGroup
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
        public event EventHandler<DungeonChangedEventArgs> DungeonChangedEvent;



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
                Debug.Assert(state.floors.Count == GetDataIndex(3));
                return state.floors.Count;
            }
        }

        public int Index { get { return index; } }

        // ================================================================================
        // Public methods
        // ================================================================================

        public bool RoomUsed(int roomIndex)
        {
            int group = roomIndex >> 8;
            if (group != MainGroup && group != SidescrollGroup)
                return false;
            foreach (Floor f in FloorPlans)
            {
                if (f.RoomUsed(roomIndex))
                    return true;
            }
            return false;
        }

        public Dungeon.Floor GetFloor(int floor)
        {
            if (floor < 0 || floor >= NumFloors)
                throw new Exception($"Dungeon {Index}: Invalid floor: {floor}");
            return state.floors[floor];
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

            SetDataIndex(3, NumFloors+1);
            state.floors.Insert(floorIndex, new(new Floor(this, GenFloorID(), GetFloorLayoutData(floorIndex))));

            DungeonChangedEvent?.Invoke(this, DungeonChangedEventArgs.CreateFloorsChanged());

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

            SetDataIndex(3, NumFloors-1);
            state.floors.RemoveAt(floorIndex);

            DungeonChangedEvent?.Invoke(this, DungeonChangedEventArgs.CreateFloorsChanged());

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
        bool DetermineGroup()
        {
            int g = GetDataIndex(0) - Project.Eval(">wGroup4RoomFlags") + 4;
            if (state._mainGroup != g)
            {
                state._mainGroup = g;
                return true;
            }
            return false;
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

            if (DetermineGroup())
            {
                foreach (Floor f in FloorPlans)
                    f.DetermineRoomsUsed();

                DungeonChangedEvent?.Invoke(this, DungeonChangedEventArgs.CreateAllRoomsChanged());
            }
        }

        /// <summary>
        /// Generate a UniqueID for a Dungeon.Floor. Note that the number used in the second part is
        /// not necessarily equal to the floor number, it's just a counter of the number of floors
        /// that have been instantiated since LynnaLab was booted up.
        /// </summary>
        string GenFloorID()
        {
            return $"{Index}-{state.floorIDCounter++}";
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
            bool floorsChanged = !old.floors.SequenceEqual(state.floors);

            if (floorsChanged)
            {
                DungeonChangedEventArgs args = DungeonChangedEventArgs.CreateFloorsChanged();
                DungeonChangedEvent?.Invoke(this, args);
            }

            // If any rooms have changed, the state handler in the Floor class will send an event.
            // NOTE: Potentially, Dungeon and Dungeon.Floor could both send events on the same undo.
            // Whether this would cause problems depends on what the clients are doing. But it could
            // be a concern that a Floor which has been deleted from its Dungeon may invoke events
            // before the listener knows about that.
        }

        // ================================================================================
        // Floor class
        // ================================================================================

        /// <summary>
        /// Represents a single floor of a dungeon.
        ///
        /// Anything that uses this should subscribe to Dungeon.DungeonChangedEvent, check for the
        /// "FloorsChanged" flag in the event arguments, and stop using any floor that returns
        /// "WasDeleted() == true".
        /// </summary>
        public class Floor : FloorPlan
        {
            public Floor(Dungeon d, string id, Data floorLayoutData)
                : base(d.Project, id)
            {
                this.state = new();
                this.state._dungeon = new(d);
                this.state._floorLayoutData = new(floorLayoutData);
                DetermineRoomsUsed(fromConstructor: true);
            }

            /// <summary>
            /// State-based constructor, for network transfer (located via reflection)
            /// </summary>
            private Floor(Project p, string id, TransactionState s)
                : base(p, id)
            {
                this.state = (State)s;
            }

            // ================================================================================
            // Variables
            // ================================================================================

            class State : TransactionState
            {
                // roomsLookup[i] = a list of positions in the map where room "i" is used.
                public List<(int x, int y)>[] roomLookup;

                // These should never change
                public InstanceResolver<Data> _floorLayoutData;
                public InstanceResolver<Dungeon> _dungeon;
            }

            State state;

            // ================================================================================
            // Properties
            // ================================================================================

            public Dungeon Dungeon { get { return state._dungeon.Instance; } }

            /// <summary>
            /// Group management is confusing. Most dungeon rooms use groups 4/5, but sidescrolling rooms
            /// use groups 6/7 instead. So there is no one single "group number" that any given dungeon
            /// uses, there are actually two: "MainGroup" and "SidescrollGroup".
            ///
            /// This means there is a "fake" version of each dungeon room, the one with the wrong group
            /// number. It can be recognized by the fact that warp data is usually missing. LynnaLab
            /// tries to hide it by auto-adjusting the group number of the room to show.
            /// </summary>
            public override int MainGroup { get { return Dungeon.MainGroup; } }

            public int SidescrollGroup { get { return Dungeon.SidescrollGroup; } }

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

            Data FloorLayoutData { get { return state._floorLayoutData.Instance; }}

            // ================================================================================
            // Methods
            // ================================================================================

            /// <summary>
            /// If this returns true, this floor was deleted from the dungeon, and should no longer be
            /// used for anything.
            /// </summary>
            public bool WasDeleted()
            {
                return !Dungeon.FloorPlans.Contains(this);
            }

            public void SetRoom(int x, int y, int room)
            {
                Project.BeginTransaction($"Change dungeon room#d{Identifier}x{x}y{y}", true);
                Project.TransactionManager.CaptureInitialState<State>(this);

                int pos = y * 8 + x;
                if (pos >= 0x40)
                    throw new ArgumentException(string.Format("Arguments {0:X},{1:X} to 'SetRoom' too high.", x, y));

                Data d = FloorLayoutData.GetDataAtOffset(y * 8 + x);
                int oldRoom = d.GetIntValue(0);

                d.SetByteValue(0, (byte)room);

                if (!state.roomLookup[oldRoom].Remove((x, y)))
                    throw new Exception("Internal error: Dungeon room lookup table invalid");
                state.roomLookup[(byte)room].Add((x, y));

                DetermineRoomsUsed();

                Dungeon.DungeonChangedEvent?.Invoke(Dungeon, DungeonChangedEventArgs.CreateRoomChanged(this, x, y));

                Project.EndTransaction();
            }

            public bool RoomUsed(int roomIndex)
            {
                int group = roomIndex >> 8;
                if (group != MainGroup && group != SidescrollGroup)
                    return false;
                return state.roomLookup[roomIndex & 0xff].Count > 0;
            }

            // FloorPlan methods

            public override Room GetRoom(int x, int y)
            {
                int pos = y * 8 + x;
                if (pos >= 0x40)
                    throw new ArgumentException(string.Format("Arguments {0:X},{1:X} to 'GetRoom' too high.", x, y));

                int roomIndex = FloorLayoutData.GetDataAtOffset(y * 8 + x).GetIntValue(0);
                Room room = Project.GetIndexedDataType<Room>(roomIndex + MainGroup * 0x100);

                // Change the group if the room is sidescrolling. As a result, the group number of
                // sidescrolling rooms in a dungeon will be different from other rooms, which will look
                // weird, but that's the way it works apparently...
                if (room.GetTileset(Season.None).SidescrollFlag)
                    room = Project.GetIndexedDataType<Room>(roomIndex + SidescrollGroup * 0x100);

                return room;
            }

            public override IEnumerable<(int x, int y)> GetRoomPositions(Room room)
            {
                if (room.Group != MainGroup && room.Group != SidescrollGroup)
                    return new List<(int, int)>();
                return state.roomLookup[room.Index & 0xff];
            }

            public override bool GetRoomPosition(Room room, out int x, out int y)
            {
                x = -1;
                y = -1;

                if (room.Group != MainGroup && room.Group != SidescrollGroup)
                    return false;
                if (state.roomLookup[room.Index & 0xff].Count == 0)
                    return false;

                var tup = state.roomLookup[room.Index & 0xff][0];
                x = tup.x;
                y = tup.y;

                return true;
            }

            /// <summary>
            /// Determine the current floor index within the parent dungneon. This is not fixed, as
            /// floors can be added and removed.
            /// </summary>
            public int GetFloorIndex()
            {
                int i = 0;
                foreach (Floor f in Dungeon.FloorPlans)
                {
                    if (f == this)
                        return i;
                    i++;
                }
                throw new Exception("GetFloorIndex: This floor isn't used in its parent dungeon?");
            }

            // ================================================================================
            // Internal methods
            // ================================================================================

            /// <summary>
            /// Recalculate room lookup table.
            /// </summary>
            internal void DetermineRoomsUsed(bool fromConstructor = false)
            {
                if (!fromConstructor)
                    Project.TransactionManager.CaptureInitialState<State>(this);

                state.roomLookup = new List<(int, int)>[256];
                for (int i = 0; i < 256; i++)
                    state.roomLookup[i] = new List<(int, int)>();
                for (int x = 0; x < MapWidth; x++)
                {
                    for (int y = 0; y < MapHeight; y++)
                    {
                        int room = GetRoom(x, y).Index & 0xff;
                        state.roomLookup[room].Add((x, y));
                    }
                }
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
                // Assume all rooms are potentially changed (too lazy to implement comparison)
                var args = DungeonChangedEventArgs.CreateSingleFloorChanged(this);
                Dungeon.DungeonChangedEvent?.Invoke(this, args);
            }
        }
    }
}
