using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class WarpSourceGroup : ProjectIndexedDataType
    {
        public int Group {
            get {
                return Index;
            }
        }

        List<WarpSourceData> warpSourceDataList;

        public WarpSourceGroup(Project p, int id) : base(p,id) {
            FileParser parser = Project.GetFileWithLabel("warpSourcesTable");
            Data d = parser.GetData("warpSourcesTable", id*2);
            string label = d.GetValue(0);

            warpSourceDataList = new List<WarpSourceData>();
            WarpSourceData warpData = parser.GetData(label) as WarpSourceData;
            while (warpData != null && warpData.WarpSourceType != WarpSourceType.WarpSourcesEnd) {
                warpSourceDataList.Add(warpData);
                warpData = warpData.NextData as WarpSourceData;
            }
            if (warpData != null)
                warpSourceDataList.Add(warpData); // WarpSourcesEnd
        }

        // Returns a new list of all the WarpDataSource objects for the given map.
        public List<WarpSourceData> GetMapWarpSourceData(int map) {
            List<WarpSourceData> newList = new List<WarpSourceData>();

            foreach (WarpSourceData warp in warpSourceDataList) {
                if (warp.Map == map)
                    newList.Add(warp);
            }

            return newList;
        }

        public void AddWarpSourceData(WarpSourceData data) {
            throw new NotImplementedException();
        }
        public void RemoveWarpSourceData(WarpSourceData data) {
            throw new NotImplementedException();
        }
    }
}
