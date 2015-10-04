using System;
using System.Collections.Generic;
using System.Drawing;

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
        ItemDrop,
        End,
        EndPointer
    }

    public class InteractionData : Data {

        public static List<List<DataValueReference>> interactionValueReferences =
            new List<List<DataValueReference>> {
                new List<DataValueReference> { // Type0
                    new DataValueReference("Condition",0,DataValueType.Byte),
                },
                new List<DataValueReference> { // NoValue
                    new DataValueReference("ID",0,DataValueType.Word),
                },
                new List<DataValueReference> { // DoubleValue
                    new DataValueReference("ID",0,DataValueType.Word),
                    new DataValueReference("Y",1,DataValueType.Byte),
                    new DataValueReference("X",2,DataValueType.Byte),
                },
                new List<DataValueReference> { // Pointer
                    new DataValueReference("Pointer",0,DataValueType.InteractionPointer),
                },
                new List<DataValueReference> { // BossPointer
                    new DataValueReference("Pointer",0,DataValueType.InteractionPointer),
                },
                new List<DataValueReference> { // Conditional
                    new DataValueReference("Pointer",0,DataValueType.InteractionPointer),
                },
                new List<DataValueReference> { // Random Enemy
                    new DataValueReference("Flags",0,DataValueType.Byte),
                    new DataValueReference("ID",1,DataValueType.Word),
                },
                new List<DataValueReference> { // Specific Enemy
                    new DataValueReference("Flags",0,DataValueType.Byte),
                    new DataValueReference("ID",1,DataValueType.Word),
                    new DataValueReference("Y",2,DataValueType.Byte),
                    new DataValueReference("X",3,DataValueType.Byte),
                },
                new List<DataValueReference> { // Part
                    new DataValueReference("ID",0,DataValueType.Word),
                    new DataValueReference("YX",1,DataValueType.Byte),
                },
                new List<DataValueReference> { // QuadrupleValue
                    new DataValueReference("ID",0,DataValueType.Word),
                    new DataValueReference("Unknown 1",1,DataValueType.Byte),
                    new DataValueReference("Unknown 2",2,DataValueType.Byte),
                    new DataValueReference("Y",3,DataValueType.Byte),
                    new DataValueReference("X",4,DataValueType.Byte),
                },
                new List<DataValueReference> { // Item Drop
                    new DataValueReference("Flags",0,DataValueType.Byte),
                    new DataValueReference("Item",1,DataValueType.Byte),
                    new DataValueReference("YX",2,DataValueType.Byte),
                },
            };


        InteractionType type;

        public InteractionData(Project p, string command, IList<string> values, FileParser parser, IList<int> spacing, InteractionType type)
            : base(p, command, values, -1, parser, spacing) {

            this.type = type;
        }

        public InteractionType GetInteractionType() {
            return type;
        }

        // Same as base.GetValue except this keeps track of values "carried
        // over" from the last interaction, for SpecificEnemy and ItemDrop
        // interactions.
        public override string GetValue(int i) {
            if (IsShortened()) {
                InteractionData last = LastData as InteractionData;
                if (last == null || (last.GetInteractionType() != GetInteractionType()))
                    this.ThrowException("Malformatted interaction");
                if (i == 0)
                    return (LastData as InteractionData).GetValue(0);
                else
                    return base.GetValue(i-1);
            }

            return base.GetValue(i);
        }
        public override int GetNumValues() {
            if (IsShortened())
                return base.GetNumValues()+1;
            return base.GetNumValues();
        }
        public override void SetValue(int i, string value) {
            if (IsShortened()) {
                if (i == 0) {
                    Elongate();
                }
                else
                    i--;
            }
            if (IsShortenable()) {
                // Check if the next interaction depends on this
                InteractionData next = NextData as InteractionData;
                if (next != null && next.GetInteractionType() == GetInteractionType())
                    next.Elongate();
            }
            base.SetValue(i, value);
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

        // Interaction colors match ZOLE
		public Color GetColor()
		{
			switch (type)
			{
				case (InteractionType)0: return Color.Black;
				case (InteractionType)1: return Color.Red;
				case (InteractionType)2: return Color.DarkOrange;
				case (InteractionType)3: return Color.Yellow;
				case (InteractionType)4: return Color.Green;
				case (InteractionType)5: return Color.Blue;
				case (InteractionType)6: return Color.Purple;
				case (InteractionType)7: return Color.FromArgb(128, 64, 0);
				case (InteractionType)8: return Color.Gray;
				case (InteractionType)9: return Color.White;
				case (InteractionType)0xA: return Color.Lime;
			}
			return Color.Magenta;
		}

        bool IsShortenable() {
            return GetInteractionType() == InteractionType.SpecificEnemy ||
                GetInteractionType() == InteractionType.ItemDrop;
        }
        // Returns true if this interaction reuses a byte from the last one
        bool IsShortened() {
            return ((GetInteractionType() == InteractionType.SpecificEnemy && base.GetNumValues() < 4) ||
                    (GetInteractionType() == InteractionType.ItemDrop && base.GetNumValues() < 3));
        }
        void Elongate() {
            if (IsShortenable() && IsShortened()) {
                SetSpacing(1,1);
                base.InsertValue(0, GetValue(0));
            }
        }
        void Shorten() {
            // Shortens the interaction if possible
            if (IsShortened() || !IsShortenable()) return;

            InteractionData last = LastData as InteractionData;
            if (last == null || last.GetInteractionType() != GetInteractionType()) return;
            if (last.GetValue(0) != GetValue(0)) return;

            RemoveValue(0);
            SetSpacing(1, 5);
        }
    }
}
