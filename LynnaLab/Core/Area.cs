using System;

namespace LynnaLab
{
	public class Area
	{
		Project project;
		int index;

		AsmParser areaFile;

		int flags1, flags2;
		int uniqueGfxHeaderIndex, gfxHeaderIndex;
		int paletteIndex;
		int tilesetIndex;
		int layoutGroup;
		int animationIndex;

		public Area(Project p, string indexStr)
		{
			project = p;
			index = project.EvalToInt(indexStr);

			areaFile = project.GetFileWithLabel("areaData");

			Data areaData = areaFile.GetData("areaData", index * 8);
			flags1 = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			flags2 = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			uniqueGfxHeaderIndex = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			gfxHeaderIndex = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			paletteIndex = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			tilesetIndex = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			layoutGroup = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			animationIndex = p.EvalToInt(areaData.Values[0]);
		}
	}
}

