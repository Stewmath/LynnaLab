using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab {
    // Data macro "m_TilesetData" (used for both mapping and collision data)
	public class TilesetData : Data {
		public TilesetData(Project p, string command, IList<string> values) 
			: base(p, command, values, -1) {
                
		}
	}

}
