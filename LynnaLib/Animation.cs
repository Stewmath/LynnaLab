using System;
using System.Collections.Generic;

namespace LynnaLib
{
    public class Animation : ProjectDataType, ProjectDataInstantiator
    {
        public int NumIndices
        {
            get { return gfxHeaderIndices.Count; }
        }

        // Parallel lists
        List<int> gfxHeaderIndices = new List<int>();
        List<int> counters = new List<int>();

        private Animation(Project p, string label) : base(p, label)
        {
            FileParser parser = Project.GetFileWithLabel(label);
            Data data = parser.GetData(label);
            while (data != null && data.CommandLowerCase == ".db")
            {
                counters.Add(Project.Eval(data.GetValue(0)));
                data = data.NextData;
                if (data.CommandLowerCase != ".db")
                    throw new Exception("Malformatted animation data");
                gfxHeaderIndices.Add(Project.Eval(data.GetValue(0)));
                data = data.NextData;
            }
        }

        static ProjectDataType ProjectDataInstantiator.Instantiate(Project p, string id)
        {
            return new Animation(p, id);
        }

        public int GetCounter(int i)
        {
            return counters[i];
        }
        public GfxHeaderData GetGfxHeader(int i)
        {
            int index = gfxHeaderIndices[i];
            FileParser parser = Project.GetFileWithLabel("animationGfxHeaders");
            var header = parser.GetData("animationGfxHeaders") as GfxHeaderData;
            for (int j = 0; j < index; j++)
                header = header.NextData as GfxHeaderData;
            return header;
        }
    }
}
