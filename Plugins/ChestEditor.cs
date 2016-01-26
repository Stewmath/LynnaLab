using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using LynnaLab;

namespace Plugins
{
    public class ChestEditor : Plugin
    {
        public abstract String Name {
            get {
                return "Chest Editor";
            }
        }
        public abstract String Tooltip {
            get {
                return "Edit Chests";
            }
        }
        public abstract bool IsDockable {
            get {
                return false;
            }
        }

        Project Project {
            return manager.Project;
        }

        PluginManager manager;

        public abstract void Init(PluginManager manager) {
            this.manager = manager;
        }

        public abstract void Exit() {
        }

        public abstract void Clicked() {
            int group = manager.GetActiveMap().Group;
            FileParser chestFileParser = Project.GetFileWithLabel("chestGroupTable");
            Data chestPointer = chestFileParser.getData("chestGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);

            Data chestGroupData = Project.GetData(pointerString);

        }
    }
}
