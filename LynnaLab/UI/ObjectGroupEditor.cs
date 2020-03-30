using System;
using System.Collections.Generic;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public class ObjectGroupEditor : Gtk.Bin
    {
        public static readonly String[] ObjectNames = {
            "Set Condition",
            "Interaction",
            "Object Pointer",
            "Boss Object Pointer",
            "Anti-Boss Object Pointer",
            "Random Position Enemy",
            "Specific Position Enemy (A)",
            "Specific Position Enemy (B)",
            "Part",
            "Item Drop",
        };

        public static readonly String[] ObjectDescriptions = {
            "Set a condition to enable or disable the following objects.",
            "A class of objects which can use scripts.",
            "A pointer to another set of object data.",
            "A pointer which only activates when bit 7 of the room flags is NOT set.",
            "A pointer which only activates when bit 7 of the room flags IS set.",
            "An enemy (or multiple enemies) in random positions in the room.",
            "An enemy at a specific position in the room. (Can set flags, can't set Var03.)",
            "An enemy at a specific position in the room. (Can't set flags, can set Var03.)",
            "A class of objects with a variety of purposes (switches, animal statues, particles...)",
            "An item drops when a tile is destroyed at a given location."
        };

        ObjectGroup _ObjectGroup;
        ObjectData activeData;

        ValueReferenceEditor ObjectDataEditor;
        RoomEditor roomEditor;


		private global::Gtk.VBox vbox1;
		private global::Gtk.HBox hbox1;
		private global::Gtk.Label label1;
		private global::Gtk.SpinButton indexSpinButton;
		private global::Gtk.Button addButton;
		private global::Gtk.Button deleteButton;
		private global::Gtk.Frame frame2;
		private global::Gtk.Alignment GtkAlignment1;
		private global::Gtk.Alignment objectDataContainer;
		private global::Gtk.Label frameLabel;


        Project Project {
            get {
                return ObjectGroup?.Project;
            }
        }
        public ObjectGroup ObjectGroup {
            get { return _ObjectGroup; }
        }
        public int SelectedIndex {
            get { return indexSpinButton.ValueAsInt; }
            set {
                indexSpinButton.Value = value;
            }
        }

        // This property accounts for pointers
        public ObjectData SelectedObjectData {
            get {
                if (SubEditor != null)
                    return SubEditor.SelectedObjectData;
                return ObjectGroup.GetObjectData(SelectedIndex);
            }
        }

        public RoomEditor RoomEditor {
            get { return roomEditor; }
            set {
                if (roomEditor != value) {
                    roomEditor = value;
                    if (SubEditor != null)
                        SubEditor.RoomEditor = value;
                }
            }
        }

        public ObjectGroupEditor SubEditor { // Sub-editor for pointers
            get {
                if (ObjectDataEditor == null) return null;
                return ObjectDataEditor.SubEditor;
            }
        }
        

        public ObjectGroupEditor()
        {
            this.Build();

            indexSpinButton.Adjustment.Lower = -1;

            addButton.Image = new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button);
            addButton.Label = "";
            deleteButton.Image = new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button);
            deleteButton.Label = "";

            indexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                int i = indexSpinButton.ValueAsInt;
                if (ObjectGroup == null || i == -1)
                    SetObjectData(null);
                else
                    SetObjectData(ObjectGroup.GetObjectData(i));
            };
        }

        protected virtual void Build()
        {
            // Container child LynnaLab.ObjectGroupEditor.Gtk.Container+ContainerChild
            this.vbox1 = new global::Gtk.VBox();
            this.vbox1.Name = "vbox1";
            this.vbox1.Spacing = 6;
            // Container child vbox1.Gtk.Box+BoxChild
            this.hbox1 = new global::Gtk.HBox();
            this.hbox1.Name = "hbox1";
            this.hbox1.Spacing = 6;
            // Container child hbox1.Gtk.Box+BoxChild
            this.label1 = new global::Gtk.Label();
            this.label1.Name = "label1";
            this.label1.LabelProp = global::Mono.Unix.Catalog.GetString("Object Index");
            this.hbox1.Add(this.label1);
            hbox1.SetChildPacking(label1, false, false, 0, Gtk.PackType.Start);
            // Container child hbox1.Gtk.Box+BoxChild
            this.indexSpinButton = new global::Gtk.SpinButton(0D, 100D, 1D);
            this.indexSpinButton.Name = "indexSpinButton";
            this.indexSpinButton.Adjustment.PageIncrement = 10D;
            this.indexSpinButton.ClimbRate = 1D;
            this.indexSpinButton.Numeric = true;
            this.hbox1.Add(this.indexSpinButton);
            hbox1.SetChildPacking(indexSpinButton, false, false, 0, Gtk.PackType.Start);
            // Container child hbox1.Gtk.Box+BoxChild
            this.addButton = new global::Gtk.Button();
            this.addButton.CanFocus = true;
            this.addButton.Name = "addButton";
            this.addButton.UseStock = true;
            this.addButton.UseUnderline = true;
            this.addButton.FocusOnClick = false;
            this.addButton.Label = "gtk-add";
            this.hbox1.Add(this.addButton);
            hbox1.SetChildPacking(addButton, false, false, 0, Gtk.PackType.Start);
            // Container child hbox1.Gtk.Box+BoxChild
            this.deleteButton = new global::Gtk.Button();
            this.deleteButton.CanFocus = true;
            this.deleteButton.Name = "deleteButton";
            this.deleteButton.UseStock = true;
            this.deleteButton.UseUnderline = true;
            this.deleteButton.FocusOnClick = false;
            this.deleteButton.Label = "gtk-remove";
            this.hbox1.Add(this.deleteButton);
            hbox1.SetChildPacking(deleteButton, false, false, 0, Gtk.PackType.Start);
            this.vbox1.Add(this.hbox1);
            vbox1.SetChildPacking(hbox1, false, false, 0, Gtk.PackType.Start);
            // Container child vbox1.Gtk.Box+BoxChild
            this.frame2 = new global::Gtk.Frame();
            this.frame2.Name = "frame2";
            // Container child frame2.Gtk.Container+ContainerChild
            this.GtkAlignment1 = new global::Gtk.Alignment(0F, 0F, 1F, 0F);
            this.GtkAlignment1.Name = "GtkAlignment1";
            this.GtkAlignment1.LeftPadding = ((uint)(12));
            // Container child GtkAlignment1.Gtk.Container+ContainerChild
            this.objectDataContainer = new global::Gtk.Alignment(0.5F, 0.5F, 1F, 1F);
            this.objectDataContainer.Name = "objectDataContainer";
            this.GtkAlignment1.Add(this.objectDataContainer);
            this.frame2.Add(this.GtkAlignment1);
            this.frameLabel = new global::Gtk.Label();
            this.frameLabel.Name = "frameLabel";
            this.frameLabel.LabelProp = global::Mono.Unix.Catalog.GetString("Object");
            this.frameLabel.UseMarkup = true;
            this.frame2.LabelWidget = this.frameLabel;
            this.vbox1.Add(this.frame2);
            this.Add(this.vbox1);
            if ((this.Child != null))
            {
                this.Child.ShowAll();
            }
            this.Hide();
            this.addButton.Clicked += new global::System.EventHandler(this.OnAddButtonClicked);
            this.deleteButton.Clicked += new global::System.EventHandler(this.OnDeleteButtonClicked);
        }

        public void SetObjectGroup(ObjectGroup group) {
            _ObjectGroup = group;
            UpdateBoundaries();
            SetObjectDataIndex(indexSpinButton.ValueAsInt);
        }

        void SetObjectDataIndex(int i) {
            if (ObjectGroup == null || i < 0 || i >= ObjectGroup.GetNumObjects())
                SetObjectData(null);
            else
                SetObjectData(ObjectGroup.GetObjectData(i));
        }
        void SetObjectData(ObjectData data) {
            activeData = data;

            Action handler = delegate() {
                if (RoomEditor != null)
                    RoomEditor.OnObjectsModified();

                UpdateDocumentation();
            };

            foreach (Gtk.Widget widget in objectDataContainer.Children) {
                objectDataContainer.Remove(widget);
                widget.Dispose();
            }
            if (ObjectDataEditor != null) {
                ObjectDataEditor.RemoveDataModifiedHandler(handler);
                ObjectDataEditor = null;
            }

            if (RoomEditor != null)
                RoomEditor.OnObjectsModified();

            if (data == null) {
                frameLabel.Text = "";
                return;
            }
            frameLabel.Text = ObjectNames[(int)activeData.GetObjectType()];

            ObjectDataEditor = new ValueReferenceEditor(Project,data);
            ObjectDataEditor.AddDataModifiedHandler(handler);

            if (SubEditor != null) {
                SubEditor.RoomEditor = RoomEditor;
            }

            objectDataContainer.Add(ObjectDataEditor);
            objectDataContainer.ShowAll();

            UpdateDocumentation();
        }

        // Update the boundaries for indexSpinButton (how many objects there are)
        void UpdateBoundaries() {
            int origValue = indexSpinButton.ValueAsInt;

            indexSpinButton.Adjustment.Lower = -1;
            int max;
            if (ObjectGroup == null)
                max = -1;
            else
                max = ObjectGroup.GetNumObjects()-1;

            indexSpinButton.Adjustment.Upper = max;
            if (indexSpinButton.ValueAsInt > max) {
                indexSpinButton.Value = max;
            }

            if (origValue != indexSpinButton.ValueAsInt)
                SetObjectDataIndex(indexSpinButton.ValueAsInt);
        }

        void UpdateDocumentation() {
            // Update tooltips in case ID has changed
            if (activeData == null)
                return;

            var editor = ObjectDataEditor;
            if (editor == null)
                return;

            while (true) {
                ValueReference r;
                try {
                    r = activeData.GetValueReference("ID");
                }
                catch(InvalidLookupException) {
                    goto next;
                }

                if (r != null) {
                    activeData.GetValueReference("SubID").Documentation = null; // Set it to null now, might replace it below
                    if (r.ConstantsMapping != null) {
                        try {
                            // Set tooltip based on ID field documentation
                            string objectName = r.ConstantsMapping.ByteToString((byte)r.GetIntValue());
                            string tooltip = objectName + "\n\n";
                            //tooltip += r.GetDocumentationField("desc");
                            editor.SetTooltip(r, tooltip.Trim());

                            Documentation doc = activeData.GetIDDocumentation();
                            activeData.GetValueReference("SubID").Documentation = doc;
                        }
                        catch(KeyNotFoundException) {
                        }
                    }
                }
                editor.UpdateHelpButtons();

next:
                if (editor.SubEditor == null || editor.SubEditor.ObjectDataEditor == null)
                    break;
                editor = editor.SubEditor.ObjectDataEditor;
            }
        }

        protected void OnDeleteButtonClicked(object sender, EventArgs e)
        {
            if (ObjectGroup != null && indexSpinButton.ValueAsInt != -1) {
                ObjectGroup.RemoveObject(indexSpinButton.ValueAsInt);
                UpdateBoundaries();
            }
        }

        protected void OnAddButtonClicked(object sender, EventArgs e)
        {
            if (ObjectGroup == null) return;

            AddObjectDialog d = new AddObjectDialog();
            d.Response += delegate(object o, Gtk.ResponseArgs resp) {
                if (resp.ResponseId == Gtk.ResponseType.Ok) {
                    if (ObjectGroup == null) return;

                    ObjectGroup.InsertObject(indexSpinButton.ValueAsInt+1, d.ObjectTypeToAdd);
                    UpdateBoundaries();
                    indexSpinButton.Value = indexSpinButton.ValueAsInt+1;
                }
            };
            d.Run();
        }
    }
}
