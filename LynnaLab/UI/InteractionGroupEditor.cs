﻿using System;
using System.Collections.Generic;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class InteractionGroupEditor : Gtk.Bin
    {
        public static String[] InteractionNames = {
            "Type 0 Interaction",
            "No Value Scriptable Interaction",
            "2-Value Scriptable Interaction",
            "Interaction Pointer",
            "Boss Interaction Pointer",
            "Conditional Interaction Pointer",
            "Random Position Enemy",
            "Specific Position Enemy",
            "Part",
            "4-Value Interaction",
            "Item Drop",
        };

        Project Project {
            get {
                if (interactionGroup == null) return null;
                return interactionGroup.Project;
            }
        }

        InteractionGroup interactionGroup;
        InteractionData activeData;

        Gtk.Frame pointerFrame;

        public InteractionGroupEditor()
        {
            this.Build();

            indexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                int i = indexSpinButton.ValueAsInt;
                if (interactionGroup == null || i == -1)
                    SetInteractionData(null);
                else
                    SetInteractionData(interactionGroup.GetInteractionData(i));
            };
        }

        public void SetInteractionGroup(InteractionGroup group) {
            this.interactionGroup = group;
            UpdateBoundaries();
            indexSpinButton.Value = 0;
            if (interactionGroup != null && interactionGroup.GetNumInteractions() != 0)
                SetInteractionData(interactionGroup.GetInteractionData(0));
            else {
                indexSpinButton.Value = -1;
                SetInteractionData(null);
            }
        }

        void SetInteractionDataIndex(int i) {
            if (interactionGroup == null || i < 0 || i >= interactionGroup.GetNumInteractions())
                SetInteractionData(null);
            else
                SetInteractionData(interactionGroup.GetInteractionData(i));
        }
        void SetInteractionData(InteractionData data) {
            activeData = data;

            foreach (Gtk.Widget widget in interactionDataContainer.Children) {
                interactionDataContainer.Remove(widget);
            }

            if (data == null) {
                frameLabel.Text = "";
                return;
            }
            frameLabel.Text = InteractionNames[(int)activeData.GetInteractionType()];

            List<String> labelList = new List<String>();
            List<Gtk.Widget> widgetList = new List<Gtk.Widget>();

            switch(data.GetInteractionType()) {
                case InteractionType.Type0:
                    {
                        labelList.Add("Condition");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,255);
                        button.Value = Project.EvalToInt(activeData.GetValue(0));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetValue(0, Wla.ToByte((byte)button.ValueAsInt));
                        };
                        button.Digits = 2;
                        widgetList.Add(button);
                    }
                    break;
                case InteractionType.NoValue:
                    {
                        labelList.Add("ID");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,0xffff);
                        button.Value = Project.EvalToInt(activeData.GetValue(0));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetValue(0, Wla.ToWord(button.ValueAsInt));
                        };
                        button.Digits = 4;
                        widgetList.Add(button);
                    }
                    break;
                case InteractionType.DoubleValue:
                    {
                        labelList.Add("ID");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,0xffff);
                        button.Value = Project.EvalToInt(activeData.GetValue(0));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(0, Wla.ToWord(b.ValueAsInt));
                        };
                        button.Digits = 4;
                        widgetList.Add(button);


                        labelList.Add("Y");
                        button = new SpinButtonHexadecimal();
                        button.Value = Project.EvalToInt(activeData.GetValue(1));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(1, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        button.Digits = 2;
                        widgetList.Add(button);


                        labelList.Add("X");
                        button = new SpinButtonHexadecimal();
                        button.Value = Project.EvalToInt(activeData.GetValue(2));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(2, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        button.Digits = 2;
                        widgetList.Add(button);
                    }
                    break;
                case InteractionType.Pointer:
                case InteractionType.BossPointer:
                case InteractionType.Conditional:
                    {
                        labelList.Add("Pointer");
                        Gtk.Entry entry = new Gtk.Entry();
                        entry.Text = activeData.GetValue(0);
                        entry.Changed += delegate(object sender, EventArgs e) {
                            UpdatePointerTextBox(sender as Gtk.Entry);
                        };
                        widgetList.Add(entry);

                        labelList.Add("");
                        pointerFrame = new Gtk.Frame();
                        pointerFrame.Label = "Pointer data (possibly shared)";
                        pointerFrame.BorderWidth = 5;
                        widgetList.Add(pointerFrame);

                        UpdatePointerTextBox(entry);
                    }
                    break;
                case InteractionType.RandomEnemy:
                    {
                        labelList.Add("Flags");
                        SpinButtonHexadecimal flagsButton = new SpinButtonHexadecimal();
                        flagsButton.Value = Project.EvalToInt(activeData.GetValue(0));
                        flagsButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetValue(0, Wla.ToByte((byte)flagsButton.ValueAsInt));
                        };
                        flagsButton.Digits = 2;
                        widgetList.Add(flagsButton);


                        labelList.Add("ID");
                        SpinButtonHexadecimal idButton = new SpinButtonHexadecimal(0,0xffff);
                        idButton.Value = Project.EvalToInt(activeData.GetValue(1));
                        idButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetValue(1, Wla.ToWord(idButton.ValueAsInt));
                        };
                        idButton.Digits = 4;
                        widgetList.Add(idButton);
                    }
                    break;
                case InteractionType.SpecificEnemy:
                    {
                        labelList.Add("Flags");
                        SpinButtonHexadecimal flagsButton = new SpinButtonHexadecimal();
                        flagsButton.Value = Project.EvalToInt(activeData.GetValue(0));
                        flagsButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetValue(0, Wla.ToByte((byte)flagsButton.ValueAsInt));
                        };
                        flagsButton.Digits = 2;
                        widgetList.Add(flagsButton);


                        labelList.Add("ID");
                        SpinButtonHexadecimal idButton = new SpinButtonHexadecimal(0,0xffff);
                        idButton.Value = Project.EvalToInt(activeData.GetValue(1));
                        idButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetValue(1, Wla.ToWord(idButton.ValueAsInt));
                        };
                        idButton.Digits = 4;
                        widgetList.Add(idButton);


                        labelList.Add("Y");
                        SpinButtonHexadecimal yButton = new SpinButtonHexadecimal();
                        yButton.Value = Project.EvalToInt(activeData.GetValue(2));
                        yButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetValue(2, Wla.ToByte((byte)yButton.ValueAsInt));
                        };
                        yButton.Digits = 2;
                        widgetList.Add(yButton);


                        labelList.Add("X");
                        SpinButtonHexadecimal xButton = new SpinButtonHexadecimal();
                        xButton.Value = Project.EvalToInt(activeData.GetValue(3));
                        xButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetValue(3, Wla.ToByte((byte)xButton.ValueAsInt));
                        };
                        xButton.Digits = 2;
                        widgetList.Add(xButton);
                    }
                    break;
                case InteractionType.Part:
                    {
                        labelList.Add("ID");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,0xffff);
                        button.Value = Project.EvalToInt(activeData.GetValue(0));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(0, Wla.ToWord(b.ValueAsInt));
                        };
                        button.Digits = 4;
                        widgetList.Add(button);


                        labelList.Add("YX");
                        button = new SpinButtonHexadecimal();
                        button.Value = Project.EvalToInt(activeData.GetValue(1));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(1, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        button.Digits = 2;
                        widgetList.Add(button);
                    }
                    break;
                case InteractionType.QuadrupleValue:
                    {
                        labelList.Add("ID");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,0xffff);
                        button.Value = Project.EvalToInt(activeData.GetValue(0));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(0, Wla.ToWord(b.ValueAsInt));
                        };
                        button.Digits = 4;
                        widgetList.Add(button);


                        labelList.Add("Unknown 1");
                        SpinButtonHexadecimal u1Button = new SpinButtonHexadecimal();
                        u1Button.Value = Project.EvalToInt(activeData.GetValue(1));
                        u1Button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(1, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        u1Button.Digits = 2;
                        widgetList.Add(u1Button);


                        labelList.Add("Unknown 2");
                        SpinButtonHexadecimal u2Button = new SpinButtonHexadecimal();
                        u2Button.Value = Project.EvalToInt(activeData.GetValue(2));
                        u2Button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(2, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        u2Button.Digits = 2;
                        widgetList.Add(u2Button);


                        labelList.Add("Y");
                        SpinButtonHexadecimal yButton = new SpinButtonHexadecimal();
                        yButton.Value = Project.EvalToInt(activeData.GetValue(3));
                        yButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(3, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        yButton.Digits = 2;
                        widgetList.Add(yButton);


                        labelList.Add("X");
                        SpinButtonHexadecimal xButton = new SpinButtonHexadecimal();
                        xButton.Value = Project.EvalToInt(activeData.GetValue(4));
                        xButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(4, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        xButton.Digits = 2;
                        widgetList.Add(xButton);
                    }
                    break;
                case InteractionType.ItemDrop:
                    {
                        labelList.Add("Flags");
                        SpinButtonHexadecimal uButton = new SpinButtonHexadecimal();
                        uButton.Value = Project.EvalToInt(activeData.GetValue(0));
                        uButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(0, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        uButton.Digits = 2;
                        widgetList.Add(uButton);


                        labelList.Add("Item");
                        SpinButtonHexadecimal u2Button = new SpinButtonHexadecimal();
                        u2Button.Value = Project.EvalToInt(activeData.GetValue(1));
                        u2Button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(1, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        u2Button.Digits = 2;
                        widgetList.Add(u2Button);


                        labelList.Add("YX");
                        SpinButtonHexadecimal yxButton = new SpinButtonHexadecimal();
                        yxButton.Value = Project.EvalToInt(activeData.GetValue(2));
                        yxButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(2, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        yxButton.Digits = 2;
                        widgetList.Add(yxButton);
                    }
                    break;
            }

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

            interactionDataContainer.Add(table);
            interactionDataContainer.ShowAll();
        }

        void UpdateBoundaries() {
            indexSpinButton.Adjustment.Lower = -1;
            int max=0;
            if (interactionGroup == null)
                max = -1;
            else
                max = interactionGroup.GetNumInteractions()-1;

            indexSpinButton.Adjustment.Upper = max;
            if (indexSpinButton.ValueAsInt > max) {
                indexSpinButton.Value = max;
            }

            SetInteractionDataIndex(indexSpinButton.ValueAsInt);
        }

        void UpdatePointerTextBox(Gtk.Entry entry) {
            pointerFrame.Remove(pointerFrame.Child);

            InteractionGroupEditor subEditor = new InteractionGroupEditor();
            Gtk.Alignment alignment = new Gtk.Alignment(0.5F, 0.5F, 0.0F, 0.8F);
            try {
                Project.GetFileWithLabel(entry.Text.Trim());
                subEditor.SetInteractionGroup(Project.GetDataType<InteractionGroup>(activeData.GetValue(0)));
                subEditor.ShowAll();
                alignment.Add(subEditor);
                activeData.SetValue(0, entry.Text.Trim());
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

        protected void OnDeleteButtonClicked(object sender, EventArgs e)
        {
            if (interactionGroup != null && indexSpinButton.ValueAsInt != -1) {
                interactionGroup.RemoveInteraction(indexSpinButton.ValueAsInt);
                UpdateBoundaries();
            }
        }

        protected void OnAddButtonClicked(object sender, EventArgs e)
        {
            if (interactionGroup == null) return;

            AddInteractionDialog d = new AddInteractionDialog();
            d.Run();
            if (d.InteractionTypeToAdd != InteractionType.End) {
                if (interactionGroup == null) return;

                interactionGroup.InsertInteraction(indexSpinButton.ValueAsInt+1, d.InteractionTypeToAdd);
                UpdateBoundaries();
                indexSpinButton.Value = indexSpinButton.ValueAsInt+1;
            }
        }
    }
}