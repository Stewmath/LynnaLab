using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using LynnaLab;
using Gtk;

namespace Plugins
{
    public class ChestEditor : Plugin
    {
        PluginManager manager;

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

        // Methods

        public override void Init(PluginManager manager) {
            this.manager = manager;
        }

        public override void Exit() {
        }

        public override void Clicked() {
            Gtk.Window win = new Window(WindowType.Toplevel);
            var gui = new ChestEditorGui(manager);
            gui.SetRoom(manager.GetActiveRoom().Index);
            win.Add(gui);
            win.ShowAll();
            gui.Destroyed += (sender2, e2) => win.Destroy();
        }

    }

    class ChestEditorGui : Gtk.Alignment {
        PluginManager manager;
        Widget vrEditor;
        Alignment vrContainer;

        int currentRoom;

        Project Project {
            get {
                return manager.Project;
            }
        }

        public ChestEditorGui(PluginManager m)
            : base(1.0F,1.0F,1.0F,1.0F)
        {
            manager = m;

            VBox vbox = new VBox();
            vrContainer = new Alignment(1.0F, 1.0F, 1.0F, 1.0F);
            vbox.Add(vrContainer);

            Alignment buttonAlign = new Alignment(0.5F, 0.5F, 0.0F, 0.2F);
            buttonAlign.TopPadding = 3;
            Button okButton = new Button("Ok");
            okButton.Clicked += (a,b) => Destroy();
			okButton.UseStock = true;
			okButton.UseUnderline = true;
			okButton.Label = "gtk-ok";
            buttonAlign.Add(okButton);
            vbox.Add(buttonAlign);

            Add(vbox);
        }

        public void SetRoom(int room) {
            currentRoom = room;

            vrContainer.Remove(vrEditor);
            vrEditor = null;

            Data data = GetChestData(room);

            if (data == null) {
                VBox vbox = new VBox();

                Button addButton = new Button("Add");
                addButton.Clicked += (a,b) => {
                    AddChestData(currentRoom);
                    SetRoom(currentRoom);
                };
                addButton.Label = "gtk-add";
                addButton.UseStock = true;

                vbox.Add(new Gtk.Label("No chest data exists for this room."));
                Alignment btnAlign = new Alignment(0.5f,0.5f,0.0f,0.2f);
                btnAlign.TopPadding = 3;
                btnAlign.Add(addButton);
                vbox.Add(btnAlign);

                vrEditor = vbox;
            }
            else {

                ValueReference v1 = new ValueReference("YX", 0, DataValueType.Byte);
                v1.SetData(data);
                data = data.NextData;
                ValueReference v2 = new ValueReference("Room", 0, DataValueType.Byte, false);
                v2.SetData(data);
                data = data.NextData;
                ValueReference v3 = new ValueReference("ID1", 0, DataValueType.Byte);
                v3.SetData(data);
                data = data.NextData;
                ValueReference v4 = new ValueReference("ID2", 0, DataValueType.Byte);
                v4.SetData(data);
                data = data.NextData;

                var vr = new ValueReferenceEditor(
                        Project,
                        new ValueReference[] {v1, v2, v3, v4},
                        "Data");
                vr.SetMaxBound(v1, 0xfe);


                VBox vbox = new VBox();
                vbox.Add(vr);

                Button delButton = new Button("Remove");
                delButton.Clicked += (a,b) => {
                    DeleteChestData(currentRoom);
                    SetRoom(currentRoom);
                };
                delButton.Label = "gtk-delete";
                delButton.UseStock = true;

                Alignment btnAlign = new Alignment(0.5f,0.5f,0.0f,0.2f);
                btnAlign.TopPadding = 3;
                btnAlign.Add(delButton);
                vbox.Add(btnAlign);

                vrEditor = vbox;
            }
            vrContainer.Add(vrEditor);
            vrContainer.ShowAll();
        }

        Data GetChestData(int room) {
            int group = room>>8;
            room &= 0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestDataGroupTable");
            Data chestPointer = chestFileParser.GetData("chestDataGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            while (chestGroupData.GetIntValue(0) != 0xff) {
                if (chestGroupData.NextData.GetIntValue(0) == room)
                    return chestGroupData;
                for (int i=0;i<4;i++)
                    chestGroupData = chestGroupData.NextData;
            }

            return null;
        }

        void AddChestData(int room) {
            int group = room>>8;
            room &= 0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestDataGroupTable");
            Data chestPointer = chestFileParser.GetData("chestDataGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            Data newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, new List<int>{-1});
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {Wla.ToByte((byte)room)}, -1, null, null);
            newData.PrintCommand = false;
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, null);
            newData.PrintCommand = false;
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, null);
            newData.PrintCommand = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);
        }

        void DeleteChestData(int room) {
            Data data = GetChestData(room);

            if (data == null)
                return;

            for (int i=0;i<4;i++) {
                Data nextData = data.NextData;
                data.FileParser.RemoveFileComponent(data);
                data = nextData;
            }
        }
    }
}
