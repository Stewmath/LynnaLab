using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    // Enum of types of values that can be had, currently only used to help
    // with default initialization
    public enum DataValueType {
        Byte=0,
        Word,
        String
    }

    public class Data : FileComponent
    {
        public static string[] defaultDataValueTypes = {
            "$00",
            "$0000",
            ""
        };

        public static string[] GetDefaultValues(DataValueType[] valueList) {
            string[] ret = new string[valueList.Length];
            for (int i=0;i<valueList.Length;i++) {
                ret[i] = defaultDataValueTypes[(int)valueList[i]];
            }
            return ret;
        }




        // These are accessed by AsmFileParser, and by nothing else pls
        internal Data nextData, prevData;

        // Size in bytes
        // -1 if indeterminate? (consider getting rid of this, it's unreliable)
        protected int size;
        FileParser parser;

        bool _modified;

        // Command is like .db, .dw, or a macro.
        string command;
        List<string> values;


        // Properties

        public bool Modified {
            get { return _modified; }
            set {
                _modified = value;
                if (value == true)
                    parser.Modified = true;
            }
        }

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
        }
        public int Size {
            get { return size; }
        }

        public bool PrintCommand {get; set;} // If false, don't output the command, only the values

        public Data Next {
            get { return nextData; }
        }
        public Data Last {
            get { return prevData; }
        }


        // Constructor

        public Data(Project p, string command, IList<string> values, int size, FileParser parser, IList<int> spacing) : base(spacing) {
            this.Project = p;
            this.command = command;
            this.values = new List<string>(values);
            this.size = size;
            this.parser = parser;

            if (this.spacing == null)
                this.spacing = new List<int>();
            while (this.spacing.Count < Values.Count+2)
                this.spacing.Add(0);

            PrintCommand = true;
            _modified = false;
        }

        public void SetValue(int i, string value) {
            if (values[i] != value) {
                values[i] = value;
                Modified = true;
            }
        }
        public void SetByteValue(int i, byte value) {
            SetValue(i, Wla.ToByte(value));
        }
        public void SetWordValue(int i, int value) {
            SetValue(i, Wla.ToWord(value));
        }
        // Removes a value, deletes the spacing prior to it
        public void RemoveValue(int i) {
            values.RemoveAt(i);
            spacing.RemoveAt(i+1);
            Modified = true;
        }
        public void InsertValue(int i, string value, int priorSpaces=1) {
            values.Insert(i, value);
            spacing.Insert(i+1, priorSpaces);
            Modified = true;
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

        // Detach from the parser
        public void Detach() {
            parser.RemoveFileComponent(this);
        }
        public void InsertIntoParserAfter(Data reference) {
            parser.InsertDataAfter(reference, this);
        }
        public void InsertIntoParserBefore(Data reference) {
            parser.InsertDataBefore(reference, this);
        }

        // Helper function for GetString
        string GetSpacingIndex(int i) {
            string s = "";
            int spaces = spacing[i];
            if (spaces == 0 && i != 0 && i != Values.Count+1) spaces = 1;
            s = GetSpacingHelper(spaces);
            return s;
        }

        public void ThrowException(string message) {
            message += " (" + parser.Filename + ")";
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
