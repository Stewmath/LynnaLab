using System;
using System.Collections.Generic;

namespace LynnaLab {
    public class ValueReferenceEditor : Gtk.Alignment
    {
        ValueReferenceGroup valueReferenceGroup;

        IList<int> maxBounds;
        // X/Y positions where the widgets are in the table
        IList<Tuple<uint,uint>> widgetPositions;
        // The widgets by index.
        IList<Gtk.Widget> widgets;

        // List of container widgets for help buttons (1 per item)
        IList<Gtk.Container> helpButtonContainers;

        // The main table which holds all the widgets.
        Gtk.Table table;

        Gtk.Frame pointerFrame;
        ObjectGroupEditor subEditor;

        event Action dataModifiedExternalEvent;
        event Action dataModifiedInternalEvent;


        Project Project {get; set;}

        public ObjectGroupEditor SubEditor { // This is only for ObjectPointer values
            get { return subEditor; }
        }

        public ValueReferenceGroup ValueReferenceGroup {
            get { return valueReferenceGroup; }
        }

        public ValueReferenceEditor(Project p, Data data, string frameText=null) 
            : this(p, data.GetValueReferenceGroup(), frameText)
        {
        }

        public ValueReferenceEditor(Project p, ValueReferenceGroup vrg, string frameText=null) 
            : this(p, vrg, 50, frameText)
        {
        }

