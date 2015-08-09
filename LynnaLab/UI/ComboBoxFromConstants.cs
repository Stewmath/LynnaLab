using System;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class ComboBoxFromConstants : Gtk.Bin
    {
        public event EventHandler Changed;

        ConstantsMapping mapping;

        public int Active {
            get { return combobox1.Active; }
            set { combobox1.Active = value; }
        }
        public string ActiveText {
            get { return combobox1.ActiveText; }
        }

        public ComboBoxFromConstants()
        {
            this.Build();
        }

        public void SetConstantsMapping(ConstantsMapping mapping) {
            this.mapping = mapping;
            foreach (string key in mapping.GetAllStrings()) {
                combobox1.AppendText(key);
            }
        }

        protected void OnCombobox1Changed(object sender, EventArgs e)
        {
            if (Changed != null)
                Changed(sender, e);
        }
    }
}
