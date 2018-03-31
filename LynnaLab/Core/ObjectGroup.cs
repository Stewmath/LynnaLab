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
            if (GetNumObjects() == 0 && !IsIsolated()) {
                // If this map is sharing data with other maps (as when "blank"), need to make
                // a unique section for this map.

                parser.RemoveLabel(Identifier);

                parser.InsertParseableTextAfter(null, new String[]{""}); // Newline
                parser.InsertComponentAfter(null, new Label(parser, Identifier));

                ObjectData endData = new ObjectData(Project,
                        ObjectCommands[(int)ObjectType.End],
                        null,
                        parser,
                        new string[]{"\t"}, // Tab at start of line
                        ObjectType.End);
                parser.InsertComponentAfter(null, endData);
                objectDataList[0] = endData;
            }

            ObjectData data = new ObjectData(Project,
                    ObjectCommands[(int)type],
                    null,
                    parser,
                    new string[]{"\t"}, // Tab at start of line
                    type);

            ValueReference.InitializeDataValues(data, data.GetValueReferences());

            if (type >= ObjectType.Pointer && type <= ObjectType.AntiBossPointer)
                data.SetValue(0, "objectData4000"); // Compileable default pointer

            data.InsertIntoParserBefore(objectDataList[index]);
            objectDataList.Insert(index, data);
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
