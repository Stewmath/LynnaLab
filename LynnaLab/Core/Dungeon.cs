using System;
using System.Drawing;
using System.IO;

namespace LynnaLab
{
    public class Dungeon : Map
    {
        // The start of the data, at the "dungeonDataXX" label
        Data dataStart;

        public Data DataStart {
            get {
                return dataStart;
            }
        }
        public int FirstLayoutIndex {
            get {
                return GetDataIndex(2);
            }
            set {
                SetDataIndex(2, value);
            }
        }
        public int NumFloors {
            get {
                return GetDataIndex(3);
            }
            set {
                SetDataIndex(3, value);
            }
        }

        // Map properties

        public override int Group {
            get {
                return GetDataIndex(0)-Project.EvalToInt(">wGroup4Flags")+4;
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
                return GetRoom(0,0).Width;
            }
        }
        public override int RoomHeight {
            get {
                return GetRoom(0,0).Height;
            }
        }

        internal Dungeon(Project p, int i) : base(p, i) {
            FileParser dungeonDataFile = Project.GetFileWithLabel("dungeonDataTable");
            Data pointerData = dungeonDataFile.GetData("dungeonDataTable", Index*2);
            string label = pointerData.GetValue(0);
            dataStart = dungeonDataFile.GetData(label);
        }

        int GetDataIndex(int i) {
            Data d = dataStart;
            for (int j=0; j<i; j++) {
                d = d.NextData;
            }
            return Project.EvalToInt(d.GetValue(0));
        }

        void SetDataIndex(int i, int val) {
            Data d = dataStart;
            for (int j=0; j<i; j++) {
                d = d.NextData;
            }
            d.SetByteValue(0, (byte)val);
        }

        public void SetRoom(int x, int y, int floor, int room) {
            int i = FirstLayoutIndex + floor;
            Stream file = Project.GetBinaryFile("rooms/dungeonLayouts.bin");
            file.Position = i*64+y*8+x;
            file.WriteByte((byte)(room&0xff));
        }

        // Map methods

        public override Room GetRoom(int x, int y, int floor=0) {
            int i = FirstLayoutIndex + floor;
            Stream file = Project.GetBinaryFile("rooms/dungeonLayouts.bin");
            file.Position = i*64+y*8+x;
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
