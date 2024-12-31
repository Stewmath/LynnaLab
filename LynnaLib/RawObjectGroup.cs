namespace LynnaLib
{
    // This class provides an interface to edit an "object group" (list of object data ending with
    // "obj_End" or "obj_EndPointer"). For most cases it's recommended to use the "ObjectGroup"
    // class instead.
    internal class RawObjectGroup : ProjectDataType, Undoable
    {
        // ================================================================================
        // Constructor
        // ================================================================================

        internal RawObjectGroup(Project p, String id) : base(p, id)
        {
            parser = Project.GetFileWithLabel(Identifier);
            ObjectData data = parser.GetData(Identifier) as ObjectData;

            while (data.GetObjectType() != ObjectType.End && data.GetObjectType() != ObjectType.EndPointer)
            {
                ObjectData next = data.NextData as ObjectData;

                if (data.GetObjectType() == ObjectType.Garbage) // Delete these (they do nothing anyway)
                    data.Detach();
                else
                    ObjectDataList.Add(data);

                data = next;
            }
            ObjectDataList.Add(data);
        }

        // ================================================================================
        // Variables
        // ================================================================================

        // Undoable stuff goes here
        class State : TransactionState
        {
            public List<ObjectData> objectDataList = new();

            public TransactionState Copy()
            {
                State s = new State();
                s.objectDataList = new(objectDataList);
                return s;
            }

            public bool Compare(TransactionState obj)
            {
                return obj is State s && objectDataList.SequenceEqual(s.objectDataList);
            }
        }

        readonly FileParser parser;
        State state = new();

        // ================================================================================
        // Properties
        // ================================================================================

        List<ObjectData> ObjectDataList
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

            Project.UndoState.CaptureInitialState(this);

            ObjectData data = ObjectDataList[index];
            data.Detach();
            ObjectDataList.RemoveAt(index);
        }

        public void InsertObject(int index, ObjectData data)
        {
            Project.UndoState.CaptureInitialState(this);

            data.Attach(parser);
            data.InsertIntoParserBefore(ObjectDataList[index]);
            ObjectDataList.Insert(index, data);
        }

        public void InsertObject(int index, ObjectType type)
        {
            ObjectData data = new ObjectData(Project, parser, type);
            InsertObject(index, data);
        }

        /// <summary>
        /// Makes a copy of the object data at the end of the file and moves the label to reference
        /// the new data. Useful if the data is being referenced by more than one pointer.
        /// </summary>
        internal void Repoint()
        {
            Project.UndoState.CaptureInitialState(this);

            parser.RemoveLabel(Identifier);

            FileComponent lastComponent;

            parser.InsertParseableTextAfter(null, new String[] { "" }); // Newline
            lastComponent = new Label(parser, Identifier);
            parser.InsertComponentAfter(null, lastComponent);

            List<ObjectData> newList = new List<ObjectData>();
            foreach (ObjectData old in ObjectDataList)
            {
                ObjectData newData = new ObjectData(old);
                newList.Add(newData);
                parser.InsertComponentAfter(lastComponent, newData);
                lastComponent = newData;
            }

            ObjectDataList = newList;
        }

        // ================================================================================
        // Undoable interface functions
        // ================================================================================

        public TransactionState GetState()
        {
            return state;
        }

        public void SetState(TransactionState s)
        {
            this.state = (State)s.Copy();
        }

        public void InvokeModifiedEvent(TransactionState prevState)
        {
        }
    }
}
