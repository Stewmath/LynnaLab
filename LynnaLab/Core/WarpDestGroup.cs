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
        FileParser fileParser;

        public WarpDestGroup(Project p, int id) : base(p,id) {
            fileParser = Project.GetFileWithLabel("warpDestTable");
            Data tmp = fileParser.GetData("warpDestTable", id*2);

            string label = tmp.GetValue(0);

            WarpDestData data = fileParser.GetData(label) as WarpDestData;

            warpDestDataList = new List<WarpDestData>();

            while (data != null) {
                data.DestGroup = this;
                data.DestIndex = warpDestDataList.Count;
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

        // Adds a new WarpDestData to the end of the group, returns the index
        public WarpDestData AddDestData() {
            WarpDestData newData = new WarpDestData(Project, WarpDestData.WarpCommand,
                    ValueReference.GetDefaultValues(WarpDestData.warpValueReferences),
                    fileParser, new List<int>{-1});

            newData.DestGroup = this;
            newData.DestIndex = warpDestDataList.Count;

            fileParser.InsertComponentAfter(warpDestDataList[warpDestDataList.Count-1], newData);
            warpDestDataList.Add(newData);

            return newData;
        }
    }
}
