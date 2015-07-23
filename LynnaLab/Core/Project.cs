using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace LynnaLab
{
	public class Project
	{
		string baseDirectory;
		string configDirectory;
		StreamWriter logWriter;

		AsmFileParser gfxHeaderFile;

		// Maps label to file which contains it
		Dictionary<string,FileParser> labelDictionary = new Dictionary<string,FileParser>();
		// List of opened binary files
		Dictionary<string,FileStream> binaryFileDictionary = new Dictionary<string,FileStream>();
		// Dictionary of .DEFINE's
		Dictionary<string,string> definesDictionary = new Dictionary<string,string>();

		public AsmFileParser GfxHeaderFile {
			get { return gfxHeaderFile; }
		}
		public string BaseDirectory {
			get { return baseDirectory; }
		}

		public Project(string d)
		{
			baseDirectory = d + '/';
			configDirectory = baseDirectory + "LynnaLab/";
			System.IO.Directory.CreateDirectory(configDirectory);

			Console.WriteLine("Opened directory \"" + baseDirectory + "\"");

			logWriter = new StreamWriter(configDirectory + "Log.txt");

			// Parse everything in constants/
			foreach (string f in Directory.EnumerateFiles(baseDirectory + "constants/")) {
				if (f.Substring(f.LastIndexOf('.')) == ".s") {
					string filename = "constants/" + f.Substring(f.LastIndexOf('/') + 1);
					new AsmFileParser(this, filename);
				}
			}
			gfxHeaderFile = new AsmFileParser(this, "data/gfxHeaders.s");
			new AsmFileParser(this, "data/areas.s");
            new AsmFileParser(this, "data/paletteData.s");
            new AsmFileParser(this, "data/paletteHeaders.s");
            new AsmFileParser(this, "data/tilesetMappings.s");
            new AsmFileParser(this, "data/tilesetCollisions.s");
            new AsmFileParser(this, "data/tilesetHeaders.s");
			new BinaryFileParser(this, "tileMappings.bin");
		}

		public void Close() {
			if (logWriter != null)
				logWriter.Close();
		}

		public void AddDefinition(string name, string value) {
			if (definesDictionary.ContainsKey("name")) {
				WriteLogLine("WARNING: \"" + name + "\" defined multiple times");
			}
			definesDictionary[name] = value;
		}
		public void AddLabel(string label, FileParser source) {
			if (labelDictionary.ContainsKey(label))
				throw new DuplicateLabelException("Label \"" + label + "\" defined for a second time.");
			labelDictionary.Add(label, source);
		}
		public FileParser GetFileWithLabel(string label) {
            try {
                return labelDictionary[label];
            } catch(KeyNotFoundException e) {
                throw new KeyNotFoundException("Label \"" + label + "\" was needed but could not be located!");
            }
		}

		public void WriteLog(string data) {
			logWriter.Write(data);
			// Also print to console
			Console.Write(data);
		}
		public void WriteLogLine(string data) {
			logWriter.WriteLine(data);
			Console.WriteLine(data);
		}

		// Handles only simple substitution
		public string Eval(string val)
		{
			val = val.Trim();

			string mapping;
			if (definesDictionary.TryGetValue(val, out mapping))
				return mapping;
			return val;
		}

		// TODO: finish arithmetic parsing
		public int EvalToInt(string val) {
			val = Eval(val).Trim();
			// Find brackets
			for (int i = 0; i < val.Length; i++) {
				if (val[i] == '(') {
					int x = 1;
					int j = 0;
					for (j = i + 1; j < val.Length; j++) {
						if (val[j] == '(')
							x++;
						else if (val[j] == ')') {
							x--;
							if (x == 0)
								break;
						}
					}
					if (j == val.Length)
						return Convert.ToInt32(val); // Will throw NumberFormatException
					string newVal = val.Substring(0, i);
					newVal += EvalToInt(val.Substring(i + 1, j));
					newVal += val.Substring(j + 1, val.Length);
					val = newVal;
				}
			}
			// Split up string while keeping delimiters
			string[] delimiters = { "+", "-", "|" };
			string source = val;
		    foreach (string delimiter in delimiters)
		        source = source.Replace(delimiter, ";" + delimiter + ";");
		    string[] parts = source.Split(';');

			if (parts.Length > 1) {
				if (parts.Length < 3)
					throw new FormatException();
				int ret = 0;
				if (parts[1] == "+")
					ret = EvalToInt(parts[0]) + EvalToInt(parts[2]);
				else if (parts[1] == "-")
					ret = EvalToInt(parts[0]) - EvalToInt(parts[2]);
				else if (parts[1] == "|")
					ret = EvalToInt(parts[0]) | EvalToInt(parts[2]);
				else
					throw new FormatException();
				string newVal = "" + ret;
				for (int j = 3; j < parts.Length; j++) {
					newVal += parts[j];
				}
				return EvalToInt(newVal);
			}

			if (val[0] == '$')
				return Convert.ToInt32(val.Substring(1), 16);
			else if (val[val.Length - 1] == 'h')
				return Convert.ToInt32(val.Substring(0, val.Length - 1), 16);
			else if (val[0] == '%')
				return Convert.ToInt32(val.Substring(1), 2);
			else
				return Convert.ToInt32(val);
		}

		public FileStream GetBinaryFile(string filename) {
			filename = baseDirectory + filename;
			FileStream stream = null;
			if (!binaryFileDictionary.TryGetValue(filename, out stream)) {
				stream = new FileStream(filename, FileMode.Open);
				binaryFileDictionary[filename] = stream;
			}
			return stream;
		}
	}

	public class DuplicateLabelException : Exception {
		public DuplicateLabelException()
			: base() {}
	    public DuplicateLabelException(string message)
			: base(message) {}
	    public DuplicateLabelException(string message, Exception inner)
			: base(message, inner) {}

	   protected DuplicateLabelException(SerializationInfo info, StreamingContext context)
			: base(info, context) {}
	}

}

