﻿using System;
using Gtk;

namespace LynnaLab
{
    public class SpinButtonHexadecimal : Gtk.SpinButton
    {
        public SpinButtonHexadecimal() : base(0, 255, 1)
        {
            Initialize();
        }
        public SpinButtonHexadecimal(int start, int end) : base(start, end, 1)
        {
            Initialize();
        }
        public SpinButtonHexadecimal(int start, int end, int interval) : base(start, end, interval)
        {
            Initialize();
        }

        void Initialize()
        {
            this.Numeric = false;
        }

        protected override int OnOutput()
        {
            this.Numeric = false;
            if (ValueAsInt < 0)
                Text = "-$" + Math.Abs(ValueAsInt).ToString("X" + Digits);
            else
                Text = "$" + ValueAsInt.ToString("X" + Digits);
            return 1;
        }
        protected override int OnInput(out double value)
        {
            string text = Text.Trim();
            bool success = false;
            value = Value;

            // Try a hex number prefixed with "$"
            try
            {
                if (text.Length > 0 && text[0] == '$')
                {
                    value = Convert.ToInt32(text.Substring(1), 16);
                    success = true;
                }
            }
            catch (Exception)
            {
            }

            // Try a negative hex number prefixed with "$"
            try
            {
                if (text.Length > 1 && text[0] == '-' && text[1] == '$')
                {
                    value = -Convert.ToInt32(text.Substring(2), 16);
                    success = true;
                }
            }
            catch (Exception)
            {
            }

            // If no "$" sign is present, we still assume it will be hexadecimal
            try
            {
                value = Convert.ToInt32(text, 16);
                success = true;
            }
            catch (Exception)
            {
            }

            if (!success)
                value = Value;
            return 1;
        }
    }
}

