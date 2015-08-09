using System;
using System.Collections.Generic;

namespace LynnaLab
{
	public class ConstantsMapping
	{
        public Project Project {
            get { return parser.Project; }
        }

        // This list is only necessary to preserve ordering
        List<string> stringList = new List<string>();

		Dictionary<string,byte> stringToByte = new Dictionary<string,byte>();
		Dictionary<byte,string> byteToString = new Dictionary<byte,string>();

        AsmFileParser parser;

		public ConstantsMapping(AsmFileParser parser, string prefix)
		{
            this.parser = parser;

            Dictionary<string,string> definesDictionary = parser.DefinesDictionary;
            foreach (string key in definesDictionary.Keys) {
                if (key.Substring(0,prefix.Length) == prefix) {
                    byte tmp;
                    if (!stringToByte.TryGetValue(key, out tmp)) {
                        try {
                            byte b = (byte)Project.EvalToInt(definesDictionary[key]);
                            stringList.Add(key);
                            stringToByte[key] = b;
                            byteToString[b] = key;
                        }
                        catch (FormatException) {}
                    }
                }
            }
		}

		public byte StringToByte(string key) {
            return stringToByte[key];
		}
		public string ByteToString(byte key) {
            return byteToString[key];
		}
        public int IndexOf(string key) {
            var list = GetAllStrings();
            return list.IndexOf(key);
        }

        public IList<string> GetAllStrings() {
            return stringList;
        }
	}
}
