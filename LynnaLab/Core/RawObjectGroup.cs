﻿using System;
using System.Collections.Generic;

namespace LynnaLab
{
    // This class provides an interface to edit an "object group" (list of object data ending with
    // "obj_End" or "obj_EndPointer"). For most cases it's recommended to use the "ObjectGroup"
    // class instead.
    internal class RawObjectGroup : ProjectDataType
    {
        public static string[] ObjectCommands = {
            "obj_Condition",
            "obj_NoValue",
            "obj_DoubleValue",
            "obj_Pointer",
            "obj_BeforeEvent",
            "obj_AfterEvent",
            "obj_RandomEnemy",
            "obj_SpecificEnemy",
            "obj_Part",
            "obj_WithParam",
            "obj_ItemDrop",
            "obj_End",
            "obj_EndPointer",
            "obj_Garbage"
        };

        public static int[] ObjectCommandMinParams = {
             1,  1,  3,  1,  1,  1,  2,  3,  2,  5,  2,  0,  0, 2
        };

        public static int[] ObjectCommandMaxParams = {
            -1, -1, -1, -1, -1, -1, -1,  4, -1, -1,  3, -1, -1, 2
        };

        public static int[] ObjectCommandDefaultParams = {
             1,  1,  3,  1,  1,  1,  2,  4,  2,  5,  3,  0,  0, 2
        };


        List<ObjectData> objectDataList = new List<ObjectData>();
        FileParser parser;


        internal RawObjectGroup(Project p, String id) : base(p, id)
        {
            parser = Project.GetFileWithLabel(Identifier);
            ObjectData data = parser.GetData(Identifier) as ObjectData;

            while (data.GetObjectType() != ObjectType.End && data.GetObjectType() != ObjectType.EndPointer) {
                ObjectData next = data.NextData as ObjectData;

                if (data.GetObjectType() == ObjectType.Garbage) // Delete these (they do nothing anyway)
                    data.Detach();
                else
                    objectDataList.Add(data);

                data = next;
            }
            objectDataList.Add(data);
        }

        public ObjectData GetObjectData(int index) {
            return objectDataList[index];
        }
        public IList<ObjectData> GetObjectDataList() {
            return new List<ObjectData>(objectDataList);
        }
        public int GetNumObjects() {
            return objectDataList.Count-1; // Not counting InteracEnd
        }

        public void RemoveObject(int index) {
            if (index >= objectDataList.Count-1)
                throw new Exception("Array index out of bounds.");

            ObjectData data = objectDataList[index];
            data.Detach();
            objectDataList.RemoveAt(index);
        }

        public void InsertObject(int index, ObjectData data) {
            data.Attach(parser);
            data.InsertIntoParserBefore(objectDataList[index]);
            objectDataList.Insert(index, data);
        }

        public void InsertObject(int index, ObjectType type) {
            ObjectData data = new ObjectData(Project, parser, type);
            InsertObject(index, data);
        }

        internal void Repoint() {
            parser.RemoveLabel(Identifier);

            FileComponent lastComponent;

            parser.InsertParseableTextAfter(null, new String[]{""}); // Newline
            lastComponent = new Label(parser, Identifier);
            parser.InsertComponentAfter(null, lastComponent);

            List<ObjectData> newList = new List<ObjectData>();
            foreach (ObjectData old in objectDataList) {
                ObjectData newData = new ObjectData(old);
                newList.Add(newData);
                parser.InsertComponentAfter(lastComponent, newData);
                lastComponent = newData;
            }

            objectDataList = newList;
        }


        // Used to be public, now considering deleting this
        void ReplaceObjects(IList<ObjectData> objectList) {
            while (GetNumObjects() > 0)
                RemoveObject(0);
            foreach (ObjectData o in objectList)
                InsertObject(GetNumObjects(), o);
        }
    }
}