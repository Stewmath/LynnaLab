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
        public override String Name {
            get {
                return "Chest Editor";
            }
        }
        public override String Tooltip {
            get {
                return "Edit Chests";
            }
        }
        public override bool IsDockable {
            get {
                return false;
            }
        }

        Project Project {
            get {
                return manager.Project;
            }
        }

        PluginManager manager;

        public override void Init(PluginManager manager) {
            this.manager = manager;
        }

        public override void Exit() {
        }

        public override void Clicked() {
        }

        Data GetChestData() {
            int group = manager.GetActiveMap().Group;
            int room = manager.GetActiveRoom().Index&0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestGroupTable");
            Data chestPointer = chestFileParser.GetData("chestGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            while (chestGroupData.GetIntValue(0) != 0xff) {
                if (chestGroupData.GetIntValue(0) == room)
                    return chestGroupData;
                for (int i=0;i<4;i++)
                    chestGroupData = chestGroupData.NextData;
            }

            return null;
        }

        Data AddChestData() {
            int group = manager.GetActiveMap().Group;

            FileParser chestFileParser = Project.GetFileWithLabel("chestGroupTable");
            Data chestPointer = chestFileParser.GetData("chestGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            return null;
//             chestFileParser.InsertComponentBefore(chestGroupData
        }
    }
}
