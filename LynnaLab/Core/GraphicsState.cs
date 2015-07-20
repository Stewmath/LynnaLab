using System;

namespace LynnaLab
{
	public class GraphicsState
	{
		byte[][] vramBuffer = new byte[2][] { new byte[0x2000], new byte[0x2000] };
		byte[][] wramBuffer = new byte[8][] { new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000], 
			new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000] };



		public GraphicsState()
		{
		}
	}
}

