using System;
using System.Collections.Generic;
using System.IO;
using Util;

namespace LynnaLib
{
    // Class represents macro:
    // m_GfxHeader filename destAddress size/continue [startOffset]
    //              0           1           2           3
    // Other types of gfx headers not supported here.
    public class GfxHeaderData : Data,IGfxHeader {
        List<string> gfxDirectories = new List<string>();

        Stream gfxStream;

        public int DestAddr {
            get { return Project.EvalToInt(GetValue(1)) & 0xfff0; }
        }
        public int DestBank {
            get { return Project.EvalToInt(GetValue(1)) & 0x000f; }
        }

        // Properties from IGfxHeader
        public int? SourceAddr {
            get { return null; }
        }
        public int? SourceBank {
            get { return null; }
        }

        public Stream GfxStream { get { return gfxStream; } }

        // The number of blocks (16 bytes each) to be read.
        public int BlockCount {
            get { return (Project.EvalToInt(GetValue(2))&0x7f)+1; }
        }

        // True if the bit indicating that there is a next value is set.
        public bool ShouldHaveNext {
            get { return (Project.EvalToInt(GetValue(2)) & 0x80) == 0x80; }
        }

        public GfxHeaderData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing) 
            : base(p, command, values, 6, parser, spacing)
        {
            string filename = GetValue(0);

            gfxStream = Project.LoadGfx(filename);

            if (gfxStream == null) {
                throw new Exception("Could not find graphics file " + filename + ".");
            }

            if (GetNumValues() > 3) {
                // Skip into part of gfx data
                gfxStream = new SubStream(gfxStream, p.EvalToInt(GetValue(3)), BlockCount*16);
            }
        }
    }

}
