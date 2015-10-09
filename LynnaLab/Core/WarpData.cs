using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public enum WarpDataTypes {
        StandardWarp=0,
        PointerWarp,
        WarpSourcesEnd
    };

    public class WarpData : Data
    {
        public static string[] WarpCommands = {
            "m_StandardWarp",
            "m_PointerWarp",
            "m_WarpSourcesEnd"
        };

        public WarpData(Project p, string command, IList<string> values,
                FileParser parser, IList<int> spacing)
            : base(p, command, values, -1, parser, spacing)
        {
        }


    }
}
