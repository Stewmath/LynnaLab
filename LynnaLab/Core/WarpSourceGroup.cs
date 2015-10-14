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
        public FileParser FileParser {
            get {
                return fileParser;
            }
        }

        WarpSourceData EndData {
            get {
                return warpSourceDataList[warpSourceDataList.Count-1];
            }
        }

        List<WarpSourceData> warpSourceDataList;
        FileParser fileParser;


        public WarpSourceGroup(Project p, int id) : base(p,id) {
            fileParser = Project.GetFileWithLabel("warpSourcesTable");
            Data d = fileParser.GetData("warpSourcesTable", id*2);
            string label = d.GetValue(0);

            warpSourceDataList = new List<WarpSourceData>();
            WarpSourceData warpData = fileParser.GetData(label) as WarpSourceData;
            while (warpData != null && warpData.WarpSourceType != WarpSourceType.WarpSourcesEnd) {
                warpSourceDataList.Add(warpData);
                warpData = warpData.NextData as WarpSourceData;
            }
            if (warpData != null)
                warpSourceDataList.Add(warpData); // WarpSourcesEnd
        }

        // Returns a new list of all the WarpSourceData objects for the given map.
        public List<WarpSourceData> GetMapWarpSourceData(int map) {
            List<WarpSourceData> newList = new List<WarpSourceData>();

            foreach (WarpSourceData warp in warpSourceDataList) {
                if (warp.Map == map)
                    newList.Add(warp);
            }

            return newList;
        }

        // Adds the given data to the end of the group and inserts the data
        // into the FileParser.
        public void AddWarpSourceData(WarpSourceData data) {
            if (warpSourceDataList.Contains(data))
                return;

            // Assumes the last element of warpSourceDataList is always the
            // m_WarpSourcesEnd command
            fileParser.InsertComponentBefore(EndData, data);
            warpSourceDataList.Insert(warpSourceDataList.Count-1, data);

            if (data.WarpSourceType == WarpSourceType.PointerWarp && data.PointerString == ".") {
                // Create a unique pointer after m_WarpSourcesEnd
                int nameIndex = 0;
                string name;
                do {
                    name = "customWarpSource" + nameIndex.ToString("d2");
                    nameIndex++;
                }
                while (Project.HasLabel(name));

                data.PointerString = name;

                Label newLabel = new Label(FileParser, name);
                // Insert label after m_WarpSourcesEnd
                FileParser.InsertComponentAfter(EndData, newLabel);

                // Create a blank PointedData to go after this label
                WarpSourceData pointedData = new WarpSourceData(Project,
                        WarpSourceData.WarpCommands[(int)WarpSourceType.PointedWarp],
                        WarpSourceData.DefaultValues[(int)WarpSourceType.PointedWarp],
                        FileParser,
                        new List<int>{-1});
                pointedData.Opcode = 0x80;
                FileParser.InsertComponentAfter(newLabel, pointedData);
            }
        }

        public void RemoveWarpSourceData(WarpSourceData data) {
            if (!warpSourceDataList.Contains(data))
                return;

            if (data.WarpSourceType == WarpSourceType.PointerWarp) {
                WarpSourceData pointedData = data.GetPointedWarp();
                // Delete label
                fileParser.RemoveFileComponent(fileParser.GetDataLabel(pointedData));
                // Delete after the label
                while (pointedData != null) {
                    WarpSourceData next = pointedData.GetNextWarp();
                    pointedData.FileParser.RemoveFileComponent(pointedData);
                    pointedData = next;
                }
            }

            data.FileParser.RemoveFileComponent(data);
            warpSourceDataList.Remove(data);
        }
    }
}
