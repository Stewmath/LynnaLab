using System;
using System.Drawing;
using System.IO;

namespace LynnaLab
{
	public class Dungeon : ProjectIndexedDataType
	{
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
        Data dataBase;

        public Dungeon(Project p, int i) : base(p, i) {
            FileParser dungeonDataFile = Project.GetFileWithLabel("dungeonDataTable");
            Data pointerData = dungeonDataFile.GetData("dungeonDataTable", Index*2);
            string label = pointerData.Values[0];
            dataBase = dungeonDataFile.GetData(label);
        }

        int GetDataIndex(int i) {
            Data d = dataBase;
            for (int j=0; j<i; j++) {
                d = d.Next;
            }
            return Project.EvalToInt(d.Values[0]);
        }

        public Room GetRoom(int floor, int x, int y) {
            int i = FirstLayoutIndex + floor;
            Stream file = Project.GetBinaryFile("dungeonLayouts/layout" + i.ToString("X2").ToLower() + ".bin");
            file.Position = y*8+x;
            int roomIndex = file.ReadByte();
            int group = GetDataIndex(0)-0xc9+4;
            Room room = Project.GetIndexedDataType<Room>(roomIndex + group*0x100);
            return room;
        }
    }
}
