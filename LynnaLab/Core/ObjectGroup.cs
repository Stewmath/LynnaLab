using System;
using System.Collections.Generic;
using System.Linq;

namespace LynnaLab
{
    public enum ObjectGroupType {
        Main,        // Top-level (has its own objects and also holds other ObjectGroups as children)
        Enemy,       // "obj_Pointer" referring to data named "enemyObjectData[...]".
        BeforeEvent, // "obj_BeforeEvent" pointer
        AfterEvent,  // "obj_AfterEvent" pointer
        Other        // "obj_Pointer" referring to something other than "enemyObjectData[...]".
    };

    // This is similar to "RawObjectGroup", but it provides a different interface for handling
    // "Pointers" to other object data. In particular, this never returns an ObjectDefinition that
    // is a pointer type. Instead, you need to call "GetAllGroups()" to get other ObjectGroup's that
    // correspond to what those pointers point to.
    //
    // Assumptions made:
    //   - The "children" do not, themselves, have any children. The tree is at most 1 level deep.
    //   - All "EnemyObjectData" and "Before/AfterEvent" references are named uniquely based on
    //     their room number. This is almost always true. There is one case in Seasons
    //     ("group5Map10EnemyData"; fairy fountains) which cannot follow this rule because multiple
    //     rooms use the same "obj_Pointer" opcode. As a result, LynnaLab cannot separate the data
    //     properly between rooms. I am planning to fix this by changing the disassembly rather than
    //     LynnaLab to accomodate this.
    public class ObjectGroup : ProjectDataType
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Invoked when the group is modified in any way (including modifying objects themselves).
        public EventHandler ModifiedEvent;


        ObjectGroup parent;
        List<ObjectGroup.ObjectStruct> objectList;

        RawObjectGroup rawObjectGroup;
        ObjectGroupType type;
        List<ObjectGroup> children;


        // Constructors

        internal ObjectGroup(Project p, String id) : base(p, id)
        {
            rawObjectGroup = Project.GetDataType<RawObjectGroup>(Identifier);

            type = ObjectGroupType.Main;
            // Other types will be set manually by its parent ObjectGroup when referenced.

            bool addedEnemyType = false;

            objectList = new List<ObjectStruct>();
            children = new List<ObjectGroup>();
            children.Add(this);

            for (int i=0; i<rawObjectGroup.GetNumObjects(); i++) {
                ObjectData obj = rawObjectGroup.GetObjectData(i);
                ObjectType objectType = obj.GetObjectType();
                if (obj.IsPointerType()) {
                    string label = obj.GetValue(0);
                    ObjectGroup child = Project.GetDataType<ObjectGroup>(label);

                    if (objectType == ObjectType.BeforeEvent)
                        child.type = ObjectGroupType.BeforeEvent;
                    else if (objectType == ObjectType.AfterEvent)
                        child.type = ObjectGroupType.AfterEvent;
                    else if (objectType == ObjectType.Pointer && label.Contains("EnemyObjectData")) {
                        // TODO: Check the full name
                        child.type = ObjectGroupType.Enemy;
                        if (addedEnemyType)
                            log.Warn("Found multiple Enemy pointers on " + Identifier + "!");
                        addedEnemyType = true;
                    }
                    else if (objectType == ObjectType.Pointer)
                        child.type = ObjectGroupType.Other;
                    else
                        throw new Exception("Unexpected thing happened");

                    child.parent = this;
                    children.Add(child);
                }
                else {
                    ObjectStruct st = new ObjectStruct();
                    st.rawIndex = i;
                    st.def = new ObjectDefinition(this, obj);
                    st.data = obj;
                    objectList.Add(st);

                    st.def.AddValueModifiedHandler(ModifiedHandler);
                }
            }
        }


        // Public methods

        public ObjectGroupType GetGroupType() {
            return type;
        }

        // Gets all non-pointer objects in this group. This is a shallow operation (does not check
        // pointers).
        public IList<ObjectDefinition> GetObjects() {
            return new List<ObjectDefinition>(objectList.Select((ObjectStruct o) => o.def));
        }

        public int GetNumObjects() {
            return objectList.Count;
        }

        public ObjectDefinition GetObject(int index) {
            return objectList[index].def;
        }

        // Returns a list of ObjectGroups representing each main group (Main, Enemy, BeforeEvent,
        // AfterEvent) plus "Other" groups if they exist. This should only be called on the "Main"
        // type.
        // TODO: This should always contain all 4 main groups even if they don't exist yet.
        // Preferably, they should also have a consistent order.
        public IList<ObjectGroup> GetAllGroups() {
            return children;
        }

