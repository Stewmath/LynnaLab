using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using Util;

namespace LynnaLab
{
    /// Represents the 4 bytes defining a chest. There should only be one of these per room.
    public class Chest {
        Data dataStart;

        public event EventHandler<EventArgs> DeletedEvent;

        internal Chest(Data dataStart) {
            this.dataStart = dataStart;
            GenerateValueReferenceGroup();
        }


        // Properties

        public ValueReferenceGroup ValueReferenceGroup { get; private set; }
        public int TreasureID {
            get { return ValueReferenceGroup.GetIntValue("ID"); }
        }
        public int TreasureSubID {
            get { return ValueReferenceGroup.GetIntValue("SubID"); }
        }
        public int TreasureIndex {
            get { return TreasureID * 256 + TreasureSubID; }
        }
        public Treasure Treasure {
            get { return Project.GetIndexedDataType<Treasure>(TreasureIndex); }
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
    }
}
