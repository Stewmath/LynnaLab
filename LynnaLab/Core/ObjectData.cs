using System;
using System.Collections.Generic;

namespace LynnaLab {
    public enum ObjectType {
        Condition=0, // TODO: rename
        Interaction,
        Pointer,
        BeforeEvent,
        AfterEvent,
        RandomEnemy,
        SpecificEnemyA, // Normal (has flags, not var03)
        SpecificEnemyB, // QuadrupleValue (has var03, not flags)
        Part,
        ItemDrop,
        End,
        EndPointer,
        Garbage
    }

    public class ObjectData : Data {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static string[] ObjectCommands = {
            "obj_Condition",
            "obj_Interaction",
            "obj_Pointer",
            "obj_BeforeEvent",
            "obj_AfterEvent",
            "obj_RandomEnemy",
            "obj_SpecificEnemyA",
            "obj_SpecificEnemyB",
            "obj_Part",
            "obj_ItemDrop",
            "obj_End",
            "obj_EndPointer",
            "obj_Garbage"
        };

        // # of parameters for each opcode, used by FileParser.cs
        public static int[] ObjectCommandMinParams = {
            1,  1,  1,  1,  1,  2,  3,  4,  2,  2,  0,  0, 2
        };

        public static int[] ObjectCommandMaxParams = {
            -1, 4, -1, -1, -1, -1,  4, -1,  4,  3, -1, -1, 2
        };

        public static int[] ObjectCommandDefaultParams = {
            1,  1,  1,  1,  1,  2,  4,  4,  2,  3,  0,  0, 2
        };



        private static IList<ValueReference> GetObjectValueReferences(ObjectType type, Data data) {
            // Interaction, Part, SpecificEnemyA, and ItemDrop objects have a variable number of
            // parameters. In the constructor, the data is set to always have the maximum number of
            // parameters so that all of these ValueReferences are valid. The extra parameters are
            // removed when it is time to save.
            switch (type) {
            case ObjectType.Condition:
                return new List<ValueReference> { // Condition
                    new DataValueReference(data,"Condition",0,DataValueType.Byte),
                };
            case ObjectType.Interaction:
                return new List<ValueReference> { // Interaction
                    new DataValueReference(data,"ID",0,DataValueType.WordBits,8,15,true,"InteractionMapping"),
                    new DataValueReference(data,"SubID",0,DataValueType.WordBits,0,7),
                    new DataValueReference(data,"Y",1,DataValueType.Byte),
                    new DataValueReference(data,"X",2,DataValueType.Byte),
                    new DataValueReference(data,"Var03",3,DataValueType.Byte),
                };
            case ObjectType.Pointer:
                return new List<ValueReference> { // Pointer
                    new DataValueReference(data,"Pointer",0,DataValueType.ObjectPointer),
                };
            case ObjectType.BeforeEvent:
                return new List<ValueReference> { // BeforeEvent
                    new DataValueReference(data,"Pointer",0,DataValueType.ObjectPointer),
                };
            case ObjectType.AfterEvent:
                return new List<ValueReference> { // AfterEvent
                    new DataValueReference(data,"Pointer",0,DataValueType.ObjectPointer),
                };
            case ObjectType.RandomEnemy:
                return new List<ValueReference> { // Random Enemy
                    new DataValueReference(data,"Flags",0,DataValueType.Byte,editable:false),
                    new DataValueReference(data,"Respawn",0,DataValueType.ByteBit,0,0),
                    new DataValueReference(data,"Uncounted",0,DataValueType.ByteBit,1,1),
                    new DataValueReference(data,"Spawn anywhere",0,DataValueType.ByteBit,2,2),
                    new DataValueReference(data,"Quantity",0,DataValueType.ByteBits,5,7),
                    new DataValueReference(data,"ID",1,DataValueType.WordBits,8,15,true,"EnemyMapping"),
                    new DataValueReference(data,"SubID",1,DataValueType.WordBits,0,7),
                };
            case ObjectType.SpecificEnemyA:
                return new List<ValueReference> { // Specific Enemy A
                    new DataValueReference(data,"Flags",0,DataValueType.Byte,editable:false),
                    new DataValueReference(data,"Respawn",0,DataValueType.ByteBit,0,0),
                    new DataValueReference(data,"Uncounted",0,DataValueType.ByteBit,1,1),
                    new DataValueReference(data,"ID",1,DataValueType.WordBits,8,15,true,"EnemyMapping"),
                    new DataValueReference(data,"SubID",1,DataValueType.WordBits,0,7),
                    new DataValueReference(data,"Y",2,DataValueType.Byte),
                    new DataValueReference(data,"X",3,DataValueType.Byte),
                };
            case ObjectType.SpecificEnemyB:
                return new List<ValueReference> { // Specific Enemy B
                    new DataValueReference(data,"ID",0,DataValueType.WordBits,8,15),
                    new DataValueReference(data,"SubID",0,DataValueType.WordBits,0,7),
                    new DataValueReference(data,"Y",1,DataValueType.Byte),
                    new DataValueReference(data,"X",2,DataValueType.Byte),
                    new DataValueReference(data,"Var03",3,DataValueType.Byte),
                };
            case ObjectType.Part:
                return new List<ValueReference> { // Part
                    new DataValueReference(data,"ID",0,DataValueType.WordBits,8,15,true,"PartMapping"),
                    new DataValueReference(data,"SubID",0,DataValueType.WordBits,0,7),
                    new DataValueReference(data,"Y",1,DataValueType.Byte),
                    new DataValueReference(data,"X",2,DataValueType.Byte),
                    new DataValueReference(data,"Var03",3,DataValueType.Byte),
                };
            case ObjectType.ItemDrop:
                return new List<ValueReference> { // Item Drop
                    new DataValueReference(data,"Flags",0,DataValueType.Byte,editable:false),
                    new DataValueReference(data,"Respawn",0,DataValueType.ByteBit,0,0),
                    new DataValueReference(data,"Item",1,DataValueType.Byte),
                    new DataValueReference(data,"Y",2,DataValueType.ByteBits,4,7),
                    new DataValueReference(data,"X",2,DataValueType.ByteBits,0,3),
                };
            case ObjectType.End:
                return new List<ValueReference> { // InteracEnd
                };
            case ObjectType.EndPointer:
                return new List<ValueReference> { // InteracEndPointer
                };
            case ObjectType.Garbage:
                return new List<ValueReference> { // Garbage
                };
            }
            return null;
        }


