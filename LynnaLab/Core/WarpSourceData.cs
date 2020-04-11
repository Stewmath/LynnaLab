using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public enum WarpSourceType {
        StandardWarp=0,
        PointedWarp, // An m_StandardWarp referenced by a PointerWarp
        PointerWarp,
        WarpSourcesEnd,
    };

    public class WarpSourceData : Data
    {
        public static string[] WarpCommands = {
            "m_StandardWarp",
            "m_PointedWarp",
            "m_PointerWarp",
            "m_WarpSourcesEnd"
        };

        public static List<string>[] DefaultValues = {
            new List<string> { // StandardWarp
                "$00",
                "$00",
                "$00",
                "$0",
                "$4", // Instant fade
            },
            new List<string> { // PointedWarp
                "$00",
                "$00",
                "$00",
                "$0",
                "$4", // Instant fade
            },
            new List<string> { // PointerWarp
                "$40",
                "$00",
                "."
            },
            new List<string> { // WarpSourcesEnd
            }
        };

        private static List<ValueReference> GetWarpValueReferences(WarpSourceType type, Data data) {
            switch (type) {
            case WarpSourceType.StandardWarp:
                return new List<ValueReference> { // StandardWarp
                    new DataValueReference(data,"Opcode",0,DataValueType.Byte, editable:false),
                    new DataValueReference(data,"Top-Left",0,DataValueType.ByteBit,0,0),
                    new DataValueReference(data,"Top-Right",0,DataValueType.ByteBit,1,1),
                    new DataValueReference(data,"Bottom-Left",0,DataValueType.ByteBit,2,2),
                    new DataValueReference(data,"Bottom-Right",0,DataValueType.ByteBit,3,3),
                    new DataValueReference(data,"Map",1,DataValueType.Byte, editable:false),
                    new DataValueReference(data,"Dest Index",2,DataValueType.WarpDestIndex),
                    new DataValueReference(data,"Dest Group",3,DataValueType.HalfByte),
                    new DataValueReference(data,"Transition",4,DataValueType.HalfByte,
                        constantsMappingString:"SourceTransitionMapping"),
                };
            case WarpSourceType.PointedWarp:
                return new List<ValueReference> { // PointedWarp
                    new DataValueReference(data,"Opcode",0,DataValueType.Byte, editable:false),

                    // For "pointed" warp sources, "map" is instead a position
                    new DataValueReference(data,"Y",1,DataValueType.ByteBits,4,7),
                    new DataValueReference(data,"X",1,DataValueType.ByteBits,0,3),

                    new DataValueReference(data,"Dest Index",2,DataValueType.WarpDestIndex),
                    new DataValueReference(data,"Dest Group",3,DataValueType.HalfByte),
                    new DataValueReference(data,"Transition",4,DataValueType.HalfByte,
                        constantsMappingString:"SourceTransitionMapping"),
                };
            case WarpSourceType.PointerWarp:
                return new List<ValueReference> { // PointerWarp
                    new DataValueReference(data,"Opcode",0,DataValueType.Byte, editable:false),
                    new DataValueReference(data,"Map",1,DataValueType.Byte, editable:false),

                    // For warp sources which point to others, the pointer replaces
                    // Group/Entrance/Dest Index.
                    new DataValueReference(data,"Pointer", 2, DataValueType.String, editable:false),
                };
            case WarpSourceType.WarpSourcesEnd:
                return new List<ValueReference> { // WarpSourcesEnd
                };
            }

            return null;
        }




        // Private variables
        WarpDestData referencedDestData;

        WarpSourceType _type;
        ValueReferenceGroup vrg;


        // Properties

        public WarpSourceType WarpSourceType {
            get { return _type; }
        }
        public ValueReferenceGroup ValueReferenceGroup {
            get { return vrg; }
        }

        public int Opcode {
            get {
                return vrg.GetIntValue("Opcode");
            }
            set {
                vrg.SetValue("Opcode",value);
            }
        }
        public int Map {
            get {
                try {
                    return vrg.GetIntValue("Map");
                }
                catch (InvalidLookupException) {
                    return -1;
                }
            }
            set {
                vrg.SetValue("Map", value);
            }
        }
        public int DestIndex {
            get {
                try {
                    return vrg.GetIntValue("Dest Index");
                }
                catch (InvalidLookupException) {
                    return -1;
                }
            }
            set {
                vrg.SetValue("Dest Index",value);
            }
        }
        public int DestGroup {
            get {
                try {
                    return vrg.GetIntValue("Dest Group");
                }
                catch (InvalidLookupException) {
                    return -1;
                }
            }
            set {
                vrg.SetValue("Dest Group",value);
            }
        }
        public int Transition {
            get {
                try {
                    return vrg.GetIntValue("Transition");
                }
                catch (InvalidLookupException) {
                    return -1;
                }
            }
            set {
                vrg.SetValue("Transition",value);
            }
        }
        public int X {
            get {
                try {
                    return vrg.GetIntValue("X");
                }
                catch (InvalidLookupException) {
                    return -1;
                }
            }
            set {
                vrg.SetValue("X",value);
            }
        }
        public int Y {
            get {
                try {
                    return vrg.GetIntValue("Y");
                }
                catch (InvalidLookupException) {
                    return -1;
                }
            }
            set {
                vrg.SetValue("Y",value);
            }
        }
        public string PointerString {
            get {
                return vrg.GetValue("Pointer");
            }
            set {
                vrg.SetValue("Pointer",value);
            }
        }


        public WarpSourceData(Project p, string command, IEnumerable<string> values,
                FileParser parser, IList<string> spacing)
            : base(p, command, values, -1, parser, spacing)
        {
            // Find type
            for (int i=0; i<WarpCommands.Length; i++) {
                string s = WarpCommands[i];
                if (this.CommandLowerCase == s.ToLower()) {
                    _type = (WarpSourceType)i;
                    break;
                }
            }

            vrg = new ValueReferenceGroup(GetWarpValueReferences(_type, this));

            referencedDestData = GetReferencedDestData();
            if (referencedDestData != null)
                referencedDestData.AddReference(this);

            this.AddModifiedEventHandler(delegate(object sender, DataModifiedEventArgs args) {
                WarpDestData newDestData = GetReferencedDestData();
                if (newDestData != referencedDestData) {
                    // Update DestData reference
                    if (referencedDestData != null)
                        referencedDestData.RemoveReference(this);
                    referencedDestData = newDestData;
                    if (newDestData != null)
                        newDestData.AddReference(this);
                }
            });
        }

        // If this is the kind of warp which points to another warp, return the
        // pointed warp, otherwise return null
        public WarpSourceData GetPointedWarp() {
            if (WarpSourceType != WarpSourceType.PointerWarp)
                throw new ArgumentException("Invalid warp type for 'GetPointedWarp' call.");

            WarpSourceData data = Project.GetData(vrg.GetValue("Pointer")) as WarpSourceData;
            return data;
        }

        // If this is a WarpSourceData which is pointed to from another one,
        // return the next in the sequence, or null if the sequence is over.
        public WarpSourceData GetNextWarp() {
            if (WarpSourceType != WarpSourceType.PointedWarp)
                throw new ArgumentException("Invalid warp type for 'GetNextWarp' call.");

            // A warp with opcode bit 7 set signals the end of the sequence
            if ((Opcode & 0x80) != 0) return null;

            FileComponent next = Next;
            while (next != null) {
                // This condition is a bit weird, but the game doesn't always
                // end with a 0x80 opcode, so I need another way to discern the
                // endpoint
                if (next is Label) return null;

                if (next is Data) return next as WarpSourceData;

                next = next.Next;
            }

            return null;
        }

        // Returns the number of PointedWarps there are after and including
        // this one. This is the number of times (plus one) that you can call
        // GetNextWarp() before you get a null value.
        //
        // If called on a PointerWarp, it returns the corresponding value for
        // its PointedWarp.
        public int GetPointedChainLength() {
            if (WarpSourceType == WarpSourceType.PointerWarp)
                return GetPointedWarp().GetPointedChainLength();
            else if (WarpSourceType != WarpSourceType.PointedWarp)
                throw new ArgumentException("Invalid warp type for 'GetPointedChainLength' call.");

            WarpSourceData next = GetNextWarp();
            if (next == null) return 1;

            return 1+next.GetPointedChainLength();
        }

        // Returns the WarpSourceData object that's "index" entries after this one.
        // (Assumes this is a PointedWarp or PointerWarp..)
        public WarpSourceData TraversePointedChain(int count) {
            if (WarpSourceType == WarpSourceType.PointerWarp)
                return GetPointedWarp().TraversePointedChain(count);
            else if (WarpSourceType != WarpSourceType.PointedWarp)
                throw new ArgumentException("Invalid warp type for 'TraversePointedWarpChain' call.");

            if (count == 0)
                return this;
            return GetNextWarp().TraversePointedChain(count-1);
        }

        public WarpDestData GetReferencedDestData() {
            WarpDestGroup group = GetReferencedDestGroup();
            if (group == null) return null;

            try {
                return group.GetWarpDest(DestIndex);
            }
            catch (ArgumentOutOfRangeException) {
                return null;
            }
        }

        public WarpDestGroup GetReferencedDestGroup() {
            if (_type == WarpSourceType.PointerWarp ||
                    _type == WarpSourceType.WarpSourcesEnd)
                return null;
            if (DestGroup >= Project.GetNumGroups())
                return null;
            return Project.GetIndexedDataType<WarpDestGroup>(DestGroup);
        }

        // Set the WarpDestData associated with this source, setting DestIndex
        // and DestGroup appropriately
        public void SetDestData(WarpDestData data) {
            DestIndex = data.DestIndex;
            DestGroup = data.DestGroup.Index;
            // The handler defined in the constructor will update the
            // referencedData variable
        }
    }
}
