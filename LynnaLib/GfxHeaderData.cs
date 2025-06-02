namespace LynnaLib
{
    // Class represents macro:
    // m_GfxHeader filename destAddress [size] [startOffset]
    //                0           1        2           3
    // Other types of gfx headers not supported here.
    public class GfxHeaderData : Data, IGfxHeader
    {
        IStream gfxStream;

        public int DestAddr
        {
            get { return GetIntValue(1) & 0xfff0; }
        }
        public int DestBank
        {
            get { return GetIntValue(1) & 0x000f; }
        }

        // Properties from IGfxHeader
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
            get {
                if (GetNumValues() >= 3)
                    return Project.Eval(GetValue(2));
                else
                    return (int)gfxStream.Length / 16;
            }
        }

        public GfxHeaderData(Project p, string id, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing)
            : base(p, id, command, values, 6, parser, spacing)
        {
            string filename = GetValue(0);

            IStream stream = Project.GetGfxStream(filename);

            if (stream == null)
            {
                throw new Exception("Could not find graphics file " + filename + ".");
            }

            gfxStream = stream;

            // Adjust the gfx stream if we're supposed to omit part of it
            if (GetNumValues() >= 3)
            {
                int start = 0;
                if (GetNumValues() >= 4)
                    start = GetIntValue(3);
                // TODO: Fix this - graphics should reference a subset of the full file
                //gfxStream = new SubStream(gfxStream, start, BlockCount * 16);
            }
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private GfxHeaderData(Project p, string id, TransactionState state)
            : base(p, id, state)
        {
        }

        public override void OnInitializedFromTransfer()
        {
            gfxStream = Project.GetGfxStream(GetValue(0));
        }
    }

}