        public ValueReferenceEditor(Project p, ValueReferenceGroup vrg, int rows, string frameText=null) 
            : base(1.0F,1.0F,1.0F,1.0F)
        {
            Project = p;

            valueReferenceGroup = vrg;
            maxBounds = new int[valueReferenceGroup.GetNumValueReferences()];
            widgetPositions = new Tuple<uint,uint>[maxBounds.Count];
            widgets = new Gtk.Widget[maxBounds.Count];
            helpButtonContainers = new Gtk.Container[maxBounds.Count];

            table = new Gtk.Table(2, 2, false);
            uint x=0,y=0;

            int cnt=0;
            foreach (ValueReference r in valueReferenceGroup.GetValueReferences()) {
                int index = cnt;
                cnt++;

                if (y >= rows) {
                    y = 0;
                    x += 3;
                }

                widgetPositions[index] = new Tuple<uint,uint>(x,y);

                // If it has a ConstantsMapping, use a combobox instead of anything else
                if (r.ConstantsMapping != null) {
                    ComboBoxFromConstants comboBox = new ComboBoxFromConstants(false);
                    comboBox.SetConstantsMapping(r.ConstantsMapping);

                    comboBox.Changed += delegate(object sender, EventArgs e) {
                        r.SetValue(comboBox.ActiveValue);
                        OnDataModifiedInternal();
                    };

                    dataModifiedExternalEvent += delegate() {
                        comboBox.ActiveValue = r.GetIntValue ();
                    };

                    table.Attach(new Gtk.Label(r.Name), x+0,x+1,y,y+1);
                    table.Attach(comboBox, x+1,x+2,y,y+1);
                    widgets[index] = comboBox;

                    helpButtonContainers[index] = new Gtk.HBox();
                    table.Attach(helpButtonContainers[index], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);

                    goto loopEnd;
                }
                // ConstantsMapping == null

                switch(r.ValueType) {
                    case DataValueType.String:
                    default:
                        {
                            table.Attach(new Gtk.Label(r.Name), x+0,x+1, y, y+1);
                            Gtk.Entry entry = new Gtk.Entry();
                            if (!r.Editable)
                                entry.Sensitive = false;
                            dataModifiedExternalEvent += delegate() {
                                entry.Text = r.GetStringValue();
                                OnDataModifiedInternal();
                            };
                            table.Attach(entry, x+1,x+2, y, y+1);
                            widgets[index] = entry;

                            helpButtonContainers[index] = new Gtk.HBox();
                            table.Attach(helpButtonContainers[index], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
                            break;
                        }
                    case DataValueType.Byte:
                    case DataValueType.HalfByte:
byteCase:
                        {
                            table.Attach(new Gtk.Label(r.Name), x+0,x+1, y, y+1);
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
                            table.Attach(spinButton, x+1,x+2, y, y+1);
                            widgets[index] = spinButton;

                            helpButtonContainers[index] = new Gtk.HBox();
                            table.Attach(helpButtonContainers[index], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
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
                            table.Attach(newDestButton, x+2,x+3, y, y+2);
                        }
                        goto byteCase;

                    case DataValueType.Word:
                        {
                            table.Attach(new Gtk.Label(r.Name), x+0,x+1, y, y+1);
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
                            table.Attach(spinButton, x+1,x+2, y, y+1);
                            widgets[index] = spinButton;

                            helpButtonContainers[index] = new Gtk.HBox();
                            table.Attach(helpButtonContainers[index], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
                        }
                        break;
                    case DataValueType.ByteBit:
                        {
                            table.Attach(new Gtk.Label(r.Name), x+0,x+1, y, y+1);
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
                            table.Attach(checkButton, x+1,x+2, y, y+1);
                            widgets[index] = checkButton;

                            helpButtonContainers[index] = new Gtk.HBox();
                            table.Attach(helpButtonContainers[index], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
                        }
                        break;
                    case DataValueType.ByteBits:
                    case DataValueType.WordBits:
                        {
                            table.Attach(new Gtk.Label(r.Name), x+0,x+1, y, y+1);
                            SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,r.MaxValue);
                            if (!r.Editable)
                                spinButton.Sensitive = false;
                            spinButton.Digits = (uint)Math.Pow(r.MaxValue, ((double)1)/16)+1;
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
                            table.Attach(spinButton, x+1,x+2, y, y+1);
                            widgets[index] = spinButton;

                            helpButtonContainers[index] = new Gtk.HBox();
                            table.Attach(helpButtonContainers[index], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
                        }
                        break;
                    case DataValueType.ObjectPointer:
                        {
                            table.Attach(new Gtk.Label(r.Name), x+0,x+1, y, y+1);

                            Gtk.Entry entry = new Gtk.Entry();
                            if (!r.Editable)
                                entry.Sensitive = false;
                            entry.Changed += delegate(object sender, EventArgs e) {
                                UpdatePointerTextBox(sender as Gtk.Entry, r);
                                OnDataModifiedInternal();
                            };
                            table.Attach(entry, x+1,x+2, y, y+1);
                            widgets[index] = entry;

                            pointerFrame = new Gtk.Frame();
                            pointerFrame.Label = "Pointer data (possibly shared)";
                            pointerFrame.BorderWidth = 5;

                            y++;
                            table.Attach(pointerFrame, x+0,x+2, y, y+1);

                            dataModifiedExternalEvent += delegate() {
                                entry.Text = r.GetStringValue();
                                UpdatePointerTextBox(entry, r);
                            };

                            helpButtonContainers[index] = new Gtk.HBox();
                            table.Attach(helpButtonContainers[index], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
                        }
                        break;
                }

loopEnd:
                y++;
            }

            table.ColumnSpacing = 6;

            if (frameText != null) {
                var frame = new Gtk.Frame(frameText);
                frame.Add(table);
                this.Add(frame);
            }
            else
                this.Add(table);

            this.ShowAll();

            Data lastData = null;
            foreach (ValueReference r in valueReferenceGroup.GetValueReferences()) {
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

            UpdateHelpButtons();
        }

        public void SetMaxBound(int i, int max) {
            if (i == -1)
                return;
            maxBounds[i] = max;
        }

        public void ReplaceWidget(int i, Gtk.Widget newWidget) {
            table.Remove(widgets[i]);
            var pos = widgetPositions[i];
            table.Attach(newWidget, pos.Item1+1, pos.Item1+2, pos.Item2, pos.Item2+1);
            widgets[i] = newWidget;
        }

        public void SetTooltip(int i, string tooltip) {
            widgets[i].TooltipText = tooltip;
        }
        public void SetTooltip(ValueReference r, string tooltip) {
            SetTooltip(valueReferenceGroup.GetIndexOf(r), tooltip);
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

            subEditor = new ObjectGroupEditor();
            Gtk.Alignment alignment = new Gtk.Alignment(0.5F, 0.5F, 0.0F, 0.8F);
            try {
                Project.GetFileWithLabel(entry.Text.Trim());
                subEditor.SetObjectGroup(Project.GetDataType<ObjectGroup>(r.GetStringValue()));
                subEditor.ShowAll();
                alignment.Add(subEditor);
                r.SetValue(entry.Text.Trim());
            }
            catch (InvalidLookupException) {
                subEditor.SetObjectGroup(null);
                Gtk.Label label = new Gtk.Label("Error: label \"" + entry.Text + "\" not found.");
                label.Show();
                alignment.Add(label);
            }
            pointerFrame.Label = entry.Text;
            pointerFrame.Add(alignment);
            pointerFrame.ShowAll();
        }

        // Check if there are entries that should have help buttons
        public void UpdateHelpButtons() {
            IList<ValueReference> refs = valueReferenceGroup.GetValueReferences();

            for (int i=0; i<refs.Count; i++) {
                Gtk.Container container = helpButtonContainers[i];
                if (container == null)
                    continue;

                // Remove previous help button
                foreach (Gtk.Widget widget in container.Children) {
                    container.Remove(widget);
                    widget.Destroy();
                }

                ValueReference r = refs[i];
                if (r.Documentation != null) {
                    Gtk.Button helpButton = new Gtk.Button("?");
                    helpButton.CanFocus = false;
                    helpButton.Clicked += delegate(object sender, EventArgs e) {
                        DocumentationDialog d = new DocumentationDialog(r.Documentation);
                    };
                    container.Add(helpButton);
                }
            }
            this.ShowAll();
        }
    }
}
