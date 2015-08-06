using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab {
    // Data macro "m_TilesetHeader"
	public class TilesetHeaderData : Data {

        Stream referencedData;

        public int DictionaryIndex {
            get { return project.EvalToInt(Values[0]); }
        }
        public Stream ReferencedData {
            get {
                return referencedData;
            }
        }
        public int DestAddress {
            get { return project.EvalToInt(Values[2]); }
        }
        public int DestBank {
            get { return project.EvalToInt(":"+Values[2]); }
        }
        public int DataSize {
            get { return project.EvalToInt(Values[3]); }
        }


		public TilesetHeaderData(Project p, string command, IList<string> values) 
			: base(p, command, values, 8) {
                try {
                    referencedData = project.GetBinaryFile("tilesets/" + Values[1] + ".bin");
                }
                catch (FileNotFoundException) {}
		}

		public bool ShouldHaveNext() {
			return (project.EvalToInt(Values[4]) & 0x80) == 0x80;
		}
	}

}
