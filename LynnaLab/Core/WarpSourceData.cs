using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public enum WarpSourceType {
        StandardWarp=0,
        PointerWarp,
        WarpSourcesEnd
    };

    public class WarpSourceData : Data
    {
        public static string[] WarpCommands = {
            "m_StandardWarp",
            "m_PointerWarp",
            "m_WarpSourcesEnd"
        };


        WarpSourceType _type;

        public WarpSourceType WarpSourceType {
            get { return _type; }
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

        }


    }
}
