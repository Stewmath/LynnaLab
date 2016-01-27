using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab
{
    // Class represents macro:
    // m_GfxHeader filename destAddress size/continue [startOffset]
    //              0           1           2           3
    public class GfxHeaderData : Data {
        static List<string> gfxDirectories = new List<string>() {
            "gfx/",
                "gfx_compressible/"
        };

        Stream gfxFile;

        public int DestAddr {
            get { return Project.EvalToInt(GetValue(1)) & 0xfff0; }
        }
        public int DestBank {
            get { return Project.EvalToInt(GetValue(1)) & 0x000f; }
        }
        public Stream GfxStream { get { return gfxFile; } }

        public GfxHeaderData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<int> spacing) 
            : base(p, command, values, 6, parser, spacing)
        {

            string filename = GetValue(0) + ".bin";

            gfxFile = null;
            foreach (string directory in gfxDirectories) {
                if (File.Exists(Project.BaseDirectory + directory + filename)) {
                    gfxFile = Project.GetBinaryFile(directory + filename);
                    if (CommandLowerCase == "m_gfxheader" && GetNumValues() > 3)
                        // Skip into part of gfx data
                        gfxFile = new SubStream(gfxFile, p.EvalToInt(GetValue(3)),
                                GetBlockCount()*16);

                    break;
                }
            }
            if (gfxFile == null) {
                throw new Exception("Could not find graphics file " + filename);
            }
        }

        // Returns the number of blocks (16 bytes each) to be read.
        public int GetBlockCount() {
            return (Project.EvalToInt(GetValue(2))&0x7f)+1;
        }

        // Returns true if the bit indicating that there is a next value is
        // set.
        public bool ShouldHaveNext() {
            return (Project.EvalToInt(GetValue(2)) & 0x80) == 0x80;
        }
    }

}
