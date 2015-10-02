using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Data : FileComponent
    {
        // These are accessed by AsmFileParser, and by nothing else pls
        internal Data nextData, prevData;

        // Size in bytes
        // -1 if indeterminate? (consider getting rid of this, it's unreliable)
        protected int size;
        FileParser parser;

        bool modified;

        // Command is like .db, .dw, or a macro.
        string command;
        List<string> values;

        protected Project Project { get; set; }

        protected FileParser Parser { get { return parser; } }

        public string Command {
            get { return command; }
        }
        public string CommandLowerCase {
            get { return command.ToLower(); }
        }
        public IList<string> Values {
            get { return values.AsReadOnly(); }
            set {
                values = new List<string>(value);
                while (spacing.Count < values.Count+2)
                    spacing.Add(0);
                modified = true;
            }
        }
        public int Size {
            get { return size; }
        }

        public bool PrintCommand {get; set;} // If false, don't output the command, only the values

        public Data Next {
            get { return nextData; }
            set {
                // Doesn't work for data that already exists in the parser
                parser.InsertDataAfter(this, value);
            }
        }
        public Data Last {
            get { return prevData; }
            set {
                parser.InsertDataBefore(this, value);
            }
        }

        public Data(Project p, string command, IList<string> values, int size, FileParser parser, IList<int> spacing) : base(spacing) {
            this.Project = p;
            this.command = command;
            this.values = new List<string>(values);
            this.size = size;
            this.parser = parser;

            PrintCommand = true;
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

        public override string GetString() {
            string s = "";
            if (PrintCommand)
                s = GetSpacingIndex(0) + Command;

            int spacingIndex = 1;
            for (int i=0; i<Values.Count; i++) {
                s += GetSpacingIndex(spacingIndex++);
                s += Values[i];
            }
            s += GetSpacingIndex(spacingIndex++);

            return s;
        }

        // Helper function for GetString
        string GetSpacingIndex(int i) {
            string s = "";
            int spaces = spacing[i];
            if (spaces == 0 && i != 0 && i != Values.Count+1) spaces = 1;
            s = GetSpacing(spaces);
            return s;
        }

        public void ThrowException(string message) {
            message += " (" + parser.Filename;
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

        public RgbData(Project p, string command, IList<string> values, FileParser parser, IList<int> spacing)
            : base(p, command, values, 2, parser, spacing) {
            }
    }
}
