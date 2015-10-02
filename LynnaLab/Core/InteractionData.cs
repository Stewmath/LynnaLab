using System;
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

        public InteractionData(Project p, string command, IList<string> values, FileParser parser, IList<int> spacing, InteractionType type)
            : base(p, command, values, -1, parser, spacing) {

            this.type = type;
        }

        public InteractionType GetInteractionType() {
            return type;
        }

        // Same as Values[i] except this keeps track of values "carried over"
        // from the last interaction, for SpecificEnemy and TypeA interactions.
        public string GetInteractionValue(int i) {
            if (IsShortened()) {
                InteractionData last = Last as InteractionData;
                if (last == null || (last.GetInteractionType() != GetInteractionType()))
                    this.ThrowException("Malformatted interaction");
                if (i == 0)
                    return (Last as InteractionData).GetInteractionValue(0);
                else
                    return Values[i-1];
            }

            return Values[i];
        }
        public void SetInteractionValue(int i, string value) {
            if (IsShortened()) {
                if (i == 0) {
                    Elongate();
                }
                else
                    i--;
            }
            if (IsShortenable()) {
                // Check if the next interaction depends on this
                InteractionData next = Next as InteractionData;
                if (next != null && next.GetInteractionType() == GetInteractionType())
                    next.Elongate();
            }
            SetValue(i, value);
        }

        public override string GetString() {
            if (Parser.GetDataLabel(this) != null) {
                // If a label points directly to this data, it can't be
                // shortened
                Elongate();
            }
            else
                Shorten(); // Try to, anyway

            return base.GetString();
        }

        bool IsShortenable() {
            return GetInteractionType() == InteractionType.SpecificEnemy || GetInteractionType() == InteractionType.TypeA;
        }
        // Returns true if this interaction reuses a byte from the last one
        bool IsShortened() {
            return ((GetInteractionType() == InteractionType.SpecificEnemy && Values.Count != 4) ||
                    (GetInteractionType() == InteractionType.TypeA && Values.Count != 3));
        }
        void Elongate() {
            if (IsShortenable() && IsShortened()) {
                SetSpacing(1,1);
                InsertValue(0, GetInteractionValue(0));
            }
        }
        void Shorten() {
            // Shortens the interaction if possible
            if (IsShortened() || !IsShortenable()) return;

            InteractionData last = Last as InteractionData;
            if (last == null || last.GetInteractionType() != GetInteractionType()) return;
            if (last.GetInteractionValue(0) != GetInteractionValue(0)) return;

            RemoveValue(0);
            SetSpacing(1, 5);
        }
    }
}
