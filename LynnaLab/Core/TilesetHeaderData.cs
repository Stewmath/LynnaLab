using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab {
    // Data macro "m_TilesetHeader"
	public class TilesetHeaderData : Data {

        Stream referencedData;

        public int DictionaryIndex {
            get { return Project.EvalToInt(Values[0]); }
        }
        public Stream ReferencedData {
            get {
                return referencedData;
            }
        }
        public int DestAddress {
            get { return Project.EvalToInt(Values[2]); }
        }
        public int DestBank {
            get { return Project.EvalToInt(":"+Values[2]); }
        }
        public int DataSize {
            get { return Project.EvalToInt(Values[3]); }
        }


		public TilesetHeaderData(Project p, string command, IList<string> values, FileParser parser, int line, int colStart) 
			: base(p, command, values, 8, parser, line, colStart) {
                try {
                    referencedData = Project.GetBinaryFile("tilesets/" + Values[1] + ".bin");
                }
                catch (FileNotFoundException) {
                    // Default is to copy from 00 I guess
                    // TODO: copy this into its own file?
                    string filename = Values[1].Substring(0, Values[1].Length-2);
                    referencedData = Project.GetBinaryFile("tilesets/" + filename + "00.bin");
                }
		}

		public bool ShouldHaveNext() {
			return (Project.EvalToInt(Values[4]) & 0x80) == 0x80;
		}
	}

}
