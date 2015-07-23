using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab
{
	public class GfxHeaderData : Data {
		static List<string> gfxDirectories = new List<string>() {
			"gfx/",
			"gfx_compressible/"
		};

		FileStream gfxFile;

		public int DestAddr {
			get { return project.EvalToInt(Values[1]) & 0xfff0; }
		}
		public int DestBank {
			get { return project.EvalToInt(Values[1]) & 0x000f; }
		}
		public FileStream GfxFile { get { return gfxFile; } }

		public GfxHeaderData(Project p, string command, IList<string> values) 
			: base(p, command, values, 6) {
			string filename = values[0] + ".bin";
		
			gfxFile = null;
			foreach (string directory in gfxDirectories) {
				if (File.Exists(project.BaseDirectory + directory + filename)) {
					gfxFile = project.GetBinaryFile(directory + filename);
					break;
				}
			}
			if (gfxFile == null) {
				throw new Exception("Could not find graphics file " + filename);
			}
		}

		public bool ShouldHaveNext() {
			return (project.EvalToInt(Values[2]) & 0x80) == 0x80;
		}
	}

}
