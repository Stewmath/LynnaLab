using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

namespace LynnaLib
{
    public class AnimationGroup : ProjectIndexedDataType
    {
        public int NumAnimations
        {
            get { return _numAnimations; }
        }

        int _numAnimations;
        Animation[] animations = new Animation[4];

        internal AnimationGroup(Project p, int i) : base(p, i)
        {
            FileParser parser = Project.GetFileWithLabel("animationGroupTable");
            Data pointer = parser.GetData("animationGroupTable", 2 * Index);
            string label = pointer.GetValue(0);

            Data data = parser.GetData(label);
            int b1 = Project.EvalToInt(data.GetValue(0));
            data = data.NextData;
            int bits = b1 & 0xf;

            if (bits >= 0xf)
                _numAnimations = 4;
            else if (bits >= 0x7)
                _numAnimations = 3;
            else if (bits >= 0x3)
                _numAnimations = 2;
            else if (bits >= 0x1)
                _numAnimations = 1;
            else
                _numAnimations = 0;

            for (int j = 0; j < NumAnimations; j++)
            {
                if (data.CommandLowerCase != ".dw")
                    throw new Exception("Malformatted animation group data (index 0x" +
                            Index.ToString("x") + "\n");
                animations[j] = Project.GetDataType<Animation>(data.GetValue(0));
                data = data.NextData;
            }
        }

        public Animation GetAnimationIndex(int i)
        {
            return animations[i];
        }
    }
}
