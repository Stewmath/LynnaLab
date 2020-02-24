using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class WarpDestData : Data
    {
        public static string WarpCommand = "m_WarpDest";

        public static List<ValueReference> warpValueReferences = 
            new List<ValueReference> {
                new ValueReference("Map",0,DataValueType.Byte),
                new ValueReference("Y",1,4,7,DataValueType.ByteBits),
                new ValueReference("X",1,0,3,DataValueType.ByteBits),
                new ValueReference("Parameter",2,DataValueType.HalfByte),
                new ValueReference("Transition",3,DataValueType.HalfByte,true,"DestTransitionMapping"),
            };

        // Properties

        public int Map {
            get {
                return GetIntValue("Map");
            }
            set {
                SetValue("Map", value);
            }
        }
        public int Transition {
            get {
                return GetIntValue("Transition");
            }
            set {
                SetValue("Transition", value);
            }
        }
        // Don't edit these properties outside of the WarpDestGroup class
        public WarpDestGroup DestGroup {get; set;}
        public int DestIndex {get; set;}


        // Variables

        HashSet<WarpSourceData> referenceSet;


        public WarpDestData(Project p, string command, IEnumerable<string> values,
                FileParser parser, IList<string> spacing)
            : base(p, command, values, 3, parser, spacing)
        {
            SetValueReferences(warpValueReferences);

            referenceSet = new HashSet<WarpSourceData>();

            DestGroup = null;
            DestIndex = -1;
        }

        public void AddReference(WarpSourceData data) {
            referenceSet.Add(data);
        }

        public void RemoveReference(WarpSourceData data) {
            referenceSet.Remove(data);
        }

        public int GetNumReferences() {
            return referenceSet.Count;
        }
    }
}
