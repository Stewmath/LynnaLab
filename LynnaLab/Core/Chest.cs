using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using Util;

namespace LynnaLib
{
    /// Represents the 4 bytes defining a chest. There should only be one of these per room.
    public class Chest {
        Data dataStart;
        WeakEventWrapper<ValueReferenceGroup> treasureModifiedEventWrapper
            = new WeakEventWrapper<ValueReferenceGroup>();


        public event EventHandler<ValueModifiedEventArgs> ModifiedEvent;
        public event EventHandler<EventArgs> DeletedEvent;

        /// This is a convenience event that always tracks changes to the underlying treasure, even
        /// when the treasure index is changed.
        public event EventHandler<ValueModifiedEventArgs> TreasureModifiedEvent;


        internal Chest(Data dataStart) {
            this.dataStart = dataStart;
            GenerateValueReferenceGroup();

            ValueReferenceGroup.ModifiedEvent += (sender, args) => ModifiedEvent?.Invoke(sender, args);
            ValueReferenceGroup.ModifiedEvent += (sender, args) => UpdateTreasureEvents();

            treasureModifiedEventWrapper.Bind<ValueModifiedEventArgs>("ModifiedEvent",
                    (sender, args) => TreasureModifiedEvent?.Invoke(sender, args));

            UpdateTreasureEvents();
        }


        // Properties

        public ValueReferenceGroup ValueReferenceGroup { get; private set; }
        public int TreasureID {
            get {
                return ValueReferenceGroup.GetIntValue("ID");
            }
            set {
                ValueReferenceGroup.SetValue("ID", value);
            }
        }
        public int TreasureSubID {
            get {
                return ValueReferenceGroup.GetIntValue("SubID");
            }
            set {
                ValueReferenceGroup.SetValue("SubID", value);
            }
        }
        public int TreasureIndex {
            get {
                return TreasureID * 256 + TreasureSubID;
            }
            set {
                ValueReferenceGroup.BeginAtomicOperation();
                TreasureID = value >> 8;
                TreasureSubID = value & 0xff;
                ValueReferenceGroup.EndAtomicOperation();
            }
        }
        public TreasureGroup TreasureGroup {
            get { return Project.GetIndexedDataType<TreasureGroup>(TreasureID); }
        }
        public TreasureObject Treasure {
            get {
                try {
                    return TreasureGroup.GetTreasureObject(TreasureSubID);
                }
                catch (InvalidTreasureException) {
                    return null;
                }
            }
            set {
                TreasureIndex = (value.ID << 8) | value.SubID;
            }
        }

        public Project Project { get { return dataStart.Project; } }


        // Methods

        public void Delete() {
            Data data = dataStart;
            for (int i=0;i<4;i++) {
                Data nextData = data.NextData;
                data.FileParser.RemoveFileComponent(data);
                data = nextData;
            }

            DeletedEvent?.Invoke(this, null);
        }


        void GenerateValueReferenceGroup() {
            Data data = dataStart;
            var v1 = new DataValueReference(data,
                    name: "Y",
                    index: 0,
                    type: DataValueType.ByteBits,
                    startBit: 4,
                    endBit: 7);
            var v2 = new DataValueReference(data,
                    name: "X",
                    index: 0,
                    type: DataValueType.ByteBits,
                    startBit: 0,
                    endBit: 3);
            data = data.NextData;
            var v3 = new DataValueReference(data,
                    name: "Room",
                    index: 0,
                    type: DataValueType.Byte,
                    editable: false);
            data = data.NextData;
            var v4 = new DataValueReference(data,
                    name: "ID",
                    index: 0,
                    type: DataValueType.Byte,
                    constantsMappingString: "TreasureMapping");
            data = data.NextData;
            var v5 = new DataValueReference(data,
                    name: "SubID",
                    index: 0,
                    type: DataValueType.Byte);

            var list = new ValueReference[] {
                v1, v2, v3, v4, v5
            };

            ValueReferenceGroup = new ValueReferenceGroup(list);
        }

        void UpdateTreasureEvents() {
            treasureModifiedEventWrapper.ReplaceEventSource(Treasure?.ValueReferenceGroup);
        }
    }
}
