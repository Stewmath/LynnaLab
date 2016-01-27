using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab {
    // Data macro "m_TilesetHeader"
    public class TilesetHeaderData : Data {

        Stream referencedData;

        public int DictionaryIndex {
            get { return Project.EvalToInt(GetValue(0)); }
        }
        public Stream ReferencedData {
            get {
                return referencedData;
            }
        }
        public int DestAddress {
            get { return Project.EvalToInt(GetValue(2)); }
        }
        public int DestBank {
            get { return Project.EvalToInt(":"+GetValue(2)); }
        }
        public int DataSize {
            get { return Project.EvalToInt(GetValue(3)); }
        }


        public TilesetHeaderData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<int> spacing) 
            : base(p, command, values, 8, parser, spacing)
        {
            try {
                referencedData = Project.GetBinaryFile("tilesets/" + GetValue(1) + ".bin");
            }
            catch (FileNotFoundException) {
                // Default is to copy from 00 I guess
                // TODO: copy this into its own file?
                string filename = GetValue(1).Substring(0, GetValue(1).Length-2);
                referencedData = Project.GetBinaryFile("tilesets/" + filename + "00.bin");
            }
        }

        public bool ShouldHaveNext() {
            return (Project.EvalToInt(GetValue(4)) & 0x80) == 0x80;
        }
    }

}