        private ObjectType objectType;
        private ValueReferenceGroup vrg;


        // Properties
        public ValueReferenceGroup ValueReferenceGroup {
            get { return vrg; }
        }

        
        // Constructors

        public ObjectData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing, int objType, ObjectData last)
            : base(p, command, values, -1, parser, spacing) {

            this.objectType = (ObjectType)objType;
            InitializeValueReferenceGroup();

            ExpandParameters(last);

            // We "modified" the data but only for internal purposes, it doesn't need to be saved
            // unless something else modifies it.
            base.Modified = false;
        }

        // Creates a new ObjectData instance, not based on existing data.
        // Unlike above, this initialized based on the "ObjectType" enum instead of
        // "ObjectDefinitionType".
        public ObjectData(Project p, FileParser parser, ObjectType type)
            : base(p, "", null, -1, parser, new string[]{"\t"}) {
            this.objectType = type;

            Command = ObjectCommands[(int)objectType];
            base.SetNumValues(ObjectCommandDefaultParams[(int)objectType], "$00");

            InitializeValueReferenceGroup();

            base.LockModifiedEvents();

            foreach (ValueReference vref in vrg.GetValueReferences())
                vref.Initialize();

            base.ClearAndUnlockModifiedEvents();
        }

        // Copy constructor
        public ObjectData(ObjectData o)
            : base(o.Project, "", null, -1, null, new string[]{"\t"})
        {
            this.Command = o.Command;
            this.objectType = o.objectType;

            base.SetNumValues(o.GetNumValues(), "$00");
            for (int i=0; i<o.GetNumValues(); i++)
                SetValue(i, o.GetValue(i));

            InitializeValueReferenceGroup();
        }

        // Common code for constructors
        void InitializeValueReferenceGroup() {
            vrg = new ValueReferenceGroup(GetObjectValueReferences(objectType, this));
        }

        public ObjectType GetObjectType() {
            return objectType;
        }

        // Get the object group pointed to, or null if no such group
        // exists.
        public ObjectGroup GetPointedObjectGroup() {
            if (!(objectType >= ObjectType.Pointer && objectType <= ObjectType.AfterEvent))
                return null;

            try {
                Project.GetFileWithLabel(GetValue(0));
                return Project.GetDataType<ObjectGroup>(GetValue(0));
            }
            catch(InvalidLookupException) {
                return null;
            }
        }

