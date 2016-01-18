using System;
using System.Drawing;
using System.IO;

namespace LynnaLab
{
    public class WorldMap : Map
    {
        // Map properties
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
                return 10;
            }
        }
        public override int RoomHeight {
            get {
                return 8;
            }
        }

        internal WorldMap(Project p, int i) : base(p, i)
        {
        }

        // IMap methods

        public override Room GetRoom(int x, int y, int floor=0) {
            return Project.GetIndexedDataType<Room>(Index*0x100+x+y*16);
        }
    }
}
