using System;
using System.Drawing;
using System.IO;

namespace LynnaLab
{
    public class Dungeon : Map
    {
        Data dataBase;

        public int FirstLayoutIndex {
            get {
                return GetDataIndex(2);
            }
        }
        public int NumFloors {
            get {
                return GetDataIndex(3);
            }
        }

        // Map properties

        public override int Group {
            get {
                return GetDataIndex(0)-0xc9+4;
            }
        }

        public override int MapWidth {
            get {
                return 8;
            }
        }
        public override int MapHeight {
            get {
                return 8;
            }
        }
        public override int RoomWidth {
            get {
                return 15;
            }
        }
        public override int RoomHeight {
            get {
                return 11;
            }
        }

        internal Dungeon(Project p, int i) : base(p, i) {
            FileParser dungeonDataFile = Project.GetFileWithLabel("dungeonDataTable");
            Data pointerData = dungeonDataFile.GetData("dungeonDataTable", Index*2);
            string label = pointerData.GetValue(0);
            dataBase = dungeonDataFile.GetData(label);
        }

        int GetDataIndex(int i) {
            Data d = dataBase;
            for (int j=0; j<i; j++) {
                d = d.NextData;
            }
            return Project.EvalToInt(d.GetValue(0));
        }

        // Map methods

        public override Room GetRoom(int x, int y, int floor=0) {
            int i = FirstLayoutIndex + floor;
            Stream file = Project.GetBinaryFile("dungeonLayouts/layout" + i.ToString("X2").ToLower() + ".bin");
            file.Position = y*8+x;
            int roomIndex = file.ReadByte();
            int group = GetDataIndex(0)-0xc9+4;
            Room room = Project.GetIndexedDataType<Room>(roomIndex + group*0x100);
            return room;
        }

        public override void GetRoomPosition(Room room, out int x, out int y, out int floor) {
            x = -1;
            y = -1;
            floor = -1;

            for (int f=0;f<NumFloors;f++) {
                for (int j=0;j<MapHeight;j++) {
                    for (int i=0;i<MapWidth;i++) {
                        if (GetRoom(i,j,f) == room) {
                            x = i;
                            y = j;
                            floor = f;
                            return;
                        }
                    }
                }
            }
        }

        public override void GetRoomPosition(Room room, out int x, out int y) {
            int f;
            GetRoomPosition(room, out x, out y, out f);
        }
    }
}
