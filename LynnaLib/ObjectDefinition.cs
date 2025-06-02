namespace LynnaLib
{
    /// Public interface over the ObjectData class.
    public class ObjectDefinition : TrackedProjectData
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        public ObjectDefinition(ObjectGroup group, ObjectData od, int uniqueID)
            : base(group.Project, CreateIdentifier(group.Identifier, uniqueID))
        {
            this.state = new()
            {
                objectGroupIR = new InstanceResolver<ObjectGroup>(group),
                uniqueID = uniqueID,
                objectDataIR = new(od)
            };
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private ObjectDefinition(Project p, string id, TransactionState state)
            : base(p, id)
        {
            this.state = (State)state;
            string expectedID = CreateIdentifier(this.state.objectGroupIR.Identifier, this.state.uniqueID);
            if (Identifier != expectedID)
                throw new DeserializationException($"Bad ObjectDefinition ID; expected {expectedID}, got {Identifier}");
        }

        void CreateValueReferenceGroup()
        {
            var descriptors = new List<ValueReferenceDescriptor>();

            foreach (var desc in ObjectData.ValueReferenceGroup.GetDescriptors())
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
                        getter: () => ObjectData.ValueReferenceGroup.GetIntValue(name),
                        setter: (v) => OnValueSet(name, v));
                descriptors.Add(new ValueReferenceDescriptor(newVref, name));
            }

            _vrg = new ValueReferenceGroup(descriptors);
            _vrg.EnableTransactions($"Edit Object#{TransactionIdentifier}", true);
        }

        static string CreateIdentifier(string groupID, int uniqueID)
        {
            return $"{groupID}-{uniqueID}";
        }

        // ================================================================================
        // Variables
        // ================================================================================

        // Undo-able stuff goes here
        internal struct State : TransactionState
        {
            public required InstanceResolver<ObjectGroup> objectGroupIR { get; init; }
            public required int uniqueID { get; init; } // Value that's unique compared to other ObjectDefinitions in this ObjectGroup

            public required InstanceResolver<ObjectData> objectDataIR { get; set; } // Can be null
        }

        ValueReferenceGroup _vrg;

        State state;

        // ================================================================================
        // Properties
        // ================================================================================

        public ObjectGroup ObjectGroup { get { return state.objectGroupIR; } }

        public ValueReferenceGroup ValueReferenceGroup { get { return vrg; } }

        public string TransactionIdentifier { get { return $"obj-{ObjectGroup.Identifier}-i{UniqueID}"; } }

        int UniqueID { get { return state.uniqueID; } }

        ObjectData ObjectData
        {
            get { return state.objectDataIR; }
            set { state.objectDataIR = new(value); }
        }

        ValueReferenceGroup vrg
        {
            get
            {
                if (_vrg == null)
                    CreateValueReferenceGroup();
                return _vrg;
            }
        }


        // ================================================================================
        // Public methods
        // ================================================================================

        public ObjectType GetObjectType()
        {
            return ObjectData.GetObjectType();
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
            return vrg.HasValue("X") && vrg.HasValue("Y") && GetSubIDDocumentation()?.GetField("postype") != "none";
        }

        // Return the center x-coordinate of the object.
        // This is different from 'GetIntValue("X")' because sometimes objects store both their Y and
        // X values in one byte. This will take care of that, and will multiply the value when the
        // positions are in this short format (ie. range $0-$f becomes $08-$f8).
        public byte GetX()
        {
            if (GetSubIDDocumentation()?.GetField("postype") == "short")
            {
                int n = vrg.GetIntValue("Y") & 0xf;
                return (byte)(n * 16 + 8);
            }
            else if (IsTypeWithShortenedXY())
            {
                int n = vrg.GetIntValue("X");
                return (byte)(n * 16 + 8);
            }
            else
                return (byte)vrg.GetIntValue("X");
        }
        // Return the center y-coordinate of the object
        public byte GetY()
        {
            if (GetSubIDDocumentation()?.GetField("postype") == "short")
            {
                int n = vrg.GetIntValue("Y") >> 4;
                return (byte)(n * 16 + 8);
            }
            else if (IsTypeWithShortenedXY())
            {
                int n = vrg.GetIntValue("Y");
                return (byte)(n * 16 + 8);
            }
            else
                return (byte)vrg.GetIntValue("Y");
        }

        public void SetX(byte n)
        {
            if (GetSubIDDocumentation()?.GetField("postype") == "short")
            {
                byte y = (byte)(vrg.GetIntValue("Y") & 0xf0);
                y |= (byte)(n / 16);
                vrg.SetValue("Y", y);
            }
            else if (IsTypeWithShortenedXY())
                vrg.SetValue("X", n / 16);
            else
                vrg.SetValue("X", n);
        }
        public void SetY(byte n)
        {
            if (GetSubIDDocumentation()?.GetField("postype") == "short")
            {
                byte y = (byte)(vrg.GetIntValue("Y") & 0x0f);
                y |= (byte)(n & 0xf0);
                vrg.SetValue("Y", y);
            }
            else if (IsTypeWithShortenedXY())
                vrg.SetValue("Y", n / 16);
            else
                vrg.SetValue("Y", n);
        }

        public GameObject GetGameObject()
        {
            if (GetObjectType() == ObjectType.Interaction)
            {
                int id = vrg.GetIntValue("ID");
                int subid = vrg.GetIntValue("SubID");
                return Project.GetIndexedDataType<InteractionObject>((id << 8) | subid);
            }
            else if (GetObjectType() == ObjectType.RandomEnemy
                     || GetObjectType() == ObjectType.SpecificEnemyA
                     || GetObjectType() == ObjectType.SpecificEnemyB)
            {
                int id = vrg.GetIntValue("ID");
                int subid = vrg.GetIntValue("SubID");
                if (id >= 0x80)
                    return null;
                return Project.GetIndexedDataType<EnemyObject>((id << 8) | subid);
            }
            else if (GetObjectType() == ObjectType.Part)
            {
                int id = vrg.GetIntValue("ID");
                int subid = vrg.GetIntValue("SubID");
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
            ObjectGroup.RemoveObject(this);
        }

        public void CopyFrom(ObjectDefinition obj)
        {
            vrg.CopyFrom(obj.vrg);
        }


        internal void SetObjectData(ObjectData data)
        {
            Project.UndoState.CaptureInitialState<State>(this);
            ObjectData = data;
        }

        // ================================================================================
        // TrackedProjectData interface functions
        // ================================================================================

        public override TransactionState GetState()
        {
            return state;
        }

        public override void SetState(TransactionState s)
        {
            State newState = (State)s;
            Helper.Assert(newState.objectDataIR != null);
            this.state = newState;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        // Returns true if the object's type causes the XY values to have 4 bits rather than 8.
        // (DOES NOT account for "@postype" parameter which can set interactions to have both Y/X
        // positions stored in the Y variable.)
        bool IsTypeWithShortenedXY()
        {
            // Don't include "Part" objects because, when converted to the "QuadrupleValue" type,
            // they can have fine-grained position values.
            return GetObjectType() == ObjectType.ItemDrop;
        }

        /// <summary>
        /// Maps writes from our ValueReferenceGroup (implemented with AbstractValueReferences) to
        /// the underlying ObjectData instance that this is wrapping over.
        /// </summary>
        void OnValueSet(string name, int value)
        {
            if (ObjectData.ValueReferenceGroup.GetIntValue(name) == value)
                return;
            ObjectGroup.Isolate();
            ObjectData.ValueReferenceGroup.SetValue(name, value);
        }
    }
}
