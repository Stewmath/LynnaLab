using System;
using System.Collections.Generic;

using LynnaLib;

namespace LynnaLab
{
    public class ValueReferenceEditor : Gtk.Bin
    {
        ValueReferenceGroup valueReferenceGroup;

        IList<int> maxBounds;
        // List of "grid" objects corresponding to each widget
        IList<Gtk.Grid> widgetGrids;
        // X/Y positions where the widgets are in the grid
        IList<Tuple<int, int>> widgetPositions;
        // The widgets by index.
        IList<IList<Gtk.Widget>> widgetLists;

        int rows;
        string frameText;

        event System.Action dataModifiedExternalEvent;
        event System.Action dataModifiedInternalEvent;


        Project Project { get { return valueReferenceGroup.Project; } }

        public ValueReferenceGroup ValueReferenceGroup
        {
            get { return valueReferenceGroup; }
        }

        public ValueReferenceEditor()
            : this(null, null)
        {
        }

        public ValueReferenceEditor(ValueReferenceGroup vrg, string frameText = null)
            : this(vrg, 50, frameText)
        {
        }

        public ValueReferenceEditor(ValueReferenceGroup vrg, int rows, string frameText = null)
        {
            this.rows = rows;
            this.frameText = frameText;

            if (vrg != null)
                Initialize(vrg);
        }

