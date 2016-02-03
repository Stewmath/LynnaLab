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

            var chestGui = new ChestEditorGui(manager);
            chestGui.SetRoom(manager.GetActiveRoom().Index);
            chestGui.Destroyed += (sender2, e2) => win.Destroy();

            var itemGui = new ItemEditorGui(manager);

            HBox hbox = new Gtk.HBox();
            hbox.Spacing = 6;
            hbox.Add(chestGui);
            hbox.Add(new VSeparator());
            hbox.Add(itemGui);
            win.Add(hbox);
            win.ShowAll();
        }

    }

    class ItemEditorGui : Gtk.Alignment {
        PluginManager manager;
        Widget vrEditor;
        Alignment vrContainer;
        SpinButtonHexadecimal highIndexButton, lowIndexButton;

        Project Project {
            get { return manager.Project; }
        }

        int Index {
            get {
                return highIndexButton.ValueAsInt<<8 | lowIndexButton.ValueAsInt;
            }
        }

        public ItemEditorGui(PluginManager manager)
            : base(1.0f,1.0f,1.0f,1.0f)
        {
            this.manager = manager;

            highIndexButton = new SpinButtonHexadecimal(0,0xff);
            highIndexButton.ValueChanged += (a,b) => {
                SetItem(Index);
            };
//             highIndexButton.Digits = 2;

            lowIndexButton = new SpinButtonHexadecimal(0,0xff);
            lowIndexButton.ValueChanged += (a,b) => {
                SetItem(Index);
            };
//             lowIndexButton.Digits = 2;

            Button addButton = new Gtk.Button();
            addButton.Clicked += (a,b) => {
                AddSubIndex(Index>>8);
                SetItem((Index&0xff00) + 0xff);
            };
			addButton.UseStock = true;
			addButton.UseUnderline = true;
            addButton.Image = new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button);

            Button removeButton = new Gtk.Button();
            removeButton.Clicked += (a,b) => {
                RemoveSubIndex(Index>>8);
                SetItem((Index&0xff00) + 0xff);
            };
			removeButton.UseStock = true;
			removeButton.UseUnderline = true;
            removeButton.Image = new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button);

            var table = new Table(3,2,false);

            uint y=0;
            table.Attach(new Gtk.Label("High Item Index:"), 0, 1, y, y+1);
            table.Attach(highIndexButton, 1, 2, y, y+1);
            y++;
            table.Attach(new Gtk.Label("Low Index:"), 0,1,y,y+1);
            table.Attach(lowIndexButton, 1,2,y,y+1);
            table.Attach(addButton,2,3,y,y+1);
            table.Attach(removeButton,3,4,y,y+1);

            vrContainer = new Alignment(1.0f,1.0f,1.0f,1.0f);

            VBox vbox = new VBox();
            vbox.Add(table);
            vbox.Add(vrContainer);

            Add(vbox);

            SetItem(0);
        }

        public void SetItem(int index) {
            int hIndex = index>>8;
            int lIndex = index&0xff;

            int max = GetNumLowIndices(hIndex);
            if (lIndex >= max)
                lIndex = max-1;

            lowIndexButton.Adjustment.Upper = max-1;
            highIndexButton.Value = hIndex;
            lowIndexButton.Value = lIndex;

            vrContainer.Remove(vrEditor);

            FileParser parser = Project.GetFileWithLabel("itemData");
            Data data = parser.GetData("itemData", hIndex*4);

            if (HighIndexUsesPointer(hIndex)) {
                string s = data.NextData.GetValue(0);
                data = Project.GetData(s);
                for (int i=0;i<4*lIndex;i++)
                    data = data.NextData;
            }
            ValueReference v1 = new ValueReference("Flags", 0, DataValueType.Byte);
            v1.SetData(data);
            data = data.NextData;
            ValueReference v2 = new ValueReference("Unknown", 0, DataValueType.Byte);
            v2.SetData(data);
            data = data.NextData;
            ValueReference v3 = new ValueReference("Text ID", 0, DataValueType.Byte);
            v3.SetData(data);
            data = data.NextData;
            ValueReference v4 = new ValueReference("Gfx", 0, DataValueType.Byte);
            v4.SetData(data);
            data = data.NextData;

            // Byte 1 is sometimes set to 0x80 for unused items?
            v1.SetValue(v1.GetIntValue()&0x7f);

            var vr = new ValueReferenceEditor(
                    Project,
                    new ValueReference[] {v1, v2, v3, v4},
                    "Data");
            vr.SetMaxBound(v1, 0x7f);

            vrEditor = vr;
            vrContainer.Add(vrEditor);
        }

        void AddSubIndex(int hIndex) {
            int n = GetNumLowIndices(hIndex);
            if (n == 256)
                return;

            Data data = GetHighIndexDataBase(hIndex);
            FileParser parser = data.FileParser;

            if (!HighIndexUsesPointer(hIndex)) {
                string pointerString = "itemData" + hIndex.ToString("x2");

                parser.InsertParseableTextBefore(data,
                        new string[] {
                        "\t.db $80",
                        "\t.dw " + pointerString,
                        "\t.db $00",
                        });

                string output = "\t.db";

                for (int i=0;i<4;i++) {
                    output += " " + data.GetValue(0);
                    Data d2 = data;
                    data = data.NextData;
                    parser.RemoveFileComponent(d2);
                }


                parser.InsertParseableTextAfter(null,
                        new string[] {
                        pointerString + ":"
                        });

                parser.InsertParseableTextAfter(null,
                        new string[] { output });

                data = parser.GetData(pointerString);
            }
            else {
                data = Project.GetData(data.NextData.GetValue(0));
            }

            for (int i=0;i<(n-1)*4+3;i++)
                data = data.NextData;

            parser.InsertParseableTextAfter(data,
                    new string[] {
                    "\t.db $00 $00 $00 $00",
                    });
        }

        void RemoveSubIndex(int hIndex) {
            int n = GetNumLowIndices(hIndex);
            if (n <= 1)
                return;

            Data data = GetHighIndexDataBase(hIndex);
            FileParser parser = data.FileParser;

            data = Project.GetData(data.NextData.GetValue(0));

            for (int i=0;i<(n-1)*4;i++)
                data = data.NextData;

            for (int i=0;i<4;i++) {
                Data d2 = data;
                data = data.NextData;
                FileParser.RemoveFileComponent(d2);
            }
        }

        Data GetHighIndexDataBase(int hIndex) {
            return Project.GetData("itemData", hIndex*4);
        }

        bool HighIndexUsesPointer(int hIndex) {
            Data data = GetHighIndexDataBase(hIndex);
            if ((data.GetIntValue(0) & 0x80) == 0)
                return false;
            return data.NextData.CommandLowerCase == ".dw";
        }

        int GetNumLowIndices(int hIndex) {
            Data data = GetHighIndexDataBase(hIndex);
            if (!HighIndexUsesPointer(hIndex))
                return 1;
            data = data.NextData;
            FileParser parser = Project.GetFileWithLabel(data.GetValue(0));
            data = parser.GetData(data.GetValue(0));
            int count=0;
            do {
                for (int i=0;i<4;i++)
                    data = data.NextData;
                count++;
            }
            while (data != null && parser.GetDataLabel(data) == null);

            return count;
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
