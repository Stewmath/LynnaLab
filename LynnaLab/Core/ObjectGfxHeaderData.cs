using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab
{
    // Class represents macro:
    // m_GfxHeader filename destAddress size/continue [startOffset]
    //              0           1           2           3
    // Other types of gfx headers not supported here.
    public class ObjectGfxHeaderData : Data,IGfxHeader {
        List<string> gfxDirectories = new List<string>();

        Stream gfxStream;

        public int? SourceAddr {
            get { return null; }
        }
        public int? SourceBank {
            get { return null; }
        }

        public Stream GfxStream { get { return gfxStream; } }

        // The number of blocks (16 bytes each) to be read.
        public int BlockCount {
            get { return 0x20; }
        }

        // True if the bit indicating that there is a next value is set.
        public bool ShouldHaveNext {
            get { return (GetIntValue(1)&0x80) == 0; }
        }

        // Should only request this if the "ShouldHaveNext" property is true.
        public ObjectGfxHeaderData NextGfxHeader {
            get {
                return NextData as ObjectGfxHeaderData;
            }
        }


        public ObjectGfxHeaderData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing) 
            : base(p, command, values, 3, parser, spacing)
        {
            string filename = GetValue(0);

            gfxStream = Project.LoadGfx(filename);
            if (gfxStream == null) {
                throw new Exception("Could not find graphics file " + filename);
            }
        }
    }

}