        void Initialize(ValueReferenceGroup vrg)
        {
            this.valueReferenceGroup = vrg;

            this.Halign = Gtk.Align.Fill;
            this.Valign = Gtk.Align.Fill;

            maxBounds = new int[valueReferenceGroup.GetNumValueReferences()];
            widgetGrids = new List<Gtk.Grid>();
            widgetPositions = new Tuple<int, int>[maxBounds.Count];
            widgetLists = new List<IList<Gtk.Widget>>();

            Gtk.Box hbox = new Gtk.Box(Gtk.Orientation.Horizontal, 0);
            hbox.Spacing = 6;

            Func<Gtk.Grid> newGrid = () =>
            {
                Gtk.Grid g = new Gtk.Grid();
                g.ColumnSpacing = 6;
                g.RowSpacing = 2;
                hbox.Add(g);
                return g;
            };

            Gtk.Grid grid = newGrid();
            int x = 0, y = 0;


            // Do not use "foreach" here. The "valueReferenceGroup" may be changed. So, whenever we
            // access a ValueReference from within an event handler, we must do so though the
            // "valueReferenceGroup" class variable, and NOT though an alias (like with foreach).
            for (int tmpCounter = 0; tmpCounter < valueReferenceGroup.Count; tmpCounter++)
            {
                int i = tmpCounter; // Variable must be distinct within each closure

                if (y >= rows)
                {
                    y = 0;
                    x = 0;
                    grid = newGrid();
                }

                // Each ValueReference may use up to 3 widgets in the grid row
                Gtk.Widget[] widgetList = new Gtk.Widget[3];

                widgetPositions[i] = new Tuple<int, int>(x, y);

                Action<Gtk.SpinButton> setSpinButtonLimits = (spinButton) =>
                {
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
                };

                int entryWidgetWidth = 1;

                // If it has a ConstantsMapping, use a combobox instead of anything else
                if (valueReferenceGroup[i].ConstantsMapping != null)
                {
                    ComboBoxFromConstants comboBox = new ComboBoxFromConstants(showHelp: true, vertical: true);
                    comboBox.SetConstantsMapping(valueReferenceGroup[i].ConstantsMapping);

                    // Must put this before the "Changed" handler below to avoid
                    // it being fired (for some reason?)
                    setSpinButtonLimits(comboBox.SpinButton);

                    comboBox.Changed += delegate (object sender, EventArgs e)
                    {
                        valueReferenceGroup[i].SetValue(comboBox.ActiveValue);
                        OnDataModifiedInternal();
                    };

                    dataModifiedExternalEvent += delegate ()
                    {
                        comboBox.ActiveValue = valueReferenceGroup[i].GetIntValue();
                    };

                    /*
                    comboBox.MarginTop = 4;
                    comboBox.MarginBottom = 4;
                    */

                    widgetList[0] = new Gtk.Label(valueReferenceGroup[i].Name);
                    widgetList[1] = comboBox;

                    entryWidgetWidth = 2;

                    goto loopEnd;
                }
                // ConstantsMapping == null

                switch (valueReferenceGroup[i].ValueType)
                {
                    case ValueReferenceType.String:
                        {
                            widgetList[0] = new Gtk.Label(valueReferenceGroup[i].Name);

                            Gtk.Entry entry = new Gtk.Entry();
                            if (!valueReferenceGroup[i].Editable)
                                entry.Sensitive = false;
                            dataModifiedExternalEvent += delegate ()
                            {
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
                            setSpinButtonLimits(spinButton);
                            spinButton.ValueChanged += delegate (object sender, EventArgs e)
                            {
                                Gtk.SpinButton button = sender as Gtk.SpinButton;
                                if (maxBounds[i] == 0 || button.ValueAsInt <= maxBounds[i])
                                {
                                    valueReferenceGroup[i].SetValue(button.ValueAsInt);
                                }
                                else
                                    button.Value = maxBounds[i];
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate ()
                            {
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
                            checkButton.Toggled += delegate (object sender, EventArgs e)
                            {
                                Gtk.CheckButton button = sender as Gtk.CheckButton;
                                valueReferenceGroup[i].SetValue(button.Active ? 1 : 0);
                                OnDataModifiedInternal();
                            };
                            dataModifiedExternalEvent += delegate ()
                            {
                                checkButton.Active = valueReferenceGroup[i].GetIntValue() == 1;
                            };

                            widgetList[1] = checkButton;
                        }
                        break;
                }

            loopEnd:
                grid.Attach(widgetList[0], x, y, 1, 1);
                grid.Attach(widgetList[1], x + 1, y, entryWidgetWidth, 1);

                widgetList[2] = new Gtk.Grid(); // Container for help button
                widgetList[2].Halign = Gtk.Align.Start;
                widgetList[2].Hexpand = true; // Let this absorb any extra space
                grid.Attach(widgetList[2], x + 2, y, 1, 1);

                widgetGrids.Add(grid);
                widgetLists.Add(widgetList);

                if (valueReferenceGroup[i].Tooltip != null)
                {
                    SetTooltip(i, valueReferenceGroup[i].Tooltip);
                }
                y++;
            }

            if (frameText != null)
            {
                var frame = new Gtk.Frame(frameText);
                frame.Add(hbox);
                this.Add(frame);
            }
            else
                this.Add(hbox);

            this.ShowAll();

            // Initial values
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent();

            UpdateHelpButtons();

            AddModifiedHandlers();
            this.Destroyed += (sender, args) => RemoveModifiedHandlers();
        }

        void AddModifiedHandlers()
        {
            foreach (ValueReference vref in valueReferenceGroup.GetValueReferences())
            {
                vref.AddValueModifiedHandler(OnDataModifiedExternal);
            }
        }

        void RemoveModifiedHandlers()
        {
            foreach (ValueReference vref in valueReferenceGroup.GetValueReferences())
            {
                vref.RemoveValueModifiedHandler(OnDataModifiedExternal);
            }
        }

        // ONLY call this function if the new ValueReferenceGroup matches the exact structure of the
        // old one. If there is any mismatch, a new ValueReferenceEditor should be created instead.
        // But when the structures do match exactly, this is preferred, so that we don't need to
        // recreate all of the necessary widgets again.
        //
        // The first time this is called, it will construct the widget lists; after that it only
        // modifies the existing widgets.
        public void ReplaceValueReferenceGroup(ValueReferenceGroup vrg)
        {
            if (valueReferenceGroup == null)
            {
                Initialize(vrg);
                return;
            }

            RemoveModifiedHandlers();
            this.valueReferenceGroup = vrg;
            AddModifiedHandlers();

            // Even if we're not rebuilding the widgets we can at least check for minor changes like
            // to the "editable" variable
            for (int i = 0; i < ValueReferenceGroup.Count; i++)
            {
                Gtk.Widget w = widgetLists[i][1];
                var vr = ValueReferenceGroup[i];

                w.Sensitive = vr.Editable;
                SetTooltip(i, vr.Tooltip);

                if (w is ComboBoxFromConstants)
                {
                    var cb = w as ComboBoxFromConstants;
                    cb.SetConstantsMapping(vr.ConstantsMapping);
                }
            }

            // Initial values
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent();

            UpdateHelpButtons();
        }

        public void SetMaxBound(int i, int max)
        {
            if (i == -1)
                return;
            maxBounds[i] = max;
        }

        // Substitute the widget for a value (index "i") with the new widget. (Currently unused)
        public void ReplaceWidget(string name, Gtk.Widget newWidget)
        {
            int i = GetValueIndex(name);
            widgetGrids[i].Remove(widgetLists[i][1]);
            widgetLists[i][1].Dispose();
            var pos = widgetPositions[i];
            widgetGrids[i].Attach(newWidget, pos.Item1 + 1, pos.Item2, 1, 1);
            widgetLists[i][1] = newWidget;
        }

        // Can replace the help button widget with something else. However if you put a container
        // there it could get overwritten with a help button again.
        public void AddWidgetToRight(string name, Gtk.Widget widget)
        {
            int i = GetValueIndex(name);
            widgetGrids[i].Remove(widgetLists[i][2]);
            widgetLists[i][2].Dispose(); // Removes help button
            widgetLists[i][2] = widget;
            var pos = widgetPositions[i];
            widgetGrids[i].Attach(widget, pos.Item1 + 2, pos.Item2, 1, 1);
        }

        int GetValueIndex(string name)
        {
            for (int i = 0; i < valueReferenceGroup.Count; i++)
            {
                if (valueReferenceGroup[i].Name == name)
                    return i;
            }
            throw new ArgumentException("Couldn't find '" + name + "' in ValueReferenceGroup.");
        }

        public void SetTooltip(int i, string tooltip)
        {
            for (int j = 0; j < widgetLists[i].Count; j++)
                widgetLists[i][j].TooltipText = tooltip;
        }
        public void SetTooltip(ValueReference r, string tooltip)
        {
            SetTooltip(valueReferenceGroup.GetIndexOf(r), tooltip);
        }
        public void SetTooltip(string name, string tooltip)
        {
            SetTooltip(valueReferenceGroup.GetValueReference(name), tooltip);
        }

        public void AddDataModifiedHandler(System.Action handler)
        {
            dataModifiedInternalEvent += handler;
        }
        public void RemoveDataModifiedHandler(System.Action handler)
        {
            dataModifiedInternalEvent -= handler;
        }

        // Data modified externally
        void OnDataModifiedExternal(object sender, ValueModifiedEventArgs e)
        {
            if (dataModifiedExternalEvent != null)
                dataModifiedExternalEvent();
        }

        // Data modified internally
        void OnDataModifiedInternal()
        {
            if (dataModifiedInternalEvent != null)
                dataModifiedInternalEvent();
        }

        // Check if there are entries that should have help buttons
        public void UpdateHelpButtons()
        {
            IList<ValueReference> refs = valueReferenceGroup.GetValueReferences();

            for (int i = 0; i < refs.Count; i++)
            {
                if (widgetLists[i][1] is ComboBoxFromConstants) // These deal with documentation themselves
                    continue;

                Gtk.Container container = widgetLists[i][2] as Gtk.Container;
                if (container == null)
                    continue;

                bool isHelpButton = true;

                // Remove previous help button
                foreach (Gtk.Widget widget in container.Children)
                {
                    // Really hacky way to check whether this is the help button as we expect, or
                    // whether the "AddWidgetToRight" function was called to replace it, in which
                    // case we don't try to add the help button at all
                    if (!(widget is Gtk.Button && (widget as Gtk.Button).Label == "?"))
                    {
                        isHelpButton = false;
                        continue;
                    }
                    container.Remove(widget);
                    widget.Dispose();
                }

                if (!isHelpButton)
                    continue;

                ValueReference r = refs[i];
                if (r.Documentation != null)
                {
                    Gtk.Button helpButton = new Gtk.Button("?");
                    helpButton.FocusOnClick = false;
                    helpButton.Clicked += delegate (object sender, EventArgs e)
                    {
                        DocumentationDialog d = new DocumentationDialog(r.Documentation);
                    };
                    container.Add(helpButton);
                }
            }
            this.ShowAll();
        }
    }
}
