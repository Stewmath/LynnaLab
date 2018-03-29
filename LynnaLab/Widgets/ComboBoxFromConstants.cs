using System;
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
                    Active = mapping.IndexOf((byte)value);
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

        // TODO: pass in a label which it will update with the name from the combobox?
        public ComboBoxFromConstants()
        {
            this.Build();
        }

        public void SetConstantsMapping(ConstantsMapping mapping) {
            this.mapping = mapping;
            keyText = new string[mapping.GetAllStrings().Count];

            int i=0;
            foreach (string key in mapping.GetAllStrings()) {
                string text = key;
                // Trim the prefix from the string
                foreach (string prefix in mapping.Prefixes) {
                    if (key.Length >= prefix.Length && key.Substring(0,prefix.Length) == prefix) {
                        text = key.Substring(prefix.Length);
                        break;
                    }
                }
                int value = mapping.StringToByte(key);
                combobox1.AppendText(Wla.ToByte((byte)value) + ": " + text);

                keyText[i] = key;
                i++;
            }
        }

        protected void OnCombobox1Changed(object sender, EventArgs e)
        {
            if (Changed != null)
                Changed(sender, e);

            if (mapping == null || combobox1.Active == -1)
                return;
            spinButton.Value = mapping.GetIndexByte(combobox1.Active);
        }

        protected void OnSpinButtonValueChanged (object sender, EventArgs e)
        {
            if (Changed != null)
                Changed(sender, e);

            if (mapping == null)
                return;
            combobox1.Active = mapping.IndexOf((byte)spinButton.ValueAsInt);
        }
    }
}
