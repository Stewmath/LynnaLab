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
        IList<IList<Gtk.Widget>> widgetLists;

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
            widgetLists = new List<IList<Gtk.Widget>>();

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

                // Each ValueReference may use up to 3 widgets in the table row
                Gtk.Widget[] widgetList = new Gtk.Widget[3];

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

                    widgetList[0] = new Gtk.Label(valueReferenceGroup[i].Name);
                    widgetList[1] = comboBox;

                    goto loopEnd;
                }
                // ConstantsMapping == null

                switch(valueReferenceGroup[i].ValueType) {
                case ValueReferenceType.String:
                    {
                        widgetList[0] = new Gtk.Label(valueReferenceGroup[i].Name);

                        Gtk.Entry entry = new Gtk.Entry();
                        if (!valueReferenceGroup[i].Editable)
                            entry.Sensitive = false;
                        dataModifiedExternalEvent += delegate() {
                            entry.Text = valueReferenceGroup[i].GetStringValue();
                            OnDataModifiedInternal();
                        };

                        widgetList[1] = entry;
                        break;
                    }
                case ValueReferenceType.Int:
                    {
                        widgetList[0] = new Gtk.Label(valueReferenceGroup[i].Name);

                        SpinButtonHexadecimal spinButton =
                            new SpinButtonHexadecimal(valueReferenceGroup[i].MinValue, valueReferenceGroup[i].MaxValue);
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
                        spinButton.Adjustment.Lower = valueReferenceGroup[i].MinValue;
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

                        widgetList[1] = spinButton;
                    }
                    break;

                case ValueReferenceType.Bool:
                    {
                        widgetList[0] = new Gtk.Label(valueReferenceGroup[i].Name);

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

                        widgetList[1] = checkButton;
                    }
                    break;
                }

loopEnd:
                table.Attach(widgetList[0], x+0,x+1, y, y+1);
                table.Attach(widgetList[1], x+1,x+2, y, y+1);

                widgetList[2] = new Gtk.HBox(); // Container for help button
                table.Attach(widgetList[2], x+2,x+3, y, y+1, 0, Gtk.AttachOptions.Fill, 0, 0);

                widgetLists.Add(widgetList);

                if (valueReferenceGroup[i].Tooltip != null) {
                    SetTooltip(i, valueReferenceGroup[i].Tooltip);
                }
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

            // Even if we're not rebuilding the widgets we can at least check for minor changes like
            // to the "editable" variable
            for (int i=0; i<ValueReferenceGroup.Count; i++) {
                Gtk.Widget w = widgetLists[i][1];
                w.Sensitive = ValueReferenceGroup[i].Editable;
                SetTooltip(i, ValueReferenceGroup[i].Tooltip);
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

        // Substitute the widget for a value (index "i") with the new widget. (Currently unused)
        public void ReplaceWidget(string name, Gtk.Widget newWidget) {
            int i = GetValueIndex(name);
            widgetLists[i][1].Dispose();
            var pos = widgetPositions[i];
            table.Attach(newWidget, pos.Item1+1, pos.Item1+2, pos.Item2, pos.Item2+1);
            widgetLists[i][1] = newWidget;
        }

        // Can replace the help button widget with something else. However if you put a container
        // there it could get overwritten with a help button again.
        public void AddWidgetToRight(string name, Gtk.Widget widget) {
            int i = GetValueIndex(name);
            widgetLists[i][2].Dispose(); // Removes help button
            widgetLists[i][2] = widget;
            var pos = widgetPositions[i];
            table.Attach(widget, pos.Item1+2, pos.Item1+3, pos.Item2, pos.Item2+1);
        }

        int GetValueIndex(string name) {
            for (int i = 0; i < valueReferenceGroup.Count; i++) {
                if (valueReferenceGroup[i].Name == name)
                    return i;
            }
            throw new ArgumentException("Couldn't find '" + name + "' in ValueReferenceGroup.");
        }

        public void SetTooltip(int i, string tooltip) {
            for (int j=0; j<widgetLists[i].Count; j++)
                widgetLists[i][j].TooltipText = tooltip;
        }
        public void SetTooltip(ValueReference r, string tooltip) {
            SetTooltip(valueReferenceGroup.GetIndexOf(r), tooltip);
        }
        public void SetTooltip(string name, string tooltip) {
            SetTooltip(valueReferenceGroup.GetValueReference(name), tooltip);
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
                Gtk.Container container = widgetLists[i][2] as Gtk.Container;
                if (container == null)
                    continue;

                bool isHelpButton = true;

                // Remove previous help button
                foreach (Gtk.Widget widget in container.Children) {
                    // Really hacky way to check whether this is the help button as we expect, or
                    // whether the "AddWidgetToRight" function was called to replace it, in which
                    // case we don't try to add the help button at all
                    if (!(widget is Gtk.Button && (widget as Gtk.Button).Label == "?")) {
                        isHelpButton = false;
                        continue;
                    }
                    container.Remove(widget);
                    widget.Dispose();
                }

                if (!isHelpButton)
                    continue;

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
