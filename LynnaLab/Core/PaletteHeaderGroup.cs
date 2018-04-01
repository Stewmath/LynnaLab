using System;
using System.Drawing;

namespace LynnaLab
{
    public class PaletteHeaderGroup : ProjectIndexedDataType
    {
        PaletteHeaderData firstPaletteHeader;

        public PaletteHeaderData FirstPaletteHeader {
            get { return firstPaletteHeader; }
        }

        PaletteHeaderGroup(Project project, int index) : base(project, index)
        {
            FileParser palettePointerFile = project.GetFileWithLabel("paletteHeaderTable");
            Data headerPointerData = palettePointerFile.GetData("paletteHeaderTable", index*2);
            FileParser paletteHeaderFile = project.GetFileWithLabel(headerPointerData.GetValue(0));
            Data headerData = paletteHeaderFile.GetData(headerPointerData.GetValue(0));

            if (!(headerData is PaletteHeaderData))
                throw new Exception("Expected palette header group " + index.ToString("X") + " to start with palette header data");
            firstPaletteHeader = (PaletteHeaderData)headerData;
        }

        // TODO: error handling
        public Color[][] GetObjPalettes() {
            Color[][] ret = new Color[8][];

            PaletteHeaderData palette = firstPaletteHeader;
            while (true) {
                Color[][] palettes = palette.GetPalettes();
                if (palette.PaletteType == PaletteType.Sprite) {
                    for (int i=0; i<palette.NumPalettes; i++) {
                        ret[i+palette.FirstPalette] = palettes[i];
                    }
                }
                if (!palette.ShouldHaveNext())
                    break;
                palette = palette.NextData as PaletteHeaderData;
            }
            return ret;
        }

        public override void Save() {
        }
    }
}
