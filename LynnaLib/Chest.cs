using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using Util;

namespace LynnaLib
{
    /// Represents the 4 bytes defining a chest. There should only be one of these per room.
    /// TODO: Forbid chests from having Y/X value "$ff"?
    public class Chest
    {
        Data dataStart;
        WeakEventWrapper<ValueReferenceGroup> treasureModifiedEventWrapper
            = new WeakEventWrapper<ValueReferenceGroup>();


        // Invoked when the chest is modified (but NOT if the TreasureObject contained in the chest
        // is modified, see TreasureModifiedEvent)
        public event EventHandler<ValueModifiedEventArgs> ModifiedEvent;

        public event EventHandler<EventArgs> DeletedEvent;

        /// This is a convenience event that always tracks changes to the underlying treasure, even
        /// when the treasure index is changed.
        public event EventHandler<ValueModifiedEventArgs> TreasureModifiedEvent;


        internal Chest(Data dataStart)
        {
            this.dataStart = dataStart;
            GenerateValueReferenceGroup();

            ValueReferenceGroup.ModifiedEvent += (sender, args) => ModifiedEvent?.Invoke(sender, args);
            ValueReferenceGroup.ModifiedEvent += (sender, args) => UpdateTreasureEvents();

            treasureModifiedEventWrapper.Bind<ValueModifiedEventArgs>("ModifiedEvent",
                    (sender, args) => TreasureModifiedEvent?.Invoke(sender, args));

            UpdateTreasureEvents();

            // When we create a new treasure subid, the treasure name used in the chest data may not
            // be updated, so make sure to update it at the last second.
            dataStart.ResolveEvent += (sender, args) =>
            {
                if (dataStart.Modified)
                    UpdateTreasureName();
            };
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

        public Project Project { get { return dataStart.Project; } }


        // Methods

        public void Delete()
        {
            dataStart.Detach();
            dataStart = null;
            DeletedEvent?.Invoke(this, null);
        }


        void GenerateValueReferenceGroup()
        {
            var v1 = new DataValueReference(dataStart,
                    name: "Y",
                    index: 0,
                    type: DataValueType.ByteBits,
                    startBit: 4,
                    endBit: 7);
            var v2 = new DataValueReference(dataStart,
                    name: "X",
                    index: 0,
                    type: DataValueType.ByteBits,
                    startBit: 0,
                    endBit: 3);
            var v3 = new DataValueReference(dataStart,
                    name: "Room",
                    index: 1,
                    type: DataValueType.Byte,
                    editable: false);
            var v4 = new AbstractIntValueReference(Project,
                    name: "ID",
                    getter: () => { return Project.EvalToInt(dataStart.GetValue(2)) >> 8; },
                    setter: (v) =>
                    {
                        UpdateTreasureName(v, -1);
                    },
                    maxValue: 255,
                    constantsMappingString: "TreasureMapping");
            var v5 = new AbstractIntValueReference(Project,
                    name: "SubID",
                    getter: () => { return Project.EvalToInt(dataStart.GetValue(2)) & 0xff; },
                    setter: (v) =>
                    {
                        UpdateTreasureName(-1, v);
                    },
                    maxValue: 255);

            var list = new ValueReference[] {
                v1, v2, v3, v4, v5
            };

            ValueReferenceGroup = new ValueReferenceGroup(list);
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
            treasureModifiedEventWrapper.ReplaceEventSource(Treasure?.ValueReferenceGroup);
        }
    }
}
