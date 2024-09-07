using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLib
{
    public enum WarpSourceType
    {
        Standard = 0,
        Position, // An m_StandardWarp referenced by a PointerWarp
        Pointer,
        EndNoDefault,
        EndWithDefault,
        FallThrough,
    };

    public class WarpSourceData : Data
    {
        public static string[] WarpCommands = {
            "m_StandardWarp",
            "m_PositionWarp",
            "m_PointerWarp",
            "m_WarpListEndNoDefault",
            "m_WarpListEndWithDefault",

            // The m_WarpListFallThrough opcode is used when the data list should end, but the
            // devs neglected to add the m_WarpListEndWithDefault opcode. So we treat both opcodes the
            // same for the purpose of parsing.
            "m_WarpListFallThrough",
        };

        public static List<string>[] DefaultValues = {
            new List<string> { // StandardWarp
                "$0",
                "$00",
                "$00",
                "$0",
                "$4", // Instant fade
            },
            new List<string> { // PositionWarp
                "$00",
                "$00",
                "$0",
                "$4", // Instant fade
            },
            new List<string> { // PointerWarp
                "$00",
                "."
            },
            new List<string> { // WarpListEndNoDefault
            },
            new List<string> { // WarpListEndWithDefault
            },
        };

        private static List<ValueReferenceDescriptor> GetWarpDescriptors(WarpSourceType type, Data data)
        {
            switch (type)
            {
                case WarpSourceType.Standard:
                    return new List<ValueReferenceDescriptor> { // StandardWarp
                        DataValueReference.Descriptor(data,"Top-Left",0,DataValueType.ByteBit,0,0),
                        DataValueReference.Descriptor(data,"Top-Right",0,DataValueType.ByteBit,1,1),
                        DataValueReference.Descriptor(data,"Bottom-Left",0,DataValueType.ByteBit,2,2),
                        DataValueReference.Descriptor(data,"Bottom-Right",0,DataValueType.ByteBit,3,3),
                        DataValueReference.Descriptor(data,"Map",1,DataValueType.Byte, editable:false),
                        DataValueReference.Descriptor(data,"Dest Index",2,DataValueType.Byte),
                        DataValueReference.Descriptor(data,"Dest Group",3,DataValueType.HalfByte),
                        DataValueReference.Descriptor(data,"Transition",4,DataValueType.HalfByte,
                                                      constantsMappingString: "SourceTransitionMapping"),
                };
                case WarpSourceType.Position:
                    return new List<ValueReferenceDescriptor> { // PositionWarp
                        DataValueReference.Descriptor(data,"Y",0,DataValueType.ByteBits,4,7),
                        DataValueReference.Descriptor(data,"X",0,DataValueType.ByteBits,0,3),

                        DataValueReference.Descriptor(data,"Dest Index",1,DataValueType.Byte),
                        DataValueReference.Descriptor(data,"Dest Group",2,DataValueType.HalfByte),
                        DataValueReference.Descriptor(data,"Transition",3,DataValueType.HalfByte,
                                                      constantsMappingString: "SourceTransitionMapping"),
                };
                case WarpSourceType.Pointer:
                    return new List<ValueReferenceDescriptor> { // PointerWarp
                        DataValueReference.Descriptor(data,"Map",0,DataValueType.Byte,
                                                      editable: false),

                        // For warp sources which point to others, the pointer replaces
                        // Group/Entrance/Dest Index.
                        DataValueReference.Descriptor(data,"Pointer", 1, DataValueType.String,
                                                      editable: false),
                };
                case WarpSourceType.EndNoDefault:
                case WarpSourceType.EndWithDefault:
                case WarpSourceType.FallThrough:
                    return new List<ValueReferenceDescriptor>
                    {
                    };
            }

            return null;
        }




        // Private variables
        WarpDestData referencedDestData;

        WarpSourceType _type;
        ValueReferenceGroup vrg;


        // Properties

        public WarpSourceType WarpSourceType
        {
            get { return _type; }
        }
        public ValueReferenceGroup ValueReferenceGroup
        {
            get { return vrg; }
        }

        public bool TopLeft
        {
            get
            {
                return vrg.GetIntValue("Top-Left") != 0;
            }
            set
            {
                vrg.SetValue("Top-Left", value ? 1 : 0);
            }
        }
        public bool TopRight
        {
            get
            {
                return vrg.GetIntValue("Top-Right") != 0;
            }
            set
            {
                vrg.SetValue("Top-Right", value ? 1 : 0);
            }
        }
        public bool BottomLeft
        {
            get
            {
                return vrg.GetIntValue("Bottom-Left") != 0;
            }
            set
            {
                vrg.SetValue("Bottom-Left", value ? 1 : 0);
            }
        }
        public bool BottomRight
        {
            get
            {
                return vrg.GetIntValue("Bottom-Right") != 0;
            }
            set
            {
                vrg.SetValue("Bottom-Right", value ? 1 : 0);
            }
        }
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
        public int DestIndex
        {
            get
            {
                return vrg.GetIntValue("Dest Index");
            }
            private set
            {
                vrg.SetValue("Dest Index", value);
            }
        }
        public int DestGroupIndex
        {
            get
            {
                return vrg.GetIntValue("Dest Group");
            }
            private set
            {
                vrg.SetValue("Dest Group", value);
            }
        }
        public WarpDestGroup DestGroup
        {
            get { return Project.GetIndexedDataType<WarpDestGroup>(DestGroupIndex); }
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
        public string PointerString
        {
            get
            {
                return vrg.GetValue("Pointer");
            }
            set
            {
                vrg.SetValue("Pointer", value);
            }
        }

        // We're counting "FallThrough" as an end opcode even though it technically isn't, as it's
        // used in places where the devs seemingly forgot to insert an end opcode.
        public bool IsEndOpcode
        {
            get
            {
                return _type == WarpSourceType.EndNoDefault
                    || _type == WarpSourceType.EndWithDefault
                    || _type == WarpSourceType.FallThrough;
            }
        }


        public WarpSourceData(Project p, string command, IEnumerable<string> values,
                FileParser parser, IList<string> spacing)
            : base(p, command, values, -1, parser, spacing)
        {
            // Find type
            for (int i = 0; i < WarpCommands.Length; i++)
            {
                string s = WarpCommands[i];
                if (this.CommandLowerCase == s.ToLower())
                {
                    _type = (WarpSourceType)i;
                    break;
                }
            }

            vrg = new ValueReferenceGroup(GetWarpDescriptors(_type, this));

            referencedDestData = GetReferencedDestData();
            if (referencedDestData != null)
                referencedDestData.AddReference(this);

            this.AddModifiedEventHandler((sender, args) => Sanitize());
            Sanitize();

            this.AddModifiedEventHandler(delegate (object sender, DataModifiedEventArgs args)
            {
                WarpDestData newDestData = GetReferencedDestData();
                if (newDestData != referencedDestData)
                {
                    // Update DestData reference
                    if (referencedDestData != null)
                        referencedDestData.RemoveReference(this);
                    referencedDestData = newDestData;
                    if (newDestData != null)
                        newDestData.AddReference(this);
                }
            });
        }

        // If this is the kind of warp which points to another warp, return the pointed warp,
        // otherwise return null
        public WarpSourceData GetPointedWarp()
        {
            if (WarpSourceType != WarpSourceType.Pointer)
                throw new ArgumentException("Invalid warp type for 'GetPointedWarp' call.");

            WarpSourceData data = Project.GetData(vrg.GetValue("Pointer")) as WarpSourceData;
            return data;
        }

        // If this is a WarpSourceData which is pointed to from another one,
        // return the next in the sequence, or null if the sequence is over.
        public WarpSourceData GetNextWarp()
        {
            // Check for m_WarpListEndNoDefault here (on top of m_WarpListEndWithDefault) as it's used
            // inappropriately in a few places
            if (IsEndOpcode)
            {
                return null;
            }
            else if (WarpSourceType != WarpSourceType.Position)
            {
                throw new ArgumentException("Invalid warp type for 'GetNextWarp' call.");
            }

            FileComponent next = Next;
            while (next != null)
            {
                if (next is WarpSourceData)
                {
                    return next as WarpSourceData;
                }
                else if (next is Label || next is Data)
                {
                    throw new ProjectErrorException("Warp source data missing m_WarpListEnd?");
                }

                // For any other FileComponent, just keep going
                next = next.Next;
            }

            return null;
        }

        // For PointerWarps only, get the number of warps that it points to. (Does not include the
        // end opcode.)
        public int GetNumPointedWarps()
        {
            if (WarpSourceType != WarpSourceType.Pointer)
                throw new ArgumentException("Invalid warp type for 'GetNumPointedWarps' call.");

            return GetPointedWarp().GetPointedChainLength() - 1;
        }

        // Returns the WarpSourceData object that's "index" entries after this one.
        // (Assumes this is a PositionWarp or PointerWarp..)
        public WarpSourceData TraversePointedChain(int count)
        {
            if (WarpSourceType == WarpSourceType.Pointer)
                return GetPointedWarp().TraversePointedChain(count);
            else if (WarpSourceType != WarpSourceType.Position)
                throw new ArgumentException("Invalid warp type for 'TraversePointedWarpChain' call.");

            if (count == 0)
                return this;
            return GetNextWarp().TraversePointedChain(count - 1);
        }

        public WarpDestData GetReferencedDestData()
        {
            WarpDestGroup group = GetReferencedDestGroup();
            if (group == null) return null;

            try
            {
                return group.GetWarpDest(DestIndex);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        public WarpDestGroup GetReferencedDestGroup()
        {
            if (_type == WarpSourceType.Pointer || IsEndOpcode)
                return null;
            return Project.GetIndexedDataType<WarpDestGroup>(DestGroupIndex);
        }

        // Set the WarpDestData associated with this source, setting DestIndex and DestGroup
        // appropriately
        public void SetDestData(WarpDestData data)
        {
            LockModifiedEvents();
            DestGroupIndex = data.DestGroup.Index;
            DestIndex = data.DestIndex;
            UnlockModifiedEvents();
            // The handler defined in the constructor will update the referencedData variable
        }

        // This hides the annoyance of the "DestData" intermediate layer
        public Room GetDestRoom()
        {
            WarpDestData destData = GetReferencedDestData();
            return Project.GetIndexedDataType<Room>((destData.Group << 8) + destData.Map);
        }


        // Private functions

        // For PositionWarps only, returns the number of PositionWarps there are after and including
        // this one. This is the number of times (plus one) that you can call GetNextWarp() before
        // you get a null value. This INCLUDES the end opcode.
        //
        // If called on a PointerWarp, it returns the corresponding value for its PositionWarp.
        int GetPointedChainLength()
        {
            if (IsEndOpcode)
                return 1;
            else if (WarpSourceType != WarpSourceType.Position)
                throw new ArgumentException("Invalid warp type for 'GetPointedChainLength' call.");

            WarpSourceData next = GetNextWarp();
            if (next == null)
            {
                // Sanity check: Last entry should be an end opcode
                throw new ProjectErrorException("Warp source data missing m_WarpListEnd?");
            }

            return 1 + next.GetPointedChainLength();
        }


        // Make sure there are no surprises
        void Sanitize()
        {
            if (WarpSourceType == WarpSourceType.Standard || WarpSourceType == WarpSourceType.Position)
            {
                if (DestGroupIndex >= Project.NumGroups)
                {
                    throw new AssemblyErrorException("Dest group for warp too high: \"" + GetString().Trim() + "\".");
                }
                if (DestIndex >= DestGroup.GetNumWarpDests())
                {
                    throw new AssemblyErrorException("Dest index for warp too high: \"" + GetString().Trim() + "\".");
                }
            }
        }
    }
}
