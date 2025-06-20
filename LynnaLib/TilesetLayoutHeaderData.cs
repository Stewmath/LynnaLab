using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLib
{
    // Data macro "m_TilesetLayoutHeader"
    public class TilesetLayoutHeaderData : Data
    {

        readonly TrackedStream referencedData;

        public int DictionaryIndex
        {
            get { return Project.Eval(GetValue(0)); }
        }
        public TrackedStream ReferencedData
        {
            get
            {
                return referencedData;
            }
        }
        public int DestAddress
        {
            get { return Project.Eval(GetValue(2)); }
        }
        public int DestBank
        {
            get { return Project.Eval(":" + GetValue(2)); }
        }
        public int DataSize
        {
            get { return Project.Eval(GetValue(3)); }
        }


        public TilesetLayoutHeaderData(Project p, string id, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing)
            : base(p, id, command, values, 8, parser, spacing)
        {
            try
            {
                referencedData = Project.GetFileStream("tileset_layouts/" + Project.GameString + "/" + GetValue(1) + ".bin");
            }
            catch (FileNotFoundException)
            {
                // Default is to copy from 00 I guess
                // TODO: copy this into its own file?
                LogHelper.GetLogger().Warn("Missing tileset layout file: " + GetValue(1));
                string filename = GetValue(1).Substring(0, GetValue(1).Length - 2);
                referencedData = Project.GetFileStream("tileset_layouts/" + Project.GameString + "/" + filename + "00.bin");
            }
        }

        public bool ShouldHaveNext()
        {
            return (Project.Eval(GetValue(4)) & 0x80) == 0x80;
        }
    }

}
