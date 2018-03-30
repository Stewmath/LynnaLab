using System;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class ComboBoxFromConstants : Gtk.Bin
    {
        public event EventHandler Changed;

        ConstantsMapping mapping;

        // Array of actual text from ConstantsMapping (because the string is changed around
        // a little bit)
        string[] keyText;

        public int Active {
            get { return combobox1.Active; }
            set {
                combobox1.Active = value;
                if (mapping != null && Active != -1)
                    spinButton.Value = mapping.GetIndexByte(combobox1.Active);
            }
        }
        public int ActiveValue {
            get { return spinButton.ValueAsInt; }
            set {
                if (mapping != null)
                    combobox1.Active = mapping.IndexOf((byte)value);
                spinButton.Value = value;
            }
        }
        public string ActiveText {
            get {
                if (combobox1.Active == -1)
                    return "";
                return keyText[combobox1.Active];
            }
            set {
                if (mapping != null) {
                    Active = mapping.IndexOf(value);
                    if (Active != -1)
                        spinButton.Value = mapping.GetIndexByte(combobox1.Active);
                }
            }
        }
        public Documentation DefaultDocumentation {
            get {
                return mapping.DefaultDocumentation;
            }
        }


        // TODO: pass in a label which it will update with the name from the combobox?
        public ComboBoxFromConstants(bool showHelp=true)
        {
            this.Build();

            if (showHelp) {
                // When clicking the "help" button, create a popup with documentation for
                // possible values. (It checks for a "@values" field in the documentation.)
                Gtk.Button helpButton = new Gtk.Button("?");
                helpButton.CanFocus = false;
                helpButton.Clicked += delegate(object sender, EventArgs e) {
                    if (DefaultDocumentation == null)
                        return;

                    DocumentationDialog d = new DocumentationDialog(DefaultDocumentation);
                    d.Run();
                    d.Destroy();
                };

                hbox1.PackStart(helpButton, false, false, 0);
            }
        }

        public void SetConstantsMapping(ConstantsMapping mapping) {
            this.mapping = mapping;
            keyText = new string[mapping.GetAllStrings().Count];

            int i=0;
            foreach (string key in mapping.GetAllStrings()) {
                string text = mapping.RemovePrefix(key);
                //int value = mapping.StringToByte(key);
                combobox1.AppendText(text);

                keyText[i] = key;
                i++;
            }
        }

        protected void OnCombobox1Changed(object sender, EventArgs e)
        {
            if (!(mapping == null || combobox1.Active == -1))
                spinButton.Value = mapping.GetIndexByte(combobox1.Active);

            if (Changed != null)
                Changed(sender, e);
        }

        protected void OnSpinButtonValueChanged (object sender, EventArgs e)
        {
            // This will invoke the combobox1 callback
            combobox1.Active = mapping.IndexOf((byte)spinButton.ValueAsInt);
        }
    }
}
