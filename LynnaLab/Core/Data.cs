using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Data 
    {
        // Size in bytes
        // -1 if indeterminate?
        protected int size;
        FileParser parser;

        bool modified;

        // Command is like .db, .dw, or a macro.
        string command;
        List<string> values;
        int line;
        int colStart, colEnd;

        protected Project Project { get; set; }

        protected FileParser Parser { get { return parser; } }

        public string Command {
            get { return command; }
        }
        public IList<string> Values {
            get { return values.AsReadOnly(); }
            set {
                values = new List<string>(value);
                modified = true;
            }
        }
        public int Size {
            get { return size; }
        }
        public Data Next {get; set;}
        public Data Last {get; set;}

        public Data(Project p, string command, IList<string> values, int size, FileParser parser, int line, int colStart=0, int colEnd=-1) {
            this.Project = p;
            this.command = command;
            this.values = new List<string>(values);
            this.size = size;
            this.parser = parser;
            this.line = line;
            this.colStart = colStart;
            this.colEnd = colEnd;
        }

        public void SetValue(int i, string value) {
            if (values[i] != value) {
                values[i] = value;
                modified = true;
            }
        }
        public void SetByteValue(int i, byte value) {
            SetValue(i, Wla.ToByte(value));
        }
        public void SetWordValue(int i, int value) {
            SetValue(i, Wla.ToWord(value));
        }

        public virtual void Save() {
            if (!modified)
                return;
            modified = false;

            string s = parser.GetLine(line);

            if (colEnd == -1)
                s = s.Remove(colStart);
            else
                s = s.Remove(colStart, colEnd-colStart);

            string insertion = "";
            if (colStart == 0)
                insertion += command;
            for (int i=0; i<Values.Count; i++) {
                if (i != 0 || colStart == 0)
                    insertion += " ";
                insertion += Values[i];
            }

            s = s.Insert(colStart, insertion);
            parser.SetLine(line, s);
        }

        public void ThrowException(string message) {
            message += " (" + parser.Filename + ": Line " + (line+1);
            throw new Exception(message);
        }
    }

    public class RgbData : Data {

        public Color Color {
            get {
                return Color.FromArgb(
                        Project.EvalToInt(Values[0])*8,
                        Project.EvalToInt(Values[1])*8,
                        Project.EvalToInt(Values[2])*8);
            }
        }

        public RgbData(Project p, string command, IList<string> values, FileParser parser, int line, int colStart)
            : base(p, command, values, 2, parser, line, colStart) {
            }
    }
}
