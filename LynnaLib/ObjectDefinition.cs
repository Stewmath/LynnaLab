namespace LynnaLib
{
    /// Public interface over the ObjectData class.
    public class ObjectDefinition : ValueReferenceGroup
    {
        ObjectGroup objectGroup;
        ObjectData objectData;


        // Constructors

        public ObjectDefinition(ObjectGroup group, ObjectData od, int index)
        {
            this.objectGroup = group;
            this.objectData = od;
            this.Index = index;

            var descriptors = new List<ValueReferenceDescriptor>();

            foreach (var desc in objectData.ValueReferenceGroup.GetDescriptors())
            {
                string name = desc.Name;
                var vref = desc.ValueReference;

                // Create a new AbstractIntValueReference which INDIRECTLY reads from the old
                // ValueReference. The underlying ValueReference may be changed; so it's important
                // that the getter and setter functions are redefined in an indirect way.
                var newVref = new AbstractIntValueReference(
                        vref,
                        maxValue: vref.MaxValue,
                        minValue: vref.MinValue,
                        getter: () => objectData.ValueReferenceGroup.GetIntValue(name),
                        setter: (v) => OnValueSet(name, v));
                descriptors.Add(new ValueReferenceDescriptor(newVref, name));
            }

            base.SetDescriptors(descriptors);
            base.EnableTransactions("Edit Object");
        }


        // Properties

        public ObjectGroup ObjectGroup { get { return objectGroup; } }
        public int Index { get; private set; } // Index within the ObjectGroup


        // Public methods

        public ObjectType GetObjectType()
        {
            return objectData.GetObjectType();
        }

        /// <summary>
        ///  Returns true if the X/Y variables are 4-bits instead of 8 (assuming it has X/Y in the
        ///  first place).
        /// </summary>
        public bool HasShortenedXY()
        {
            return IsTypeWithShortenedXY() || GetSubIDDocumentation()?.GetField("postype") == "short";
        }

        /// Returns true if this type has X/Y variables, AND we don't have "postype == none".
        public bool HasXY()
        {
            return HasValue("X") && HasValue("Y") && GetSubIDDocumentation()?.GetField("postype") != "none";
        }

        // Return the center x-coordinate of the object.
        // This is different from 'GetIntValue("X")' because sometimes objects store both their Y and
        // X values in one byte. This will take care of that, and will multiply the value when the
        // positions are in this short format (ie. range $0-$f becomes $08-$f8).
        public byte GetX()
        {
            if (GetSubIDDocumentation()?.GetField("postype") == "short")
            {
                int n = GetIntValue("Y") & 0xf;
                return (byte)(n * 16 + 8);
            }
            else if (IsTypeWithShortenedXY())
            {
                int n = GetIntValue("X");
                return (byte)(n * 16 + 8);
            }
            else
                return (byte)GetIntValue("X");
        }
        // Return the center y-coordinate of the object
        public byte GetY()
        {
            if (GetSubIDDocumentation()?.GetField("postype") == "short")
            {
                int n = GetIntValue("Y") >> 4;
                return (byte)(n * 16 + 8);
            }
            else if (IsTypeWithShortenedXY())
            {
                int n = GetIntValue("Y");
                return (byte)(n * 16 + 8);
            }
            else
                return (byte)GetIntValue("Y");
        }

        public void SetX(byte n)
        {
            if (GetSubIDDocumentation()?.GetField("postype") == "short")
            {
                byte y = (byte)(GetIntValue("Y") & 0xf0);
                y |= (byte)(n / 16);
                SetValue("Y", y);
            }
            else if (IsTypeWithShortenedXY())
                SetValue("X", n / 16);
            else
                SetValue("X", n);
        }
        public void SetY(byte n)
        {
            if (GetSubIDDocumentation()?.GetField("postype") == "short")
            {
                byte y = (byte)(GetIntValue("Y") & 0x0f);
                y |= (byte)(n & 0xf0);
                SetValue("Y", y);
            }
            else if (IsTypeWithShortenedXY())
                SetValue("Y", n / 16);
            else
                SetValue("Y", n);
        }

        public GameObject GetGameObject()
        {
            if (GetObjectType() == ObjectType.Interaction)
            {
                int id = GetIntValue("ID");
                int subid = GetIntValue("SubID");
                return Project.GetIndexedDataType<InteractionObject>((id << 8) | subid);
            }
            else if (GetObjectType() == ObjectType.RandomEnemy
                     || GetObjectType() == ObjectType.SpecificEnemyA
                     || GetObjectType() == ObjectType.SpecificEnemyB)
            {
                int id = GetIntValue("ID");
                int subid = GetIntValue("SubID");
                if (id >= 0x80)
                    return null;
                return Project.GetIndexedDataType<EnemyObject>((id << 8) | subid);
            }
            else if (GetObjectType() == ObjectType.Part)
            {
                int id = GetIntValue("ID");
                int subid = GetIntValue("SubID");
                if (id >= 0x80)
                    return null;
                return Project.GetIndexedDataType<PartObject>((id << 8) | subid);
            }
            // TODO: other types
            return null;
        }

        public Documentation GetIDDocumentation()
        {
            return GetGameObject()?.GetIDDocumentation();
        }

        public Documentation GetSubIDDocumentation()
        {
            return GetGameObject()?.GetSubIDDocumentation();
        }

        // Remove self from the parent ObjectGroup.
        public void Remove()
        {
            objectGroup.RemoveObject(Index);
        }


        internal void SetObjectData(ObjectData data)
        {
            objectData = data;
        }

        internal void UpdateIndex()
        {
            Index = objectGroup.IndexOf(this);
            if (Index == -1)
                throw new Exception("Unexpected thing happened");
        }


        // Returns true if the object's type causes the XY values to have 4 bits rather than 8.
        // (DOES NOT account for "@postype" parameter which can set interactions to have both Y/X
        // positions stored in the Y variable.)
        bool IsTypeWithShortenedXY()
        {
            // Don't include "Part" objects because, when converted to the "QuadrupleValue" type,
            // they can have fine-grained position values.
            return GetObjectType() == ObjectType.ItemDrop;
        }

        void OnValueSet(string name, int value)
        {
            if (objectData.ValueReferenceGroup.GetIntValue(name) == value)
                return;
            objectGroup.Isolate();
            objectData.ValueReferenceGroup.SetValue(name, value);
        }
    }
}
