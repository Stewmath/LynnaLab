using System;
using System.Collections.Generic;

namespace LynnaLab
{
    public enum ObjectGroupType {
        Main,        // Top-level
        Enemy,       // "obj_Pointer" referring to data named "enemyObjectData[...]".
        BeforeEvent, // "obj_BeforeEvent" pointer
        AfterEvent,  // "obj_AfterEvent" pointer
        Other        // "obj_Pointers" referring to something other than "enemyObjectData[...]".
    };

    // This is similar to "RawObjectGroup", but it provides a different interface for handling
    // "Pointers" to other object data. In particular, this never returns an ObjectDefinition that
    // is a pointer type. Instead, you need to call "GetAllGroups()" to get other ObjectGroup's that
    // correspond to what those pointers point to.
    // TODO: Handle callbacks when the RawObjectGroup modifies its pointers (update the "children"
    // variable).
    // TODO: Handle "Enemy" types being referenced by more than one "Main" type.
    public class ObjectGroup : ProjectDataType
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        private RawObjectGroup rawObjectGroup;
        ObjectGroupType type;
        List<ObjectGroup> children;


        // Constructors

        internal ObjectGroup(Project p, String id) : base(p, id)
        {
            rawObjectGroup = Project.GetDataType<RawObjectGroup>(Identifier);

            type = ObjectGroupType.Main;
            // Other types will be set manually by its parent ObjectGroup when referenced.

            bool addedEnemyType = false;

            children = new List<ObjectGroup>();
            children.Add(this);
            for (int i=0; i<rawObjectGroup.GetNumObjects(); i++) {
                ObjectData obj = rawObjectGroup.GetObjectData(i);
                ObjectType objectType = obj.GetObjectType();
                if (obj.IsPointerType()) {
                    string label = obj.GetValue(0);
                    ObjectGroup child = Project.GetDataType<ObjectGroup>(label);

                    if (objectType == ObjectType.BossPointer)
                        child.type = ObjectGroupType.BeforeEvent;
                    else if (objectType == ObjectType.AntiBossPointer)
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

                    children.Add(child);
                }
            }
        }


        // Public methods

        // Gets all non-pointer objects in this group. This is a shallow operation (does not check
        // pointers).
        public IList<ObjectDefinition> GetObjects() {
            var list = new List<ObjectDefinition>();
            for (int i=0; i<rawObjectGroup.GetNumObjects(); i++) {
                ObjectData data = rawObjectGroup.GetObjectData(i);
                if (data.IsPointerType())
                    continue;
                ObjectDefinition obj = new ObjectDefinition(this, data);
                list.Add(obj);
            }
            return list;
        }

        public int GetNumObjects() {
            return GetObjects().Count;
        }

        public ObjectDefinition GetObject(int index) {
            return GetObjects()[index];
        }

        // Returns a list of ObjectGroups representing each main group (Main, Enemy, BeforeEvent,
        // AfterEvent) plus "Other" groups if they exist. This should only be called on the "Main"
        // type.
        // TODO: This should always contain all 4 main groups even if they don't exist yet.
        // Preferably, they should also have a consistent order.
        public IList<ObjectGroup> GetAllGroups() {
            return children;
        }

        public void AddObject(ObjectType type) {
            rawObjectGroup.InsertObject(rawObjectGroup.GetNumObjects(), type);
        }

        public void RemoveObject(int index) {
            int count = 0;
            for (int i=0; i<rawObjectGroup.GetNumObjects(); i++) {
                ObjectData obj = rawObjectGroup.GetObjectData(i);
                if (obj.IsPointerType())
                    continue;
                if (count == index) {
                    rawObjectGroup.RemoveObject(i);
                    return;
                }
                count++;
            }
            throw new ArgumentException("Argument index=" + index + " is too high.");
        }

        public void MoveObject(int index, int newIndex) {
            // TODO
        }


        // Internal methods

        // Calling this ensures that all of the data pointed to by this ObjectGroup is not shared
        // with anything else. The only exception is pointers to ObjectGroupType "Other". These
        // should not be directly edited (and it is / will be disabled in the UI).
        internal void Separate() {
            // TODO
        }
    }
}