        public bool IsPointerType() {
            ObjectType type = GetObjectType();
            return type == ObjectType.Pointer || type == ObjectType.BeforeEvent || type == ObjectType.AfterEvent;
        }

        public override string GetString() {
            // "ContractParameters" puts this into an invalid state, so we need to prevent callbacks
            base.LockModifiedEvents();

            ContractParameters(GetLastObjectData());
            string retval = base.GetString();

            // Undo "contraction" of parameters (put object back into valid state)
            ExpandParameters(GetLastObjectData());

            base.ClearAndUnlockModifiedEvents();
            return retval;
        }


        // Private methods

        // Returns true if this object reuses a byte from the last one
        bool IsShortened() {
            return ((GetObjectType() == ObjectType.SpecificEnemyA && base.GetNumValues() < 4) ||
                    (GetObjectType() == ObjectType.ItemDrop && base.GetNumValues() < 3));
        }

        // For types which take a variable number of parameters, this sets the number of values to
        // the maximum, inserting placeholders where necessary. This is done as soon as the data is
        // loaded, and is only undone temporarily while saving.
        void ExpandParameters(ObjectData last) {
            // For types which can reuse the first type, get and remember that value.
            if (IsShortened()) {
                if (last == null || (last.GetObjectType() != GetObjectType()))
                    this.ThrowException(new AssemblyErrorException("Malformatted object"));
                base.SetSpacing(1, " ");
                base.InsertValue(0, last.GetValue(0));
            }
            else if (objectType == ObjectType.Interaction) {
                if (base.GetNumValues() < 4)
                    base.SetNumValues(4, "$00");
            }
            else if (objectType == ObjectType.Part) {
                if (base.GetNumValues() != 2 && base.GetNumValues() != 4)
                    log.Warn("Part object has an unexpected number of parameters: " + base.GetString());
                if (base.GetNumValues() < 4) {
                    int y = (base.GetIntValue(1) & 0xf0) + 8;
                    int x = ((base.GetIntValue(1) & 0x0f) << 4) + 8;
                    base.SetNumValues(4, "$00");
                    base.SetValue(1, Wla.ToHex(y, 2));
                    base.SetValue(2, Wla.ToHex(x, 2));
                }
            }
        }

        // The opposite of ExpandParameters; called while saving.
        void ContractParameters(ObjectData last) {
            // For types which can reuse the first type, get and remember that value.
            if (GetObjectType() == ObjectType.SpecificEnemyA || GetObjectType() == ObjectType.ItemDrop) {
                if (last != null && last.GetObjectType() == this.GetObjectType()
                        && last.GetIntValue(0) == this.GetIntValue(0)) {
                    base.RemoveValue(0);
                    base.SetSpacing(1, "     ");
                }
            }
            else if (objectType == ObjectType.Interaction) {
                if (base.GetIntValue(3) == 0) { // Check var03
                    if (base.GetIntValue(1) == 0 && base.GetIntValue(2) == 0) // Check Y, X
                        base.SetNumValues(1, "$00");
                    else
                        base.SetNumValues(3, "$00");
                }
            }
            else if (objectType == ObjectType.Part) {
                bool expanded = false;

                if (base.GetIntValue(3) != 0) // Check var03
                    expanded = true;
                else if ((base.GetIntValue(1) - 8) % 16 != 0 || (base.GetIntValue(2) - 8) % 16 != 0) // Check Y, X
                    expanded = true;

                if (!expanded) {
                    int yx = (((base.GetIntValue(1) - 8) / 16) << 4) | ((base.GetIntValue(2) - 8) / 16);
                    base.SetNumValues(2, "$00");
                    base.SetValue(1, Wla.ToHex(yx, 2));
                }
            }
        }

        // This finds the last "ObjectData" before this one. However, if there is a label between
        // this ObjectData and the last one, this returns null. This is necessary because, for
        // object data types which reuse values, they must not reuse a value from an ObjectData that
        // came before a label.
        ObjectData GetLastObjectData() {
            FileComponent last = Prev;

            while (last != null) {
                if (last is Label)
                    return null;
                if (last is Data)
                    return last as ObjectData;
                last = last.Prev;
            }

            return null;
        }
    }
}
