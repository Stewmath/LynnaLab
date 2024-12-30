using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLib
{
    public class WarpDestGroup : ProjectIndexedDataType, Undoable
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        internal WarpDestGroup(Project p, int id) : base(p, id)
        {
            fileParser = Project.GetFileWithLabel("warpDestTable");
            Data tmp = fileParser.GetData("warpDestTable", id * 2);

            string label = tmp.GetValue(0);

            WarpDestData data = fileParser.GetData(label) as WarpDestData;

            while (data != null)
            {
                data.DestGroup = this;
                data.DestIndex = WarpDestDataList.Count;
                WarpDestDataList.Add(data);

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

        // ================================================================================
        // Variables
        // ================================================================================

        // Everything in the State class is affected by undo/redo
        class State : TransactionState
        {
            public List<WarpDestData> warpDestDataList = new List<WarpDestData>();

            public TransactionState Copy()
            {
                State s = new State();
                s.warpDestDataList = new List<WarpDestData>(warpDestDataList);
                return s;
            }

            public bool Compare(TransactionState obj)
            {
                return (obj is State state) && warpDestDataList.SequenceEqual(state.warpDestDataList);
            }
        };

        State state = new State();
        readonly FileParser fileParser;
        List<WarpDestData> WarpDestDataList { get { return state.warpDestDataList; } }

        // ================================================================================
        // Properties
        // ================================================================================

        public int Group
        {
            get
            {
                return Index;
            }
        }


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
        public WarpDestData AddDestData()
        {
            Project.UndoState.RecordChange(this);

            WarpDestData newData = new WarpDestData(Project,
                    WarpDestData.WarpCommand,
                    null,
                    fileParser,
                    new List<string> { "\t" });

            foreach (ValueReferenceDescriptor desc in newData.ValueReferenceGroup.GetDescriptors())
                desc.ValueReference.Initialize();

            newData.Transition = 1;

            newData.DestGroup = this;
            newData.DestIndex = WarpDestDataList.Count;

            fileParser.InsertComponentAfter(WarpDestDataList[WarpDestDataList.Count - 1], newData);
            WarpDestDataList.Add(newData);

            return newData;
        }

        // Returns either an unused WarpDestData, or creates a new one if no unused ones exist.
        public WarpDestData GetNewOrUnusedDestData()
        {
            // Check if there's unused destination data already
            for (int i = 0; i < GetNumWarpDests(); i++)
            {
                WarpDestData destData = GetWarpDest(i);
                if (destData.GetNumReferences() == 0)
                {
                    return GetWarpDest(i);
                }
            }
            // TODO: check if there's room to add data
            return AddDestData();
        }

        // ================================================================================
        // Undoable interface methods
        // ================================================================================

        public TransactionState GetState()
        {
            return state;
        }

        public void SetState(TransactionState s)
        {
            state = (State)s.Copy();
        }

        public void InvokeModifiedEvent()
        {
        }
    }
}
