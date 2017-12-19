using System;
using System.IO;
using System.Collections.Generic;

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

        public override void Save() {
        }
    }
}
