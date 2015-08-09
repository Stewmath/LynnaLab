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
        ConstantsMapping uniqueGfxMapping, mainGfxMapping, paletteHeaderMapping;

        public AreaEditor(Area a)
        {
            this.Build();

            uniqueGfxMapping = new ConstantsMapping(
                        a.Project.GetFileParser("constants/uniqueGfxHeaders.s") as AsmFileParser,
                        "UNIQGFXH_");
            mainGfxMapping = new ConstantsMapping(
                        a.Project.GetFileParser("constants/gfxHeaders.s") as AsmFileParser,
                        "GFXH_");
            paletteHeaderMapping = new ConstantsMapping(
                        a.Project.GetFileParser("constants/paletteHeaders.s") as AsmFileParser,
                        "PALH_");

            SetArea(a);

            areaSpinButton.Adjustment.Upper = 0x66;
            uniqueGfxComboBox.SetConstantsMapping(uniqueGfxMapping);
            mainGfxComboBox.SetConstantsMapping(mainGfxMapping);
            palettesComboBox.SetConstantsMapping(paletteHeaderMapping);

            SetArea(a);
        }

        void SetArea(Area a) {
            area = a;
            areaSpinButton.Value = area.Index;
            SetFlags1(a.Flags1);
            SetFlags2(a.Flags2);
            SetUniqueGfx(a.UniqueGfxString);
            SetMainGfx(a.MainGfxString);
            SetPaletteHeader(a.PaletteHeaderString);
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
                uniqueGfxComboBox.Active = uniqueGfxMapping.IndexOf(value);
                area.UniqueGfxString = value;
            }
            catch (FormatException) {
            }
        }
        void SetMainGfx(string value) {
            try {
                mainGfxComboBox.Active = mainGfxMapping.IndexOf(value);
                area.MainGfxString = value;
            }
            catch (FormatException) {
            }
        }
        void SetPaletteHeader(string value) {
            try {
                palettesComboBox.Active = paletteHeaderMapping.IndexOf(value);
                area.PaletteHeaderString = value;
            }
            catch (FormatException) {
            }
        }

        protected void OnOkButtonClicked(object sender, EventArgs e)
        {
            Parent.Hide();
            Parent.Destroy();
        }

        protected void OnFlags1SpinButtonValueChanged(object sender, EventArgs e)
        {
            SpinButton button = sender as SpinButton;
            SetFlags1(button.ValueAsInt);
        }

        protected void OnFlags2SpinButtonValueChanged(object sender, EventArgs e)
        {
            SpinButton button = sender as SpinButton;
            SetFlags2(button.ValueAsInt);
        }

        protected void OnAreaSpinButtonValueChanged(object sender, EventArgs e)
        {
            SpinButton button = sender as SpinButton;
            SetArea(Project.GetIndexedDataType<Area>(button.ValueAsInt));
        }

        protected void OnUniqueGfxComboBoxChanged(object sender, EventArgs e) {
            var comboBox = sender as ComboBox;
            if (comboBox.ActiveText != null)
                SetUniqueGfx(comboBox.ActiveText);
        }

        protected void OnMainGfxComboBoxChanged(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox.ActiveText != null)
                SetMainGfx(comboBox.ActiveText);
        }

        protected void OnPalettesComboBoxChanged(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox.ActiveText != null)
                SetPaletteHeader(comboBox.ActiveText);
        }
    }
}

