using System;
using System.Collections.Generic;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class ObjectGroupEditor : Gtk.Bin
    {
        public static readonly String[] ObjectNames = {
            "Set Condition",
            "No Value Interaction",
            "2-Value Interaction",
            "Object Pointer",
            "Boss Object Pointer",
            "Anti-Boss Object Pointer",
            "Random Position Enemy",
            "Specific Position Enemy",
            "Part",
            "Object With Parameter",
            "Item Drop",
        };

        public static readonly String[] ObjectDescriptions = {
            "Set a condition to enable or disable the following objects.",
            "A class of objects which can use scripts. Does not take an explicit X/Y value.",
            "A class of objects which can use scripts. Does take an explicit X/Y value.",
            "A pointer to another set of object data.",
            "A pointer which only activates when bit 7 of the room flags is NOT set.",
            "A pointer which only activates when bit 7 of the room flags IS set.",
            "An enemy (or multiple enemies) in random positions in the room.",
            "An enemy at a specific position in the room.",
            "A class of objects with a variety of purposes (switches, animal statues, particles...)",
            "An object of type 'Interaction' (0), 'Enemy' (1), or 'Part' (2) which also takes a parameter.",
            "An item drops when a tile is destroyed at a given location."
        };

        ObjectGroup _ObjectGroup;
        ObjectData activeData;

        ValueReferenceEditor ObjectDataEditor;
        RoomEditor roomEditor;

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
                widget.Destroy();
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
            d.Run();
            if (d.ObjectTypeToAdd != ObjectType.End) {
                if (ObjectGroup == null) return;

                ObjectGroup.InsertObject(indexSpinButton.ValueAsInt+1, d.ObjectTypeToAdd);
                UpdateBoundaries();
                indexSpinButton.Value = indexSpinButton.ValueAsInt+1;
            }
        }
    }
}
