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

        // Index of the entry in the combobox
        public int Active {
            get { return combobox1.Active; }
            set {
                combobox1.Active = value;
                if (mapping != null && Active != -1)
                    spinButton.Value = mapping.GetIndexByte(combobox1.Active);
            }
        }
        // Byte value of the entry in the combobox
        public int ActiveValue {
            get { return spinButton.ValueAsInt; }
            set {
                if (mapping != null && mapping.HasValue(value))
                    combobox1.Active = mapping.IndexOf(value);
                spinButton.Value = value;
            }
        }
        // String value of the entry in the combobox (formerly "ActiveText" in
        // gtk2)
        public string ActiveId {
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
                return mapping.OverallDocumentation;
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
                //combobox1.AppendText(text); // TODO

                keyText[i] = key;
                i++;
            }
        }

        bool fromCombo = false;
        bool fromSpin = false;

        protected void OnCombobox1Changed(object sender, EventArgs e)
        {
            fromCombo = true;

            if (!(mapping == null || combobox1.Active == -1))
                spinButton.Value = mapping.GetIndexByte(combobox1.Active);

            if (Changed != null && !fromSpin)
                Changed(this, e);

            fromCombo = false;
        }

        protected void OnSpinButtonValueChanged (object sender, EventArgs e)
        {
            fromSpin = true;

            // This will invoke the combobox1 callback
            if (mapping != null)
                combobox1.Active = mapping.IndexOf((byte)spinButton.ValueAsInt);

            if (Changed != null && !fromCombo)
                Changed(this, e);

            fromSpin = false;
        }
    }
}
