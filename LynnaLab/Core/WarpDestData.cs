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
                new ValueReference("YX",1,DataValueType.Byte),
                new ValueReference("Unknown",2,DataValueType.Byte),
            };

        // Properties

        // Don't edit these properties outside of the WarpDestGroup class
        public WarpDestGroup DestGroup {get; set;}
        public int DestIndex {get; set;}


        // Variables

        HashSet<WarpSourceData> referenceSet;


        public WarpDestData(Project p, string command, IList<string> values,
                FileParser parser, IList<int> spacing)
            : base(p, command, values, 3, parser, spacing)
        {
            SetValueReferences(warpValueReferences);

            referenceSet = new HashSet<WarpSourceData>();
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
