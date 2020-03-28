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

        internal WarpDestGroup(Project p, int id) : base(p,id) {
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
            WarpDestData newData = new WarpDestData(Project,
                    WarpDestData.WarpCommand,
                    null,
                    fileParser,
                    new List<string>{"\t"});

            DataValueReference.InitializeDataValues(newData, newData.GetValueReferences());

            newData.Transition = 1;

            newData.DestGroup = this;
            newData.DestIndex = warpDestDataList.Count;

            fileParser.InsertComponentAfter(warpDestDataList[warpDestDataList.Count-1], newData);
            warpDestDataList.Add(newData);

            return newData;
        }

        // Returns either an unused WarpDestData, or creates a new one if no unused ones exist.
        public WarpDestData GetNewOrUnusedDestData() {
            // Check if there's unused destination data already
            for (int i=0; i<GetNumWarpDests(); i++) {
                WarpDestData destData = GetWarpDest(i);
                if (destData.GetNumReferences() == 0) {
                    return GetWarpDest(i);
                }
            }
            // TODO: check if there's room to add data
            return AddDestData();
        }
    }
}
