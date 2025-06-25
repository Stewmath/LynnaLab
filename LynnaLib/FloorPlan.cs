namespace LynnaLib
{
    /// <summary>
    /// A floor plan is a layout of rooms; either an overworld map (WorldMap) or a dungeon floor
    /// (Dungeon.Floor). This only provides read access, but in the case of dungeon floors the
    /// layout can be modified through that subclass.
    /// </summary>
    public abstract class FloorPlan : TrackedProjectData
    {
        /// <summary>
        /// This is called "MainGroup" instead of "Group" because the dungeons don't really have a
        /// single canonical group. Most rooms in a dungeon belong to group 4 or 5, except for
        /// sidescrolling rooms, which belong to group 6 or 7. The "main" group is considered to be 4
        /// or 5 in this case.
        /// </summary>
        public abstract int MainGroup { get; }

        public abstract int MapWidth { get; }
        public abstract int MapHeight { get; }
        public abstract int RoomWidth { get; }
        public abstract int RoomHeight { get; }
        public abstract Season Season { get; }

        protected FloorPlan(Project p, string id) : base(p, id)
        {
        }

        /// <summary>
        /// Gets the Room at the given position.
        /// </summary>
        public abstract Room GetRoom(int x, int y);

        /// <summary>
        /// Get all locations of a room on the map. Returns an empty list if it's not on the map.
        /// </summary>
        public abstract IEnumerable<(int x, int y)> GetRoomPositions(Room room);

        /// <summary>
        /// Gets just one location of a room on a map. Returns false if it's not on the map.
        /// </summary>
        public abstract bool GetRoomPosition(Room room, out int x, out int y);

        /// <summary>
        /// Gets the RoomLayout at the given position.
        /// </summary>
        public RoomLayout GetRoomLayout(int x, int y)
        {
            return GetRoom(x, y).GetLayout(Season);
        }
    }
}
