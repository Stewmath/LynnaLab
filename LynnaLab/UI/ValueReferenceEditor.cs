using System;
using System.Collections.Generic;

namespace LynnaLab {
    public class ValueReferenceEditor : Gtk.Alignment
    {
        IList<ValueReference> valueReferences;
        Gtk.Frame pointerFrame;
        InteractionGroupEditor subEditor;

        event EventHandler dataModifiedExternalEvent;
        event EventHandler dataModifiedInternalEvent;


        Project Project {get; set;}

        public InteractionGroupEditor SubEditor { // This is only for InteractionPointer values
            get { return subEditor; }
        }

        public ValueReferenceEditor(Project p, Data data, string frameText=null) 
        : base(1.0F,1.0F,1.0F,1.0F) {
            Project = p;

            valueReferences = data.GetValueReferences();

//             List<String> labelList = new List<String>();
//             List<Gtk.Widget> widgetList = new List<Gtk.Widget>();

            Gtk.Table table = new Gtk.Table(2, 2, false);
            uint y=0;

            foreach (ValueReference r in valueReferences) {
                switch(r.ValueType) {
                    case DataValueType.String:
                    default:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);
                            Gtk.Entry entry = new Gtk.Entry();
                            if (!r.Editable)
                                entry.Sensitive = false;
                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                entry.Text = r.GetStringValue();
                                OnDataModifiedInternal();
                            };
                            table.Attach(entry, 1, 2, y, y+1);
                            break;
                        }
                    case DataValueType.Byte:
byteCase:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);
                            SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,255);
                            if (!r.Editable)
                                spinButton.Sensitive = false;
                            spinButton.Digits = 2;
                            spinButton.ValueChanged += delegate(object sender, EventArgs e) {
                                Gtk.SpinButton button = sender as Gtk.SpinButton;
                                r.SetValue(button.ValueAsInt);
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                spinButton.Value = r.GetIntValue();
                            };
                            table.Attach(spinButton, 1, 2, y, y+1);
                        }
                        break;

                    case DataValueType.WarpDestIndex:
                        {
                            Gtk.Button newDestButton = new Gtk.Button("New\nDestination");
                            newDestButton.Clicked += delegate(object sender, EventArgs e) {
                                WarpSourceData warpData = (WarpSourceData)data;
                                WarpDestGroup group = warpData.GetReferencedDestGroup();
                                warpData.SetDestData(group.AddDestData());
                            };
                            table.Attach(newDestButton, 2, 3, y, y+2);
                        }
                        goto byteCase;

                    case DataValueType.Word:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);
                            SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,0xffff);
                            if (!r.Editable)
                                spinButton.Sensitive = false;
                            spinButton.Digits = 4;
                            spinButton.ValueChanged += delegate(object sender, EventArgs e) {
                                Gtk.SpinButton button = sender as Gtk.SpinButton;
                                r.SetValue(button.ValueAsInt);
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                spinButton.Value = r.GetIntValue();
                            };
                            table.Attach(spinButton, 1, 2, y, y+1);
                        }
                        break;
                    case DataValueType.ByteBit:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);
                            Gtk.CheckButton checkButton = new Gtk.CheckButton();
                            if (!r.Editable)
                                checkButton.Sensitive = false;
                            checkButton.Toggled += delegate(object sender, EventArgs e) {
                                Gtk.CheckButton button = sender as Gtk.CheckButton;
                                r.SetValue(button.Active ? 1 : 0);
                                OnDataModifiedInternal();
                            };
                        }
                        break;
                    case DataValueType.ByteBits:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);
                            SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,r.MaxValue);
                            if (!r.Editable)
                                spinButton.Sensitive = false;
                            spinButton.Digits = (uint)((r.MaxValue+0xf)/0x10);
                            spinButton.ValueChanged += delegate(object sender, EventArgs e) {
                                Gtk.SpinButton button = sender as Gtk.SpinButton;
                                r.SetValue(button.ValueAsInt);
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                spinButton.Value = r.GetIntValue();
                            };
                            table.Attach(spinButton, 1, 2, y, y+1);
                        }
                        break;
                    case DataValueType.InteractionPointer:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);

                            Gtk.Entry entry = new Gtk.Entry();
                            if (!r.Editable)
                                entry.Sensitive = false;
                            entry.Changed += delegate(object sender, EventArgs e) {
                                UpdatePointerTextBox(sender as Gtk.Entry, r);
                                OnDataModifiedInternal();
                            };
                            table.Attach(entry, 1, 2, y, y+1);

                            pointerFrame = new Gtk.Frame();
                            pointerFrame.Label = "Pointer data (possibly shared)";
                            pointerFrame.BorderWidth = 5;

                            y++;
                            table.Attach(pointerFrame, 0, 2, y, y+1);

                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                entry.Text = r.GetStringValue();
                                UpdatePointerTextBox(entry, r);
                            };
                        }
                        break;
                }

                y++;
            }

            table.ColumnSpacing = 6;

            this.Add(table);
            this.ShowAll();

            data.AddDataModifiedHandler(OnDataModifiedExternal);
            // Destroy handler
            this.Destroyed += delegate(object sender, EventArgs e) {
                data.RemoveDataModifiedHandler(OnDataModifiedExternal);
            };

            // Initial values
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent(this, null);
        }

        // Data modified externally
        void OnDataModifiedExternal(object sender, EventArgs e) {
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent(this, null);
        }

        // Data modified internally
        void OnDataModifiedInternal() {
            if (dataModifiedInternalEvent != null)
                dataModifiedInternalEvent(this, null);
        }

        public void AddDataModifiedHandler(EventHandler handler) {
            dataModifiedInternalEvent += handler;
        }
        public void RemoveDataModifiedHandler(EventHandler handler) {
            dataModifiedInternalEvent -= handler;
        }

        void UpdatePointerTextBox(Gtk.Entry entry, ValueReference r) {
            pointerFrame.Remove(pointerFrame.Child);

            subEditor = new InteractionGroupEditor();
            Gtk.Alignment alignment = new Gtk.Alignment(0.5F, 0.5F, 0.0F, 0.8F);
            try {
                Project.GetFileWithLabel(entry.Text.Trim());
                subEditor.SetInteractionGroup(Project.GetDataType<InteractionGroup>(r.GetStringValue()));
                subEditor.ShowAll();
                alignment.Add(subEditor);
                r.SetValue(entry.Text.Trim());
            }
            catch (LabelNotFoundException e) {
                subEditor.SetInteractionGroup(null);
                Gtk.Label label = new Gtk.Label("Error: label \"" + entry.Text + "\" not found.");
                label.Show();
                alignment.Add(label);
            }
            pointerFrame.Label = entry.Text;
            pointerFrame.Add(alignment);
            pointerFrame.ShowAll();
        }

    }
}
