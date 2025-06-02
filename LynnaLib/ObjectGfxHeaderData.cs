using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLib
{
    // Class represents macro:
    // m_ObjectGfxHeader filename [continue]
    //                    0        1
    // Other types of gfx headers not supported here.
    public class ObjectGfxHeaderData : Data, IGfxHeader
    {
        IStream gfxStream;

        public int? SourceAddr
        {
            get { return null; }
        }
        public int? SourceBank
        {
            get { return null; }
        }

        public IStream GfxStream { get { return gfxStream; } }

        // The number of blocks (16 bytes each) to be read.
        public int BlockCount
        {
            get { return 0x20; }
        }

        // True if the bit indicating that there is a next value is set.
        public bool ShouldHaveNext
        {
            get { return GetNumValues() >= 2 && (GetIntValue(1)) != 0; }
        }

        // Should only request this if the "ShouldHaveNext" property is true.
        public ObjectGfxHeaderData NextGfxHeader
        {
            get
            {
                return NextData as ObjectGfxHeaderData;
            }
        }


        public ObjectGfxHeaderData(Project p, string id, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing)
            : base(p, id, command, values, 3, parser, spacing)
        {
            string filename = GetValue(0);

            IStream stream = Project.GetGfxStream(filename);
            if (stream == null)
            {
                throw new Exception("Could not find graphics file " + filename);
            }
            gfxStream = stream;
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private ObjectGfxHeaderData(Project p, string id, TransactionState state)
            : base(p, id, state)
        {
        }

        public override void OnInitializedFromTransfer()
        {
            gfxStream = Project.GetGfxStream(GetValue(0));
        }
    }

}
