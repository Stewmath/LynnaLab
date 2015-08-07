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

		Stream gfxFile;

		public int DestAddr {
			get { return Project.EvalToInt(Values[1]) & 0xfff0; }
		}
		public int DestBank {
			get { return Project.EvalToInt(Values[1]) & 0x000f; }
		}
		public Stream GfxStream { get { return gfxFile; } }

		public GfxHeaderData(Project p, string command, IList<string> values, FileParser parser, int line, int colStart) 
			: base(p, command, values, 6, parser, line, colStart) {
			string filename = values[0] + ".bin";
		
			gfxFile = null;
			foreach (string directory in gfxDirectories) {
				if (File.Exists(Project.BaseDirectory + directory + filename)) {
					gfxFile = Project.GetBinaryFile(directory + filename);
                    if (Command == "m_gfxheader" && Values.Count > 3)
                        // Skip into part of gfx data
                        gfxFile = new SubStream(gfxFile, p.EvalToInt(Values[3]),
                                (p.EvalToInt(Values[2])+1)*16);

					break;
				}
			}
			if (gfxFile == null) {
				throw new Exception("Could not find graphics file " + filename);
			}
		}

		public bool ShouldHaveNext() {
			return (Project.EvalToInt(Values[2]) & 0x80) == 0x80;
		}
	}

}
