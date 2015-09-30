using System.Collections.Generic;

namespace LynnaLab {
    public enum InteractionType {
        Type0=0,
        NoValue,
        DoubleValue,
        Pointer,
        BossPointer,
        Conditional,
        RandomEnemy,
        SpecificEnemy,
        Part,
        QuadrupleValue,
        TypeA,
        End,
        EndPointer
    }

    public class InteractionData : Data {
        InteractionType type;

        public InteractionData(Project p, string command, IList<string> values, FileParser parser, int line, int colStart, InteractionType type)
            : base(p, command, values, -1, parser, line, colStart) {

            this.type = type;
        }

        public InteractionType GetType() {
            return type;
        }
    }

}
