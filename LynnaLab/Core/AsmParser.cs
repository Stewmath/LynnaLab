using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
	public class Data 
	{
		// Command is like .db, .dw, or a macro.
		string command;
		string[] values;
		// Size in bytes
		// -1 if indeterminate?
		int size;

		Data nextData;

		public string Command {
			get { return command; }
		}
		public string[] Values {
			get { return values; }
		}
		public int Size {
			get { return size; }
		}
		public Data Next {
			get { return nextData; }
			set { nextData = value; }
		}

		public Data(string command, string[] values, int size) {
			this.command = command;
			this.values = values;
			this.size = size;
		}
	}

	public class AsmParser
	{
		class Label {
			string name;
			// An index for dataList (start of the data it references)
			int index;

			public string Name {
				get { return name; }
			}
			public int Index {
				get { return index; }
			}

			public Label(string n, int i) {
				name = n;
				index = i;
			}
		}

		Project project;

		string filename;
		string fullFilename;

		Dictionary<string,Label> labelDictionary = new Dictionary<string,Label>();
		List<Data> dataList = new List<Data>();

		/*
		public List<string> LabelList {
			get { return labelList; }
		}
*/

		public AsmParser(Project p, string f)
		{
			project = p;
			this.filename = f;
			this.fullFilename = p.BaseDirectory + f;

			// Made-up label for the start of the file
			AddLabel(new Label(f + "_start", 0));

			string[] lines = File.ReadAllLines(fullFilename);

			for (int i=0; i<lines.Length; i++) {
				string line = lines[i];
				line = line.Split(';')[0].Trim();
				if (line.Length == 0)
					continue;

				// TODO: split tokens more intelligently, ie: recognize this as one token: $8000 | $03
				string[] tokens = line.Split(new char[] { ' ', '\t'} );
				string warningString = "WARNING while parsing \"" + filename + "\": Line " + (i+1) + ": ";

				string value;

				if (tokens.Length > 0) {
					switch (tokens[0].ToLower()) {
						case ".define":
							if (tokens.Length < 3) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected .DEFINE to have a string and a value.");
								break;
							}
							value = "";
							for (int j = 2; j < tokens.Length; j++) {
								value += tokens[j];
								value += " ";
							}
							value = value.Trim();
							project.AddDefinition(tokens[1], value);
							break;
						case ".dw":
							if (tokens.Length < 2) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected .DW to have a value.");
								break;
							}
							for (int j=1; j<tokens.Length; j++) {
								string[] values = { tokens[j] };
								Data d = new Data(tokens[0], values, 2);
								AddData(d);
							}
							break;
						case ".db":
							if (tokens.Length < 2) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected .DB to have a value.");
								break;
							}
							for (int j=1; j<tokens.Length; j++) {
								string[] values = { tokens[j] };
								Data d = new Data(tokens[0], values, 1);
								AddData(d);
							}
							break;
						case "m_gfxheader":
						case "m_gfxheaderforcemode":
							if (tokens.Length < 4 || tokens.Length > 5) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected " + tokens[0] + " to take 4-5 parameters");
								break;
							}
							{
								List<string> values = new List<string>();
								for (int j = 1; j < tokens.Length; j++)
									values.Add(tokens[j]);
								Data d = new Data(tokens[0], values.ToArray(), 6);
								AddData(d);
								break;
							}

						default:
							if (tokens[0][tokens[0].Length - 1] == ':') {
								// Label
								string s = tokens[0].Substring(0, tokens[0].Length - 1); 
								Label label = new Label(s,dataList.Count);
								AddLabel(label);
							} else {
								// Unknown data
								project.WriteLog(warningString);
								project.WriteLogLine("Did not understand \"" + tokens[0] + "\".");
							}
							break;
					}
				}
			}

			project.WriteLogLine("Parsed \"" + filename + "\" successfully maybe.");
		}

		public Data GetData(string labelStr, int offset=0) {
			int origOffset = offset;

			Label label = labelDictionary[labelStr];
			int index = label.Index;
			while (index < dataList.Count && index >= 0) {
				if (offset == 0)
					return dataList[index];
				offset -= dataList[index].Size;
				index++;
			}
			throw new Exception("Provided offset (" + origOffset + ") relative to label \"" + labelStr +
				"\" was invalid.");
		}

		void AddLabel(Label label) {
			labelDictionary.Add(label.Name, label);
			project.AddLabel(label.Name, this);
		}
		void AddData(Data data) {
			if (dataList.Count != 0)
				dataList[dataList.Count - 1].Next = data;
			dataList.Add(data);
		}
	}
}

