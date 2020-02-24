using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    // Reads/writes warp source data per group. Can search through all of a group's warp data to get
    // only data for a particular room index (a list of "WarpSourceData" instances).
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


        internal WarpSourceGroup(Project p, int id) : base(p,id) {
            RegenWarpSourceDataList();
        }

        void RegenWarpSourceDataList() {
            fileParser = Project.GetFileWithLabel("warpSourcesTable");
            Data d = fileParser.GetData("warpSourcesTable", Index*2);

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

        // Returns a new list of all the WarpSourceData objects for the given room.
        // In the future it would probably better if this abstracts things such that it only returns
        // "StandardWarps" and "PointedWarps", and not "PointerWarps". But I ended up implementing
        // that abstraction in the UI section instead.
        public List<WarpSourceData> GetMapWarpSourceData(int map) {
            List<WarpSourceData> newList = new List<WarpSourceData>();

            foreach (WarpSourceData warp in warpSourceDataList) {
                if (warp.Map == map)
                    newList.Add(warp);
            }

            return newList;
        }

        // Returns the (hopefully unique) PointerWarp for a given map, or null if not found.
        public WarpSourceData GetMapPointerWarp(int map) {
            foreach (WarpSourceData warp in warpSourceDataList) {
                if (warp.WarpSourceType == WarpSourceType.PointerWarp && warp.Map == map)
                    return warp;
            }
            return null;
        }

        // Adds the given data to the group and inserts the data into the FileParser.
        // If "data" is of type "WarpSourceType.StandardWarp", this gets inserted before any
        // "PointerWarp" if such a warp exists.
        // If "data" is of type "WarpSourceType.PointedWarp", this gets inserted at the end of the
        // "PointerWarp" list; it automatically creates a "PointerWarp" if necessary.
        // For any other warp type, this throws an ArgumentException.
        // The "room" argument is necessary for PointedWarps (which don't have a field for it).
        public void AddWarpSourceData(WarpSourceData data, int room) {
            if (warpSourceDataList.Contains(data)) // (doesn't check pointed warps...)
                throw new ArgumentException("Argument already exists in the warp source group.");

            // This code locating the "pointerWarp" assumes that the data is well-formed (if
            // a PointerWarp exists, it's the last entry for that room)
            WarpSourceData pointerWarp = GetMapPointerWarp(room);

            if (data.WarpSourceType == WarpSourceType.StandardWarp) {
                Data insertPosition;
                if (pointerWarp != null) {
                    insertPosition = pointerWarp;
                }
                else {
                    // Assumes the last element of warpSourceDataList is always the
                    // "m_WarpSourcesEnd" command
                    insertPosition = EndData;
                }

                FileParser.InsertComponentBefore(insertPosition, data);
            }
            else if (data.WarpSourceType == WarpSourceType.PointedWarp) {
                FileComponent insertPosition;
                if (pointerWarp == null) {
                    pointerWarp = new WarpSourceData(Project,
                            command: WarpSourceData.WarpCommands[(int)WarpSourceType.PointerWarp],
                            values: WarpSourceData.DefaultValues[(int)WarpSourceType.PointerWarp],
                            parser: FileParser,
                            spacing: new List<string>{"\t","  "});
                    pointerWarp.Map = room;

                    // Create a unique pointer after m_WarpSourcesEnd
                    string name = Project.GetUniqueLabelName(
                            String.Format("group{0}Room{1:x2}WarpSources", room>>8, room&0xff));
                    pointerWarp.PointerString = name;
                    Label newLabel = new Label(FileParser, name);

                    // Insert PointerWarp before m_WarpSourcesEnd
                    FileParser.InsertComponentBefore(EndData, pointerWarp);

                    // Insert label after m_WarpSourcesEnd
                    FileParser.InsertComponentAfter(EndData, newLabel);
                    insertPosition = newLabel;
                }
                else // Already exists, jump to the end of the pointed warp list
                    insertPosition = pointerWarp.TraversePointedChain(pointerWarp.GetPointedChainLength()-1);

                FileParser.InsertComponentAfter(insertPosition, data);
            }
            else {
                throw new ArgumentException("Can't add this warp source type to the warp source group.");
            }

            RegenWarpSourceDataList();
        }

        // Similar to above, this only supports removing StandardWarps or PointedWarps (the rest is
        // handled automatically).
        public void RemoveWarpSourceData(WarpSourceData data, int room) {
            if (data.WarpSourceType == WarpSourceType.StandardWarp) {
                data.FileParser.RemoveFileComponent(data);
            }
            else if (data.WarpSourceType == WarpSourceType.PointedWarp) {
                WarpSourceData pointerWarp = GetMapPointerWarp(room);

                if (pointerWarp == null)
                    throw new ArgumentException("WarpSourceGroup doesn't contain the data to remove?");

                if (pointerWarp.GetPointedChainLength() == 1) {
                    // Delete label & PointerWarp (do this before deleting its last PointedWarp,
                    // otherwise it'll start reading subsequent data and get an incorrect count)
                    fileParser.RemoveFileComponent(Project.GetLabel(pointerWarp.PointerString));
                    fileParser.RemoveFileComponent(pointerWarp);
                }

                data.FileParser.RemoveFileComponent(data);
            }
            else
                throw new ArgumentException("RemoveWarpSourceData doesn't support this warp source type.");

            RegenWarpSourceDataList();
        }
    }
}
