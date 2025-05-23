using System;
using System.IO;

namespace LynnaLib
{
    public class WorldMap : Map
    {
        readonly int group;
        readonly Season season;

        internal WorldMap(Project p, int group, Season season) : base(p)
        {
            this.group = group;
            this.season = season;
        }


        // Map properties
        public override int MainGroup
        {
            get
            {
                return group;
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
        public override Season Season { get { return season; } }


        // Map methods

        public override Room GetRoom(int x, int y, int floor = 0)
        {
            return Project.GetIndexedDataType<Room>(group * 0x100 + x + y * 16);
        }
        public override IEnumerable<(int x, int y, int floor)> GetRoomPositions(Room room)
        {
            if (room.Group != group)
                return new List<(int, int, int)>();
            return new List<(int, int, int)> { (room.Index % 16, (room.Index & 0xff) / 16, 0 ) };
        }
        public override bool GetRoomPosition(Room room, out int x, out int y, out int floor)
        {
            if (room.Group != group)
            {
                // Not in this group
                x = -1;
                y = -1;
                floor = -1;
                return false;
            }
            x = room.Index % 16;
            y = (room.Index % 0x100) / 16;
            floor = 0;
            return true;
        }
    }
}
