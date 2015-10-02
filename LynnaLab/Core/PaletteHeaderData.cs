using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab {
    public enum PaletteType {
        Background=0,
        Sprite
    };

    // Class represents macro:
    // m_PaletteHeader[Bg|Spr] startIndex numPalettes address continue
    //                          0           1           2       3
	public class PaletteHeaderData : Data {
        FileParser paletteDataFile;

        bool sourceFromRam = false;

        public RgbData Data {
            get {
                if (sourceFromRam)
                    return null;
                return (RgbData)paletteDataFile.GetData(Values[2]);
            }
        }

		public PaletteType PaletteType {
			get { return CommandLowerCase == "m_paletteheaderbg" ? PaletteType.Background : PaletteType.Sprite; }
		}
        public int FirstPalette {
            get { return Project.EvalToInt(Values[0]); }
        }
        public int NumPalettes {
            get { return Project.EvalToInt(Values[1]); }
        }

		public PaletteHeaderData(Project p, string command, IList<string> values, FileParser parser, IList<int> spacing)
			: base(p, command, values, 3, parser, spacing) {

                int dest = -1;
                try {
                    dest = Project.EvalToInt(values[2]);
                }
                catch(FormatException) {
                    dest = -1;
                }

                if (dest != -1)
                    sourceFromRam = true;
                else {
                    paletteDataFile = Project.GetFileWithLabel(Values[2]);
                    if (!(paletteDataFile.GetData(Values[2]) is RgbData))
                        throw new Exception("Label \"" + Values[2] + "\" was expected to reference data defined with m_RGB16");
                }
		}

		public bool ShouldHaveNext() {
			return (Project.EvalToInt(Values[3]) & 0x80) == 0x80;
		}

        public bool SourceFromRam() {
            return sourceFromRam;
        }
	}

}
