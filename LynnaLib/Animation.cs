using System;
using System.Collections.Generic;

namespace LynnaLib
{
    public class Animation : ProjectDataType
    {
        public int NumIndices {
            get { return gfxHeaderIndices.Count; }
        }

        // Parallel lists
        List<int> gfxHeaderIndices = new List<int>();
        List<int> counters = new List<int>();

        internal Animation(Project p, string label) : base(p, label) {
            FileParser parser = Project.GetFileWithLabel(label);
            Data data = parser.GetData(label);
            while (data != null && data.CommandLowerCase == ".db") {
                counters.Add(Project.EvalToInt(data.GetValue(0)));
                data = data.NextData;
                if (data.CommandLowerCase != ".db")
                    throw new Exception("Malformatted animation data");
                gfxHeaderIndices.Add(Project.EvalToInt(data.GetValue(0)));
                data = data.NextData;
            }
        }

        public int GetCounter(int i) {
            return counters[i];
        }
        public GfxHeaderData GetGfxHeader(int i) {
            int index = gfxHeaderIndices[i];
            FileParser parser = Project.GetFileWithLabel("animationGfxHeaders");
            var header = parser.GetData("animationGfxHeaders") as GfxHeaderData;
            for (int j=0; j<index; j++)
                header = header.NextData as GfxHeaderData;
            return header;
        }
    }
}
