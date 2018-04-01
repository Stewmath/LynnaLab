using System;
using System.Collections.Generic;
using System.Drawing;

namespace LynnaLab {
    public enum ObjectType {
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

        public static List<List<ValueReference>> objectValueReferences =
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
                    new ValueReference("Object Type",0,DataValueType.Byte),
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


        ObjectType type;

        public ObjectData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing, ObjectType type)
            : base(p, command, values, -1, parser, spacing) {

            this.type = type;

            SetValueReferences(objectValueReferences[(int)type]);
        }

        public ObjectType GetObjectType() {
            return type;
        }

        // Same as base.GetValue except this keeps track of values "carried
        // over" from the last object, for SpecificEnemy and ItemDrop
        // objects.
        public override string GetValue(int i) {
            if (IsShortened()) {
                ObjectData last = LastData as ObjectData;

                if (last == null || (last.GetObjectType() != GetObjectType()))
                    this.ThrowException(new Exception("Malformatted object"));

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
                if (next != null && next.GetObjectType() == GetObjectType())
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
			switch (type)
			{
				case (ObjectType)0: return Color.Black;
				case (ObjectType)1: return Color.Red;
				case (ObjectType)2: return Color.DarkOrange;
				case (ObjectType)3: return Color.Yellow;
				case (ObjectType)4: return Color.Green;
				case (ObjectType)5: return Color.Blue;
				case (ObjectType)6: return Color.Purple;
				case (ObjectType)7: return Color.FromArgb(128, 64, 0);
				case (ObjectType)8: return Color.Gray;
				case (ObjectType)9: return Color.Magenta;
				case (ObjectType)0xA: return Color.Lime;
			}
            return Color.White;
		}

        // Returns true if XY values are 4 bits rather than 8.
        // (DOES NOT account for "@postype" parameter which can set interactions to have both Y/X
        // positions stored in the Y variable.)
        bool HasShortenedXY() {
            return GetObjectType() == ObjectType.Part ||
                GetObjectType() == ObjectType.ItemDrop;
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
            else if (HasShortenedXY()) {
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
            else if (HasShortenedXY()) {
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
            else if (HasShortenedXY())
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
            else if (HasShortenedXY())
                SetValue("Y", n/16);
            else
                SetValue("Y", n);
        }

        // Get the object group pointed to, or null if no such group
        // exists.
        public ObjectGroup GetPointedObjectGroup() {
            if (!(type >= ObjectType.Pointer && type <= ObjectType.AntiBossPointer)) return null;

            try {
                Project.GetFileWithLabel(GetValue(0));
                return Project.GetDataType<ObjectGroup>(GetValue(0));
            }
            catch(InvalidLookupException) {
                return null;
            }
        }

        public GameObject GetGameObject() {
            if (type == ObjectType.NoValue || type == ObjectType.DoubleValue || (type == ObjectType.QuadrupleValue && GetIntValue("Object Type") == 0)) {
                return Project.GetIndexedDataType<InteractionObject>((GetIntValue("ID")<<8) | GetIntValue("SubID"));
            }
            else if (type == ObjectType.RandomEnemy || type == ObjectType.SpecificEnemy || (type == ObjectType.QuadrupleValue && GetIntValue("Object Type") == 1)) {
                return Project.GetIndexedDataType<EnemyObject>((GetIntValue("ID")<<8) | GetIntValue("SubID"));
            }
            else if (type == ObjectType.Part || (type == ObjectType.QuadrupleValue && GetIntValue("Object Type") == 2)) {
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

        bool IsShortenable() {
            return GetObjectType() == ObjectType.SpecificEnemy ||
                GetObjectType() == ObjectType.ItemDrop;
        }
        // Returns true if this object reuses a byte from the last one
        bool IsShortened() {
            return ((GetObjectType() == ObjectType.SpecificEnemy && base.GetNumValues() < 4) ||
                    (GetObjectType() == ObjectType.ItemDrop && base.GetNumValues() < 3));
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
            if (last == null || last.GetObjectType() != GetObjectType()) return;
            if (last.GetValue(0) != GetValue(0)) return;

            RemoveValue(0);
            SetSpacing(1, "     ");
        }
    }
}
