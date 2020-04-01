using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using LynnaLab;
using Gtk;

// "Flattens" objects in the disassembly by replacing all "obj_Pointer"s with the data itself.
namespace Plugins
{
    public class ObjectFlattener : Plugin
    {
        PluginManager manager;

        public override String Name { get { return "Object Flattener"; } }
        public override String Tooltip { get { return "Remove object pointers"; } }
        public override bool IsDockable { get { return false; } }
        public override string Category { get { return "Debug"; } }

        Project Project {
            get {
                return manager.Project;
            }
        }

        // Methods

        public override void Init(PluginManager manager) {
            this.manager = manager;
        }

        public override void Exit() {
        }

        int pointerCount=0;

        public override void Clicked() {
            for (int r=0; r<Project.GetNumRooms(); r++) {
                if (Project.GameString == "seasons" && r >= 0x200 && r < 0x400)
                    continue;
                Console.WriteLine(Wla.ToHex(r, 2));
                pointerCount=0;
                Room room = Project.GetIndexedDataType<Room>(r);
                ObjectGroup group = room.GetObjectGroup();
                IList<ObjectData> objects = ParseGroup(group);
                group.ReplaceObjects(objects);

                if (pointerCount > 1)
                    Console.WriteLine("ROOM " + Wla.ToHex(r, 3) + " HAS " + pointerCount + " POINTERS");
            }
        }

        IList<ObjectData> ParseGroup(ObjectGroup group) {
            var objects = new List<ObjectData>();
            for (int o=0; o<group.GetNumObjects(); o++) {
                ObjectData data = group.GetObjectData(o);
                if (data.GetObjectType() == ObjectType.Pointer
                        || data.GetObjectType() == ObjectType.BossPointer
                        || data.GetObjectType() == ObjectType.AntiBossPointer) {
                    objects.AddRange(ParseGroup(data.GetPointedObjectGroup()));
                    pointerCount++;
                }
                else
                    objects.Add(new ObjectData(data));
            }
            return objects;
        }
    }
}
