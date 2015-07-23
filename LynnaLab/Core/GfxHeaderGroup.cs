using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
	public class GfxHeaderGroup
	{
		Project project;
		int index;

		GfxHeaderData firstGfxHeader;

		public GfxHeaderData FirstGfxHeader {
			get { return firstGfxHeader; }
		}

		public GfxHeaderGroup(Project project, string name)
		{
			this.project = project;
			index = project.EvalToInt(name);
		}
		public GfxHeaderGroup(Project project, int index)
		{
			this.project = project;
			this.index = index;

			FileParser gfxPointerFile = project.GetFileWithLabel("gfxHeaderGroupTable");
			Data headerPointerData = gfxPointerFile.GetData("gfxHeaderGroupTable", index*2);
			FileParser gfxHeaderFile = project.GetFileWithLabel(headerPointerData.Values[0]);
			Data headerData = gfxHeaderFile.GetData(headerPointerData.Values[0]);

			if (!(headerData is GfxHeaderData))
				throw new Exception("Expected GFX header group " + index.ToString("X") + " to start with GFX header data");
			firstGfxHeader = (GfxHeaderData)headerData;
		}
	}
}
