using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace LynnaLib
{
    public enum ObjectGroupType
    {
        Main,        // Top-level (has its own objects and also holds other ObjectGroups as children)
        Enemy,       // "obj_Pointer" referring to data named "enemyObjectData[...]".
        BeforeEvent, // "obj_BeforeEvent" pointer
        AfterEvent,  // "obj_AfterEvent" pointer
        Shared       // "obj_Pointer" referring to something other than "enemyObjectData[...]".
    };

    // This is similar to "RawObjectGroup", but it provides a different interface for handling
    // "Pointers" to other object data. In particular, this never returns an ObjectDefinition that
    // is a pointer type. Instead, you need to call "GetAllGroups()" to get other ObjectGroup's that
    // correspond to what those pointers point to.
    //
    // Assumptions made:
    //   - The "children" do not, themselves, have any children. The tree is at most 1 level deep.
    //   - The "Main" data for each group is named "groupXMapYYObjectData".
    //   - All "EnemyData" and "Before/AfterEvent" references are named uniquely based on
    //     their room number. This is almost always true. There is one case in Seasons
    //     ("group5Map10EnemyData"; fairy fountains) which cannot follow this rule because multiple
    //     rooms use the same "obj_Pointer" opcode. As a result, LynnaLib cannot separate the data
    //     properly between rooms. The hack-base branch of the disassembly solves this by fully
    //     separating the data; however, on the master branch, edits to the objects in one room will
    //     affect others as well.
    //
    // Other notes:
    //   - This class makes no guarantees about the ordering of pointers. This probably isn't very
    //     important, but there could be edge-cases where the order of object loading matters.
    //   - This generally isn't designed to work if the underlying ObjectData or RawObjectGroup
    //     structures are modified independently. This should be the primary (only) interface for
    //     editing objects.
    //   - Some groups share their data (ie. groups 4/6 and 5/7 in both games, + seasons groups
    //     1-3). In this case, a label named, say, "group4Map00ObjectData" would also apply to group
    //     6 despite the name. This shouldn't cause any problems.
    public class ObjectGroup : TrackedProjectData
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // ================================================================================
        // Constructors
        // ================================================================================

        internal ObjectGroup(Project p, String id, ObjectGroupType type) : base(p, id)
        {
            Children = new List<InstanceResolver<ObjectGroup>>();
            Children.Add(new(this));

            state.type = type;

            if (!Project.HasLabel(id))
                return; // This will be considered a "stub" type until data is given to it

            RawObjectGroup = Project.GetDataType<RawObjectGroup>(Identifier, createIfMissing: true);

            bool addedEnemyType = false;

            ObjectList = new List<ObjectStruct>();

            for (int i = 0; i < RawObjectGroup.GetNumObjects(); i++)
            {
                ObjectData obj = RawObjectGroup.GetObjectData(i);
                ObjectType objectType = obj.GetObjectType();
                if (obj.IsPointerType())
                {
                    string label = obj.GetValue(0);

                    ObjectGroupType t;

                    if (objectType == ObjectType.BeforeEvent)
                        t = ObjectGroupType.BeforeEvent;
                    else if (objectType == ObjectType.AfterEvent)
                        t = ObjectGroupType.AfterEvent;
                    else if (objectType == ObjectType.Pointer && label.Contains("EnemyObjectData"))
                    {
                        // TODO: Check the full name
                        t = ObjectGroupType.Enemy;
                        if (addedEnemyType)
                            log.Warn("Found multiple Enemy pointers on " + Identifier + "!");
                        addedEnemyType = true;
                    }
                    else if (objectType == ObjectType.Pointer)
                        t = ObjectGroupType.Shared;
                    else
                        throw new Exception("Unexpected thing happened");

                    ObjectGroup child = Project.GetObjectGroup(label, t);
                    Children.Add(new(child));
                    child.AddParent(this);
                }
                else
                {
                    var def = new ObjectDefinition(this, obj, state.uniqueIDCounter++);

                    ObjectStruct st = new ObjectStruct
                    {
                        rawIndex = i,
                        def = new(def),
                        data = new(obj)
                    };
                    ObjectList.Add(st);
                }
            }

            if (state.type == ObjectGroupType.Main)
            {
                // Get the room number based on the identifier name.
                Regex rx = new Regex(@"group([0-9])Map([0-9a-f]{2})ObjectData",
                        RegexOptions.Compiled | RegexOptions.IgnoreCase);

                MatchCollection matches = rx.Matches(Identifier);

                if (matches.Count != 1)
                {
                    log.Error("ObjectGroup '" + Identifier + "' has an unexpected name. Can't get room number.");
                    state.type = ObjectGroupType.Shared;
                }
                else
                {
                    string groupString = matches[0].Groups[1].Value;
                    string roomString = matches[0].Groups[2].Value;

                    System.Action<ObjectGroupType, string> AddGroupIfMissing = (t, basename) =>
                    {
                        foreach (ObjectGroup group in Children)
                        {
                            if (group.Type == t)
                                return;
                        }

                        string childId = string.Format(basename, groupString, roomString);

                        if (Project.HasLabel(childId))
                        {
                            log.Error("Can't create object group '" + childId + "' because it exists already.");
                        }
                        else
                        {
                            ObjectGroup child = Project.GetObjectGroup(childId, t);
                            Children.Add(new(child));
                            child.AddParent(this);
                        }
                    };

                    AddGroupIfMissing(ObjectGroupType.Enemy, "group{0}Map{1}EnemyObjectData");
                    AddGroupIfMissing(ObjectGroupType.BeforeEvent, "group{0}Map{1}BeforeEventObjectData");
                    AddGroupIfMissing(ObjectGroupType.AfterEvent, "group{0}Map{1}AfterEventObjectData");
                }
            }

            Children.Sort((a, b) => a.Instance.Type.CompareTo(b.Instance.Type));

            // Sanity check
            UpdateRawIndices(verify: true);
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private ObjectGroup(Project p, string id, TransactionState s)
            : base(p, id)
        {
            this.state = (State)s;
        }


        // ================================================================================
        // Variables
        // ================================================================================

        // Undo-able stuff goes here
        class State : TransactionState
        {
            public ObjectGroupType type;

            public int uniqueIDCounter = 0;

            // The RawObjectGroup corresponding to this ObjectGroup. If null, it doesn't actually
            // exist, but it can be created at any time.
            // IE. A room with no enemy objects defined typically has its "enemy" object group as a
            // stub, as there is no pointer to any enemy data in its "main" object group.
            public InstanceResolver<RawObjectGroup> rawObjectGroup; // Can be null

            public List<ObjectGroup.ObjectStruct> objectList = new();

            // List of ObjectGroups that reference this ObjectGroup.
            // NOTE: this list may not be complete at any given time. It depends on which ObjectGroups
            // have been loaded.
            // Typically though, aside from ObjectGroupType.Shared, there should be at most one parent
            // if the assumptions outlined earlier are correct? Even if multiple top-level object groups
            // point to the same data, said data should have multiple different labels which are
            // represented as different ObjectGroup instances, each of which will point to its own
            // parent. (This breaks down if they use the same label.)
            // Honestly, it is weird to be tracking this in the State class! An undo would undo the
            // discovery of a new parent because of a newly loaded ObjectGroup... this needs to be
            // rethought. But it shouldn't matter in most cases.
            public List<InstanceResolver<ObjectGroup>> parents = new();

            // List of ObjectGroups referenced by this ObjectGroup (includes self)
            public List<InstanceResolver<ObjectGroup>> children = new();
        }

        State state = new();

        // ================================================================================
        // Events
        // ================================================================================

        // Invoked when the structure of the object group is modified (ie. adding, deleting, or
        // rearranging objects). Not invoked when an object's data is modified.
        public event EventHandler<EventArgs> StructureModifiedEvent;

        // ================================================================================
        // Properties
        // ================================================================================

        ObjectGroupType Type { get { return state.type; } }

        RawObjectGroup RawObjectGroup
        {
            get
            {
                return state.rawObjectGroup?.Instance;
            }
            set
            {
                if (value == null)
                    state.rawObjectGroup = null;
                else
                    state.rawObjectGroup = new(value);
            }
        }

        List<ObjectGroup.ObjectStruct> ObjectList
        {
            get { return state.objectList; }
            set { state.objectList = value; }
        }

        List<InstanceResolver<ObjectGroup>> Parents
        {
            get { return state.parents; }
            set { state.parents = value; }
        }
        List<InstanceResolver<ObjectGroup>> Children
        {
            get { return state.children; }
            set { state.children = value; }
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public ObjectGroupType GetGroupType()
        {
            return Type;
        }

        // Gets all non-pointer objects in this group. This is a shallow operation (does not check
        // pointers).
        public IReadOnlyList<ObjectDefinition> GetObjects()
        {
            if (IsStub())
                return new List<ObjectDefinition>();
            return new List<ObjectDefinition>(ObjectList.Select((ObjectStruct o) => o.def.Instance));
        }

        public int GetNumObjects()
        {
            if (IsStub())
                return 0;
            return ObjectList.Count;
        }

        public ObjectDefinition GetObject(int index)
        {
            if (IsStub())
                throw new IndexOutOfRangeException();
            return ObjectList[index].def;
        }

        public int IndexOf(ObjectDefinition obj)
        {
            for (int i = 0; i < ObjectList.Count; i++)
            {
                if (ObjectList[i].def.Instance == obj)
                    return i;
            }
            return -1;
        }

        // Returns a list of ObjectGroups representing each main group (Main, Enemy, BeforeEvent,
        // AfterEvent) plus "Shared" groups if they exist. This includes itself.
        // In most cases this should only be called on the "Main" type. For other types, typically
        // this returns a list containing only itself. Though it is technically possible for child
        // groups to contain children themselves, it is not normal.
        public IEnumerable<ObjectGroup> GetAllGroups()
        {
            return Children.Select((g) => g.Instance);
        }

        /// <summary>
        /// Adds a new object, returns the index of the newly created object. (Or at least, returns
        /// the index of the last object in the list, or -1. In theory, event handlers could modify
        /// the object list again before this returns.)
        /// </summary>
        public int AddObject(ObjectType t)
        {
            Project.BeginTransaction("Add object");
            Project.UndoState.CaptureInitialState<State>(this);

            AddObjectToEnd(t);
            ModifiedHandler(this, null);

            Project.EndTransaction();

            return GetNumObjects() - 1;
        }

        /// <summary>
        /// Adds a clone of an existing object, returns the last object in the list like above.
        /// </summary>
        public int AddObjectClone(ObjectDefinition obj)
        {
            Project.BeginTransaction("Clone object");
            Project.UndoState.CaptureInitialState<State>(this);

            ObjectDefinition newObj = AddObjectToEnd(obj.GetObjectType());
            newObj.CopyFrom(obj);
            ModifiedHandler(this, null);

            Project.EndTransaction();

            return GetNumObjects() - 1;
        }

        public void RemoveObject(int index)
        {
            Project.BeginTransaction("Delete object");
            Project.UndoState.CaptureInitialState<State>(this);

            if (IsStub() || index >= ObjectList.Count)
                throw new ArgumentException("Argument index=" + index + " is too high.");

            Isolate();

            RawObjectGroup.RemoveObject(ObjectList[index].rawIndex);

            ObjectList.RemoveAt(index);

            UpdateRawIndices();
            ModifiedHandler(this, null);

            Project.EndTransaction();
        }

        public void RemoveObject(ObjectDefinition obj)
        {
            int i = 0;
            foreach (var s in ObjectList)
            {
                if (s.def.Instance == obj)
                {
                    RemoveObject(i);
                    return;
                }
                i++;
            }
            throw new Exception("Object to remove not found");
        }

        public void MoveObject(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex)
                return;

            Project.BeginTransaction($"Rearrange objects#{Identifier}", true);
            Project.UndoState.CaptureInitialState<State>(this);

            Isolate();

            ObjectStruct oldSt = ObjectList[oldIndex];
            ObjectStruct newSt = ObjectList[newIndex];

            int oldRaw = oldSt.rawIndex;
            int newRaw = newSt.rawIndex;

            ObjectData data = RawObjectGroup.GetObjectData(oldRaw);
            RawObjectGroup.RemoveObject(oldRaw);
            RawObjectGroup.InsertObject(newRaw, data);

            ObjectList.RemoveAt(oldIndex);
            ObjectList.Insert(newIndex, oldSt);

            UpdateRawIndices();
            ModifiedHandler(this, null);

            Project.EndTransaction();
        }

        public void RemoveGroup(ObjectGroup group)
        {
            if (!Children.Contains(new(group)) || group.Type != ObjectGroupType.Shared)
                throw new Exception("Tried to remove an invalid object group.");

            Project.BeginTransaction("Delete object group");
            Project.UndoState.CaptureInitialState<State>(this);

            bool foundGroup = false;

            for (int i = 0; i < RawObjectGroup.GetNumObjects(); i++)
            {
                ObjectData data = RawObjectGroup.GetObjectData(i);
                if (data.IsPointerType() && data.GetValue(0) == group.Identifier)
                {
                    RawObjectGroup.RemoveObject(i);
                    foundGroup = true;
                    break;
                }
            }

            if (!foundGroup)
                throw new Exception("Error removing '" + group.Identifier + "' from the object group.");

            Isolate();

            Children.Remove(new(group));

            UpdateRawIndices();
            ModifiedHandler(this, null);

            Project.EndTransaction();
        }


        // ================================================================================
        // Internal methods
        // ================================================================================

        // Calling this ensures that all of the data pointed to by this ObjectGroup is not shared
        // with anything else. The only exception is pointers to ObjectGroupType "Shared". These
        // should not be directly edited (and it is / will be disabled in the UI).
        // Since "ObjectDefinition" objects call this, they need to be in a valid state after this
        // is called.
        private bool beganIsolate;

        internal void Isolate()
        {
            if (IsStub() || beganIsolate)
                return;
            if (Type == ObjectGroupType.Shared) // We don't try to make these unique
                return;

            // This assertion may actually fail on the master branch due to breakage of our
            // assumptions (see notes at top of file). This implementation of Isolate() only works
            // if unique labels are used for every room, even if said labels reference the same
            // data - resulting in unique ObjectGroup instances for each reference to the data (each
            // of which has only one parent, instead of a single instance with multiple parents).
            Debug.Assert(Parents.Count <= 1);

            beganIsolate = true;

            foreach (ObjectGroup parent in Parents)
            {
                parent.Isolate();
            }

            FileComponent firstToDelete;

            if (!IsIsolated(out firstToDelete))
            {
                Project.UndoState.CaptureInitialState<State>(this);

                RawObjectGroup.Repoint();

                // ObjectData has been cloned in the RawObjectGroup, so reload it
                foreach (ref ObjectStruct st in CollectionsMarshal.AsSpan(ObjectList))
                {
                    st = st with { data = new(RawObjectGroup.GetObjectData(st.rawIndex)) };
                    st.def.Instance.SetObjectData(st.data);
                }

                // Delete orphaned data
                FileComponent d = firstToDelete;
                while (d != null)
                {
                    FileComponent next = d.Next;

                    if (d is Label)
                        break;
                    d.Detach();

                    d = next;
                }

                ModifiedHandler(this, null);
            }

            foreach (ObjectGroup child in GetAllGroups())
            {
                if (child == this)
                    continue;
                child.Isolate();
            }

            beganIsolate = false;
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
            UpdateRawIndices(verify: true);

            ModifiedHandler(this, null);
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        void AddParent(ObjectGroup group)
        {
            Parents.Add(new(group));
        }

        /// <summary>
        /// Internal helper method. Caller must invoke "ModifiedHandler()" at some point after this.
        /// </summary>
        ObjectDefinition AddObjectToEnd(ObjectType type)
        {
            if (IsStub())
            {
                Debug.Assert(Parents.Count == 1);
                Parents[0].Instance.UnstubChild(this);
            }
            Isolate();

            RawObjectGroup.InsertObject(RawObjectGroup.GetNumObjects(), type);

            int rawIndex = RawObjectGroup.GetNumObjects() - 1;
            ObjectData data = RawObjectGroup.GetObjectData(rawIndex);

            var def = new ObjectDefinition(this, data, state.uniqueIDCounter++);

            ObjectStruct st = new ObjectStruct
            {
                rawIndex = rawIndex,
                data = new(data),
                def = new(def),
            };
            ObjectList.Add(st);

            UpdateRawIndices();

            return st.def;
        }

        // Call this whenever the ordering of objects in objectList changes
        void UpdateRawIndices(bool verify = false)
        {
            // RawObjectGroup may be null here if a previously stubbed group was unstubbed, then
            // undone (turned into a stub again)
            if (RawObjectGroup == null)
            {
                Debug.Assert(ObjectList.Count == 0);
                return;
            }

            Dictionary<ObjectData, int> dict = new Dictionary<ObjectData, int>();
            for (int i = 0; i < RawObjectGroup.GetNumObjects(); i++)
                dict.Add(RawObjectGroup.GetObjectData(i), i);

            foreach (ref ObjectStruct st in CollectionsMarshal.AsSpan(ObjectList))
            {
                if (verify)
                    Debug.Assert(st.rawIndex == dict[st.data]);
                else
                    st = st with { rawIndex = dict[st.data] };
            }
        }

        // Returns true if no data is shared with another label.
        // Also returns, as an out parameter, a FileComponent from which to start deleting until the
        // next Label is found, to prevent data from being orphaned. Returns null if this is not to
        // be done.
        bool IsIsolated(out FileComponent firstToDelete)
        {
            firstToDelete = null;

            FileComponent d = RawObjectGroup.GetObjectData(0);

            // Search for shared data before this label

            d = d.Prev;
            while (!(d is Label || d is Data))
                d = d.Prev;

            if (!(d is Label))
            {
                // This shouldn't happen. A RawObjectGroup is constructed based on a label, so
                // a label must exist here.
                throw new Exception("Unexpected thing happened");
            }

            d = d.Prev;

            while (true)
            {
                if (d == null) // Start of file
                    break;
                else if (d is Label)
                    return false; // Something else references this
                else if (d is ObjectData)
                {
                    ObjectType type = (d as ObjectData).GetObjectType();
                    if (type == ObjectType.End || type == ObjectType.EndPointer)
                        break;
                }
                else if (d is Data)
                {
                    log.Warn("Unexpected Data found before label '" + Identifier + "'?");
                    return false;
                }
                d = d.Prev;
            }

            // Search for shared data after this label

            d = RawObjectGroup.GetObjectData(0);
            firstToDelete = d;

            while (true)
            {
                if (d == null) // End of file
                    break;
                else if (d is Label)
                    return false; // Something else references this
                else if (d is ObjectData)
                {
                    ObjectType type = (d as ObjectData).GetObjectType();
                    if (type == ObjectType.End || type == ObjectType.EndPointer)
                        break;
                }
                else if (d is Data)
                {
                    log.Warn("Unexpected Data found after label '" + Identifier + "'?");
                    return false;
                }
                d = d.Next;
            }

            firstToDelete = null;
            return true;
        }

        void ModifiedHandler(object sender, ValueModifiedEventArgs args)
        {
            StructureModifiedEvent?.Invoke(sender, args);
            foreach (var parent in Parents)
                parent.Instance.ModifiedHandler(sender, args);
        }

        bool IsStub()
        {
            return RawObjectGroup == null;
        }

        // Create a new pointer to a child ObjectGroup and give it blank data.
        void UnstubChild(ObjectGroup child)
        {
            Debug.Assert(Children.Contains(new(child)));

            Isolate();

            FileParser parentParser = RawObjectGroup.GetObjectData(0).FileParser;
            FileParser childParser = Project.GetDefaultEnemyObjectFile();
            if (childParser == null)
            {
                log.Warn("Couldn't open the default enemy object file.");
                childParser = parentParser;
            }


            // Create the new data

            childParser.InsertParseableTextAfter(null, new string[] {
                    "", // Newline
                    child.Identifier + ":",
                    "\tobj_EndPointer"
                    });

            child.RawObjectGroup = Project.GetDataType<RawObjectGroup>(child.Identifier, createIfMissing: true);
            child.ObjectList = new List<ObjectStruct>();


            // Create the pointer to the new data

            ObjectType objType;

            if (child.Type == ObjectGroupType.Enemy)
                objType = ObjectType.Pointer;
            else if (child.Type == ObjectGroupType.BeforeEvent)
                objType = ObjectType.BeforeEvent;
            else if (child.Type == ObjectGroupType.AfterEvent)
                objType = ObjectType.AfterEvent;
            else
                throw new Exception("Invalid ObjectGroup child type");

            ObjectData pointerData = new ObjectData(Project, parentParser, objType);
            pointerData.SetValue(0, child.Identifier);
            RawObjectGroup.InsertObject(RawObjectGroup.GetNumObjects(), pointerData);
        }


        // The "record" type has value equality (like structs) but is a reference type (like classes).
        // By making it immutable, we don't need to worry about writing and using copy constructors.
        // The bottom line is we can use this in our "State" class without worrying about overriding
        // equality or copy operations.
        record ObjectStruct
        {
            // Index in the RawObjectGroup corresponding to this object. It is different because the
            // raw object group contains entries for pointer types, while this doesn't.
            public required int rawIndex { get; init; }

            // The ObjectDefinition is an abstraction around the corresponding ObjectData.
            public required InstanceResolver<ObjectDefinition> def { get; init; }
            public required InstanceResolver<ObjectData> data { get; init; }
        }
    }
}
