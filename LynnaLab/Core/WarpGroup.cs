using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class WarpGroup : ProjectIndexedDataType
    {
        public int Group {
            get {
                return 0;
            }
        }

        public WarpGroup(Project p, int id) : base(p,id) {
            FileParser parser = Project.GetFileWithLabel("warpSourcesTable");
            Data d = parser.GetData("warpSourcesTable", id*2);
            string label = d.GetValue(0);

            WarpData warpData = parser.GetData(label) as WarpData;
        }

        public List<WarpData> GetMapWarpData(int map) {
        }

        public void AddWarpData(WarpData data) {
            throw new NotImplementedException();
        }
        public void RemoveWarpData(WarpData data) {
            throw new NotImplementedException();
        }
    }
}
