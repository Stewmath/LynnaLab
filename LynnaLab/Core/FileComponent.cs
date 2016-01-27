using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    public abstract class FileComponent {
        protected List<int> spacing;
        protected FileParser parser;

        Project _project;

        public bool EndsLine {get; set;} // True if a newline comes after this data

        // True if it's an internally-made object, not to be written back to
        // the file
        public bool Fake {get; set;}

        public FileComponent Next {
            get {
                return parser.GetNextFileComponent(this);
            }
        }
        public FileComponent Prev {
            get {
                return parser.GetPrevFileComponent(this);
            }
        }
        public FileParser FileParser {
            get { return parser; }
        }
        public Project Project {
            get { return _project; }
        }

        public FileComponent(FileParser parser, IList<int> spacing) {
            if (parser != null)
                _project = parser.Project;
            EndsLine = true;
            Fake = false;
            if (spacing != null)
                this.spacing = new List<int>(spacing);
            this.parser = parser;
        }

        public int GetSpacing(int index) {
            return spacing[index];
        }
        public void SetSpacing(int index, int spaces) {
            spacing[index] = spaces;
        }
        public void SetFileParser(FileParser p) {
            parser = p;
            if (parser != null)
                _project = parser.Project;
        }

        public abstract string GetString();

        protected void SetProject(Project p) {
            _project = p;
        }

        // Returns a string representing the given space value
        protected static string GetSpacingHelper(int spaces) {
            string s = "";
            while (spaces > 0) {
                s += ' ';
                spaces--;
            }
            while (spaces < 0) {
                s += '\t';
                spaces++;
            }
            return s;
        }
    }

    public class StringFileComponent : FileComponent {
        string str;

        public StringFileComponent(FileParser parser, string s, IList<int> spacing) : base(parser, spacing) {
            str = s;
            if (this.spacing == null)
                this.spacing = new List<int>();
            while (this.spacing.Count < 2)
                this.spacing.Add(0);
        }

        public override string GetString() {
            return GetSpacingHelper(spacing[0]) + str + GetSpacingHelper(spacing[1]);
        }
    }

    public class Label : FileComponent {
        string name;

        public string Name {
            get { return name; }
        }

        public Label(FileParser parser, string n, IList<int> spacing=null) : base(parser, spacing) {
            name = n;
            if (spacing == null) {
                this.spacing = new List<int>{0,0};
            }
        }

        public override string GetString() {
            return GetSpacingHelper(spacing[0]) + name + ":" + GetSpacingHelper(spacing[1]);
        }
    }
}
