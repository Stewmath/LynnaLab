using System;
using System.Collections.Generic;
using System.Drawing;

namespace LynnaLab {
    // Similar to "ObjectDefinitionType" below, but this abstracts the "NoValue", "DoubleValue",
    // and "SpecificEnemy" types into just the "Interaction", "SpecificEnemy", or "Part"
    // types.
    public enum ObjectType {
        Conditional=0,
        Interaction,
        Pointer,
        BossPointer,
        AntiBossPointer,
        RandomEnemy,
        SpecificEnemy,
        Part,
        ItemDrop,
        End,
        EndPointer
    }

    // This corresponds to "opcodes" for defining objects.
    enum ObjectDefinitionType {
        Conditional=0,
        NoValue,
        DoubleValue,
        Pointer,
        BossPointer,
        AntiBossPointer,
        RandomEnemy,
        SpecificEnemy,
        Part,
        QuadrupleValue,
        ItemDrop,
        End,
        EndPointer
    }


    public class ObjectData : Data {

        private static List<List<ValueReference>> objectValueReferences =
            new List<List<ValueReference>> {
                new List<ValueReference> { // Conditional
                    new ValueReference("Condition",0,DataValueType.Byte),
                },
                new List<ValueReference> { // NoValue
                    new ValueReference("ID",0,8,15,DataValueType.WordBits,true,"InteractionMapping"),
                    new ValueReference("SubID",0,0,7,DataValueType.WordBits),
                },
                new List<ValueReference> { // DoubleValue
                    new ValueReference("ID",0,8,15,DataValueType.WordBits,true,"InteractionMapping"),
                    new ValueReference("SubID",0,0,7,DataValueType.WordBits),
                    new ValueReference("Y",1,DataValueType.Byte),
                    new ValueReference("X",2,DataValueType.Byte),
                },
                new List<ValueReference> { // Pointer
                    new ValueReference("Pointer",0,DataValueType.ObjectPointer),
                },
                new List<ValueReference> { // BossPointer
                    new ValueReference("Pointer",0,DataValueType.ObjectPointer),
                },
                new List<ValueReference> { // AntiBossPointer
                    new ValueReference("Pointer",0,DataValueType.ObjectPointer),
                },
                new List<ValueReference> { // Random Enemy
                    new ValueReference("Flags",0,DataValueType.Byte),
                    new ValueReference("ID",1,8,15,DataValueType.WordBits,true,"EnemyMapping"),
                    new ValueReference("SubID",1,0,7,DataValueType.WordBits),
                },
                new List<ValueReference> { // Specific Enemy
                    new ValueReference("Flags",0,DataValueType.Byte),
                    new ValueReference("ID",1,8,15,DataValueType.WordBits,true,"EnemyMapping"),
                    new ValueReference("SubID",1,0,7,DataValueType.WordBits),
                    new ValueReference("Y",2,DataValueType.Byte),
                    new ValueReference("X",3,DataValueType.Byte),
                },
                new List<ValueReference> { // Part
                    new ValueReference("ID",0,8,15,DataValueType.WordBits,true,"PartMapping"),
                    new ValueReference("SubID",0,0,7,DataValueType.WordBits),
                    new ValueReference("Y",1,4,7,DataValueType.ByteBits),
                    new ValueReference("X",1,0,3,DataValueType.ByteBits),
                },
                new List<ValueReference> { // QuadrupleValue
                    new ValueReference("Object Type",0,DataValueType.Byte,editable:false),
                    new ValueReference("ID",1,8,15,DataValueType.WordBits),
                    new ValueReference("SubID",1,0,7,DataValueType.WordBits),
                    new ValueReference("Var03",2,DataValueType.Byte),
                    new ValueReference("Y",3,DataValueType.Byte),
                    new ValueReference("X",4,DataValueType.Byte),
                },
                new List<ValueReference> { // Item Drop
                    new ValueReference("Flags",0,DataValueType.Byte),
                    new ValueReference("Item",1,DataValueType.Byte),
                    new ValueReference("Y",2,4,7,DataValueType.ByteBits),
                    new ValueReference("X",2,0,3,DataValueType.ByteBits),
                },
                new List<ValueReference> { // InteracEnd
                },
                new List<ValueReference> { // InteracEndPointer
                },
            };


        private ObjectDefinitionType definitionType;

