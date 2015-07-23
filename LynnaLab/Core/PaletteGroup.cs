using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
	public class PaletteHeaderGroup
	{
		Project project;
		int index;

		PaletteHeaderData firstPaletteHeader;

		public PaletteHeaderData FirstPaletteHeader {
			get { return firstPaletteHeader; }
		}

		public PaletteHeaderGroup(Project project, string name)
            : this(project, project.EvalToInt(name))
		{
		}
		public PaletteHeaderGroup(Project project, int index)
		{
			this.project = project;
			this.index = index;

			FileParser palettePointerFile = project.GetFileWithLabel("paletteHeaderGroupTable");
			Data headerPointerData = palettePointerFile.GetData("paletteHeaderGroupTable", index*2);
			FileParser paletteHeaderFile = project.GetFileWithLabel(headerPointerData.Values[0]);
			Data headerData = paletteHeaderFile.GetData(headerPointerData.Values[0]);

			if (!(headerData is PaletteHeaderData))
				throw new Exception("Expected palette header group " + index.ToString("X") + " to start with palette header data");
			firstPaletteHeader = (PaletteHeaderData)headerData;
		}
	}
}
