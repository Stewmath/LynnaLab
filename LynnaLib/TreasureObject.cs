using System;
using System.Collections.Generic;
using Util;

namespace LynnaLib
{
    /// Represents a "treasure object", which is a treasure with additional properties including
    /// graphics, text, etc. (see "data/{game}/treasureObjectData.s".)
    /// Has an "ID" (the treasure index) and an additional "subID" representing a "variant" of the
    /// treasure.
    public class TreasureObject
    {
        readonly TreasureGroup treasureGroup;
        readonly Data baseData;
        ValueReferenceGroup vrg;

        internal TreasureObject(TreasureGroup treasureGroup, int subid, Data baseData)
        {
            this.treasureGroup = treasureGroup;
            this.SubID = subid;

            // Check if index is too high
            if (ID >= Project.NumTreasures)
                throw new InvalidTreasureException(string.Format("ID {0:X2} for treasure was too high!", ID));

            this.baseData = baseData;
            GenerateValueReferenceGroup();

            // Add the treasure definition to the Project's definition list and to the
            // TreasureObjectMapping.
            Project.AddDefinition(Name, Wla.ToWord((ID << 8) | SubID));
            Project.TreasureObjectMapping.AddKeyValuePair(Name, (ID << 8) | SubID);
        }


        // Properties

        public ValueReferenceGroup ValueReferenceGroup
        {
            get { return vrg; }
        }

        public int Graphics
        {
            get { return vrg.GetIntValue("Graphics"); }
        }

        public int ID
        {
            get { return treasureGroup.Index; }
        }
        public int SubID
        {
            get; private set;
        }

        public string Name
        {
            get { return baseData.GetValue(4); }
        }

        // Allows one to get/set the raw value of the 1st byte directly (spawn mode, grab mode, "set
        // item obtained" bit) instead of using the ValueReferenceGroup.
        public byte CollectByte
        {
            get
            {
                return (byte)baseData.GetIntValue(0);
            }
            set
            {
                // Bypassing the ValueReferenceGroup shouldn't cause any problems with its modified
                // handler since we're not using AbstractIntValueReference.
                baseData.SetByteValue(0, value);
            }
        }

        public string TransactionIdentifier { get { return $"treasureobject-{ID:X2}-{SubID:X2}"; } }


        Project Project { get { return treasureGroup.Project; } }


        // Private methods

        void GenerateValueReferenceGroup()
        {
            vrg = new ValueReferenceGroup(new ValueReferenceDescriptor[] {
                DataValueReference.Descriptor(baseData,
                        index: 0,
                        name: "Spawn Mode",
                        type: DataValueType.ByteBits,
                        startBit: 4,
                        endBit: 6,
                        constantsMappingString: "TreasureSpawnModeMapping"),
                DataValueReference.Descriptor(baseData,
                        index: 0,
                        name: "Grab Mode",
                        type: DataValueType.ByteBits,
                        startBit: 0,
                        endBit: 2,
                        constantsMappingString: "TreasureGrabModeMapping"),
                DataValueReference.Descriptor(baseData,
                        index: 1,
                        name: "Parameter",
                        type: DataValueType.Byte),
                DataValueReference.Descriptor(baseData,
                        index: 2,
                        name: "Text Index",
                        type: DataValueType.String,
                        tooltip: "Will show text \"TX_00XX\" when you open the chest."),
                DataValueReference.Descriptor(baseData,
                        index: 3,
                        name: "Graphics",
                        type: DataValueType.Byte),
                DataValueReference.Descriptor(baseData,
                        index: 0,
                        name: "Set 'Item Obtained' Flag",
                        type: DataValueType.ByteBit,
                        startBit: 3,
                        tooltip: "Sets a flag indicating that an item has been received in this room. In the case of chests, this will make the chest \"opened\" when you revisit the room. (Flag is named \"ROOMFLAG_ITEM\" in the disassembly.)"),
            });

            vrg.EnableTransactions("Edit treasure object#" + TransactionIdentifier, true);
        }
    }
}