        public ObjectData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing, int definitionType)
            : base(p, command, values, -1, parser, spacing) {

            this.definitionType = (ObjectDefinitionType)definitionType;
            SetValueReferences(objectValueReferences[(int)definitionType]);
        }

        public ObjectType GetObjectType() {
            switch (definitionType) {
            case ObjectDefinitionType.Conditional:
                return ObjectType.Conditional;

            case ObjectDefinitionType.NoValue:
            case ObjectDefinitionType.DoubleValue:
                return ObjectType.Interaction;

            case ObjectDefinitionType.Pointer:
                return ObjectType.Pointer;

            case ObjectDefinitionType.BossPointer:
                return ObjectType.BossPointer;

            case ObjectDefinitionType.AntiBossPointer:
                return ObjectType.AntiBossPointer;

            case ObjectDefinitionType.RandomEnemy:
                return ObjectType.RandomEnemy;

            case ObjectDefinitionType.SpecificEnemy:
                return ObjectType.SpecificEnemy;

            case ObjectDefinitionType.Part:
                return ObjectType.Part;

            case ObjectDefinitionType.QuadrupleValue: {
                int t = GetIntValue("Object Type");
                if (t == 0)
                    return ObjectType.Interaction;
                else if (t == 1)
                    return ObjectType.SpecificEnemy;
                else if (t == 2)
                    return ObjectType.Part;
                else
                    throw new AssemblyErrorException(string.Format("Invalid 'obj_withParam' object (type {0}).", t.ToString()));
            }

            case ObjectDefinitionType.ItemDrop:
                return ObjectType.ItemDrop;

            case ObjectDefinitionType.End:
                return ObjectType.End;

            case ObjectDefinitionType.EndPointer:
                return ObjectType.EndPointer;

            default:
                throw new Exception("Unknown ObjectType?");
            }
        }

        // Same as base.GetValue except this keeps track of values "carried
        // over" from the last object, for SpecificEnemy and ItemDrop
        // objects.
        public override string GetValue(int i) {
            if (IsShortened()) {
                ObjectData last = LastData as ObjectData;

                if (last == null || (last.GetObjectDefinitionType() != GetObjectDefinitionType()))
                    this.ThrowException(new AssemblyErrorException("Malformatted object"));

                if (i == 0)
                    return (LastData as ObjectData).GetValue(0);
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
                // Check if the next object depends on this
                ObjectData next = NextData as ObjectData;
                if (next != null && next.GetObjectDefinitionType() == GetObjectDefinitionType())
                    next.Elongate();
            }
            base.SetValue(i, value);
        }

        public override string GetString() {
            if (FileParser.GetDataLabel(this) != null) {
                // If a label points directly to this data, it can't be
                // shortened
                Elongate();
            }
            else
                Shorten(); // Try to, anyway

            return base.GetString();
        }

        // Object colors match ZOLE mostly
		public Color GetColor()
		{
			switch (GetObjectType())
			{
				case ObjectType.Conditional:        return Color.Black;
				case ObjectType.Interaction:        return Color.DarkOrange;
				case ObjectType.Pointer:            return Color.Yellow;
				case ObjectType.BossPointer:        return Color.Green;
				case ObjectType.AntiBossPointer:    return Color.Blue;
				case ObjectType.RandomEnemy:        return Color.FromArgb(128, 64, 0);
				case ObjectType.SpecificEnemy:      return Color.FromArgb(128, 64, 0);
				case ObjectType.Part:               return Color.Gray;
				case ObjectType.ItemDrop:           return Color.Lime;
			}
            return Color.White;
		}

        /// <summary>
        ///  Returns true if the X/Y variables are 4-bits instead of 8 (assuming it has X/Y in the
        ///  first place).
        /// </summary>
        public bool HasShortenedXY() {
            return IsTypeWithShortenedXY() || GetSubIDDocumentation()?.GetField("postype") == "short";
        }

        // Returns true if the object's type causes the XY values to have 4 bits rather than 8.
        // (DOES NOT account for "@postype" parameter which can set interactions to have both Y/X
        // positions stored in the Y variable.)
        bool IsTypeWithShortenedXY() {
            return GetObjectDefinitionType() == ObjectDefinitionType.Part ||
                GetObjectDefinitionType() == ObjectDefinitionType.ItemDrop;
        }

        public bool HasXY() {
            try {
                GetValue("X");
                GetValue("Y");
                return true;
            }
            catch (InvalidLookupException) {
                return false;
            }
        }

