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
            AddLabel(new Label(Basename + "_start", 0));
        }

        protected void AddLabel(Label label) {
            labelDictionary.Add(label.Name, label);
            project.AddLabel(label.Name, this);
            activeLabel = label;
        }
        protected void AddData(Data data) {
            if (dataList.Count != 0) {
                dataList[dataList.Count - 1].Next = data;
                data.Last = dataList[dataList.Count - 1];
            }
            if (activeLabel != null)
                dataDictionary[data] = activeLabel;
            activeLabel = null;
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

        public Label GetDataLabel(Data data) {
            Label label;
            if (dataDictionary.TryGetValue(data, out label)) return label;
            return null;
        }

        public abstract string GetLine(int i);
        public abstract void SetLine(int i, string line);
        public abstract void Save();
    }
}

