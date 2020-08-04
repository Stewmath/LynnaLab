using System;
using System.Drawing;
using System.IO;

namespace LynnaLab
{
    public class WorldMap : Map
    {
        // Map properties
        public override int MainGroup {
            get {
                return Index;
            }
        }
        public override int MapWidth {
            get {
                return 16;
            }
        }
        public override int MapHeight {
            get {
                return 16;
            }
        }
        public override int RoomWidth {
            get {
                return GetRoom(0,0).Width;
            }
        }
        public override int RoomHeight {
            get {
                return GetRoom(0,0).Height;
            }
        }

        internal WorldMap(Project p, int i) : base(p, i)
        {
        }

        // Map methods

        public override Room GetRoom(int x, int y, int floor=0) {
            return Project.GetIndexedDataType<Room>(Index*0x100+x+y*16);
        }
        public override bool GetRoomPosition(Room room, out int x, out int y) {
            int f;
            return GetRoomPosition(room, out x, out y, out f);
        }
        public override bool GetRoomPosition(Room room, out int x, out int y, out int floor) {
            if (room.Index/0x100 != Index) {
                // Not in this group
                x = -1;
                y = -1;
                floor = -1;
                return false;
            }
            x = room.Index%16;
            y = (room.Index%0x100)/16;
            floor = 0;
            return true;
        }
    }
}
