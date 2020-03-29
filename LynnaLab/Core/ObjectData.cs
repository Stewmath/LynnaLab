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

    // This corresponds to "opcodes" for defining objects in the disassembly.
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


    // This class has two "ValueReferenceGroups".
    // One is private and consists of "DataValueReferences" which map 1:1 to data values.
    // The other is public (used for "GetValue" function, etc) and handles a layer of abstraction to
    // transparently convert between externally facing object types (ObjectType enum) and internal
    // object types (ObjectDefinitionType enum).
    // These sometimes are the same object, but not always.
    public class ObjectData : Data {

        private static List<List<ValueReference>> objectValueReferences =
            new List<List<ValueReference>> {
                new List<ValueReference> { // Conditional
                    new DataValueReference("Condition",0,DataValueType.Byte),
                },
                new List<ValueReference> { // Interaction
                    new DataValueReference("ID",0,8,15,DataValueType.WordBits,true,"InteractionMapping"),
                    new DataValueReference("SubID",0,0,7,DataValueType.WordBits),
                },
                new List<ValueReference> { // DoubleValue
                    new DataValueReference("ID",0,8,15,DataValueType.WordBits,true,"InteractionMapping"),
                    new DataValueReference("SubID",0,0,7,DataValueType.WordBits),
                    new DataValueReference("Y",1,DataValueType.Byte),
                    new DataValueReference("X",2,DataValueType.Byte),
                },
                new List<ValueReference> { // Pointer
                    new DataValueReference("Pointer",0,DataValueType.ObjectPointer),
                },
                new List<ValueReference> { // BossPointer
                    new DataValueReference("Pointer",0,DataValueType.ObjectPointer),
                },
                new List<ValueReference> { // AntiBossPointer
                    new DataValueReference("Pointer",0,DataValueType.ObjectPointer),
                },
                new List<ValueReference> { // Random Enemy
                    new DataValueReference("Flags",0,DataValueType.Byte),
                    new DataValueReference("ID",1,8,15,DataValueType.WordBits,true,"EnemyMapping"),
                    new DataValueReference("SubID",1,0,7,DataValueType.WordBits),
                },
                new List<ValueReference> { // Specific Enemy
                    new DataValueReference("Flags",0,DataValueType.Byte),
                    new DataValueReference("ID",1,8,15,DataValueType.WordBits,true,"EnemyMapping"),
                    new DataValueReference("SubID",1,0,7,DataValueType.WordBits),
                    new DataValueReference("Y",2,DataValueType.Byte),
                    new DataValueReference("X",3,DataValueType.Byte),
                },
                new List<ValueReference> { // Part
                    new DataValueReference("ID",0,8,15,DataValueType.WordBits,true,"PartMapping"),
                    new DataValueReference("SubID",0,0,7,DataValueType.WordBits),
                    new DataValueReference("Y",1,4,7,DataValueType.ByteBits),
                    new DataValueReference("X",1,0,3,DataValueType.ByteBits),
                },
                new List<ValueReference> { // QuadrupleValue
                    new DataValueReference("Object Type",0,DataValueType.Byte,editable:false),
                    new DataValueReference("ID",1,8,15,DataValueType.WordBits),
                    new DataValueReference("SubID",1,0,7,DataValueType.WordBits),
                    new DataValueReference("Var03",2,DataValueType.Byte),
                    new DataValueReference("Y",3,DataValueType.Byte),
                    new DataValueReference("X",4,DataValueType.Byte),
                },
                new List<ValueReference> { // Item Drop
                    new DataValueReference("Flags",0,DataValueType.Byte),
                    new DataValueReference("Item",1,DataValueType.Byte),
                    new DataValueReference("Y",2,4,7,DataValueType.ByteBits),
                    new DataValueReference("X",2,0,3,DataValueType.ByteBits),
                },
                new List<ValueReference> { // InteracEnd
                },
                new List<ValueReference> { // InteracEndPointer
                },
            };


        private ObjectDefinitionType definitionType;
        private ValueReferenceGroup dataValueReferenceGroup;


        public ObjectData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing, int definitionType)
            : base(p, command, values, -1, parser, spacing) {

            this.definitionType = (ObjectDefinitionType)definitionType;
            InitializeDefinitionType();
        }

        // Creates a new ObjectData instance, not based on existing data.
        // Unlike above, this initialized based on the "ObjectType" enum instead of
        // "ObjectDefinitionType".
        public ObjectData(Project p, FileParser parser, ObjectType type)
            : base(p, "", null, -1, parser, new string[]{"\t"}) {
            switch (type) {
                case ObjectType.Conditional:
                    definitionType = ObjectDefinitionType.Conditional;
                    break;
                case ObjectType.Interaction:
                    definitionType = ObjectDefinitionType.NoValue;
                    break;
                case ObjectType.Pointer:
                    definitionType = ObjectDefinitionType.Pointer;
                    break;
                case ObjectType.BossPointer:
                    definitionType = ObjectDefinitionType.BossPointer;
                    break;
                case ObjectType.AntiBossPointer:
                    definitionType = ObjectDefinitionType.AntiBossPointer;
                    break;
                case ObjectType.RandomEnemy:
                    definitionType = ObjectDefinitionType.RandomEnemy;
                    break;
                case ObjectType.SpecificEnemy:
                    definitionType = ObjectDefinitionType.SpecificEnemy;
                    break;
                case ObjectType.Part:
                    definitionType = ObjectDefinitionType.Part;
                    break;
                case ObjectType.ItemDrop:
                    definitionType = ObjectDefinitionType.ItemDrop;
                    break;
                case ObjectType.End:
                    definitionType = ObjectDefinitionType.End;
                    break;
                case ObjectType.EndPointer:
                    definitionType = ObjectDefinitionType.EndPointer;
                    break;
                default:
                    throw new Exception("Unexpected thing happened");
            }

            Command = ObjectGroup.ObjectCommands[(int)definitionType];
            InitializeDefinitionType();

            base.disableCallbacks += 1;

            base.SetNumValues(ObjectGroup.ObjectCommandDefaultParams[(int)definitionType]);
            DataValueReference.InitializeDataValues(this, dataValueReferenceGroup.GetValueReferences());

            if (definitionType >= ObjectDefinitionType.Pointer && definitionType <= ObjectDefinitionType.AntiBossPointer)
                base.SetValue(0, "objectData4000"); // Compileable default pointer

            base.disableCallbacks -= 1;
        }

        // Common code for constructors
        void InitializeDefinitionType() {
            dataValueReferenceGroup = new ValueReferenceGroup(objectValueReferences[(int)definitionType]);
            base.SetValueReferences(dataValueReferenceGroup);

            // Create the AbstractValueReference
            if (GetObjectType() == ObjectType.Interaction || GetObjectType() == ObjectType.SpecificEnemy
                    || GetObjectType() == ObjectType.Part) {
                List<ValueReference> refList = new List<ValueReference>();

                if (GetObjectType() == ObjectType.SpecificEnemy) {
                    refList.Add(new AbstractIntValueReference(this, "Has Flags", DataValueType.ByteBit));
                }

                refList.AddRange(new ValueReference[] {
                    new AbstractIntValueReference(this, "ID", DataValueType.Byte),
                    new AbstractIntValueReference(this, "SubID", DataValueType.Byte),
                    new AbstractIntValueReference(this, "Y", DataValueType.Byte),
                    new AbstractIntValueReference(this, "X", DataValueType.Byte),
                    new AbstractIntValueReference(this, "Var03", DataValueType.Byte)
                });
                base.SetValueReferences(refList);
            }
        }

        // Transform the "ObjectDefinitonType" from one thing to another. This involves changing the
        // format of the data.
        void TranslateDefinitionType(ObjectDefinitionType definitionType, int subType=-1) {
            if (definitionType == this.definitionType)
                return;

            // Having the Data class make callbacks in the middle of this method is a Bad Thing.
            base.disableCallbacks += 1;

            Dictionary<string, int> oldValues = new Dictionary<string,int>();

            foreach (var r in dataValueReferenceGroup.GetValueReferences()) {
                // Check if X/Y format must be updated for parts
                if (GetObjectType() == ObjectType.Part && (r.Name == "X" || r.Name == "Y")) {
                    if (this.definitionType == ObjectDefinitionType.Part
                            && definitionType == ObjectDefinitionType.QuadrupleValue) {
                        oldValues[r.Name] = r.GetIntValue() * 16 + 8;
                    }
                    else if (this.definitionType == ObjectDefinitionType.QuadrupleValue
                            && definitionType == ObjectDefinitionType.Part) {
                        oldValues[r.Name] = (r.GetIntValue() - 8) / 16;
                    }
                    else
                        throw new Exception("Unexpected thing happened");
                }
                else
                    oldValues[r.Name] = r.GetIntValue();
            }

            this.definitionType = definitionType;
            base.Command = ObjectGroup.ObjectCommands[(int)definitionType];
            base.SetNumValues(ObjectGroup.ObjectCommandMinParams[(int)definitionType]);
            dataValueReferenceGroup = new ValueReferenceGroup(objectValueReferences[(int)definitionType]);
            dataValueReferenceGroup.SetHandler(this);

            // Re-write old values (they may have changed position with the new data format)
            foreach (var r in dataValueReferenceGroup.GetValueReferences()) {
                if (!oldValues.ContainsKey(r.Name))
                    dataValueReferenceGroup.SetValue(r.Name, DataValueReference.defaultDataValues[(int)r.ValueType]);
                else
                    dataValueReferenceGroup.SetValue(r.Name, oldValues[r.Name]);
            }

            if (subType != -1) // Must come after the above
                dataValueReferenceGroup.SetValue("Object Type", subType);

            base.disableCallbacks -= 1;
            // TODO: force the callbacks to be called now?
        }

        // ValueReferenceHandler override
        public override string GetValue(string name) {
            return Wla.ToHex(GetIntValue(name), 2);
        }

        // ValueReferenceHandler override
        public override int GetIntValue(string name) {
            if (name == "Has Flags") // ObjectType.SpecificEnemy only
                return GetObjectDefinitionType() == ObjectDefinitionType.QuadrupleValue ? 0 : 1;

            if (!dataValueReferenceGroup.HasValue(name) && (name == "Y" || name == "X" || name == "Var03")) // "Optional" values
                return 0;
            if ((name == "Y" || name == "X") && definitionType == ObjectDefinitionType.Part) {
                return dataValueReferenceGroup.GetIntValue(name)*16 + 8;
            }
            return dataValueReferenceGroup.GetIntValue(name);
        }

        // ValueReferenceHandler override
        public override void SetValue(string name, string value) {
            SetValue(name, Project.EvalToInt(value));
        }

        // ValueReferenceHandler override
        // This transparently transforms the underlying ObjectDefinitionType if the need arises so
        // that we can seamlessly edit stuff like "Var03" without having to change types manually.
        // TODO: Check that the SpecificEnemy, ItemDrop "Flags" value still works properly.
        public override void SetValue(string name, int value) {
            Func<int> GetQuadSubtype = () => {
                ObjectType type = GetObjectType();
                if (type == ObjectType.Interaction)
                    return 0;
                else if (type == ObjectType.SpecificEnemy)
                    return 1;
                else if (type == ObjectType.Part)
                    return 2;
                else
                    throw new Exception("Object type can't be converted to quad-value");
            };

            if (name == "Has Flags") { // ObjectType.SpecificEnemy only
                if (value != GetIntValue(name)) {
                    // TODO: change type
                }
            }
            else if (name == "X" || name == "Y") {
                if (GetObjectDefinitionType() == ObjectDefinitionType.NoValue) {
                    if (value != 0) {
                        TranslateDefinitionType(ObjectDefinitionType.DoubleValue);
                        dataValueReferenceGroup.SetValue(name, value);
                    }
                }
                else if (GetObjectDefinitionType() == ObjectDefinitionType.DoubleValue) {
                    dataValueReferenceGroup.SetValue(name, value);
                    if (GetIntValue("X") == 0 && GetIntValue("Y") == 0) {
                        TranslateDefinitionType(ObjectDefinitionType.NoValue);
                    }
                }
                // Part objects have shortened Y/X positions, unless placed as a "Quadruple Value"
                // type object.
                else if (GetObjectType() == ObjectType.Part) {
                    int x = (name == "X" ? value : GetIntValue("X"));
                    int y = (name == "Y" ? value : GetIntValue("Y"));
                    if (GetIntValue("Var03") == 0 && (x-8)%16 == 0 && (y-8)%16 == 0) {
                        TranslateDefinitionType(ObjectDefinitionType.Part);
                        dataValueReferenceGroup.SetValue(name, (value-8)/16);
                    }
                    else {
                        TranslateDefinitionType(ObjectDefinitionType.QuadrupleValue, 2);
                        dataValueReferenceGroup.SetValue(name, value);
                    }
                }
                else
                    dataValueReferenceGroup.SetValue(name, value);
            }
            else if (name == "Var03") {
                if (value == 0) {
                    if (GetObjectType() == ObjectType.Interaction) {
                        if (GetIntValue("X") == 0 && GetIntValue("Y") == 0)
                            TranslateDefinitionType(ObjectDefinitionType.NoValue);
                        else
                            TranslateDefinitionType(ObjectDefinitionType.DoubleValue);
                    }
                    else if (GetObjectType() == ObjectType.SpecificEnemy)
                        TranslateDefinitionType(ObjectDefinitionType.SpecificEnemy);
                    else if (GetObjectType() == ObjectType.Part)
                        TranslateDefinitionType(ObjectDefinitionType.Part);
                }
                else {
                    // TODO: SpecificEnemy check
                    TranslateDefinitionType(ObjectDefinitionType.QuadrupleValue, GetQuadSubtype());
                    dataValueReferenceGroup.SetValue(name, value);
                }
            }
            else {
                dataValueReferenceGroup.SetValue(name, value);
            }
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
                int t = dataValueReferenceGroup.GetIntValue("Object Type");
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

        public bool HasXY() {
            return HasValue("X") && HasValue("Y");
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

        // Returns true if the object's type causes the XY values to have 4 bits rather than 8.
        // (DOES NOT account for "@postype" parameter which can set interactions to have both Y/X
        // positions stored in the Y variable.)
        bool IsTypeWithShortenedXY() {
            // Don't include "Part" objects because, when converted to the "QuadrupleValue" type,
            // they can have fine-grained position values.
            return GetObjectDefinitionType() == ObjectDefinitionType.ItemDrop;
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
