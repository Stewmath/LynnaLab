using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Animation : ProjectDataType
    {
        public int NumIndices {
            get { return gfxHeaderIndices.Count; }
        }

        // Parallel lists
        List<int> gfxHeaderIndices = new List<int>();
        List<int> counters = new List<int>();

        public Animation(Project p, string label) : base(p, label) {
            FileParser parser = Project.GetFileWithLabel(label);
            Data data = parser.GetData(label);
            while (data != null && data.Command == ".db") {
                counters.Add(Project.EvalToInt(data.Values[0]));
                data = data.Next;
                if (data.Command != ".db")
                    throw new Exception("Malformatted animation data");
                gfxHeaderIndices.Add(Project.EvalToInt(data.Values[0]));
                data = data.Next;
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
                header = header.Next as GfxHeaderData;
            return header;
        }
    }
}
