using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{

    public class Data : FileComponent
    {
        // Size in bytes
        // -1 if indeterminate? (consider getting rid of this, it's unreliable)
        protected int size;
        bool _modified;

        // Command is like .db, .dw, or a macro.
        string command;
        List<string> values;

        // These DataValueReferences provide an alternate interface to editing
        // the values
        List<ValueReference> valueReferences;


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
        public int Size {
            get { return size; }
        }

        public bool PrintCommand {get; set;} // If false, don't output the command, only the values

        public Data NextData {
            get {
                FileComponent c = this;
                do {
                    c = parser.GetNextFileComponent(c);
                    if (c is Data) return c as Data;
                } while(c != null);
                return c as Data;
            }
        }
        public Data LastData {
            get {
                FileComponent c = this;
                do {
                    c = parser.GetPrevFileComponent(c);
                    if (c is Data) return c as Data;
                } while(c != null);
                return c as Data;
            }
        }


        // Constructor

        public Data(Project p, string command, IList<string> values, int size, FileParser parser, IList<int> spacing) : base(parser, spacing) {
            this.Project = p;
            this.command = command;
            this.values = new List<string>(values);
            this.size = size;

            if (this.spacing == null)
                this.spacing = new List<int>();
            while (this.spacing.Count < values.Count+2)
                this.spacing.Add(0);

            PrintCommand = true;
            _modified = false;
        }


        public virtual string GetValue(int i) {
            return values[i];
        }
        public int GetIntValue(int i) {
            // TODO: error handling
            return Project.EvalToInt(GetValue(i));
        }
        public string GetValue(string s) { // Get a value based on a value reference name
            foreach (ValueReference r in valueReferences) {
                if (r.Name == s) {
                    return r.GetStringValue();
                }
            }
            ThrowException(new NotFoundException("Couldn't find ValueReference corresponding to \"" + s + "\"."));
            return null;
        }
        public int GetIntValue(string s) {
            foreach (ValueReference r in valueReferences) {
                if (r.Name == s) {
                    return r.GetIntValue();
                }
            }
            ThrowException(new NotFoundException("Couldn't find ValueReference corresponding to \"" + s + "\"."));
            return 0;
        }

        public virtual int GetNumValues() {
            return values.Count;
        }


        public virtual void SetValue(int i, string value) {
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
        public void InsertValue(int i, string value, DataValueType valueType=DataValueType.String,int priorSpaces=-1) {
            values.Insert(i, value);
            spacing.Insert(i+1, priorSpaces);
            Modified = true;
        }

        public IList<ValueReference> GetValueReferences() {
            if (valueReferences == null)
                return null;
            return valueReferences.AsReadOnly();
        }
        public void SetValueReferences(IList<ValueReference> references) {
            valueReferences = new List<ValueReference>();
            foreach (ValueReference r in references)
                valueReferences.Add(new ValueReference(r));
            foreach (ValueReference r in valueReferences)
                r.SetData(this);
        }

        public override string GetString() {
            string s = "";
            if (PrintCommand)
                s = GetSpacingIndex(0) + Command;

            int spacingIndex = 1;
            for (int i=0; i<values.Count; i++) {
                s += GetSpacingIndex(spacingIndex++);
                s += values[i];
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
            if (spaces == 0 && i != 0 && i != values.Count+1) spaces = 1;
            s = GetSpacingHelper(spaces);
            return s;
        }

        public void ThrowException(Exception e) {
            throw e;
        }
    }

    public class RgbData : Data {

        public Color Color {
            get {
                return Color.FromArgb(
                        Project.EvalToInt(GetValue(0))*8,
                        Project.EvalToInt(GetValue(1))*8,
                        Project.EvalToInt(GetValue(2))*8);
            }
        }

        public RgbData(Project p, string command, IList<string> values, FileParser parser, IList<int> spacing)
            : base(p, command, values, 2, parser, spacing) {
            }
    }
}
