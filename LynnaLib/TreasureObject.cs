using System;
using System.Collections.Generic;
using Util;

namespace LynnaLib
{
    /// Represents a "treasure object", which is a treasure with additional properties including
    /// graphics, text, etc. (see "data/{game}/treasureObjectData.s".)
    /// Has an "ID" (the treasure index) and an additional "subID" representing a "variant" of the
    /// treasure.
    public class TreasureObject : TrackedProjectData
    {
        class State : TransactionState
        {
            public required int SubID { get; init; }
            public required InstanceResolver<TreasureGroup> treasureGroup { get; init; }
            public required InstanceResolver<Data> baseData { get; init; }
        }

        State state;
        ValueReferenceGroup vrg;


        internal TreasureObject(TreasureGroup treasureGroup, string identifier, int subid, Data baseData)
            : base(treasureGroup.Project, identifier)
        {
            state = new()
            {
                SubID = subid,
                treasureGroup = new(treasureGroup),
                baseData = new(baseData),
            };

            // Check if index is too high
            if (ID >= Project.NumTreasures)
                throw new InvalidTreasureException(string.Format("ID {0:X2} for treasure was too high!", ID));

            GenerateValueReferenceGroup();

            // Add the treasure definition to the Project's definition list and to the
            // TreasureObjectMapping.
            Project.AddDefinition(Name, Wla.ToWord((ID << 8) | SubID));
            Project.TreasureObjectMapping.AddKeyValuePair(Name, (ID << 8) | SubID);
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private TreasureObject(Project p, string id, TransactionState state)
            : base(p, id)
        {
            this.state = (State)state;
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
            get { return state.treasureGroup.Instance.Index; }
        }
        public int SubID
        {
            get { return state.SubID; }
        }

        public string Name
        {
            get { return BaseData.GetValue(4); }
        }

        // Allows one to get/set the raw value of the 1st byte directly (spawn mode, grab mode, "set
        // item obtained" bit) instead of using the ValueReferenceGroup.
        public byte CollectByte
        {
            get
            {
                return (byte)BaseData.GetIntValue(0);
            }
            set
            {
                // Bypassing the ValueReferenceGroup shouldn't cause any problems with its modified
                // handler since we're not using AbstractIntValueReference.
                BaseData.SetByteValue(0, value);
            }
        }

        public string TransactionIdentifier { get { return $"treasureobject-{ID:X2}-{SubID:X2}"; } }


        Data BaseData { get { return state.baseData.Instance; } }


        // Private methods

        void GenerateValueReferenceGroup()
        {
            vrg = new ValueReferenceGroup(new ValueReferenceDescriptor[] {
                DataValueReference.Descriptor(BaseData,
                        index: 0,
                        name: "Spawn Mode",
                        type: DataValueType.ByteBits,
                        startBit: 4,
                        endBit: 6,
                        constantsMappingString: "TreasureSpawnModeMapping"),
                DataValueReference.Descriptor(BaseData,
                        index: 0,
                        name: "Grab Mode",
                        type: DataValueType.ByteBits,
                        startBit: 0,
                        endBit: 2,
                        constantsMappingString: "TreasureGrabModeMapping"),
                DataValueReference.Descriptor(BaseData,
                        index: 1,
                        name: "Parameter",
                        type: DataValueType.Byte),
                DataValueReference.Descriptor(BaseData,
                        index: 2,
                        name: "Text Index",
                        type: DataValueType.String,
                        tooltip: "Will show text \"TX_00XX\" when you open the chest."),
                DataValueReference.Descriptor(BaseData,
                        index: 3,
                        name: "Graphics",
                        type: DataValueType.Byte),
                DataValueReference.Descriptor(BaseData,
                        index: 0,
                        name: "Set 'Item Obtained' Flag",
                        type: DataValueType.ByteBit,
                        startBit: 3,
                        tooltip: "Sets a flag indicating that an item has been received in this room. In the case of chests, this will make the chest \"opened\" when you revisit the room. (Flag is named \"ROOMFLAG_ITEM\" in the disassembly.)"),
            });

            vrg.EnableTransactions("Edit treasure object#" + TransactionIdentifier, true);
        }

        // ================================================================================
        // TrackedProjectState implementation
        // ================================================================================

        public override TransactionState GetState()
        {
            return state;
        }
        public override void SetState(TransactionState state)
        {
            this.state = (State)state;
        }
        public override void InvokeUndoEvents(TransactionState oldState)
        {
        }
        public override void OnInitializedFromTransfer()
        {
            // Can't put this in the state-based constructor since it involves dereferencing
            // "BaseData".
            GenerateValueReferenceGroup();
        }
    }
}
