using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
	public class Label {
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
			index = i; }
	}


	public class FileParser
	{
		private Project project;

		protected string filename; // Relative to base directory
		protected string fullFilename; // Full path

		protected Dictionary<string,Label> labelDictionary = new Dictionary<string,Label>();
		protected List<Data> dataList = new List<Data>();

        public Project Project {
            get { return project; }
        }

		public string Basename {
			get { 
				int i = filename.LastIndexOf('.');
				if (i == -1)
					return filename;
				return filename.Substring(0, i);
			}
		}

		protected FileParser (Project p, string f)
		{
			project = p;
			this.filename = f;
			this.fullFilename = p.BaseDirectory + f;

			// Made-up label for the start of the file
			AddLabel(new Label(Basename + "_start", 0));
		}
		
		protected void AddLabel(Label label) {
			labelDictionary.Add(label.Name, label);
			project.AddLabel(label.Name, this);
		}
		protected void AddData(Data data) {
			if (dataList.Count != 0)
				dataList[dataList.Count - 1].Next = data;
			dataList.Add(data);
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

	}
}

