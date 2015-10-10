using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class WarpDestGroup : ProjectIndexedDataType
    {
        public int Group {
            get {
                return Index;
            }
        }

        List<WarpDestData> warpDestDataList;

        public WarpDestGroup(Project p, int id) : base(p,id) {
            FileParser parser = Project.GetFileWithLabel("warpDestTable");
            Data tmp = parser.GetData("warpDestTable", id*2);

            string label = tmp.GetValue(0);

            WarpDestData data = parser.GetData(label) as WarpDestData;

            warpDestDataList = new List<WarpDestData>();

            while (data != null) {
                warpDestDataList.Add(data);

                FileComponent component = data.Next;
                data = null;
                while (component != null) {
                    if (component is Label) {
                        data = null;
                        break;
                    }
                    else if (component is Data) {
                        data = component as WarpDestData;
                        break;
                    }
                    component = component.Next;
                }
            }
        }

        public int GetNumWarpDests() {
            return warpDestDataList.Count;
        }

        public WarpDestData GetWarpDest(int index) {
            return warpDestDataList[index];
        }
    }
}
