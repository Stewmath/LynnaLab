using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLib
{
    // Represents a "Map", or layout of rooms. Subclass "WorldMap" represents a group of 256 rooms
    // laid out in a square, while the "Dungeon" subclass represents an 8x8 dungeon with a tweakable
    // layout.
    public abstract class Map
    {
        public Project Project { get; private set; }

        // This is called "MainGroup" instead of "Group" because the "Dungeon" subclass doesn't
        // really have a single canonical group. Most rooms in a dungeon belong to group 4 or 5,
        // except for sidescrolling rooms, which belong to group 6 or 7. The "main" group is
        // considered to be 4 or 5 in this case.
        public abstract int MainGroup { get; }

        public abstract int MapWidth { get; }
        public abstract int MapHeight { get; }
        public abstract int RoomWidth { get; }
        public abstract int RoomHeight { get; }
        public abstract int Season { get; }

        internal Map(Project p)
        {
            Project = p;
        }

        public abstract Room GetRoom(int x, int y, int floor = 0);
        public abstract bool GetRoomPosition(Room room, out int x, out int y, out int floor);

        public bool GetRoomPosition(Room room, out int x, out int y)
        {
            int f;
            return GetRoomPosition(room, out x, out y, out f);
        }

        public RoomLayout GetRoomLayout(int x, int y, int floor = 0)
        {
            return GetRoom(x, y, floor).GetLayout(Season);
        }
    }
}
