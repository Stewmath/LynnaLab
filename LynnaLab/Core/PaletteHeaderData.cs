using System;
using System.Collections.Generic;
using System.Drawing;

namespace LynnaLab
{
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
                return (RgbData)paletteDataFile.GetData(GetValue(2));
            }
        }

        public PaletteType PaletteType {
            get { return CommandLowerCase == "m_paletteheaderbg" ? PaletteType.Background : PaletteType.Sprite; }
        }
        public int FirstPalette {
            get { return Project.EvalToInt(GetValue(0)); }
        }
        public int NumPalettes {
            get { return Project.EvalToInt(GetValue(1)); }
        }

        public PaletteHeaderData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing)
            : base(p, command, values, 3, parser, spacing)
        {

            int dest = -1;
            try {
                dest = Project.EvalToInt(GetValue(2));
            }
            catch(FormatException) {
                dest = -1;
            }

            if (dest != -1)
                sourceFromRam = true;
            else {
                paletteDataFile = Project.GetFileWithLabel(GetValue(2));
                if (!(paletteDataFile.GetData(GetValue(2)) is RgbData))
                    throw new Exception("Label \"" + GetValue(2) + "\" was expected to reference data defined with m_RGB16");
            }
        }

        public Color[][] GetPalettes() {
            Color[][] ret = new Color[NumPalettes][];

            RgbData data = Data;

            for (int i=0; i<NumPalettes; i++) {
                ret[i] = new Color[4];
                for (int j=0; j<4; j++) {
                    ret[i][j] = data.Color;
                    data = data.NextData as RgbData;
                }
            }

            return ret;
        }

        public bool ShouldHaveNext() {
            return (Project.EvalToInt(GetValue(3)) & 0x80) == 0x80;
        }

        public bool SourceFromRam() {
            return sourceFromRam;
        }
    }

}
