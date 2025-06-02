using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLib
{
    public class WarpDestGroup : TrackedIndexedProjectData, IndexedProjectDataInstantiator
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        private WarpDestGroup(Project p, int id) : base(p, id)
        {
            Data tmp = FileParser.GetData("warpDestTable", id * 2);

            string label = tmp.GetValue(0);

            WarpDestData data = FileParser.GetData(label) as WarpDestData;

            while (data != null)
            {
                WarpDestDataList.Add(new(data));

                FileComponent component = data.Next;
                data = null;
                while (component != null)
                {
                    if (component is Label)
                    {
                        data = null;
                        break;
                    }
                    else if (component is Data)
                    {
                        data = component as WarpDestData;
                        break;
                    }
                    component = component.Next;
                }
            }
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private WarpDestGroup(Project p, string id, TransactionState state)
            : base(p, int.Parse(id))
        {
            this.state = (State)state;

            if (this.state.warpDestDataList == null)
                throw new DeserializationException();
        }

        static ProjectDataType IndexedProjectDataInstantiator.Instantiate(Project p, int index)
        {
            return new WarpDestGroup(p, index);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        // Everything in the State class is affected by undo/redo
        class State : TransactionState
        {
            public List<InstanceResolver<WarpDestData>> warpDestDataList = new();
        };

        State state = new State();
        FileParser _fileParser;

        // ================================================================================
        // Properties
        // ================================================================================

        FileParser FileParser
        {
            get
            {
                if (_fileParser == null)
                    _fileParser = Project.GetFileWithLabel("warpDestTable");
                return _fileParser;
            }
        }

        public int Group
        {
            get
            {
                return Index;
            }
        }

        List<InstanceResolver<WarpDestData>> WarpDestDataList { get { return state.warpDestDataList; } }

        // ================================================================================
        // Public methods
        // ================================================================================

        public int GetNumWarpDests()
        {
            return WarpDestDataList.Count;
        }

        public WarpDestData GetWarpDest(int index)
        {
            return WarpDestDataList[index];
        }

        // Adds a new WarpDestData to the end of the group, returns the index
        public (int, WarpDestData) AddDestData()
        {
            Project.UndoState.CaptureInitialState<State>(this);

            WarpDestData newData = new WarpDestData(
                Project,
                Project.GenUniqueID(typeof(WarpDestData)),
                WarpDestData.WarpCommand,
                null,
                FileParser,
                new List<string> { "\t" });

            foreach (ValueReferenceDescriptor desc in newData.ValueReferenceGroup.GetDescriptors())
                desc.ValueReference.Initialize();

            newData.Transition = 1;

            FileParser.InsertComponentAfter(WarpDestDataList[WarpDestDataList.Count - 1], newData);
            WarpDestDataList.Add(new(newData));

            return (WarpDestDataList.Count-1, newData);
        }

        // Returns either an unused WarpDestData, or creates a new one if no unused ones exist.
        public (int index, WarpDestData data) GetNewOrUnusedDestData()
        {
            // Check if there's unused destination data already
            for (int i = 0; i < GetNumWarpDests(); i++)
            {
                WarpDestData destData = GetWarpDest(i);
                if (destData.GetNumReferences() == 0)
                {
                    return (i, GetWarpDest(i));
                }
            }
            // TODO: check if there's room to add data
            return AddDestData();
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
        }
    }
}
