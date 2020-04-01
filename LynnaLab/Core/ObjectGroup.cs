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


        internal ObjectGroup(Project p, String id) : base(p, id)
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
            if (GetNumObjects() == 0 && !IsIsolated()) {
                // If this map is sharing data with other maps (as when "blank"), need to make
                // a unique section for this map.

                parser.RemoveLabel(Identifier);

                parser.InsertParseableTextAfter(null, new String[]{""}); // Newline
                parser.InsertComponentAfter(null, new Label(parser, Identifier));

                ObjectData endData = new ObjectData(Project, parser, ObjectType.End);
                parser.InsertComponentAfter(null, endData);
                objectDataList[0] = endData;
            }

            data.Attach(parser);
            data.InsertIntoParserBefore(objectDataList[index]);
            objectDataList.Insert(index, data);
        }

        public void InsertObject(int index, ObjectType type) {
            ObjectData data = new ObjectData(Project, parser, type);
            InsertObject(index, data);
        }

        public void ReplaceObjects(IList<ObjectData> objectList) {
            while (GetNumObjects() > 0)
                RemoveObject(0);
            foreach (ObjectData o in objectList)
                InsertObject(GetNumObjects(), o);
        }

        // Returns true if no data is shared with another label
        bool IsIsolated() {
            FileComponent d = objectDataList[0];

            d = d.Prev;
            if (!(d is Label))
                return true; // This would be an odd case, there should be at least one label...

            if (d.Prev is Label)
                return false;
            return true;
        }
    }
}
