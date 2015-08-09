using System;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class AreaEditor : Gtk.Bin
    {
        Project Project {
            get { return area.Project; }
        }

        Area area;
        ConstantsMapping mapping;

        public AreaEditor(Area a)
        {
            this.Build();

            mapping = new ConstantsMapping(
                        a.Project.GetFileParser("constants/uniqueGfxHeaders.s") as AsmFileParser,
                        "UNIQGFXH_");

            SetArea(a);

            areaSpinButton.Adjustment.Upper = 0x66;
            uniqueGfxComboBox.SetConstantsMapping(mapping);

            SetArea(a);
        }

        void SetArea(Area a) {
            area = a;
            SetFlags1(a.Flags1);
            SetFlags2(a.Flags2);
            SetUniqueGfx(a.UniqueGfxString);
        }

        void SetFlags1(int value) {
            flags1SpinButton.Value = value;
            area.Flags1 = value;
        }
        void SetFlags2(int value) {
            flags2SpinButton.Value = value;
            area.Flags2 = value;
        }
        void SetUniqueGfx(string value) {
            try {
                uniqueGfxComboBox.Active = mapping.IndexOf(value);
                area.UniqueGfxString = value;
            }
            catch (FormatException) {
            }
        }

        protected void OnFlags2SpinButtonValueChanged(object sender, EventArgs e)
        {
            SpinButton button = sender as SpinButton;
            SetFlags2(button.ValueAsInt);
        }

        protected void OnFlags1SpinButtonValueChanged(object sender, EventArgs e)
        {
            SpinButton button = sender as SpinButton;
            SetFlags1(button.ValueAsInt);
        }

        protected void OnAreaSpinButtonValueChanged(object sender, EventArgs e)
        {
            SpinButton button = sender as SpinButton;
            SetArea(Project.GetIndexedDataType<Area>(button.ValueAsInt));
        }

        protected void OnUniqueGfxComboBoxChanged(object sender, EventArgs e) {
            if (uniqueGfxComboBox.ActiveText != null)
                SetUniqueGfx(uniqueGfxComboBox.ActiveText);
        }
    }
}

