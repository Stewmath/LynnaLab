namespace LynnaLib
{
    public class WorldMap : FloorPlan, ProjectDataInstantiator
    {
        // Stub state - we don't actually need to track anything through undo/redo. Only have this
        // due to requirements of the base class.
        class State : TransactionState
        {
            public required int group { get; init; }
            public required Season season { get; init; }
        }

        State state;

        private WorldMap(Project p, int group, Season season) : base(p, $"{group}_{season}")
        {
            if (!p.IsInConstructor)
                throw new Exception("Dungeons should not be loaded outside of the Project constructor.");

            state = new()
            {
                group = group,
                season = season,
            };
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private WorldMap(Project p, string id, TransactionState s)
            : base(p, id)
        {
            this.state = (State)s;

            if (!Project.GroupSeasonIsValid(state.group, state.season))
                throw new DeserializationException($"Bad group/season pair: {state.group}, {state.season}");
        }

        // TODO: Remove this - not necessary
        static ProjectDataType ProjectDataInstantiator.Instantiate(Project p, string id)
        {
            string[] split = id.Split("_");
            if (split.Length != 2)
                throw new DeserializationException();

            int group;
            Season season;
            if (!int.TryParse(split[0], out group))
                throw new DeserializationException();
            if (!Season.TryParse(split[1], out season))
                throw new DeserializationException();

            if (group < 0 || group >= p.NumGroups)
                throw new DeserializationException();
            if (!p.GroupSeasonIsValid(group, season))
                throw new DeserializationException();

            return new WorldMap(p, group, season);
        }


        // Map properties
        public override int MainGroup
        {
            get
            {
                return state.group;
            }
        }

        public override int MapWidth
        {
            get
            {
                return 16;
            }
        }
        public override int MapHeight
        {
            get
            {
                return 16;
            }
        }
        public override int RoomWidth
        {
            get
            {
                return GetRoom(0, 0).Width;
            }
        }
        public override int RoomHeight
        {
            get
            {
                return GetRoom(0, 0).Height;
            }
        }
        public override Season Season { get { return state.season; } }


        // Map methods

        public override Room GetRoom(int x, int y)
        {
            return Project.GetIndexedDataType<Room>(MainGroup * 0x100 + x + y * 16);
        }
        public override IEnumerable<(int x, int y)> GetRoomPositions(Room room)
        {
            if (room.Group != MainGroup)
                return new List<(int, int)>();
            return new List<(int, int)> { (room.Index % 16, (room.Index & 0xff) / 16) };
        }
        public override bool GetRoomPosition(Room room, out int x, out int y)
        {
            if (room.Group != MainGroup)
            {
                // Not in this group
                x = -1;
                y = -1;
                return false;
            }
            x = room.Index % 16;
            y = (room.Index % 0x100) / 16;
            return true;
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

        }
    }
}
