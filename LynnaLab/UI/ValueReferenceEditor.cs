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

        event System.Action dataModifiedExternalEvent;
        event System.Action dataModifiedInternalEvent;


        Project Project {get; set;}

        public ValueReferenceGroup ValueReferenceGroup {
            get { return valueReferenceGroup; }
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

            // Do not use "foreach" here. The "valueReferenceGroup" may be changed. So, whenever we
            // access a ValueReference from within an event handler, we must do so though the
            // "valueReferenceGroup" class variable, and NOT though an alias (like with foreach).
            for (int tmpCounter=0; tmpCounter < valueReferenceGroup.Count; tmpCounter++) {
                int i = tmpCounter; // Variable must be distinct within each closure

                if (y >= rows) {
                    y = 0;
                    x += 3;
                }

                widgetPositions[i] = new Tuple<uint,uint>(x,y);

                // If it has a ConstantsMapping, use a combobox instead of anything else
                if (valueReferenceGroup[i].ConstantsMapping != null) {
                    ComboBoxFromConstants comboBox = new ComboBoxFromConstants(false);
                    comboBox.SetConstantsMapping(valueReferenceGroup[i].ConstantsMapping);

                    comboBox.Changed += delegate(object sender, EventArgs e) {
                        valueReferenceGroup[i].SetValue(comboBox.ActiveValue);
                        OnDataModifiedInternal();
                    };

                    dataModifiedExternalEvent += delegate() {
                        comboBox.ActiveValue = valueReferenceGroup[i].GetIntValue ();
                    };

                    table.Attach(new Gtk.Label(valueReferenceGroup[i].Name), x+0,x+1,y,y+1);
                    table.Attach(comboBox, x+1,x+2,y,y+1);
                    widgets[i] = comboBox;

                    helpButtonContainers[i] = new Gtk.HBox();
                    table.Attach(helpButtonContainers[i], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);

                    goto loopEnd;
                }
                // ConstantsMapping == null

                switch(valueReferenceGroup[i].ValueType) {
                case ValueReferenceType.String:
                    {
                        table.Attach(new Gtk.Label(valueReferenceGroup[i].Name), x+0,x+1, y, y+1);
                        Gtk.Entry entry = new Gtk.Entry();
                        if (!valueReferenceGroup[i].Editable)
                            entry.Sensitive = false;
                        dataModifiedExternalEvent += delegate() {
                            entry.Text = valueReferenceGroup[i].GetStringValue();
                            OnDataModifiedInternal();
                        };
                        table.Attach(entry, x+1,x+2, y, y+1);
                        widgets[i] = entry;

                        helpButtonContainers[i] = new Gtk.HBox();
                        table.Attach(helpButtonContainers[i], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
                        break;
                    }
                case ValueReferenceType.Int:
intCase:
                    {
                        table.Attach(new Gtk.Label(valueReferenceGroup[i].Name), x+0,x+1, y, y+1);
                        SpinButtonHexadecimal spinButton = new SpinButtonHexadecimal(0,valueReferenceGroup[i].MaxValue);
                        if (!valueReferenceGroup[i].Editable)
                            spinButton.Sensitive = false;
                        if (valueReferenceGroup[i].MaxValue < 0x10)
                            spinButton.Digits = 1;
                        else if (valueReferenceGroup[i].MaxValue < 0x100)
                            spinButton.Digits = 2;
                        else if (valueReferenceGroup[i].MaxValue < 0x1000)
                            spinButton.Digits = 3;
                        else
                            spinButton.Digits = 4;
                        spinButton.Adjustment.Upper = valueReferenceGroup[i].MaxValue;
                        spinButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton button = sender as Gtk.SpinButton;
                            if (maxBounds[i] == 0 || button.ValueAsInt <= maxBounds[i]) {
                                valueReferenceGroup[i].SetValue(button.ValueAsInt);
                            }
                            else
                                button.Value = maxBounds[i];
                            OnDataModifiedInternal();
                        };
                        dataModifiedExternalEvent += delegate() {
                            spinButton.Value = valueReferenceGroup[i].GetIntValue();
                        };
                        table.Attach(spinButton, x+1,x+2, y, y+1);
                        widgets[i] = spinButton;

                        helpButtonContainers[i] = new Gtk.HBox();
                        table.Attach(helpButtonContainers[i], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
                    }
                    break;

                case ValueReferenceType.Bool:
                    {
                        table.Attach(new Gtk.Label(valueReferenceGroup[i].Name), x+0,x+1, y, y+1);
                        Gtk.CheckButton checkButton = new Gtk.CheckButton();
                        checkButton.FocusOnClick = false;
                        if (!valueReferenceGroup[i].Editable)
                            checkButton.Sensitive = false;
                        checkButton.Toggled += delegate(object sender, EventArgs e) {
                            Gtk.CheckButton button = sender as Gtk.CheckButton;
                            valueReferenceGroup[i].SetValue(button.Active ? 1 : 0);
                            OnDataModifiedInternal();
                        };
                        dataModifiedExternalEvent += delegate() {
                            checkButton.Active = valueReferenceGroup[i].GetIntValue() == 1;
                        };
                        table.Attach(checkButton, x+1,x+2, y, y+1);
                        widgets[i] = checkButton;

                        helpButtonContainers[i] = new Gtk.HBox();
                        table.Attach(helpButtonContainers[i], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);
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

            // Initial values
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent();

            UpdateHelpButtons();

            AddModifiedHandlers();
            this.Destroyed += (sender, args) => RemoveModifiedHandlers();
        }

        void AddModifiedHandlers() {
            foreach (ValueReference vref in valueReferenceGroup.GetValueReferences()) {
                vref.AddValueModifiedHandler(OnDataModifiedExternal);
            }
        }

        void RemoveModifiedHandlers() {
            foreach (ValueReference vref in valueReferenceGroup.GetValueReferences()) {
                vref.RemoveValueModifiedHandler(OnDataModifiedExternal);
            }
        }

        // ONLY call this function if the new ValueReferenceGroup matches the exact structure of the
        // old one. If there is any mismatch, a new ValueReferenceEditor should be created instead.
        // But when the structures do match exactly, this is preferred, so that we don't need to
        // recreate all of the necessary widgets again.
        public void ReplaceValueReferenceGroup(ValueReferenceGroup vrg) {
            RemoveModifiedHandlers();
            this.valueReferenceGroup = vrg;
            AddModifiedHandlers();

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

        // Add a new widget to the right side of a value
        public void AddWidgetToSide(string name, Gtk.Widget newWidget, uint width=1, uint height=1) {
            int i = GetValueIndex(name);
            var pos = widgetPositions[i];
            table.Attach(newWidget, pos.Item1+2, pos.Item1+2+width, pos.Item2, pos.Item2+height);
            widgets[i] = newWidget;
        }

        // Substitute the widget for a value (index "i") with the new widget.
        public void ReplaceWidget(string name, Gtk.Widget newWidget) {
            int i = GetValueIndex(name);
            widgets[i].Dispose();
            var pos = widgetPositions[i];
            table.Attach(newWidget, pos.Item1+1, pos.Item1+2, pos.Item2, pos.Item2+1);
            widgets[i] = newWidget;
        }

        int GetValueIndex(string name) {
            for (int i = 0; i < valueReferenceGroup.Count; i++) {
                if (valueReferenceGroup[i].Name == name)
                    return i;
            }
            throw new ArgumentException("Couldn't find '" + name + "' in ValueReferenceGroup.");
        }

        public void SetTooltip(int i, string tooltip) {
            widgets[i].TooltipText = tooltip;
        }
        public void SetTooltip(ValueReference r, string tooltip) {
            SetTooltip(valueReferenceGroup.GetIndexOf(r), tooltip);
        }

        public void AddDataModifiedHandler(System.Action handler) {
            dataModifiedInternalEvent += handler;
        }
        public void RemoveDataModifiedHandler(System.Action handler) {
            dataModifiedInternalEvent -= handler;
        }

        // Data modified externally
        void OnDataModifiedExternal(object sender, ValueModifiedEventArgs e) {
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent();
        }

        // Data modified internally
        void OnDataModifiedInternal() {
            if (dataModifiedInternalEvent != null)
                dataModifiedInternalEvent();
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
                    widget.Dispose();
                }

                ValueReference r = refs[i];
                if (r.Documentation != null) {
                    Gtk.Button helpButton = new Gtk.Button("?");
                    helpButton.FocusOnClick = false;
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
