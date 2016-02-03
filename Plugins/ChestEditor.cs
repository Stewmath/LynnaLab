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

            VBox vbox = new VBox();

            var chestGui = new ChestEditorGui(manager);
            chestGui.SetRoom(manager.GetActiveRoom().Index);
            chestGui.Destroyed += (sender2, e2) => win.Destroy();

            Frame chestFrame = new Frame();
            chestFrame.Label = "Chest Data";
            chestFrame.Add(chestGui);

            var itemGui = new ItemEditorGui(manager);
            Frame itemFrame = new Frame();
            itemFrame.Label = "Item Data";
            itemFrame.Add(itemGui);

            chestGui.SetItemEditor(itemGui);

            HBox hbox = new Gtk.HBox();
            hbox.Spacing = 6;
            hbox.Add(chestFrame);
//             hbox.Add(new VSeparator());
            hbox.Add(itemFrame);

            Button okButton = new Gtk.Button();
            okButton.UseStock = true;
            okButton.Label = "gtk-ok";
            okButton.Clicked += (a,b) => {
                win.Destroy();
            };

            Alignment buttonAlign = new Alignment(0.5f,0.5f,0f,0f);
            buttonAlign.Add(okButton);

            vbox.Add(hbox);
            vbox.Add(buttonAlign);

            win.Add(vbox);
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

            Button hAddButton = new Gtk.Button();
            hAddButton.Clicked += (a,b) => {
                AddHighIndex();
                SetItem(0xffff);
            };
			hAddButton.UseStock = true;
			hAddButton.UseUnderline = true;
            hAddButton.Image = new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button);

            Button hRemoveButton = new Gtk.Button();
            hRemoveButton.Clicked += (a,b) => {
                if ((Index>>8) < GetNumHighIndices()-1) {
                    Gtk.MessageDialog d = new MessageDialog(null,
                            DialogFlags.DestroyWithParent,
                            MessageType.Warning,
                            ButtonsType.YesNo,
                            "This will shift the indices for all items starting from" + 
                            Wla.ToByte((byte)(Index>>8)) + "! Are you sure you want to continue?"
                            );
                    var r = (ResponseType)d.Run();
                    d.Destroy();
                    if (r != Gtk.ResponseType.Yes)
                        return;
                }
                RemoveHighIndex(Index>>8);
                SetItem(Index);
            };
			hRemoveButton.UseStock = true;
			hRemoveButton.UseUnderline = true;
            hRemoveButton.Image = new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button);

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
                if ((Index&0xff) < GetNumLowIndices(Index>>8)-1) {
                    Gtk.MessageDialog d = new MessageDialog(null,
                            DialogFlags.DestroyWithParent,
                            MessageType.Warning,
                            ButtonsType.YesNo,
                            "This will shift all sub-indices for item " +
                            Wla.ToByte((byte)(Index>>8)) + " starting from sub-index" +
                            Wla.ToByte((byte)(Index&0xff)) + "! Are you sure you want to continue?"
                            );
                    var r = (ResponseType)d.Run();
                    d.Destroy();
                    if (r != Gtk.ResponseType.Yes)
                        return;
                }
                RemoveSubIndex(Index);
                SetItem((Index&0xff00) + 0xff);
            };
			removeButton.UseStock = true;
			removeButton.UseUnderline = true;
            removeButton.Image = new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button);

            var table = new Table(3,2,false);

            uint y=0;
            table.Attach(new Gtk.Label("High Item Index:"), 0, 1, y, y+1);
            table.Attach(highIndexButton, 1, 2, y, y+1);
            table.Attach(hAddButton,2,3,y,y+1);
            table.Attach(hRemoveButton,3,4,y,y+1);
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

            int hMax = GetNumHighIndices();
            if (hIndex >= hMax)
                hIndex = hMax-1;

            int lMax = GetNumLowIndices(hIndex);
            if (lIndex >= lMax)
                lIndex = lMax-1;

            highIndexButton.Adjustment.Upper = hMax-1;
            lowIndexButton.Adjustment.Upper = lMax-1;
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

        void RemoveSubIndex(int index) {
            int hIndex = index>>8;
            int lIndex = index&0xff;

            int n = GetNumLowIndices(hIndex);
            if (n <= 1)
                return;

            Data data = GetHighIndexDataBase(hIndex);
            FileParser parser = data.FileParser;

            data = Project.GetData(data.NextData.GetValue(0));

            for (int i=0;i<lIndex*4;i++)
                data = data.NextData;

            for (int i=0;i<4;i++) {
                Data d2 = data;
                data = data.NextData;
                parser.RemoveFileComponent(d2);
            }
        }

        void AddHighIndex() {
            int max = GetNumHighIndices();
            Data data = GetHighIndexDataBase(max-1);
            FileParser parser = data.FileParser;

            int n = 4;
            if (HighIndexUsesPointer(max-1))
                n = 3;
            for (int i=0;i<n-1;i++)
                data = data.NextData;

            parser.InsertParseableTextAfter(data,
                    new string[] {
                    "",
                    "\t.db $00 $00 $00 $00",
                    });
        }

        void RemoveHighIndex(int hIndex) {
            if (hIndex >= GetNumHighIndices())
                return;

            Data data = GetHighIndexDataBase(hIndex);
            FileParser parser = data.FileParser;

            string pointerString = "";
            int n=4;
            if (HighIndexUsesPointer(hIndex)) {
                pointerString = data.NextData.GetValue(0);
                n=3;
            }

            for (int i=0;i<n;i++) {
                if (i == 3) {
                    FileComponent next = parser.GetNextFileComponent(data);
                    var s = next as StringFileComponent;
                    if (s != null && s.GetString() == "") {
                        parser.RemoveFileComponent(next);
                    }
                }

                Data d2 = data;
                data = data.NextData;

                parser.RemoveFileComponent(d2);
            }

            if (pointerString != "") {
                data = parser.GetData(pointerString);
                LynnaLab.Label l = parser.GetDataLabel(data);
                parser.RemoveFileComponent(l);
                do {
                    Data d2 = data;
                    data = data.NextData;
                    parser.RemoveFileComponent(d2);
                }
                while (data != null && parser.GetDataLabel(data) == null);
            }
        }

        int GetNumHighIndices() {
            // Read until the first label after itemData.
            FileParser parser = Project.GetFileWithLabel("itemData");
            Data data = parser.GetData("itemData");

            int count=0;
            do {
                for (int i=0; i<4;) {
                    i += data.Size;
                    data = data.NextData;
                }
                count++;
            }
            while (data != null && parser.GetDataLabel(data) == null);

            return count;
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
        SpinButtonHexadecimal indexSpinButton;

        ItemEditorGui friend;

        ValueReference v1,v2,v3,v4;

        int RoomIndex {
            get {
                return indexSpinButton.ValueAsInt;
            }
        }

        Project Project {
            get {
                return manager.Project;
            }
        }

        public ChestEditorGui(PluginManager m)
            : base(1.0F,1.0F,1.0F,1.0F)
        {
            manager = m;

            indexSpinButton = new SpinButtonHexadecimal(0,Project.GetNumRooms()-1);
            indexSpinButton.ValueChanged += (a,b) => {
                SetRoom(indexSpinButton.ValueAsInt);
            };

            HBox roomIndexBox = new HBox();
            roomIndexBox.Add(new Gtk.Label("Room Index:"));
            roomIndexBox.Add(indexSpinButton);

            VBox vbox = new VBox();
            vbox.Add(roomIndexBox);

            vrContainer = new Alignment(1.0F, 1.0F, 1.0F, 1.0F);
            vbox.Add(vrContainer);

            Alignment buttonAlign = new Alignment(0.5F, 0.5F, 0.0F, 0.2F);
            buttonAlign.TopPadding = 3;

            Button syncButton = new Button("Sync Item Data");
            syncButton.Clicked += (a,b) => {
                SyncItemEditor();
            };
            buttonAlign.Add(syncButton);

            vbox.Add(buttonAlign);

            Add(vbox);
        }

        void SyncItemEditor() {
            if (friend != null && v3 != null) {
                friend.SetItem(v3.GetIntValue()<<8 | v4.GetIntValue());
            }
        }

        public void SetItemEditor(ItemEditorGui gui) {
            friend = gui;

            SyncItemEditor();
        }

        public void SetRoom(int room) {
            indexSpinButton.Value = room;

            vrContainer.Remove(vrEditor);
            vrEditor = null;
            v1 = null;
            v2 = null;
            v3 = null;
            v4 = null;

            Data data = GetChestData(room);

            if (data == null) {
                VBox vbox = new VBox();

                Button addButton = new Button("Add");
                addButton.Clicked += (a,b) => {
                    AddChestData(RoomIndex);
                    SetRoom(RoomIndex);
                };
                addButton.Label = "gtk-add";
                addButton.UseStock = true;

                vbox.Add(new Gtk.Label("No chest data exists\nfor this room."));
                Alignment btnAlign = new Alignment(0.5f,0.5f,0.0f,0.2f);
                btnAlign.TopPadding = 3;
                btnAlign.Add(addButton);
                vbox.Add(btnAlign);

                vrEditor = vbox;
            }
            else {

                v1 = new ValueReference("YX", 0, DataValueType.Byte);
                v1.SetData(data);
                data = data.NextData;
                v2 = new ValueReference("Room", 0, DataValueType.Byte, false);
                v2.SetData(data);
                data = data.NextData;
                v3 = new ValueReference("ID1", 0, DataValueType.Byte);
                v3.SetData(data);
                data = data.NextData;
                v4 = new ValueReference("ID2", 0, DataValueType.Byte);
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
                    DeleteChestData(RoomIndex);
                    SetRoom(RoomIndex);
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
