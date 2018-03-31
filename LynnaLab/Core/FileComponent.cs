using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    /// <summary>
    ///  This usually corresponds to a line in a file, but it can generally also be a logical unit
    ///  (ie. a documentation block).
    /// </summary>
    public abstract class FileComponent {
        protected List<string> spacing;
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

        public FileComponent(FileParser parser, IList<string> spacing) {
            if (parser != null)
                _project = parser.Project;
            EndsLine = true;
            Fake = false;
            if (spacing != null)
                this.spacing = new List<string>(spacing);
            this.parser = parser;
        }

        public string GetSpacing(int index) {
            return spacing[index];
        }
        public void SetSpacing(int index, string space) {
            spacing[index] = space;
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
    }

    public class StringFileComponent : FileComponent {
        string str;

        public StringFileComponent(FileParser parser, string s, IList<string> spacing) : base(parser, spacing) {
            str = s;
            if (this.spacing == null)
                this.spacing = new List<string>();
            while (this.spacing.Count < 2)
                this.spacing.Add("");
        }

        public override string GetString() {
            return spacing[0] + str + spacing[1];
        }
    }

    public class Label : FileComponent {
        string name;

        public string Name {
            get { return name; }
        }

        public Label(FileParser parser, string n, IList<string> spacing=null) : base(parser, spacing) {
            name = n;
            if (spacing == null) {
                this.spacing = new List<string>{"",""};
            }
            while (this.spacing.Count < 2)
                this.spacing.Add("");
        }

        public override string GetString() {
            return spacing[0] + name + ":" + spacing[1];
        }
    }
}
