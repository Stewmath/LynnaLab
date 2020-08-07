using System;
using System.Drawing;
using System.IO;

namespace LynnaLab
{
    public class Dungeon : Map
    {
        // The start of the data, at the "dungeonDataXX" label
        Data dataStart;

        // roomsUsed[i] = # of times room "i" is used in this dungeon
        int[] roomsUsed = new int[256];


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

        public override int MainGroup {
            get {
                return GetDataIndex(0)-Project.EvalToInt(">wGroup4Flags")+4;
            }
        }

        // I don't know why, but sidescrolling rooms use groups 6/7 instead of 4/5
        public int SidescrollGroup {
            get {
                return MainGroup + 2;
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

            DetermineRoomsUsed();
        }

        void DetermineRoomsUsed() {
            for (int f=0; f<NumFloors; f++) {
                for (int x=0; x<MapWidth; x++) {
                    for (int y=0; y<MapHeight; y++) {
                        roomsUsed[GetRoom(x, y, f).Index & 0xff]++;
                    }
                }
            }
        }

        int GetDataIndex(int i) {
            Data d = dataStart;
            for (int j=0; j<i; j++) {
                d = d.NextData;
            }
            return Project.EvalToInt(d.GetValue(0));
        }

        Data GetFloorLayoutData(int floor) {
            return Project.GetData("dungeonLayoutData", (FirstLayoutIndex + floor) * 64);
        }

        void SetDataIndex(int i, int val) {
            Data d = dataStart;
            for (int j=0; j<i; j++) {
                d = d.NextData;
            }
            d.SetByteValue(0, (byte)val);
        }

        public void SetRoom(int x, int y, int floor, int room) {
            int pos = y * 8 + x;
            if (pos >= 0x40)
                throw new ArgumentException(string.Format("Arguments {0:X},{1:X} to 'SetRoom' too high.", x, y));
            if (floor >= NumFloors)
                throw new ArgumentException(string.Format("Floor {0} too high.", floor));

            Data d = GetFloorLayoutData(floor).GetDataAtOffset(y * 8 + x);

            roomsUsed[d.GetIntValue(0)]--;
            d.SetByteValue(0, (byte)room);

            roomsUsed[(byte)room]++;
        }

        public bool RoomUsed(int roomIndex) {
            int group = roomIndex >> 8;
            if (group != MainGroup && group != SidescrollGroup)
                return false;
            return roomsUsed[roomIndex & 0xff] > 0;
        }

        // Map methods

        public override Room GetRoom(int x, int y, int floor=0) {
            int pos = y * 8 + x;
            if (pos >= 0x40)
                throw new ArgumentException(string.Format("Arguments {0:X},{1:X} to 'GetRoom' too high.", x, y));
            if (floor >= NumFloors)
                throw new ArgumentException(string.Format("Floor {0} too high.", floor));

            int roomIndex = GetFloorLayoutData(floor).GetDataAtOffset(y * 8 + x).GetIntValue(0);
            Room room = Project.GetIndexedDataType<Room>(roomIndex + MainGroup*0x100);

            // Change the group if the room is sidescrolling. As a result, the group number of
            // sidescrolling rooms in a dungeon will be different from other rooms, which will look
            // weird, but that's the way it works apparently...
            if (room.Tileset.SidescrollFlag)
                room = Project.GetIndexedDataType<Room>(roomIndex + SidescrollGroup*0x100);

            return room;
        }

        public override bool GetRoomPosition(Room room, out int x, out int y, out int floor) {
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
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public override bool GetRoomPosition(Room room, out int x, out int y) {
            int f;
            return GetRoomPosition(room, out x, out y, out f);
        }
    }
}
