using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class WarpDestData : Data
    {
        public static string WarpCommand = "m_WarpDest";


        private static List<ValueReference> GetWarpValueReferences(Data data) {
            return new List<ValueReference> {
                new DataValueReference(data,"Map",0,DataValueType.Byte),
                new DataValueReference(data,"Y",1,DataValueType.ByteBits,4,7),
                new DataValueReference(data,"X",1,DataValueType.ByteBits,0,3),
                new DataValueReference(data,"Parameter",2,DataValueType.HalfByte),
                new DataValueReference(data,"Transition",3,DataValueType.HalfByte,
                    constantsMappingString:"DestTransitionMapping"),
            };
        }


        // Private variables

        HashSet<WarpSourceData> referenceSet;
        ValueReferenceGroup vrg;


        // Properties

        public int Map {
            get {
                return vrg.GetIntValue("Map");
            }
            set {
                vrg.SetValue("Map", value);
            }
        }
        public int Transition {
            get {
                return vrg.GetIntValue("Transition");
            }
            set {
                vrg.SetValue("Transition", value);
            }
        }
        public ValueReferenceGroup ValueReferenceGroup {
            get { return vrg; }
        }

        public int Group {
            get { return DestGroup.Index; }
        }

        // Don't edit these properties outside of the WarpDestGroup class (TODO: review this)
        internal WarpDestGroup DestGroup {get; set;}
        internal int DestIndex {get; set;}


        public WarpDestData(Project p, string command, IEnumerable<string> values,
                FileParser parser, IList<string> spacing)
            : base(p, command, values, 3, parser, spacing)
        {
            vrg = new ValueReferenceGroup(GetWarpValueReferences(this));

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
