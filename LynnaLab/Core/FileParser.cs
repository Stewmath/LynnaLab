using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Label : FileComponent {
        string name;
        // An index for dataList (start of the data it references)
        int index;

        public string Name {
            get { return name; }
        }
        public int Index {
            get { return index; }
        }

        public Label(FileParser parser, string n, int i) : base(parser, null) {
            name = n;
            index = i;
        }
        public Label(FileParser parser, string n, int i, IList<int> spacing) : base(parser, spacing) {
            name = n;
            index = i;
        }

        public override string GetString() {
            return GetSpacingHelper(spacing[0]) + name + ":" + GetSpacingHelper(spacing[1]);
        }
    }


    public abstract class FileParser
    {
        private Project project;

        string filename; // Relative to base directory
        string fullFilename; // Full path

        Dictionary<string,Label> labelDictionary = new Dictionary<string,Label>();

        // Keeps track of whether data is pointed to directly by a label
        Dictionary<Data,Label> dataDictionary = new Dictionary<Data,Label>();

        List<Data> dataList = new List<Data>();

        // The last label defined. Gets linked to the next Data field added,
        // then gets unset
        Label activeLabel;

        public bool Modified {get; set;}
        public Project Project {
            get { return project; }
        }
        public string Filename {
            get { return filename; }
        }
        protected string FullFilename {
            get { return fullFilename; }
        }
        protected List<Data> DataList {
            get { return dataList; }
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
            Label l = new Label(this, Basename + "_start", 0);
            l.Fake = true;
            AddLabel(l);
        }

        protected virtual bool AddLabel(Label label) {
            labelDictionary.Add(label.Name, label);
            project.AddLabel(label.Name, this);
            activeLabel = label;
            return true;
        }
        protected virtual bool AddData(Data data) {
            if (activeLabel != null)
                dataDictionary[data] = activeLabel;
            activeLabel = null;
            dataList.Add(data);
            return true;
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

        public Label GetDataLabel(Data data) {
            Label label;
            if (dataDictionary.TryGetValue(data, out label)) return label;
            return null;
        }

        public abstract bool InsertDataAfter(Data refData, Data newData);
        public abstract bool InsertDataBefore(Data refData, Data newData);
        public abstract FileComponent GetNextFileComponent(FileComponent reference);
        public abstract FileComponent GetPrevFileComponent(FileComponent reference);
        public abstract void RemoveFileComponent(FileComponent component);

        public abstract void Save();
    }
}
