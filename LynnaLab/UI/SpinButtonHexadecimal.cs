using System;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class SpinButtonHexadecimal : Gtk.SpinButton
    {
        public SpinButtonHexadecimal() : base(0,100,1)
        {
            this.Numeric = false;
        }

        protected override int OnOutput() {
            this.Numeric = false;
            Text = "0x" + ValueAsInt.ToString("X" + Digits);
            return 1;
        }
        protected override int OnInput(out double value) {
            try {
                value = Convert.ToInt32(Text);
            }
            catch (Exception e) {
                try {
                    value = Convert.ToInt32(Text.Substring(2),16);
                }
                catch (Exception) {
                    value = Value;
                }
            }
            return 1;
        }
    }
}

