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

        public WarpDestData(Project p, string command, IList<string> values,
                FileParser parser, IList<int> spacing)
            : base(p, command, values, 3, parser, spacing)
        {
            SetValueReferences(warpValueReferences);
        }
    }
}
