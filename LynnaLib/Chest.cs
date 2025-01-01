namespace LynnaLib
{
    /// <summary>
    /// Represents the 4 bytes defining a chest. There should only be one of these per room.
    /// </summary>
    public class Chest
    {
        readonly Data dataStart;
        readonly Room room;

        public event EventHandler<EventArgs> DeletedEvent;


        internal Chest(Room room, Data dataStart)
        {
            this.room = room;
            this.dataStart = dataStart;
            GenerateValueReferenceGroup();

            ValueReferenceGroup.ModifiedEvent += (sender, args) =>
            {
                // We cannot allow a chest to have a YX value of $ff, as this marks the end of the list.
                if (dataStart.GetIntValue(0) == 0xff)
                {
                    dataStart.LockModifiedEvents();
                    dataStart.SetByteValue(0, 0xfe);
                    dataStart.ClearAndUnlockModifiedEvents();
                }
                UpdateTreasureEvents();
            };

            UpdateTreasureEvents();

            // When we create a new treasure subid, the treasure name used in the chest data may not
            // be updated, so make sure to update it at the last second.
            dataStart.ResolveEvent += (sender, args) =>
            {
                if (dataStart.Modified)
                    UpdateTreasureName();
            };

            Debug.Assert((room.Index & 0xff) == ValueReferenceGroup.GetIntValue("Room"));
        }


        // Properties

        public ValueReferenceGroup ValueReferenceGroup { get; private set; }
        public int TreasureID
        {
            get
            {
                return ValueReferenceGroup.GetIntValue("ID");
            }
            set
            {
                ValueReferenceGroup.SetValue("ID", value);
            }
        }
        public int TreasureSubID
        {
            get
            {
                return ValueReferenceGroup.GetIntValue("SubID");
            }
            set
            {
                ValueReferenceGroup.SetValue("SubID", value);
            }
        }
        public int TreasureIndex
        {
            get
            {
                return TreasureID * 256 + TreasureSubID;
            }
            set
            {
                ValueReferenceGroup.BeginAtomicOperation();
                TreasureID = value >> 8;
                TreasureSubID = value & 0xff;
                ValueReferenceGroup.EndAtomicOperation();
            }
        }
        public TreasureGroup TreasureGroup
        {
            get { return Project.GetIndexedDataType<TreasureGroup>(TreasureID); }
        }
        public TreasureObject Treasure
        {
            get
            {
                try
                {
                    return TreasureGroup.GetTreasureObject(TreasureSubID);
                }
                catch (InvalidTreasureException)
                {
                    return null;
                }
            }
            set
            {
                TreasureIndex = (value.ID << 8) | value.SubID;
            }
        }

        public string TransactionIdentifier
        {
            get { return $"chest-r{room.Index:X3}"; }
        }

        public Project Project { get { return dataStart.Project; } }


        // ================================================================================
        // Public methods
        // ================================================================================

        public void Delete()
        {
            Project.BeginTransaction("Delete chest");

            dataStart.Detach();

            Project.UndoState.OnRewind("Delete chest", () =>
            { // On undo
                room.ChestRevived(this);
            }, (_) =>
            { // On redo / right now
                InvokeDeletedEvent();
            });

            Project.EndTransaction();
        }

        internal void InvokeDeletedEvent()
        {
            DeletedEvent?.Invoke(this, null);
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        void GenerateValueReferenceGroup()
        {
            var v1 = DataValueReference.Descriptor(dataStart,
                    name: "Y",
                    index: 0,
                    type: DataValueType.ByteBits,
                    startBit: 4,
                    endBit: 7);
            var v2 = DataValueReference.Descriptor(dataStart,
                    name: "X",
                    index: 0,
                    type: DataValueType.ByteBits,
                    startBit: 0,
                    endBit: 3);
            var v3 = DataValueReference.Descriptor(dataStart,
                    name: "Room",
                    index: 1,
                    type: DataValueType.Byte,
                    editable: false);
            var v4 = AbstractIntValueReference.Descriptor(Project,
                    name: "ID",
                    getter: () => { return Project.Eval(dataStart.GetValue(2)) >> 8; },
                    setter: (v) =>
                    {
                        UpdateTreasureName(v, -1);
                    },
                    maxValue: Project.NumTreasures - 1,
                    constantsMappingString: "TreasureMapping");
            var v5 = AbstractIntValueReference.Descriptor(Project,
                    name: "SubID",
                    getter: () => { return Project.Eval(dataStart.GetValue(2)) & 0xff; },
                    setter: (v) =>
                    {
                        UpdateTreasureName(-1, v);
                    },
                    maxValue: 255);

            var list = new ValueReferenceDescriptor[] {
                v1, v2, v3, v4, v5
            };

            ValueReferenceGroup = new ValueReferenceGroup(list);
            ValueReferenceGroup.EnableTransactions("Edit chest#" + TransactionIdentifier, true);
        }

        void UpdateTreasureName(int id = -1, int subid = -1)
        {
            if (id == -1)
                id = TreasureID;
            if (subid == -1)
                subid = TreasureSubID;

            int full = (id << 8) | subid;
            string val;
            if (Project.TreasureObjectMapping.HasValue(full))
                val = Project.TreasureObjectMapping.ByteToString(full);
            else
                val = Wla.ToWord(full);
            dataStart.SetValue(2, val);
        }

        void UpdateTreasureEvents()
        {
        }
    }
}
