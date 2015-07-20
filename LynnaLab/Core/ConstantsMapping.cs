using System;
using System.Collections.Generic;

namespace LynnaLab
{
	public class ConstantsMapping
	{
		//Dictionary dict;

		public ConstantsMapping(Project p, string filename)
		{
			//dict = new Dictionary();

			AsmParser parser = new AsmParser(p, filename);
		}

		public byte StringToByte(string val) {
			return 0;
		}
	}
}

