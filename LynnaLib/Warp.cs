namespace LynnaLib
{
    // This class is an abstraction of WarpSourceData and WarpDestData, managed by the WarpGroup
    // class. It hides annoying details such as managing warp destination indices manually.
    public class Warp : TrackedProjectData
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        internal Warp(WarpGroup group, WarpSourceData data, int uniqueID)
            : base(group.Project, $"{group.Identifier}-{uniqueID}")
        {
            state = new()
            {
                sourceGroup = new(group),
                uniqueID = uniqueID,
                warpSource = new(data),
            };

            // Do not, repeat, do NOT install a modified handler onto the source data - not as long
            // as the "DestGroup" and "DestIndex" fields behave as they do currently; they should
            // behave as a single atomic field, but they don't. The handler would trigger when only
            // one, but not the other, has been modified, putting things into a invalid state.
            //
            // In any case there is generally NO NEED for a modified handler from the source data
            // because it should never be modified except through this class.
            //
            // This does make undo/redo management a tad more complicated, but it's manageable.
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private Warp(Project p, string id, TransactionState s)
            : base(p, id)
        {
            this.state = (State)s;
        }

        // ================================================================================
        // Variables
        // ================================================================================

        // Everything in the State class is affected by undo/redo
        class State : TransactionState
        {
            public InstanceResolver<WarpGroup> sourceGroup { get; init; }
            public int uniqueID { get; init; } // ID that's unique compared to any warp in this room
            public InstanceResolver<WarpSourceData> warpSource;
        };

        State state = new State();
        ValueReferenceGroup _vrg;

        LockableEvent<EventArgs> ModifiedEvent = new LockableEvent<EventArgs>();


        // ================================================================================
        // Properties
        // ================================================================================

        // Properties from warp source

        public WarpSourceType WarpSourceType
        {
            get { return SourceData.WarpSourceType; }
        }
        public ValueReferenceGroup ValueReferenceGroup
        {
            get { return vrg; }
        }

        ValueReferenceGroup vrg
        {
            get
            {
                if (_vrg == null)
                    ConstructValueReferenceGroup();
                return _vrg;
            }
        }

        public bool TopLeft
        {
            get {return vrg.GetIntValue("Top-Left") != 0;}
            set {vrg.SetValue("Top-Left", value ? 1 : 0);}
        }
        public bool TopRight
        {
            get {return vrg.GetIntValue("Top-Right") != 0;}
            set {vrg.SetValue("Top-Right", value ? 1 : 0);}
        }
        public bool BottomLeft
        {
            get {return vrg.GetIntValue("Bottom-Left") != 0;}
            set {vrg.SetValue("Bottom-Left", value ? 1 : 0);}
        }
        public bool BottomRight
        {
            get {return vrg.GetIntValue("Bottom-Right") != 0;}
            set {vrg.SetValue("Bottom-Right", value ? 1 : 0);}
        }
        public int SourceTransition
        {
            get {return vrg.GetIntValue("Src Transition");}
            set {vrg.SetValue("Src Transition", value);}
        }
        public int SourceX
        {
            get {return vrg.GetIntValue("Source X");}
            set {vrg.SetValue("Source X", value);}
        }
        public int SourceY
        {
            get {return vrg.GetIntValue("Source Y");}
            set {vrg.SetValue("Source Y", value);}
        }

        public Room SourceRoom
        {
            get {return SourceGroup.Room;}
        }

        // Propreties from warp destination

        public int DestRoomIndex
        {
            get {return vrg.GetIntValue("Dest Room");}
            set {vrg.SetValue("Dest Room", value);}
        }

        public Room DestRoom
        {
            get { return Project.GetIndexedDataType<Room>(DestRoomIndex); }
            set { DestRoomIndex = value.Index; }
        }

        public int DestY
        {
            get {return vrg.GetIntValue("Dest Y");}
            set {vrg.SetValue("Dest Y", value);}
        }
        public int DestX
        {
            get {return vrg.GetIntValue("Dest X");}
            set {vrg.SetValue("Dest X", value);}
        }
        public int DestParameter
        {
            get {return vrg.GetIntValue("Dest Parameter");}
            set {vrg.SetValue("Dest Parameter", value);}
        }
        public int DestTransition
        {
            get {return vrg.GetIntValue("Dest Transition");}
            set {vrg.SetValue("Dest Transition", value);}
        }

        // Other properties

        public WarpGroup SourceGroup { get { return state.sourceGroup.Instance; } }

        public string TransactionIdentifier { get { return $"warp-r{SourceRoom.Index:X3}i{state.uniqueID}"; } }

        // Underlying warp source data object. In general, manipulating this directly
        // isn't recommended; direct modifications to the base data don't trigger event handlers
        // set by the "AddModifiedHandler" function. Same with "DestData".
        internal WarpSourceData SourceData
        {
            get { return state.warpSource.Instance; }
            set
            {
                Project.TransactionManager.CaptureInitialState<State>(this);
                state.warpSource = new(value);
            }
        }

        WarpDestData DestData { get { return SourceData.ReferencedDestData; } }

        ValueReferenceGroup SourceVrg { get { return SourceData.ValueReferenceGroup; } }
        ValueReferenceGroup DestVrg { get { return DestData.ValueReferenceGroup; } }

        // ================================================================================
        // Public methods
        // ================================================================================

        public void Remove()
        {
            SourceGroup.RemoveWarp(this);
        }

        public void AddModifiedHandler(EventHandler<EventArgs> handler)
        {
            ModifiedEvent += handler;
        }

        public void RemoveModifiedHandler(EventHandler<EventArgs> handler)
        {
            ModifiedEvent -= handler;
        }

        // ================================================================================
        // TrackedProjectData interface functions
        // ================================================================================

        public override TransactionState GetState()
        {
            return state;
        }

        public override void SetState(TransactionState state)
        {
            this.state = (State)state;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
            ModifiedEvent?.Invoke(this, null);
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        // ValueReferenceGroup for simpler editing based on a few named parameters.
        // All modifications to the underlying data should be done through the ValueReferenceGroup
        // when possible, so that its "value changed" event handlers are properly invoked.
        void ConstructValueReferenceGroup()
        {
            var descriptors = new List<ValueReferenceDescriptor>();

            ValueReferenceDescriptor desc;

            if (WarpSourceType == WarpSourceType.Standard)
            {
                desc = AbstractBoolValueReference.Descriptor(Project,
                        name: "Top-Left",
                        getter: () => SourceData.TopLeft,
                        setter: (value) => SourceData.TopLeft = value);
                descriptors.Add(desc);

                desc = AbstractBoolValueReference.Descriptor(Project,
                        name: "Top-Right",
                        getter: () => SourceData.TopRight,
                        setter: (value) => SourceData.TopRight = value);
                descriptors.Add(desc);

                desc = AbstractBoolValueReference.Descriptor(Project,
                        name: "Bottom-Left",
                        getter: () => SourceData.BottomLeft,
                        setter: (value) => SourceData.BottomLeft = value);
                descriptors.Add(desc);

                desc = AbstractBoolValueReference.Descriptor(Project,
                        name: "Bottom-Right",
                        getter: () => SourceData.BottomRight,
                        setter: (value) => SourceData.BottomRight = value);
                descriptors.Add(desc);
            }
            else if (WarpSourceType == WarpSourceType.Position)
            {
                desc = AbstractIntValueReference.Descriptor(Project,
                        name: "Source Y",
                        getter: () => SourceData.Y,
                        setter: (value) => SourceData.Y = value,
                        maxValue: 15);
                descriptors.Add(desc);

                desc = AbstractIntValueReference.Descriptor(Project,
                        name: "Source X",
                        getter: () => SourceData.X,
                        setter: (value) => SourceData.X = value,
                        maxValue: 15);
                descriptors.Add(desc);
            }
            else
                throw new Exception("Invalid warp source type for warp.");

            desc = AbstractIntValueReference.Descriptor(Project,
                    name: "Src Transition",
                    getter: () => SourceData.Transition,
                    setter: (value) => SourceData.Transition = value,
                    maxValue: 15,
                    constantsMappingString: "SourceTransitionMapping");
            descriptors.Add(desc);

            desc = AbstractIntValueReference.Descriptor(Project,
                    name: "Dest Room",
                    getter: () => (SourceData.DestGroupIndex << 8) | DestData.Map,
                    setter: (value) =>
                    {
                        if (DestRoomIndex != value)
                        {
                            if (SourceData.DestGroupIndex != value >> 8)
                            { // Group changed
                                IsolateDestData(value >> 8);
                            }
                            else
                                IsolateDestData();
                            DestData.Map = value & 0xff;
                        }
                    },
                    maxValue: Project.NumRooms - 1); // TODO: seasons has some "gap" rooms
            descriptors.Add(desc);

            desc = AbstractIntValueReference.Descriptor(Project,
                    name: "Dest Y",
                    getter: () => DestData.Y,
                    setter: (value) =>
                    {
                        if (DestData.Y != value)
                        {
                            IsolateDestData();
                            DestData.Y = value;
                        }
                    },
                    maxValue: 15);
            descriptors.Add(desc);

            desc = AbstractIntValueReference.Descriptor(Project,
                    name: "Dest X",
                    getter: () => DestData.X,
                    setter: (value) =>
                    {
                        if (DestData.X != value)
                        {
                            IsolateDestData();
                            DestData.X = value;
                        }
                    },
                    maxValue: 15);
            descriptors.Add(desc);

            desc = AbstractIntValueReference.Descriptor(Project,
                    name: "Dest Parameter",
                    getter: () => DestData.Parameter,
                    setter: (value) =>
                    {
                        if (DestData.Parameter != value)
                        {
                            IsolateDestData();
                            DestData.Parameter = value;
                        }
                    },
                    maxValue: 15);
            descriptors.Add(desc);

            desc = AbstractIntValueReference.Descriptor(Project,
                    name: "Dest Transition",
                    getter: () => DestData.Transition,
                    setter: (value) =>
                    {
                        if (DestData.Transition != value)
                        {
                            IsolateDestData();
                            DestData.Transition = value;
                        }
                    },
                    maxValue: 15,
                    constantsMappingString: "DestTransitionMapping");
            descriptors.Add(desc);

            _vrg = new ValueReferenceGroup(descriptors);
            _vrg.AddValueModifiedHandler((sender, args) => OnDataModified(sender, null));
            _vrg.EnableTransactions($"Edit warp data#{TransactionIdentifier}", true);
        }


        // Call this to ensure that the destination data this warp uses is not also used by anything
        // else. If it is, we find unused dest data or create new data.
        void IsolateDestData(int newGroup = -1)
        {
            if (newGroup == -1)
                newGroup = SourceData.DestGroupIndex;

            Debug.Assert(DestData.GetNumReferences() >= 1); // This warp references DestData

            WarpDestData oldDest = DestData;
            if (newGroup != SourceData.DestGroupIndex)
            {
                if (newGroup >= Project.NumGroups)
                    throw new Exception(string.Format("Group {0} is too high for warp destination.", newGroup));
                var destGroup = Project.GetIndexedDataType<WarpDestGroup>(newGroup);
                AllocateNewDestData(destGroup);
            }
            else
            {
                if (DestData.GetNumReferences() != 1)
                { // Used by another warp source
                    AllocateNewDestData(SourceData.DestGroup);
                }
            }

            if (oldDest != DestData)
            {
                DestData.Map = oldDest.Map;
                DestData.Y = oldDest.Y;
                DestData.X = oldDest.X;
                DestData.Parameter = oldDest.Parameter;
                DestData.Transition = oldDest.Transition;
            }

            if (DestData.GetNumReferences() != 1)
                throw new Exception("Internal error: New warp destination has "
                        + DestData.GetNumReferences() + " references.");
        }

        // Used to always use this function so that I could add and remove modified hooks on the
        // underlying "dest data". But all modifications should be done through the "Warp" class
        // anyway, not the "WarpDestData" class, so that's not necessary.
        void AllocateNewDestData(WarpDestGroup group)
        {
            (int i, WarpDestData data) = group.GetNewOrUnusedDestData();
            SourceData.SetDestData(group.Index, i, data);
        }


        void OnDataModified(object sender, DataModifiedEventArgs args)
        {
            // Because the ValueReferenceGroup is made up entirely of AbstractIntValueReferences, it
            // does not have any listeners installed on the underlying data. This is done on
            // purpose (see note in constructor). But it means that we do not see undo/redo events
            // that change the underlying data. So we must notify the Undo system to do something.
            // (This is just to ensure "InvokeUndoEvents" is called.)
            Project.TransactionManager.CaptureInitialState<State>(this);

            ModifiedEvent?.Invoke(this, null);
        }
    }
}
