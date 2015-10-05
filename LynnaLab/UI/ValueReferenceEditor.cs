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

            List<String> labelList = new List<String>();
            List<Gtk.Widget> widgetList = new List<Gtk.Widget>();

            foreach (ValueReference r in valueReferences) {
                switch(r.ValueType) {
                    case DataValueType.String:
                    default:
                        {
                            labelList.Add(r.Name);
                            Gtk.Entry entry = new Gtk.Entry();
                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                entry.Text = r.GetStringValue();
                                OnDataModifiedInternal();
                            };
                            widgetList.Add(entry);
                            break;
                        }
                    case DataValueType.Byte:
                        {
                            labelList.Add(r.Name);
                            SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,255);
                            spinButton.Digits = 2;
                            spinButton.ValueChanged += delegate(object sender, EventArgs e) {
                                Gtk.SpinButton button = sender as Gtk.SpinButton;
                                r.SetValue(button.ValueAsInt);
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                spinButton.Value = r.GetIntValue();
                            };
                            widgetList.Add(spinButton);
                        }
                        break;
                    case DataValueType.Word:
                        {
                            labelList.Add(r.Name);
                            SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,0xffff);
                            spinButton.Digits = 4;
                            spinButton.ValueChanged += delegate(object sender, EventArgs e) {
                                Gtk.SpinButton button = sender as Gtk.SpinButton;
                                r.SetValue(button.ValueAsInt);
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                spinButton.Value = r.GetIntValue();
                            };
                            widgetList.Add(spinButton);
                        }
                        break;
                    case DataValueType.ByteBits:
                        {
                            labelList.Add(r.Name);
                            SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,r.MaxValue);
                            spinButton.Digits = (uint)((r.MaxValue+0xf)/0x10);
                            spinButton.ValueChanged += delegate(object sender, EventArgs e) {
                                Gtk.SpinButton button = sender as Gtk.SpinButton;
                                r.SetValue(button.ValueAsInt);
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                spinButton.Value = r.GetIntValue();
                            };
                            widgetList.Add(spinButton);
                        }
                        break;
                    case DataValueType.InteractionPointer:
                        {
                            labelList.Add(r.Name);

                            Gtk.Entry entry = new Gtk.Entry();
                            entry.Changed += delegate(object sender, EventArgs e) {
                                UpdatePointerTextBox(sender as Gtk.Entry, r);
                                OnDataModifiedInternal();
                            };
                            widgetList.Add(entry);

                            labelList.Add("");
                            pointerFrame = new Gtk.Frame();
                            pointerFrame.Label = "Pointer data (possibly shared)";
                            pointerFrame.BorderWidth = 5;
                            widgetList.Add(pointerFrame);

                            dataModifiedExternalEvent += delegate(object sender, EventArgs e) {
                                entry.Text = r.GetStringValue();
                                UpdatePointerTextBox(entry, r);
                            };
                        }
                        break;
                }
            }

            Gtk.Frame frame = null;
            if (frameText != null)
                frame = new Gtk.Frame(frameText);

            Gtk.Table table = new Gtk.Table(2, 2, false);
            table.ColumnSpacing = 6;
            uint y=0;

            for (int i=0;i<labelList.Count;i++) {
                table.Attach(new Gtk.Label(labelList[i]), 0, 1, y, y+1);
                if (widgetList[i] is Gtk.Frame)
                    table.Attach(widgetList[i], 0, 2, y, y+1);
                else
                    table.Attach(widgetList[i], 1, 2, y, y+1);
                y++;
            }

            if (frame == null) {
                this.Add(table);
            }
            else {
                frame.Add(table);
                this.Add(frame);
            }
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
