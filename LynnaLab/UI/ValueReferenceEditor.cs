using System;
using System.Collections.Generic;

namespace LynnaLab {
    public class ValueReferenceEditor : Gtk.Alignment
    {
        IList<ValueReference> valueReferences;
        IList<int> maxBounds;

        Gtk.Frame pointerFrame;
        InteractionGroupEditor subEditor;

        event Action dataModifiedExternalEvent;
        event Action dataModifiedInternalEvent;


        Project Project {get; set;}

        public InteractionGroupEditor SubEditor { // This is only for InteractionPointer values
            get { return subEditor; }
        }

        public ValueReferenceEditor(Project p, Data data, string frameText=null) 
            : this(p, data.GetValueReferences(), frameText)
        {
        }

        public ValueReferenceEditor(Project p, IList<ValueReference> vrs, string frameText=null) 
        : base(1.0F,1.0F,1.0F,1.0F)
        {
            Project = p;

            valueReferences = vrs;
            maxBounds = new int[valueReferences.Count];

            Gtk.Table table = new Gtk.Table(2, 2, false);
            uint y=0;

            int cnt=0;
            foreach (ValueReference r in valueReferences) {
                int index = cnt;
                cnt++;

                if (r.ConstantsMapping != null) {
                    ComboBoxFromConstants comboBox = new ComboBoxFromConstants();
                    comboBox.SetConstantsMapping(r.ConstantsMapping);

                    comboBox.Changed += delegate(object sender, EventArgs e) {
                        r.SetValue(comboBox.ActiveValue);
                    };

                    dataModifiedExternalEvent += delegate() {
                        comboBox.ActiveValue = r.GetIntValue();
                    };

                    table.Attach(new Gtk.Label(r.Name), 0,1,y,y+1);
                    table.Attach(comboBox, 1,2,y,y+1);

                    goto loopEnd;
                }
                // ConstantsMapping == null

                switch(r.ValueType) {
                    case DataValueType.String:
                    default:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);
                            Gtk.Entry entry = new Gtk.Entry();
                            if (!r.Editable)
                                entry.Sensitive = false;
                            dataModifiedExternalEvent += delegate() {
                                entry.Text = r.GetStringValue();
                                OnDataModifiedInternal();
                            };
                            table.Attach(entry, 1, 2, y, y+1);
                            break;
                        }
                    case DataValueType.Byte:
                    case DataValueType.HalfByte:
byteCase:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);
                            SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,255);
                            if (!r.Editable)
                                spinButton.Sensitive = false;
                            if (r.ValueType == DataValueType.HalfByte) {
                                spinButton.Digits = 1;
                                spinButton.Adjustment.Upper = 15;
                            }
                            else
                                spinButton.Digits = 2;
                            spinButton.ValueChanged += delegate(object sender, EventArgs e) {
                                Gtk.SpinButton button = sender as Gtk.SpinButton;
                                if (maxBounds[index] == 0 ||button.ValueAsInt <= maxBounds[index]) {
                                    r.SetValue(button.ValueAsInt);
                                }
                                else
                                    button.Value = maxBounds[index];
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate() {
                                spinButton.Value = r.GetIntValue();
                            };
                            table.Attach(spinButton, 1, 2, y, y+1);
                        }
                        break;

                    case DataValueType.WarpDestIndex:
                        {
                            Gtk.Button newDestButton = new Gtk.Button("New\nDestination");
                            newDestButton.Clicked += delegate(object sender, EventArgs e) {
                                WarpSourceData warpData = (WarpSourceData)r.Data;
                                WarpDestGroup destGroup = warpData.GetReferencedDestGroup();
                                // Check if there's unused destination data
                                // already
                                for (int i=0; i<destGroup.GetNumWarpDests(); i++) {
                                    WarpDestData destData = destGroup.GetWarpDest(i);
                                    if (destData.GetNumReferences() == 0) {
                                        Gtk.MessageDialog d = new Gtk.MessageDialog(null,
                                                Gtk.DialogFlags.DestroyWithParent,
                                                Gtk.MessageType.Warning,
                                                Gtk.ButtonsType.YesNo,
                                                "Destination index " + i.ToString("X2") + " is not used by any sources. Use this index?\n\n(\"No\" will create a new destination instead.)");
                                        Gtk.ResponseType response = (Gtk.ResponseType)d.Run();
                                        d.Destroy();

                                        if (response == Gtk.ResponseType.Yes)
                                            warpData.SetDestData(destGroup.GetWarpDest(i));
                                        else if (response == Gtk.ResponseType.No)
                                            warpData.SetDestData(destGroup.AddDestData());
                                        break;
                                    }
                                }
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
                                if (maxBounds[index] == 0 || button.ValueAsInt <= maxBounds[index]) {
                                    r.SetValue(button.ValueAsInt);
                                }
                                else
                                    button.Value = maxBounds[index];
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate() {
                                spinButton.Value = r.GetIntValue();
                            };
                            table.Attach(spinButton, 1, 2, y, y+1);
                        }
                        break;
                    case DataValueType.ByteBit:
                        {
                            table.Attach(new Gtk.Label(r.Name), 0, 1, y, y+1);
                            Gtk.CheckButton checkButton = new Gtk.CheckButton();
                            checkButton.CanFocus = false;
                            if (!r.Editable)
                                checkButton.Sensitive = false;
                            checkButton.Toggled += delegate(object sender, EventArgs e) {
                                Gtk.CheckButton button = sender as Gtk.CheckButton;
                                r.SetValue(button.Active ? 1 : 0);
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate() {
                                checkButton.Active = r.GetIntValue() == 1;
                            };
                            table.Attach(checkButton, 1, 2, y, y+1);
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
                                if (maxBounds[index] == 0 || button.ValueAsInt <= maxBounds[index]) {
                                    r.SetValue(button.ValueAsInt);
                                }
                                else
                                    button.Value = maxBounds[index];
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate() {
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

                            dataModifiedExternalEvent += delegate() {
                                entry.Text = r.GetStringValue();
                                UpdatePointerTextBox(entry, r);
                            };
                        }
                        break;
                }

loopEnd:
                y++;
            }

            table.ColumnSpacing = 6;

            this.Add(table);
            this.ShowAll();

            Data lastData = null;
            foreach (ValueReference r in valueReferences) {
                if (lastData != r.Data) {
                    lastData = r.Data;
                    r.Data.AddDataModifiedHandler(OnDataModifiedExternal);
                    // Destroy handler
                    this.Destroyed += delegate(object sender, EventArgs e) {
                        r.Data.RemoveDataModifiedHandler(OnDataModifiedExternal);
                    };
                }
            }

            // Initial values
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent();
        }

        public void SetMaxBound(ValueReference r, int max) {
            int i = valueReferences.IndexOf(r);
            if (i == -1)
                return;
            maxBounds[i] = max;
        }

        public void AddDataModifiedHandler(Action handler) {
            dataModifiedInternalEvent += handler;
        }
        public void RemoveDataModifiedHandler(Action handler) {
            dataModifiedInternalEvent -= handler;
        }

        // Data modified externally
        void OnDataModifiedExternal(object sender, EventArgs e) {
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent();
        }

        // Data modified internally
        void OnDataModifiedInternal() {
            if (dataModifiedInternalEvent != null)
                dataModifiedInternalEvent();
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
            catch (LabelNotFoundException) {
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
