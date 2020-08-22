using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using Util;

namespace LynnaLab
{
    /// Represents an instance of INTERACID_TREASURE.
    /// Index is the ID (upper byte) and the subID (lower byte) corresponding to a chest. When
    /// spawning INTERACID_TREASURE, the ID becomes the subID and the subID is written to "var03".
    public class Treasure : ProjectIndexedDataType {
        Data baseData;
        ValueReferenceGroup vrg;
        bool usesPointer;

        internal Treasure(Project p, int i) : base(p, i) {
            // TODO: Check if Index is too high, handle it somehow
            Data data = Project.GetData("treasureObjectData", ID * 4);

            if ((data.GetIntValue(0) & 0x80) != 0)
                usesPointer = true;
            if (SubID != 0 && !usesPointer)
                InvalidSubidException();

            if (usesPointer) {
                data = Project.GetData(data.NextData.GetValue(0)); // Follow pointer
                int count = SubID;
                while (count > 0) {
                    FileComponent com = data;
                    for (int j=0; j<4;) {
                        com = com.Next;
                        if (com is Data)
                            j++;
                        else if (com is Label || com == null)
                            InvalidSubidException();
                    }
                    data = com as Data;
                    count--;
                }
            }

            baseData = data;
            GenerateValueReferenceGroup();
        }


        // Properties

        public ValueReferenceGroup ValueReferenceGroup {
            get { return vrg; }
        }

        public int Graphics {
            get { return vrg.GetIntValue("Graphics"); }
        }

        int ID {
            get { return Index >> 8; }
        }
        int SubID {
            get { return Index & 0xff; }
        }


        // Private methods

        void GenerateValueReferenceGroup() {
            vrg = new ValueReferenceGroup(new ValueReference[] {
                new DataValueReference(baseData,
                        index: 0,
                        name: "Spawn Mode",
                        type: DataValueType.ByteBits,
                        startBit: 4,
                        endBit: 6,
                        constantsMappingString: "TreasureSpawnModeMapping"),
                new DataValueReference(baseData,
                        index: 0,
                        name: "Set 'Item Obtained' Flag",
                        type: DataValueType.ByteBit,
                        startBit: 3,
                        tooltip: "Sets a flag indicating that an item has been received in this room. In the case of chests, this will make the chest \"opened\" when you revisit the room. (Flag is named \"ROOMFLAG_ITEM\" in the disassembly.)"),
                new DataValueReference(baseData,
                        index: 0,
                        name: "Grab Mode",
                        type: DataValueType.ByteBits,
                        startBit: 0,
                        endBit: 2,
                        constantsMappingString: "TreasureGrabModeMapping"),
                new DataValueReference(baseData.NextData,
                        index: 0,
                        name: "Parameter",
                        type: DataValueType.Byte),
                new DataValueReference(baseData.NextData.NextData,
                        index: 0,
                        name: "Text Index",
                        type: DataValueType.Byte,
                        tooltip: "Will show text \"TX_00XX\" when you open the chest."),
                new DataValueReference(baseData.NextData.NextData.NextData,
                        index: 0,
                        name: "Graphics",
                        type: DataValueType.Byte),
            });
        }

        void InvalidSubidException() {
            throw new InvalidTreasureException(string.Format("SubID {0:X2} for treasure {1:X2} was invalid!", SubID, ID));
        }
    }
}
