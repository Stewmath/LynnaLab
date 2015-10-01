using System;
using System.Collections.Generic;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class InteractionGroupEditor : Gtk.Bin
    {
        String[] InteractionNames = {
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
            indexSpinButton.Adjustment.Lower = -1;
            indexSpinButton.Adjustment.Upper = group.GetNumInteractions()-1;
            indexSpinButton.Value = 0;
            if (interactionGroup != null && interactionGroup.GetNumInteractions() != 0)
                SetInteractionData(interactionGroup.GetInteractionData(0));
            else {
                indexSpinButton.Value = -1;
                SetInteractionData(null);
            }
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
                        button.Value = Project.EvalToInt(activeData.Values[0]);
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetByteValue(0, (byte)button.ValueAsInt);
                        };
                        button.Digits = 2;
                        widgetList.Add(button);
                    }
                    break;
                case InteractionType.NoValue:
                    {
                        labelList.Add("ID");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,0xffff);
                        button.Value = Project.EvalToInt(activeData.Values[0]);
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetWordValue(0, button.ValueAsInt);
                        };
                        button.Digits = 4;
                        widgetList.Add(button);
                    }
                    break;
                case InteractionType.DoubleValue:
                    {
                        labelList.Add("ID");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,0xffff);
                        button.Value = Project.EvalToInt(activeData.GetInteractionValue(0));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(0, Wla.ToWord(b.ValueAsInt));
                        };
                        button.Digits = 4;
                        widgetList.Add(button);


                        labelList.Add("Y");
                        button = new SpinButtonHexadecimal();
                        button.Value = Project.EvalToInt(activeData.GetInteractionValue(1));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(1, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        button.Digits = 2;
                        widgetList.Add(button);


                        labelList.Add("X");
                        button = new SpinButtonHexadecimal();
                        button.Value = Project.EvalToInt(activeData.GetInteractionValue(2));
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
                        entry.Text = activeData.Values[0];
                        entry.Changed += delegate(object sender, EventArgs e) {
                            activeData.SetValue(0, entry.Text);
                        };
                        widgetList.Add(entry);

                        labelList.Add("");
                        InteractionGroupEditor subEditor = new InteractionGroupEditor();
                        subEditor.SetInteractionGroup(Project.GetDataType<InteractionGroup>(activeData.Values[0]));
                        subEditor.ShowAll();

                        Gtk.Alignment alignment = new Gtk.Alignment(0.5F, 0.5F, 0.0F, 0.8F);
                        alignment.Add(subEditor);

                        Gtk.Frame frame = new Gtk.Frame();
                        frame.Label = "Pointer data (possibly shared)";
                        frame.BorderWidth = 5;
                        frame.Add(alignment);
                        widgetList.Add(frame);
                    }
                    break;
                case InteractionType.RandomEnemy:
                    {
                        labelList.Add("Flags");
                        SpinButtonHexadecimal flagsButton = new SpinButtonHexadecimal();
                        flagsButton.Value = Project.EvalToInt(activeData.Values[0]);
                        flagsButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetByteValue(0, (byte)flagsButton.ValueAsInt);
                        };
                        flagsButton.Digits = 2;
                        widgetList.Add(flagsButton);


                        labelList.Add("ID");
                        SpinButtonHexadecimal idButton = new SpinButtonHexadecimal(0,0xffff);
                        idButton.Value = Project.EvalToInt(activeData.Values[1]);
                        idButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetWordValue(1, idButton.ValueAsInt);
                        };
                        idButton.Digits = 4;
                        widgetList.Add(idButton);
                    }
                    break;
                case InteractionType.SpecificEnemy:
                    {
                        labelList.Add("Flags");
                        SpinButtonHexadecimal flagsButton = new SpinButtonHexadecimal();
                        flagsButton.Value = Project.EvalToInt(activeData.GetInteractionValue(0));
                        flagsButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetInteractionValue(0, Wla.ToByte((byte)flagsButton.ValueAsInt));
                        };
                        flagsButton.Digits = 2;
                        widgetList.Add(flagsButton);


                        labelList.Add("ID");
                        SpinButtonHexadecimal idButton = new SpinButtonHexadecimal(0,0xffff);
                        idButton.Value = Project.EvalToInt(activeData.GetInteractionValue(1));
                        idButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetInteractionValue(1, Wla.ToWord(idButton.ValueAsInt));
                        };
                        idButton.Digits = 4;
                        widgetList.Add(idButton);


                        labelList.Add("Y");
                        SpinButtonHexadecimal yButton = new SpinButtonHexadecimal();
                        yButton.Value = Project.EvalToInt(activeData.GetInteractionValue(2));
                        yButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetInteractionValue(2, Wla.ToByte((byte)yButton.ValueAsInt));
                        };
                        yButton.Digits = 2;
                        widgetList.Add(yButton);


                        labelList.Add("X");
                        SpinButtonHexadecimal xButton = new SpinButtonHexadecimal();
                        xButton.Value = Project.EvalToInt(activeData.GetInteractionValue(3));
                        xButton.ValueChanged += delegate(object sender, EventArgs e) {
                            activeData.SetInteractionValue(3, Wla.ToByte((byte)xButton.ValueAsInt));
                        };
                        xButton.Digits = 2;
                        widgetList.Add(xButton);
                    }
                    break;
                case InteractionType.Part:
                    {
                        labelList.Add("ID");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,0xffff);
                        button.Value = Project.EvalToInt(activeData.GetInteractionValue(0));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(0, Wla.ToWord(b.ValueAsInt));
                        };
                        button.Digits = 4;
                        widgetList.Add(button);


                        labelList.Add("YX");
                        button = new SpinButtonHexadecimal();
                        button.Value = Project.EvalToInt(activeData.GetInteractionValue(1));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(1, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        button.Digits = 2;
                        widgetList.Add(button);
                    }
                    break;
                case InteractionType.QuadrupleValue:
                    {
                        labelList.Add("ID");
                        SpinButtonHexadecimal button = new SpinButtonHexadecimal(0,0xffff);
                        button.Value = Project.EvalToInt(activeData.GetInteractionValue(0));
                        button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(0, Wla.ToWord(b.ValueAsInt));
                        };
                        button.Digits = 4;
                        widgetList.Add(button);


                        labelList.Add("Unknown 1");
                        SpinButtonHexadecimal u1Button = new SpinButtonHexadecimal();
                        u1Button.Value = Project.EvalToInt(activeData.GetInteractionValue(1));
                        u1Button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(1, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        u1Button.Digits = 2;
                        widgetList.Add(u1Button);


                        labelList.Add("Unknown 2");
                        SpinButtonHexadecimal u2Button = new SpinButtonHexadecimal();
                        u2Button.Value = Project.EvalToInt(activeData.GetInteractionValue(2));
                        u2Button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(2, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        u2Button.Digits = 2;
                        widgetList.Add(u2Button);


                        labelList.Add("Y");
                        SpinButtonHexadecimal yButton = new SpinButtonHexadecimal();
                        yButton.Value = Project.EvalToInt(activeData.GetInteractionValue(3));
                        yButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(3, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        yButton.Digits = 2;
                        widgetList.Add(yButton);


                        labelList.Add("X");
                        SpinButtonHexadecimal xButton = new SpinButtonHexadecimal();
                        xButton.Value = Project.EvalToInt(activeData.GetInteractionValue(4));
                        xButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetValue(4, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        xButton.Digits = 2;
                        widgetList.Add(xButton);
                    }
                    break;
                case InteractionType.TypeA:
                    {
                        labelList.Add("Flags");
                        SpinButtonHexadecimal uButton = new SpinButtonHexadecimal();
                        uButton.Value = Project.EvalToInt(activeData.GetInteractionValue(0));
                        uButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(0, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        uButton.Digits = 2;
                        widgetList.Add(uButton);


                        labelList.Add("Item");
                        SpinButtonHexadecimal u2Button = new SpinButtonHexadecimal();
                        u2Button.Value = Project.EvalToInt(activeData.GetInteractionValue(1));
                        u2Button.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(1, Wla.ToByte((byte)b.ValueAsInt));
                        };
                        u2Button.Digits = 2;
                        widgetList.Add(u2Button);


                        labelList.Add("YX");
                        SpinButtonHexadecimal yxButton = new SpinButtonHexadecimal();
                        yxButton.Value = Project.EvalToInt(activeData.GetInteractionValue(2));
                        yxButton.ValueChanged += delegate(object sender, EventArgs e) {
                            Gtk.SpinButton b = sender as Gtk.SpinButton;
                            activeData.SetInteractionValue(2, Wla.ToByte((byte)b.ValueAsInt));
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
    }
}
