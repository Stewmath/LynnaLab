using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
	public class GfxHeaderGroup : ProjectIndexedDataType
	{
		GfxHeaderData firstGfxHeader;

		public GfxHeaderData FirstGfxHeader {
			get { return firstGfxHeader; }
		}

		public GfxHeaderGroup(Project project, string name) : this(project, project.EvalToInt(name))
		{
		}
		public GfxHeaderGroup(Project project, int index) : base(project, index)
		{
			FileParser gfxPointerFile = project.GetFileWithLabel("gfxHeaderGroupTable");
			Data headerPointerData = gfxPointerFile.GetData("gfxHeaderGroupTable", index*2);
			FileParser gfxHeaderFile = project.GetFileWithLabel(headerPointerData.Values[0]);
			Data headerData = gfxHeaderFile.GetData(headerPointerData.Values[0]);

			if (!(headerData is GfxHeaderData))
				throw new Exception("Expected GFX header group " + index.ToString("X") + " to start with GFX header data");
			firstGfxHeader = (GfxHeaderData)headerData;
		}

        public override void Save() {
        }
	}
}
