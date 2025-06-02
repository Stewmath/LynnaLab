namespace LynnaLib
{
    // This class provides an interface to edit an "object group" (list of object data ending with
    // "obj_End" or "obj_EndPointer"). For most cases it's recommended to use the "ObjectGroup"
    // class instead.
    internal class RawObjectGroup : TrackedProjectData, ProjectDataInstantiator
    {
        // ================================================================================
        // Constructor
        // ================================================================================

        private RawObjectGroup(Project p, string id) : base(p, id)
        {
            ObjectData data = Parser.GetData(Identifier) as ObjectData;

            while (data.GetObjectType() != ObjectType.End && data.GetObjectType() != ObjectType.EndPointer)
            {
                ObjectData next = data.NextData as ObjectData;

                if (data.GetObjectType() == ObjectType.Garbage)
                {
                    // We used to "Detach()" the data here to delete garbage date, but we now have
                    // strict checks in the UndoState class that disallow such modifications during
                    // initialization! Doesn't make much difference to us - it's ignored regardless.
                    //data.Detach();
                }
                else
                    ObjectDataList.Add(new(data));

                data = next;
            }
            ObjectDataList.Add(new(data));
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private RawObjectGroup(Project p, string id, TransactionState s)
            : base(p, id)
        {
            this.state = (State)s;
        }

        static ProjectDataType ProjectDataInstantiator.Instantiate(Project p, string id)
        {
            return new RawObjectGroup(p, id);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        // Undoable stuff goes here
        class State : TransactionState
        {
            public List<InstanceResolver<ObjectData>> objectDataList = new();
        }

        State state = new();
        FileParser _parser;

        // ================================================================================
        // Properties
        // ================================================================================

        FileParser Parser
        {
            get
            {
                if (_parser == null)
                    _parser = Project.GetFileWithLabel(Identifier);
                return _parser;
            }
        }

        List<InstanceResolver<ObjectData>> ObjectDataList
        {
            get { return state.objectDataList; }
            set { state.objectDataList = value; }
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public ObjectData GetObjectData(int index)
        {
            return ObjectDataList[index];
        }
        public int GetNumObjects()
        {
            return ObjectDataList.Count - 1; // Not counting InteracEnd
        }

        public void RemoveObject(int index)
        {
            if (index >= ObjectDataList.Count - 1)
                throw new Exception("Array index out of bounds.");

            Project.UndoState.CaptureInitialState<State>(this);

            ObjectData data = ObjectDataList[index];
            data.Detach();
            ObjectDataList.RemoveAt(index);
        }

        public void InsertObject(int index, ObjectData data)
        {
            Project.UndoState.CaptureInitialState<State>(this);

            data.Attach(Parser);
            data.InsertIntoParserBefore(ObjectDataList[index]);
            ObjectDataList.Insert(index, new(data));
        }

        public void InsertObject(int index, ObjectType type)
        {
            ObjectData data = new ObjectData(Project, Parser, type);
            InsertObject(index, data);
        }

        /// <summary>
        /// Makes a copy of the object data at the end of the file and moves the label to reference
        /// the new data. Useful if the data is being referenced by more than one pointer.
        /// </summary>
        internal void Repoint()
        {
            Project.UndoState.CaptureInitialState<State>(this);

            Parser.RemoveLabel(Identifier);

            FileComponent lastComponent;

            Parser.InsertParseableTextAfter(null, new String[] { "" }); // Newline
            lastComponent = new Label(Project.GenUniqueID(typeof(Label)), Parser, Identifier);
            Parser.InsertComponentAfter(null, lastComponent);

            List<InstanceResolver<ObjectData>> newList = new();
            foreach (ObjectData old in ObjectDataList)
            {
                ObjectData newData = new ObjectData(old);
                newList.Add(new(newData));
                Parser.InsertComponentAfter(lastComponent, newData);
                lastComponent = newData;
            }

            ObjectDataList = newList;
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
            this.state = (State)s;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
        }
    }
}
