namespace LynnaLib
{
    public class WarpDestData : Data
    {
        public static string WarpCommand = "m_WarpDest";


        private static List<ValueReferenceDescriptor> GetWarpValueReferences(Data data)
        {
            return new List<ValueReferenceDescriptor> {
                DataValueReference.Descriptor(data,"Map",0,DataValueType.Byte),
                DataValueReference.Descriptor(data,"Y",1,DataValueType.ByteBits,4,7),
                DataValueReference.Descriptor(data,"X",1,DataValueType.ByteBits,0,3),
                DataValueReference.Descriptor(data,"Parameter",2,DataValueType.HalfByte),
                DataValueReference.Descriptor(data,"Transition",3,DataValueType.HalfByte,
                    constantsMappingString:"DestTransitionMapping"),
            };
        }


        // Private variables

        class WarpDestState : Data.DataState
        {
            public HashSet<InstanceResolver<WarpSourceData>> referenceSet;

            public override void CaptureInitialState(FileComponent parent)
            {
                parent.Project.TransactionManager.CaptureInitialState<WarpDestState>(parent);
            }
        }

        ValueReferenceGroup vrg;


        // Properties

        private WarpDestState State { get { return base.state as WarpDestState; } }

        public int Map
        {
            get
            {
                return vrg.GetIntValue("Map");
            }
            set
            {
                vrg.SetValue("Map", value);
            }
        }
        public int Y
        {
            get
            {
                return vrg.GetIntValue("Y");
            }
            set
            {
                vrg.SetValue("Y", value);
            }
        }
        public int X
        {
            get
            {
                return vrg.GetIntValue("X");
            }
            set
            {
                vrg.SetValue("X", value);
            }
        }
        public int Parameter
        {
            get
            {
                return vrg.GetIntValue("Parameter");
            }
            set
            {
                vrg.SetValue("Parameter", value);
            }
        }
        public int Transition
        {
            get
            {
                return vrg.GetIntValue("Transition");
            }
            set
            {
                vrg.SetValue("Transition", value);
            }
        }

        public ValueReferenceGroup ValueReferenceGroup
        {
            get { return vrg; }
        }


        public WarpDestData(Project p, string id, string command, IEnumerable<string> values,
                FileParser parser, IList<string> spacing)
            : base(p, id, command, values, 3, parser, spacing, () => new WarpDestState())
        {
            vrg = new ValueReferenceGroup(GetWarpValueReferences(this));

            State.referenceSet = new HashSet<InstanceResolver<WarpSourceData>>();
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private WarpDestData(Project p, string id, TransactionState state)
            : base(p, id, state)
        {
        }

        public void AddReference(WarpSourceData data)
        {
            Project.TransactionManager.CaptureInitialState<WarpDestState>(this);
            if (!State.referenceSet.Add(new(data)))
                throw new Exception("Internal error: Warp dest data reference set state invalid");
        }

        public void RemoveReference(WarpSourceData data)
        {
            Project.TransactionManager.CaptureInitialState<WarpDestState>(this);
            if (!State.referenceSet.Remove(new(data)))
                throw new Exception("Internal error: Warp dest data reference set state invalid");
        }

        public int GetNumReferences()
        {
            return State.referenceSet.Count;
        }

        public override void OnInitializedFromTransfer()
        {
            vrg = new ValueReferenceGroup(GetWarpValueReferences(this));
        }
    }
}
