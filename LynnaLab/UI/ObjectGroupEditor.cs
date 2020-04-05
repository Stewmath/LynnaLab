using System;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public class ObjectGroupEditor : Gtk.Bin
    {
        public static readonly String[] ObjectNames = {
            "Condition",
            "Interaction",
            "Object Pointer",
            "Before Event Pointer",
            "After Event Pointer",
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


        ObjectGroup topObjectGroup, selectedObjectGroup;
        ObjectDefinition activeObject;
        int selectedIndex = -1;

        ValueReferenceEditor ObjectDataEditor;
        RoomEditor roomEditor;

        Dictionary<ObjectGroup, ObjectBox> objectBoxDict = new Dictionary<ObjectGroup, ObjectBox>();

		private Gtk.Container objectDataContainer, objectBoxContainer;
		private Gtk.Label frameLabel;

        bool disableBoxCallback = false;


        Project Project {
            get {
                return TopObjectGroup?.Project;
            }
        }

        // The TOP-LEVEL object group for this room.
        public ObjectGroup TopObjectGroup {
            get { return topObjectGroup; }
        }

        // The object group containing the current selected object.
        public ObjectGroup SelectedObjectGroup {
            get { return selectedObjectGroup; }
        }
        // The index of the selected object within SelectedObjectGroup.
        public int SelectedIndex {
            get { return selectedIndex; }
            set { selectedIndex = value; }
        }

        public ObjectDefinition SelectedObject {
            get {
                return SelectedObjectGroup.GetObject(SelectedIndex);
            }
        }

        public RoomEditor RoomEditor {
            get { return roomEditor; }
            set { roomEditor = value; }
        }


        public ObjectGroupEditor() {
            Gtk.Builder builder = new Builder();
            builder.AddFromString(Helper.ReadResourceFile("LynnaLab.Glade.ObjectGroupEditor.ui"));
            builder.Autoconnect(this);

            this.Child = (Gtk.Widget)builder.GetObject("mainBox");
            this.objectBoxContainer = (Gtk.Container)builder.GetObject("objectBoxContainer");
            this.objectDataContainer = (Gtk.Container)builder.GetObject("objectDataContainer");
            this.frameLabel = (Gtk.Label)builder.GetObject("objectDataContainerLabel");

            this.ShowAll();
        }

        public void SetObjectGroup(ObjectGroup topObjectGroup) {
            if (this.topObjectGroup != null)
                this.topObjectGroup.ModifiedEvent -= ObjectGroupModifiedHandler;
            this.topObjectGroup = topObjectGroup;
            this.topObjectGroup.ModifiedEvent += ObjectGroupModifiedHandler;

            objectBoxDict.Clear();
            objectBoxContainer.Foreach(objectBoxContainer.Remove);

            foreach (ObjectGroup group in topObjectGroup.GetAllGroups()) {
                var objectBox = new ObjectBox(group);

                objectBox.TileSelectedEvent += delegate(object sender, int index) {
                    if (!disableBoxCallback)
                        SelectObject(objectBox.ObjectGroup, index);
                };

                objectBoxDict.Add(group, objectBox);

                Gtk.Frame frame = new Gtk.Frame();
                frame.Label = GetGroupName(group);
                frame.Add(objectBox);
                objectBoxContainer.Add(frame);
            }

            SelectObject(TopObjectGroup, 0);
            this.ShowAll();
        }

        public void SelectObject(ObjectGroup group, int index) {
            if (!topObjectGroup.GetAllGroups().Contains(group))
                throw new Exception("Tried to select from an invalid object group.");

            index = Math.Min(index, group.GetNumObjects()-1);

            selectedObjectGroup = group;
            selectedIndex = index;

            disableBoxCallback = true;

            foreach (ObjectGroup g2 in topObjectGroup.GetAllGroups()) {
                if (g2 == selectedObjectGroup)
                    objectBoxDict[g2].SetSelectedIndex(index);
                else
                    objectBoxDict[g2].SetSelectedIndex(-1);
            }

            disableBoxCallback = false;

            if (selectedIndex == -1)
                SetObject(null);
            else
                SetObject(selectedObjectGroup.GetObject(selectedIndex));
        }


        void SetObject(ObjectDefinition obj) {
            if (activeObject == obj)
                return;
            activeObject = obj;

            System.Action handler = delegate() {
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

            if (obj == null) {
                frameLabel.Text = "";
                return;
            }
            frameLabel.Text = ObjectNames[(int)activeObject.GetObjectType()];

            ObjectDataEditor = new ValueReferenceEditor(Project, obj);
            ObjectDataEditor.AddDataModifiedHandler(handler);

            objectDataContainer.Add(ObjectDataEditor);
            objectDataContainer.ShowAll();

            UpdateDocumentation();
        }

        void ObjectGroupModifiedHandler(object sender, EventArgs args) {
            SelectObject(selectedObjectGroup, selectedIndex);
        }

        void UpdateDocumentation() {
            // Update tooltips in case ID has changed
            if (activeObject == null)
                return;

            var editor = ObjectDataEditor;
            if (editor == null)
                return;

            ValueReference r;
            try {
                r = activeObject.GetValueReference("ID");
            }
            catch(InvalidLookupException) {
                return;
            }

            if (r != null) {
                activeObject.GetValueReference("SubID").Documentation = null; // Set it to null now, might replace it below
                if (r.ConstantsMapping != null) {
                    try {
                        // Set tooltip based on ID field documentation
                        string objectName = r.ConstantsMapping.ByteToString((byte)r.GetIntValue());
                        string tooltip = objectName + "\n\n";
                        //tooltip += r.GetDocumentationField("desc");
                        editor.SetTooltip(r, tooltip.Trim());

                        Documentation doc = activeObject.GetIDDocumentation();
                        activeObject.GetValueReference("SubID").Documentation = doc;
                    }
                    catch(KeyNotFoundException) {
                    }
                }
            }
            editor.UpdateHelpButtons();
        }


        // Static methods

        // Objects colors match ZOLE mostly
		public static Cairo.Color GetObjectColor(ObjectType type)
		{
			switch (type)
			{
				case ObjectType.Conditional:        return CairoHelper.ConvertColor(System.Drawing.Color.Black);
				case ObjectType.Interaction:        return CairoHelper.ConvertColor(System.Drawing.Color.DarkOrange);
				case ObjectType.Pointer:            return CairoHelper.ConvertColor(System.Drawing.Color.Yellow);
				case ObjectType.BeforeEvent:        return CairoHelper.ConvertColor(System.Drawing.Color.Green);
				case ObjectType.AfterEvent:         return CairoHelper.ConvertColor(System.Drawing.Color.Blue);
				case ObjectType.RandomEnemy:        return new Cairo.Color(128/256.0, 64/256.0, 0/256.0);
				case ObjectType.SpecificEnemyA:     return new Cairo.Color(128/256.0, 64/256.0, 0/256.0);
				case ObjectType.SpecificEnemyB:     return new Cairo.Color(128/256.0, 64/256.0, 0/256.0);
				case ObjectType.Part:               return CairoHelper.ConvertColor(System.Drawing.Color.Gray);
				case ObjectType.ItemDrop:           return CairoHelper.ConvertColor(System.Drawing.Color.Lime);
			}
            return new Cairo.Color(1.0, 1.0, 1.0); // End, EndPointer, Garbage types should never be drawn
		}

        static String GetGroupName(ObjectGroup group) {
            ObjectGroupType type = group.GetGroupType();

            switch (type) {
            case ObjectGroupType.Main:
                return "Main objects";
            case ObjectGroupType.Enemy:
                return "Enemy objects";
            case ObjectGroupType.BeforeEvent:
                return "(Enemy) objects before event";
            case ObjectGroupType.AfterEvent:
                return "(Enemy) objects after event";
            case ObjectGroupType.Other:
                return group.Identifier;
            }

            throw new Exception("Unexpected thing happened");
        }
    }
}
