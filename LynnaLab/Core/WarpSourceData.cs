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

        public static List<List<ValueReference>> warpValueReferences =
            new List<List<ValueReference>> {
                new List<ValueReference> { // StandardWarp
                    new ValueReference("Opcode",0,DataValueType.Byte),
                    new ValueReference("Map",1,DataValueType.Byte),
                    new ValueReference("Group",3,0,3,DataValueType.ByteBits),
                    new ValueReference("Entrance",3,0,3,DataValueType.ByteBits),
                    new ValueReference("Dest Index",2,DataValueType.Byte),
                },
                new List<ValueReference> { // PointedWarp
                    new ValueReference("Opcode",0,DataValueType.Byte),

                    // For "pointed" warp sources, "map" is instead a position
                    new ValueReference("Y",1,4,7,DataValueType.ByteBits),
                    new ValueReference("X",1,0,3,DataValueType.ByteBits),

                    new ValueReference("Group",3,0,3,DataValueType.ByteBits),
                    new ValueReference("Entrance",3,0,3,DataValueType.ByteBits),
                    new ValueReference("Dest Index",2,DataValueType.Byte),
                },
                new List<ValueReference> { // PointerWarp
                    new ValueReference("Opcode",0,DataValueType.Byte),
                    new ValueReference("Map",1,DataValueType.Byte),

                    // For warp sources which point to others, the pointer replaces
                    // Group/Entrance/Dest Index.
                    new ValueReference("Pointer", 2, DataValueType.String),
                },
                new List<ValueReference> { // WarpSourcesEnd
                    new ValueReference("Opcode",0,DataValueType.Byte),
                    new ValueReference("Map",1,DataValueType.Byte),
                    new ValueReference("Group",3,0,3,DataValueType.ByteBits),
                    new ValueReference("Entrance",3,0,3,DataValueType.ByteBits),
                    new ValueReference("Dest Index",2,DataValueType.Byte),
                }
            };


        WarpSourceType _type;

        public WarpSourceType WarpSourceType {
            get { return _type; }
        }

        public int Opcode {
            get {
                return GetIntValue("Opcode");
            }
            set {
                SetValue("Opcode",value);
            }
        }
        public int Map {
            get {
                return GetIntValue("Map");
            }
            set {
                SetValue("Map", value);
            }
        }
        public int Group {
            get {
                return GetIntValue("Group");
            }
            set {
                SetValue("Group",value);
            }
        }
        public int Entrance {
            get {
                return GetIntValue("Entrance");
            }
            set {
                SetValue("Entrance",value);
            }
        }
        public int DestIndex {
            get {
                return GetIntValue("DestIndex");
            }
            set {
                SetValue("DestIndex",value);
            }
        }
        public int X {
            get {
                return GetIntValue("X");
            }
            set {
                SetValue("X",value);
            }
        }
        public int Y {
            get {
                return GetIntValue("Y");
            }
            set {
                SetValue("Y",value);
            }
        }

        public WarpSourceData(Project p, string command, IList<string> values,
                FileParser parser, IList<int> spacing)
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

            SetValueReferences(warpValueReferences[(int)WarpSourceType]);
        }

        // If this is the kind of warp which points to another warp, return the
        // pointed warp, otherwise return null
        public WarpSourceData GetPointedWarp() {
            if (WarpSourceType != WarpSourceType.PointerWarp) return null;

            WarpSourceData data = (WarpSourceData)Project.GetData(GetValue("Pointer"));
            return data;
        }

        // If this is a WarpSourceData which is pointed to from another one,
        // return the next in the sequence, or null if the sequence is over.
        public WarpSourceData GetNextWarp() {
            if (WarpSourceType != WarpSourceType.PointedWarp) return null;

            // A warp with opcode bit 7 set signals the end of the sequence
            if ((Opcode & 0x80) == 0) return null;

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
    }
}
