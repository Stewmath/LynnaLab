using System;
using System.Collections.Generic;

namespace LynnaLab
{

    public class ObjectGroup : ProjectDataType
    {
        public static string[] ObjectCommands = {
            "obj_Conditional",
            "obj_NoValue",
            "obj_DoubleValue",
            "obj_Pointer",
            "obj_BossPointer",
            "obj_AntiBossPointer",
            "obj_RandomEnemy",
            "obj_SpecificEnemy",
            "obj_Part",
            "obj_WithParam",
            "obj_ItemDrop",
            "obj_End",
            "obj_EndPointer"
        };

        public static int[] ObjectCommandMinParams = {
             1,  1,  3,  1,  1,  1,  2,  3,  2,  5,  2,  0,  0
        };

        public static int[] ObjectCommandMaxParams = {
            -1, -1, -1, -1, -1, -1, -1,  4, -1, -1,  3, -1, -1
        };


        List<ObjectData> objectDataList = new List<ObjectData>();
        FileParser parser;


        internal ObjectGroup(Project p, String id) : base(p, id)
        {
            parser = Project.GetFileWithLabel(Identifier);
            ObjectData data = parser.GetData(Identifier) as ObjectData;

            while (data.GetObjectType() != ObjectType.End && data.GetObjectType() != ObjectType.EndPointer) {
                objectDataList.Add(data);
                data = data.NextData as ObjectData;
            }
            objectDataList.Add(data);
        }

        public ObjectData GetObjectData(int index) {
            return objectDataList[index];
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

        public void InsertObject(int index, ObjectType type) {
            ObjectData data = new ObjectData(Project,
                    ObjectCommands[(int)type],
                    null,
                    parser,
                    new int[]{-1}, // Tab at start of line
                    type);

            ValueReference.InitializeDataValues(data, data.GetValueReferences());

            if (type >= ObjectType.Pointer && type <= ObjectType.AntiBossPointer)
                data.SetValue(0, "objectData4000"); // Compileable default pointer

            data.InsertIntoParserBefore(objectDataList[index]);
            objectDataList.Insert(index, data);
        }

        // Returns true if no data is shared with another label
        bool IsIsolated() {
            return true;
        }
    }
}
