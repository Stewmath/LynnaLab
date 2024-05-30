using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;

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
    //     properly between rooms. I am planning to fix this by changing the disassembly rather than
    //     LynnaLib to accomodate this.
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
    public class ObjectGroup : ProjectDataType
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Invoked when the group is modified in any way (including modifying objects themselves).
        event EventHandler<EventArgs> modifiedEvent;


        // NOTE: this list may not be complete at any given time
        List<ObjectGroup> parents = new List<ObjectGroup>();

        ObjectGroupType type;

        List<ObjectGroup.ObjectStruct> objectList;
        RawObjectGroup rawObjectGroup;
        List<ObjectGroup> children;


        // Constructors

        internal ObjectGroup(Project p, String id, ObjectGroupType type) : base(p, id)
        {
            children = new List<ObjectGroup>();
            children.Add(this);

            this.type = type;

            if (!Project.HasLabel(id))
                return; // This will be considered a "stub" type until data is given to it

            rawObjectGroup = Project.GetDataType<RawObjectGroup>(Identifier);

            bool addedEnemyType = false;

            objectList = new List<ObjectStruct>();

            for (int i = 0; i < rawObjectGroup.GetNumObjects(); i++)
            {
                ObjectData obj = rawObjectGroup.GetObjectData(i);
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
                    children.Add(child);
                    child.AddParent(this);
                }
                else
                {
                    ObjectStruct st = new ObjectStruct();
                    st.rawIndex = i;
                    st.def = new ObjectDefinition(this, obj, objectList.Count);
                    st.data = obj;
                    objectList.Add(st);

                    st.def.ModifiedEvent += ModifiedHandler;
                }
            }

            if (this.type == ObjectGroupType.Main)
            {
                // Get the room number based on the identifier name.
                Regex rx = new Regex(@"group([0-9])Map([0-9a-f]{2})ObjectData",
                        RegexOptions.Compiled | RegexOptions.IgnoreCase);

                MatchCollection matches = rx.Matches(Identifier);

                if (matches.Count != 1)
                {
                    log.Error("ObjectGroup '" + Identifier + "' has an unexpected name. Can't get room number.");
                    this.type = ObjectGroupType.Shared;
                }
                else
                {
                    string groupString = matches[0].Groups[1].Value;
                    string roomString = matches[0].Groups[2].Value;

                    System.Action<ObjectGroupType, string> AddGroupIfMissing = (t, basename) =>
                    {
                        foreach (ObjectGroup group in children)
                        {
                            if (group.type == t)
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
                            children.Add(child);
                            child.AddParent(this);
                        }
                    };

                    AddGroupIfMissing(ObjectGroupType.Enemy, "group{0}Map{1}EnemyObjectData");
                    AddGroupIfMissing(ObjectGroupType.BeforeEvent, "group{0}Map{1}BeforeEventObjectData");
                    AddGroupIfMissing(ObjectGroupType.AfterEvent, "group{0}Map{1}AfterEventObjectData");
                }
            }

            children.Sort((a, b) => a.type.CompareTo(b.type));
        }


        // Public methods

        public ObjectGroupType GetGroupType()
        {
            return type;
        }

        // Gets all non-pointer objects in this group. This is a shallow operation (does not check
        // pointers).
        public IReadOnlyList<ObjectDefinition> GetObjects()
        {
            if (IsStub())
                return new List<ObjectDefinition>();
            return new List<ObjectDefinition>(objectList.Select((ObjectStruct o) => o.def));
        }

        public int GetNumObjects()
        {
            if (IsStub())
                return 0;
            return objectList.Count;
        }

        public ObjectDefinition GetObject(int index)
        {
            if (IsStub())
                throw new IndexOutOfRangeException();
            return objectList[index].def;
        }

        public int IndexOf(ObjectDefinition obj)
        {
            for (int i = 0; i < objectList.Count; i++)
            {
                if (objectList[i].def == obj)
                    return i;
            }
            return -1;
        }

        // Returns a list of ObjectGroups representing each main group (Main, Enemy, BeforeEvent,
        // AfterEvent) plus "Shared" groups if they exist. This should only be called on the "Main"
        // type.
        public IReadOnlyList<ObjectGroup> GetAllGroups()
        {
            return children;
        }

        public int AddObject(ObjectType type)
        {
            if (IsStub())
            {
                Debug.Assert(parents.Count == 1);
                parents[0].UnstubChild(this);
            }
            Isolate();

            rawObjectGroup.InsertObject(rawObjectGroup.GetNumObjects(), type);

            ObjectStruct st = new ObjectStruct();
            st.rawIndex = rawObjectGroup.GetNumObjects() - 1;
            st.data = rawObjectGroup.GetObjectData(st.rawIndex);
            st.def = new ObjectDefinition(this, st.data, objectList.Count);
            st.def.AddValueModifiedHandler(ModifiedHandler);
            objectList.Add(st);

            UpdateRawIndices();
            ModifiedHandler(this, null);

            return GetNumObjects() - 1;
        }

        public void RemoveObject(int index)
        {
            if (IsStub() || index >= objectList.Count)
                throw new ArgumentException("Argument index=" + index + " is too high.");

            Isolate();

            rawObjectGroup.RemoveObject(objectList[index].rawIndex);

            objectList[index].def.RemoveValueModifiedHandler(ModifiedHandler);
            objectList.RemoveAt(index);

            UpdateRawIndices();
            ModifiedHandler(this, null);
        }

        public void MoveObject(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex)
                return;

            Isolate();

            ObjectStruct oldSt = objectList[oldIndex];
            ObjectStruct newSt = objectList[newIndex];

            int oldRaw = oldSt.rawIndex;
            int newRaw = newSt.rawIndex;

            ObjectData data = rawObjectGroup.GetObjectData(oldRaw);
            rawObjectGroup.RemoveObject(oldRaw);
            rawObjectGroup.InsertObject(newRaw, data);

            objectList.RemoveAt(oldIndex);
            objectList.Insert(newIndex, oldSt);

            UpdateRawIndices();
            ModifiedHandler(this, null);
        }

        public void RemoveGroup(ObjectGroup group)
        {
            if (!children.Contains(group) || group.type != ObjectGroupType.Shared)
                throw new Exception("Tried to remove an invalid object group.");

            bool foundGroup = false;

            for (int i = 0; i < rawObjectGroup.GetNumObjects(); i++)
            {
                ObjectData data = rawObjectGroup.GetObjectData(i);
                if (data.IsPointerType() && data.GetValue(0) == group.Identifier)
                {
                    rawObjectGroup.RemoveObject(i);
                    foundGroup = true;
                    break;
                }
            }

            if (!foundGroup)
                throw new Exception("Error removing '" + group.Identifier + "' from the object group.");

            Isolate();

            children.Remove(group);

            UpdateRawIndices();
            ModifiedHandler(this, null);
        }

        public void AddModifiedHandler(EventHandler<EventArgs> handler)
        {
            modifiedEvent += handler;
        }

        public void RemoveModifiedHandler(EventHandler<EventArgs> handler)
        {
            modifiedEvent -= handler;
        }


        // Internal methods

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
            if (type == ObjectGroupType.Shared) // We don't try to make these unique
                return;

            beganIsolate = true;

            foreach (var parent in parents)
            {
                parent.Isolate();
            }

            FileComponent firstToDelete;

            if (!IsIsolated(out firstToDelete))
            {
                rawObjectGroup.Repoint();

                // ObjectData has been cloned in the RawObjectGroup, so reload it
                foreach (ObjectStruct st in objectList)
                {
                    st.data = rawObjectGroup.GetObjectData(st.rawIndex);
                    st.def.SetObjectData(st.data);
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


        // Private methods

        void AddParent(ObjectGroup group)
        {
            parents.Add(group);
        }

        // Call this whenever the ordering of objects in objectList changes
        void UpdateRawIndices()
        {
            Dictionary<ObjectData, int> dict = new Dictionary<ObjectData, int>();
            for (int i = 0; i < rawObjectGroup.GetNumObjects(); i++)
                dict.Add(rawObjectGroup.GetObjectData(i), i);

            foreach (ObjectStruct st in objectList)
            {
                st.rawIndex = dict[st.data];
                st.def.UpdateIndex();
            }
        }

        // Returns true if no data is shared with another label.
        // Also returns, as an out parameter, a FileComponent from which to start deleting until the
        // next Label is found, to prevent data from being orphaned. Returns null if this is not to
        // be done.
        bool IsIsolated(out FileComponent firstToDelete)
        {
            firstToDelete = null;

            FileComponent d = rawObjectGroup.GetObjectData(0);

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

            d = rawObjectGroup.GetObjectData(0);
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
            modifiedEvent?.Invoke(this, null);
            foreach (var parent in parents)
                parent.ModifiedHandler(sender, args);
        }

        bool IsStub()
        {
            return rawObjectGroup == null;
        }

        // Create a new pointer to a child ObjectGroup and give it blank data.
        void UnstubChild(ObjectGroup child)
        {
            Debug.Assert(children.Contains(child));

            Isolate();

            FileParser parentParser = rawObjectGroup.GetObjectData(0).FileParser;
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

            child.rawObjectGroup = new RawObjectGroup(Project, child.Identifier);
            child.objectList = new List<ObjectStruct>();


            // Create the pointer to the new data

            ObjectType objType;

            if (child.type == ObjectGroupType.Enemy)
                objType = ObjectType.Pointer;
            else if (child.type == ObjectGroupType.BeforeEvent)
                objType = ObjectType.BeforeEvent;
            else if (child.type == ObjectGroupType.AfterEvent)
                objType = ObjectType.AfterEvent;
            else
                throw new Exception("Invalid ObjectGroup child type");

            ObjectData pointerData = new ObjectData(Project, parentParser, objType);
            pointerData.SetValue(0, child.Identifier);
            rawObjectGroup.InsertObject(rawObjectGroup.GetNumObjects(), pointerData);
        }


        class ObjectStruct
        {
            public int rawIndex;
            public ObjectDefinition def;
            public ObjectData data;
        }
    }
}
