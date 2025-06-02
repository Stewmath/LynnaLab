namespace LynnaLib
{
    // Reads/writes warp source data per room. Searches through all of a group's warp data to get
    // only data for a particular room index (a list of "WarpSourceData" instances).
    //
    // Assertions:
    // - A full-screen warp is not used with "specific-position" warps. (Whichever comes first takes
    //   precedence.)
    // - If a "PointerWarp" referencing "PositionWarps" is used, no warp data for the room is defined
    //   after that point. (The game would not see it.)
    // - Each warp type is only used in its appropriate location (StandardWarp and PointerWarp at
    //   the top level, PositionWarp only as the data pointed to by a PointerWarp).
    public class WarpGroup : TrackedIndexedProjectData, IndexedProjectDataInstantiator
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // ================================================================================
        // Constructors
        // ================================================================================

        private WarpGroup(Project p, int id) : base(p, id)
        {
            state.fileParser = new(Project.GetFileWithLabel("warpSourcesTable"));
            RegenWarpSourceDataList();
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private WarpGroup(Project p, string id, TransactionState s)
            : base(p, int.Parse(id))
        {
            this.state = (State)s;
            // No need to call RegenWarpSourceDataList() - it's all in the state tracking
        }

        static ProjectDataType IndexedProjectDataInstantiator.Instantiate(Project p, int index)
        {
            return new WarpGroup(p, index);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        // Everything in the State class is affected by undo/redo
        class State : TransactionState
        {
            public InstanceResolver<FileParser> fileParser;

            // Parallel lists
            public List<InstanceResolver<Warp>> warpList = new();
            public List<InstanceResolver<WarpSourceData>> warpSourceDataList;

            public InstanceResolver<WarpSourceData> endWarp; // "m_WarpListEnd" opcode for top level data (shouldn't be null)
            public InstanceResolver<WarpSourceData> pointerWarp; // "m_PointerWarp" (can be null but should be unique)
            public InstanceResolver<WarpSourceData> lastStandardWarp; // New data added goes after this (can be null)

            public int uniqueIDCounter;
        };


        State state = new State();

        // Purposely tracked outside of State to manage event handlers. Can't use InstanceResolvers
        // because they may refer to deleted warps, which InstanceResolvers can't find.
        List<Warp> registeredWarps = new();

        // ================================================================================
        // Properties
        // ================================================================================

        List<InstanceResolver<Warp>> WarpList { get { return state.warpList; } }
        List<InstanceResolver<WarpSourceData>> WarpSourceDataList { get { return state.warpSourceDataList; } }
        WarpSourceData EndWarp
        {
            get { return state.endWarp.Instance; }
            set { state.endWarp = new(value); }
        }
        WarpSourceData PointerWarp
        {
            get
            {
                if (state.pointerWarp == null)
                    return null;
                return state.pointerWarp?.Instance;
            }
            set
            {
                if (value == null)
                    state.pointerWarp = null;
                else
                    state.pointerWarp = new(value);
            }
        }
        WarpSourceData LastStandardWarp
        {
            get
            {
                if (state.lastStandardWarp == null)
                    return null;
                return state.lastStandardWarp?.Instance;
            }
            set
            {
                if (value == null)
                    state.lastStandardWarp = null;
                else
                    state.lastStandardWarp = new(value);
            }
        }

        public int Group
        {
            get
            {
                return Index;
            }
        }
        public FileParser FileParser
        {
            get
            {
                return state.fileParser;
            }
        }

        public int Count
        {
            get { return WarpSourceDataList.Count; }
        }

        public Room Room
        {
            get { return Project.GetIndexedDataType<Room>(Index); }
        }

        int Map
        {
            get { return Index & 0xff; }
        }

        // ================================================================================
        // Events
        // ================================================================================

        // Event invoked when:
        // - Warps are added to or removed from the warp group. "sender" is "this" (WarpGroup).
        // - Underlying warp data is modified. "sender" is the "Warp" object that was modified.
        public event EventHandler<EventArgs> ModifiedEvent;

        // ================================================================================
        // Public methods
        // ================================================================================

        // Returns a new list of all the WarpSourceData objects for the given room.
        // This does not return "PointerWarp" types. It instead traverses them and returns the
        // resulting PositionWarps.
        public IEnumerable<Warp> GetWarps()
        {
            return WarpList.Select((w) => w.Instance);
        }

        public int IndexOf(Warp warp)
        {
            return WarpList.IndexOf(new(warp));
        }

        public Warp GetWarp(int index)
        {
            return WarpList[index];
        }

        public bool ContainsWarp(Warp warp)
        {
            return WarpList.Contains(new(warp));
        }

        // Adds the given data to the group and inserts the data into the FileParser.
        // If "data" is of type "WarpSourceType.StandardWarp", this gets inserted before any
        // "PointerWarp" if such a warp exists.
        // If "data" is of type "WarpSourceType.PositionWarp", this gets inserted at the end of the
        // "PointerWarp" list; it automatically creates a "PointerWarp" if necessary.
        // For any other warp type, this throws an ArgumentException.
        // Returns the index at which the new warp was placed.
        public int AddWarp(WarpSourceType type)
        {
            Project.BeginTransaction("Add warp");
            Project.TransactionManager.CaptureInitialState<State>(this);

            Action<WarpSourceData> InsertInMainList = (d) =>
            {
                if (LastStandardWarp != null)
                {
                    FileParser.InsertComponentAfter(LastStandardWarp, d);
                }
                else if (PointerWarp != null && d != PointerWarp)
                {
                    FileParser.InsertComponentBefore(PointerWarp, d);
                }
                else
                {
                    FileParser.InsertComponentBefore(EndWarp, d);
                }
            };

            WarpSourceData data = new WarpSourceData(
                Project,
                Project.GenUniqueID(typeof(WarpSourceData)),
                command: WarpSourceData.WarpCommands[(int)type],
                values: WarpSourceData.DefaultValues[(int)type],
                parser: FileParser,
                spacing: new List<string> { "\t" });

            if (type == WarpSourceType.Standard)
            {
                data.Map = Map;
                InsertInMainList(data);
            }
            else if (type == WarpSourceType.Position)
            {
                FileComponent pointedDataInsertPosition;
                if (PointerWarp == null)
                {
                    PointerWarp = new WarpSourceData(
                        Project,
                        Project.GenUniqueID(typeof(WarpSourceData)),
                        command: WarpSourceData.WarpCommands[(int)WarpSourceType.Pointer],
                        values: WarpSourceData.DefaultValues[(int)WarpSourceType.Pointer],
                        parser: FileParser,
                        spacing: new List<string> { "\t", "  " });
                    PointerWarp.Map = Map;

                    // Create a unique pointer after top-level data end
                    string name = Project.GetUniqueLabelName(
                            String.Format("group{0}Room{1:x2}WarpSources", Index >> 8, Index & 0xff));
                    PointerWarp.PointerString = name;
                    Label newLabel = new Label(Project.GenUniqueID(typeof(Label)), FileParser, name);

                    InsertInMainList(PointerWarp);

                    // Insert label after top-level data end
                    FileParser.InsertComponentAfter(EndWarp, newLabel);

                    // Extra spacing (keeping this disabled for now as it causes the file size to
                    // keep growing)
                    //FileParser.InsertParseableTextAfter(EndWarp, new string[] { "" });

                    // Insert m_WarpListEndNoDefault opcode
                    var endOpcode = new WarpSourceData(
                        Project,
                        Project.GenUniqueID(typeof(WarpSourceData)),
                        command: WarpSourceData.WarpCommands[(int)WarpSourceType.EndNoDefault],
                        values: WarpSourceData.DefaultValues[(int)WarpSourceType.EndNoDefault],
                        parser: FileParser,
                        spacing: new List<string> { "\t" });
                    FileParser.InsertComponentAfter(newLabel, endOpcode);

                    pointedDataInsertPosition = newLabel;
                }
                else
                { // Already exists, jump to the end of the pointed warp list
                    var lastPositionWarp = PointerWarp.TraversePointedChain(PointerWarp.GetNumPointedWarps() - 1);
                    pointedDataInsertPosition = lastPositionWarp;
                }

                FileParser.InsertComponentAfter(pointedDataInsertPosition, data);
            }
            else
            {
                throw new ArgumentException("Can't add this warp source type to the warp source group.");
            }

            // Default warp destination should use unique data
            var (destIndex, destData) = data.DestGroup.GetNewOrUnusedDestData();
            data.SetDestData(data.DestGroup.Index, destIndex, destData);

            // Default values (we may be overwriting a previously unused warp)
            data.ReferencedDestData.Map = 0;
            data.ReferencedDestData.Y = 0;
            data.ReferencedDestData.X = 0;
            data.ReferencedDestData.Parameter = 0;
            data.ReferencedDestData.Transition = 1;

            RegenWarpSourceDataList();
            ModifiedEvent?.Invoke(this, null);

            Project.EndTransaction();

            // TODO
            return 0;
        }

        // Similar to above, this only supports removing StandardWarps or PositionWarps (the rest is
        // handled automatically).
        public void RemoveWarp(Warp warp)
        {
            Project.BeginTransaction("Delete warp");
            Project.TransactionManager.CaptureInitialState<State>(this);

            WarpSourceData data = warp.SourceData;
            WarpDestData destData = data.ReferencedDestData;

            if (data.WarpSourceType == WarpSourceType.Standard)
            {
                data.Detach();
            }
            else if (data.WarpSourceType == WarpSourceType.Position)
            {
                if (PointerWarp == null)
                    throw new ArgumentException("WarpGroup doesn't contain the data to remove?");

                // Is this the only PositionWarp that this PointerWarp is referencing? If so, delete
                // all the data.
                if (PointerWarp.GetNumPointedWarps() == 1)
                {
                    // Delete label
                    FileParser.RemoveFileComponent(Project.GetLabel(PointerWarp.PointerString));
                    // Delete PointerWarp (do this before deleting its last PositionWarp, otherwise
                    // it'll start reading subsequent data and get an incorrect count)
                    PointerWarp.Detach();
                    // Delete end opcode
                    data.NextData.Detach();
                }

                // Delete PositionWarp
                data.Detach();

                PointerWarp = null;
            }
            else
                throw new ArgumentException("RemoveWarp doesn't support this warp source type.");

            RegenWarpSourceDataList();
            ModifiedEvent?.Invoke(this, null);

            Project.EndTransaction();
        }

        public void RemoveWarp(int index)
        {
            RemoveWarp(GetWarp(index));
        }

        // ================================================================================
        // TrackedProjectData interface functions
        // ================================================================================

        public override TransactionState GetState()
        {
            return state;
        }

        public override void SetState(TransactionState s)
        {
            state = (State)s;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
            UpdateWarpModifiedHandlers();
            ModifiedEvent?.Invoke(this, null);
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        // Update modified handlers
        void UpdateWarpModifiedHandlers()
        {
            foreach (Warp warp in registeredWarps)
                warp.RemoveModifiedHandler(OnDataModified);

            registeredWarps.Clear();

            foreach (InstanceResolver<Warp> warpIR in state.warpList)
            {
                warpIR.Instance.AddModifiedHandler(OnDataModified);
                registeredWarps.Add(warpIR.Instance);
            }
        }

        void RegenWarpSourceDataList()
        {
            // Now begin reading the file data for the new warps
            Data d = FileParser.GetData("warpSourcesTable", (Index >> 8) * 2);

            string label = d.GetValue(0);

            var newWarpList = new List<InstanceResolver<Warp>>();
            state.warpSourceDataList = new List<InstanceResolver<WarpSourceData>>();

            WarpSourceData warpData = FileParser.GetData(label) as WarpSourceData;

            PointerWarp = null;
            LastStandardWarp = null;

            while (warpData != null && !warpData.IsEndOpcode)
            {
                if (Map == warpData.Map)
                {
                    if (warpData.WarpSourceType == WarpSourceType.Pointer)
                    {
                        if (PointerWarp != null)
                        {
                            throw new AssemblyErrorException(string.Format(
                                        "Room {0:X3} has multiple 'Pointer' warp sources!", Index));
                        }
                        PointerWarp = warpData;
                        WarpSourceData pWarp = warpData.GetPointedWarp();
                        while (pWarp != null)
                        {
                            if (pWarp.IsEndOpcode)
                                break;
                            if (pWarp.WarpSourceType != WarpSourceType.Position)
                                throw new AssemblyErrorException(string.Format(
                                            "Unexpected warp type '{0}' in {1}!",
                                            pWarp.WarpSourceType, warpData.PointerString));
                            WarpSourceDataList.Add(new(pWarp));
                            pWarp = pWarp.GetNextWarp();
                        }
                    }
                    else if (warpData.WarpSourceType == WarpSourceType.Standard)
                    {
                        if (PointerWarp != null)
                            throw new AssemblyErrorException("Encountered a 'Standard Warp' after a Pointer Warp; this is invalid!");
                        WarpSourceDataList.Add(new(warpData));
                        LastStandardWarp = warpData;
                    }
                    else
                    {
                        throw new AssemblyErrorException(string.Format(
                                    "Unexpected warp type '{0}' for room {1:X3}!",
                                    warpData.WarpSourceType, Index));
                    }
                }

                // For each entry added to WarpSourceDataList, update newWarpList with a
                // corresponding entry
                while (newWarpList.Count < WarpSourceDataList.Count)
                {
                    WarpSourceData dataToAdd = WarpSourceDataList[newWarpList.Count];

                    // Check if the object existed in the old list. If it didn't, create a new one.
                    // If it did, add it to the new list.
                    bool addedData = false;
                    if (WarpList != null)
                    {
                        foreach (Warp oldWarp in WarpList)
                        {
                            if (oldWarp.SourceData == dataToAdd)
                            {
                                newWarpList.Add(new(oldWarp));
                                addedData = true;
                                break;
                            }
                        }
                    }
                    if (!addedData)
                    {
                        var warp = new Warp(this, dataToAdd, state.uniqueIDCounter++);
                        newWarpList.Add(new(warp));
                    }
                }

                warpData = warpData.NextData as WarpSourceData;
            }
            if (warpData != null)
                EndWarp = warpData;
            else
            {
                throw new AssemblyErrorException(string.Format(
                            "Warp source data for room {0:X3} doesn't have an end?", Index));
            }

            state.warpList = newWarpList;

            UpdateWarpModifiedHandlers();
        }


        void OnDataModified(object sender, EventArgs args)
        {
            ModifiedEvent?.Invoke(sender, null);
        }
    }
}