        public int AddObject(ObjectType type) {
            rawObjectGroup.InsertObject(rawObjectGroup.GetNumObjects(), type);

            ObjectStruct st = new ObjectStruct();
            st.rawIndex = rawObjectGroup.GetNumObjects()-1;
            st.data = rawObjectGroup.GetObjectData(st.rawIndex);
            st.def = new ObjectDefinition(this, st.data);
            st.def.AddValueModifiedHandler(ModifiedHandler);
            objectList.Add(st);

            UpdateRawIndices();
            ModifiedHandler(this, null);

            return GetNumObjects()-1;
        }

        public void RemoveObject(int index) {
            if (index >= objectList.Count)
                throw new ArgumentException("Argument index=" + index + " is too high.");

            rawObjectGroup.RemoveObject(objectList[index].rawIndex);

            objectList[index].def.RemoveValueModifiedHandler(ModifiedHandler);
            objectList.RemoveAt(index);

            UpdateRawIndices();
            ModifiedHandler(this, null);
        }

        public void MoveObject(int oldIndex, int newIndex) {
            if (oldIndex == newIndex)
                return;

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


        // Internal methods

        // Calling this ensures that all of the data pointed to by this ObjectGroup is not shared
        // with anything else. The only exception is pointers to ObjectGroupType "Other". These
        // should not be directly edited (and it is / will be disabled in the UI).
        // Since "ObjectDefinition" objects call this, they need to be in a valid state after this
        // is called.
        private bool beganIsolate;

        internal void Isolate() {
            if (type != ObjectGroupType.Main && !parent.beganIsolate) {
                parent.Isolate();
                return;
            }

            beganIsolate = true;

            FileComponent firstToDelete;

            if (!IsIsolated(out firstToDelete)) {
                rawObjectGroup.Repoint();

                // ObjectData has been cloned in the RawObjectGroup, so reload it
                foreach (ObjectStruct st in objectList) {
                    st.data = rawObjectGroup.GetObjectData(st.rawIndex);
                    st.def.SetObjectData(st.data);
                }

                // Delete orphaned data
                FileComponent d = firstToDelete;
                while (d != null) {
                    FileComponent next = d.Next;

                    if (d is Label)
                        break;
                    d.Detach();

                    d = next;
                }

                ModifiedHandler(this, null);
            }

            foreach (ObjectGroup child in GetAllGroups()) {
                if (child == this)
                    continue;
                child.Isolate();
            }

            beganIsolate = false;
        }


        // Private methods

        void UpdateRawIndices() {
            Dictionary<ObjectData,int> dict = new Dictionary<ObjectData,int>();
            for (int i=0; i<rawObjectGroup.GetNumObjects(); i++)
                dict.Add(rawObjectGroup.GetObjectData(i), i);

            foreach (ObjectStruct st in objectList) {
                st.rawIndex = dict[st.data];
            }
        }

        // Returns true if no data is shared with another label.
        // Also returns, as an out parameter, a FileComponent from which to start deleting until the
        // next Label is found, to prevent data from being orphaned. Returns null if this is not to
        // be done.
        bool IsIsolated(out FileComponent firstToDelete) {
            firstToDelete = null;

            FileComponent d = rawObjectGroup.GetObjectData(0);

            // Search for shared data before this label

            d = d.Prev;
            while (!(d is Label || d is Data))
                d = d.Prev;

            if (!(d is Label)) {
                // This shouldn't happen. A RawObjectGroup is constructed based on a label, so
                // a label must exist here.
                throw new Exception("Unexpected thing happened");
            }

            d = d.Prev;

            while (true) {
                if (d == null) // Start of file
                    break;
                else if (d is Label)
                    return false; // Something else references this
                else if (d is ObjectData) {
                    ObjectType type = (d as ObjectData).GetObjectType();
                    if (type == ObjectType.End || type == ObjectType.EndPointer)
                        break;
                }
                else if (d is Data) {
                    log.Warn("Unexpected Data found before label '" + Identifier + "'?");
                    return false;
                }
                d = d.Prev;
            }

            // Search for shared data after this label

            d = rawObjectGroup.GetObjectData(0);
            firstToDelete = d;

            while (true) {
                if (d == null) // End of file
                    break;
                else if (d is Label)
                    return false; // Something else references this
                else if (d is ObjectData) {
                    ObjectType type = (d as ObjectData).GetObjectType();
                    if (type == ObjectType.End || type == ObjectType.EndPointer)
                        break;
                }
                else if (d is Data) {
                    log.Warn("Unexpected Data found after label '" + Identifier + "'?");
                    return false;
                }
                d = d.Next;
            }

            firstToDelete = null;
            return true;
        }

        void ModifiedHandler(object sender, EventArgs args) {
            if (ModifiedEvent != null)
                ModifiedEvent(this, args);
            if (parent != null)
                parent.ModifiedHandler(sender, args);
        }


        class ObjectStruct {
            public int rawIndex;
            public ObjectDefinition def;
            public ObjectData data;
        }
    }
}
