using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
	public class BinaryFileParser : FileParser
	{
		public BinaryFileParser (Project p, string f)
			: base(p, f)
		{
			byte[] data = File.ReadAllBytes(fullFilename);

			foreach (byte b in data) {
				AddData(new Data(p, ".db", new string[] { b.ToString() }, 1));
			}

			project.WriteLogLine("Parsed \"" + filename + "\" successfully maybe.");
		}
	}
}

