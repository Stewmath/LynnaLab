using System;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class SpinButtonHexadecimal : Gtk.SpinButton
    {
        public SpinButtonHexadecimal() : base(0,255,1)
        {
            this.Numeric = false;
        }

        protected override int OnOutput() {
            this.Numeric = false;
            Text = "$" + ValueAsInt.ToString("X" + Digits);
            return 1;
        }
        protected override int OnInput(out double value) {
            string text = Text.Trim();
            bool success = false;
            value = Value;
            try {
                value = Convert.ToInt32(text);
                success = true;
            }
            catch (Exception e) {
            }
            try {
                if (text.Length > 0 && text[0] == '$') {
                    value = Convert.ToInt32(text.Substring(1),16);
                    success = true;
                }
            }
            catch (Exception) {
            }
            if (!success)
                value = Value;
            return 1;
        }
    }
}