        // Return the center x-coordinate of the object.
        // This is different from 'GetIntValue("X")' because sometimes objects store both their Y and
        // X values in one byte. This will take care of that, and will multiply the value when the
        // positions are in this short format (ie. range $0-$f becomes $08-$f8).
        public byte GetX() {
            if (GetSubIDDocumentation()?.GetField("postype") == "short") {
                int n = GetIntValue("Y")&0xf;
                return (byte)(n*16+8);
            }
            else if (IsTypeWithShortenedXY()) {
                int n = GetIntValue("X");
                return (byte)(n*16+8);
            }
            else
                return (byte)GetIntValue("X");
        }
        // Return the center y-coordinate of the object
        public byte GetY() {
            if (GetSubIDDocumentation()?.GetField("postype") == "short") {
                int n = GetIntValue("Y")>>4;
                return (byte)(n*16+8);
            }
            else if (IsTypeWithShortenedXY()) {
                int n = GetIntValue("Y");
                return (byte)(n*16+8);
            }
            else
                return (byte)GetIntValue("Y");
        }

        public void SetX(byte n) {
            if (GetSubIDDocumentation()?.GetField("postype") == "short") {
                byte y = (byte)(GetIntValue("Y")&0xf0);
                y |= (byte)(n/16);
                SetValue("Y", y);
            }
            else if (IsTypeWithShortenedXY())
                SetValue("X", n/16);
            else
                SetValue("X", n);
        }
        public void SetY(byte n) {
            if (GetSubIDDocumentation()?.GetField("postype") == "short") {
                byte y = (byte)(GetIntValue("Y")&0x0f);
                y |= (byte)(n&0xf0);
                SetValue("Y", y);
            }
            else if (IsTypeWithShortenedXY())
                SetValue("Y", n/16);
            else
                SetValue("Y", n);
        }

        // Get the object group pointed to, or null if no such group
        // exists.
        public ObjectGroup GetPointedObjectGroup() {
            if (!(definitionType >= ObjectDefinitionType.Pointer
                        && definitionType <= ObjectDefinitionType.AntiBossPointer))
                return null;

            try {
                Project.GetFileWithLabel(GetValue(0));
                return Project.GetDataType<ObjectGroup>(GetValue(0));
            }
            catch(InvalidLookupException) {
                return null;
            }
        }

        public GameObject GetGameObject() {
            if (GetObjectType() == ObjectType.Interaction) {
                return Project.GetIndexedDataType<InteractionObject>((GetIntValue("ID")<<8) | GetIntValue("SubID"));
            }
            else if (GetObjectType() == ObjectType.RandomEnemy || GetObjectType() == ObjectType.SpecificEnemy) {
                return Project.GetIndexedDataType<EnemyObject>((GetIntValue("ID")<<8) | GetIntValue("SubID"));
            }
            else if (GetObjectType() == ObjectType.Part) {
                return Project.GetIndexedDataType<PartObject>((GetIntValue("ID")<<8) | GetIntValue("SubID"));
            }
            // TODO: other types
            return null;
        }

        public Documentation GetIDDocumentation() {
            return GetGameObject()?.GetIDDocumentation();
        }

        public Documentation GetSubIDDocumentation() {
            return GetGameObject()?.GetSubIDDocumentation();
        }

        // Private methods

        ObjectDefinitionType GetObjectDefinitionType() {
            return definitionType;
        }

        bool IsShortenable() {
            return GetObjectDefinitionType() == ObjectDefinitionType.SpecificEnemy ||
                GetObjectDefinitionType() == ObjectDefinitionType.ItemDrop;
        }
        // Returns true if this object reuses a byte from the last one
        bool IsShortened() {
            return ((GetObjectDefinitionType() == ObjectDefinitionType.SpecificEnemy && base.GetNumValues() < 4) ||
                    (GetObjectDefinitionType() == ObjectDefinitionType.ItemDrop && base.GetNumValues() < 3));
        }
        void Elongate() {
            if (IsShortenable() && IsShortened()) {
                SetSpacing(1, " ");
                base.InsertValue(0, GetValue(0));
            }
        }
        void Shorten() {
            // Shortens the object if possible
            if (IsShortened() || !IsShortenable()) return;

            ObjectData last = LastData as ObjectData;
            if (last == null || last.GetObjectDefinitionType() != GetObjectDefinitionType()) return;
            if (last.GetValue(0) != GetValue(0)) return;

            RemoveValue(0);
            SetSpacing(1, "     ");
        }
    }
}
