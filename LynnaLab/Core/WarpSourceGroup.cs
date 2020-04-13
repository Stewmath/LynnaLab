using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LynnaLab
{
    // Reads/writes warp source data per room. Searches through all of a group's warp data to get
    // only data for a particular room index (a list of "WarpSourceData" instances).
    //
    // Assertions:
    // - A full-screen warp is not used with "specific-position" warps. (Whichever comes first takes
    //   precedence.)
    // - If a "PointerWarp" referencing "PointedWarps" is used, no warp data for the room is defined
    //   after that point. (The game would not see it.)
    // - Each warp type is only used in its appropriate location (StandardWarp and PointerWarp at
    //   the top level, PointedWarp only as the data pointed to by a PointerWarp).
    public class WarpSourceGroup : ProjectIndexedDataType
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        LockableEvent<EventArgs> ModifiedEvent = new LockableEvent<EventArgs>();


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

        WarpSourceData EndData { get; set; }
        WarpSourceData PointerWarp { get; set; }

        int Map {
            get { return Index & 0xff; }
        }

        List<WarpSourceData> warpSourceDataList;
        FileParser fileParser;


        internal WarpSourceGroup(Project p, int id) : base(p,id) {
            RegenWarpSourceDataList();

            foreach (WarpSourceData data in warpSourceDataList)
                data.AddModifiedEventHandler(OnDataModified);
        }

        void RegenWarpSourceDataList() {
            fileParser = Project.GetFileWithLabel("warpSourcesTable");
            Data d = fileParser.GetData("warpSourcesTable", (Index>>8)*2);

            string label = d.GetValue(0);

            warpSourceDataList = new List<WarpSourceData>();
            WarpSourceData warp = fileParser.GetData(label) as WarpSourceData;
            while (warp != null && warp.WarpSourceType != WarpSourceType.End) {
                if (Map == warp.Map) {
                    if (warp.WarpSourceType == WarpSourceType.Pointer) {
                        if (PointerWarp != null) {
                            throw new AssemblyErrorException(string.Format(
                                        "Room {0:3} has multiple 'Pointer' warp sources!", Index));
                        }
                        PointerWarp = warp;
                        WarpSourceData pWarp = warp.GetPointedWarp();
                        while (pWarp != null) {
                            if (pWarp.WarpSourceType == WarpSourceType.End)
                                break;
                            if (pWarp.WarpSourceType != WarpSourceType.Pointed)
                                throw new AssemblyErrorException(string.Format(
                                            "Unexpected warp type '{0}' in {1}!",
                                            pWarp.WarpSourceType, warp.PointerString));
                            warpSourceDataList.Add(pWarp);
                            pWarp = pWarp.GetNextWarp();
                        }
                    }
                    else if (warp.WarpSourceType == WarpSourceType.Standard)
                        warpSourceDataList.Add(warp);
                    else {
                        throw new AssemblyErrorException(string.Format(
                                    "Unexpected warp type '{0}' for room {1:X3}!",
                                    warp.WarpSourceType, Index));
                    }
                }
                warp = warp.NextData as WarpSourceData;
            }
            if (warp != null)
                EndData = warp;
            else {
                throw new AssemblyErrorException(string.Format(
                            "Warp source data for room {0:X3} doesn't have an end?", Index));
            }
        }

        // Returns a new list of all the WarpSourceData objects for the given room.
        // This does not return "PointerWarp" types. It instead traverses them and returns the
        // resulting PointedWarps.
        public ReadOnlyCollection<WarpSourceData> GetWarpSources() {
            return new ReadOnlyCollection<WarpSourceData>(warpSourceDataList);
        }

        public WarpSourceData GetWarpSource(int index) {
            return warpSourceDataList[index];
        }

        // Adds the given data to the group and inserts the data into the FileParser.
        // If "data" is of type "WarpSourceType.StandardWarp", this gets inserted before any
        // "PointerWarp" if such a warp exists.
        // If "data" is of type "WarpSourceType.PointedWarp", this gets inserted at the end of the
        // "PointerWarp" list; it automatically creates a "PointerWarp" if necessary.
        // For any other warp type, this throws an ArgumentException.
        // The "room" argument is necessary for PointedWarps (which don't have a field for it).
        public void AddWarpSource(WarpSourceData data) {
            if (warpSourceDataList.Contains(data)) // (doesn't check pointed warps...)
                throw new ArgumentException("Argument already exists in the warp source group.");

            // This code locating the "pointerWarp" assumes that the data is well-formed (if
            // a PointerWarp exists, it's the last entry for that room)

            if (data.WarpSourceType == WarpSourceType.Standard) {
                Data insertPosition;
                if (PointerWarp != null) {
                    insertPosition = PointerWarp;
                }
                else {
                    // Assumes the last element of warpSourceDataList is always the
                    // "m_WarpSourcesEnd" command
                    insertPosition = EndData;
                }

                FileParser.InsertComponentBefore(insertPosition, data);
            }
            else if (data.WarpSourceType == WarpSourceType.Pointed) {
                FileComponent insertPosition;
                if (PointerWarp == null) {
                    PointerWarp = new WarpSourceData(Project,
                            command: WarpSourceData.WarpCommands[(int)WarpSourceType.Pointer],
                            values: WarpSourceData.DefaultValues[(int)WarpSourceType.Pointer],
                            parser: FileParser,
                            spacing: new List<string>{"\t","  "});
                    PointerWarp.Map = Map;

                    // Create a unique pointer after m_WarpSourcesEnd
                    string name = Project.GetUniqueLabelName(
                            String.Format("group{0}Room{1:x2}WarpSources", Index>>8, Index&0xff));
                    PointerWarp.PointerString = name;
                    Label newLabel = new Label(FileParser, name);

                    // Insert PointerWarp before m_WarpSourcesEnd
                    FileParser.InsertComponentBefore(EndData, PointerWarp);

                    // Insert label after m_WarpSourcesEnd
                    FileParser.InsertComponentAfter(EndData, newLabel);
                    insertPosition = newLabel;
                }
                else // Already exists, jump to the end of the pointed warp list
                    insertPosition = PointerWarp.TraversePointedChain(PointerWarp.GetPointedChainLength()-1);

                FileParser.InsertComponentAfter(insertPosition, data);
            }
            else {
                throw new ArgumentException("Can't add this warp source type to the warp source group.");
            }

            data.AddModifiedEventHandler(OnDataModified);
            RegenWarpSourceDataList();
            ModifiedEvent.Invoke(this, null);
        }

        // Similar to above, this only supports removing StandardWarps or PointedWarps (the rest is
        // handled automatically).
        public void RemoveWarpSource(WarpSourceData data) {
            if (data.WarpSourceType == WarpSourceType.Standard) {
                data.RemoveModifiedEventHandler(OnDataModified);
                data.Detach();
            }
            else if (data.WarpSourceType == WarpSourceType.Pointed) {
                if (PointerWarp == null)
                    throw new ArgumentException("WarpSourceGroup doesn't contain the data to remove?");

                if (PointerWarp.GetPointedChainLength() == 1) {
                    // Delete label & PointerWarp (do this before deleting its last PointedWarp,
                    // otherwise it'll start reading subsequent data and get an incorrect count)
                    fileParser.RemoveFileComponent(Project.GetLabel(PointerWarp.PointerString));
                    fileParser.RemoveFileComponent(PointerWarp);
                }

                data.RemoveModifiedEventHandler(OnDataModified);
                data.Detach();
                PointerWarp = null;
            }
            else
                throw new ArgumentException("RemoveWarpSourceData doesn't support this warp source type.");

            RegenWarpSourceDataList();
            ModifiedEvent.Invoke(this, null);
        }

        public void AddModifiedHandler(EventHandler<EventArgs> handler) {
            ModifiedEvent += handler;
        }

        public void RemoveModifiedHandler(EventHandler<EventArgs> handler) {
            ModifiedEvent -= handler;
        }


        void OnDataModified(object sender, DataModifiedEventArgs args) {
            ModifiedEvent.Invoke(this, null);
        }
    }
}
