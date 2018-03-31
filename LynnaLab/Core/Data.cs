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

        // Event called whenever data is modified
        event EventHandler dataModifiedEvent;

        // The ValueReferenceGroup provides an alternate method to access the data (via string
        // names)
        ValueReferenceGroup valueReferenceGroup;


        // Properties

        public bool Modified {
            get { return _modified; }
            set {
                _modified = value;
                if (value == true)
                    parser.Modified = true;
            }
        }

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

        public Data(Project p, string command, IEnumerable<string> values, int size, FileParser parser, IList<string> spacing) : base(parser, spacing) {
            base.SetProject(p);
            this.command = command;
            if (values == null)
                this.values = new List<string>();
            else
                this.values = new List<string>(values);
            this.size = size;

            if (this.spacing == null)
                this.spacing = new List<string>();
            while (this.spacing.Count < this.values.Count+2)
                this.spacing.Add("");

            PrintCommand = true;
            _modified = false;
        }


        public virtual string GetValue(int i) {
            if (i >= GetNumValues())
                throw new InvalidLookupException("Value " + i + " is out of range in Data object.");
            return values[i];
        }
        public int GetIntValue(int i) {
            // TODO: error handling
            try {
                return Project.EvalToInt(GetValue(i));
            }
            catch (Exception e) {
                throw e;
            }
        }
        public string GetValue(string s) { // Get a value based on a value reference name
            ValueReference r = GetValueReference(s);
            if (r == null)
                ThrowException(new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + s + "\"."));
            return r.GetStringValue();
        }
        public int GetIntValue(string s) {
            ValueReference r = GetValueReference(s);
            if (r == null) {
                throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + s + "\".");
            }
            else {
                return r.GetIntValue();
            }
        }

        public virtual int GetNumValues() {
            return values.Count;
        }


        public virtual void SetValue(int i, string value) {
            if (values[i] != value) {
                values[i] = value;
                Modified = true;
                if (dataModifiedEvent != null)
                    dataModifiedEvent(this, null);
            }
        }
        public void SetByteValue(int i, byte value) {
            SetValue(i, Wla.ToByte(value));
        }
        public void SetWordValue(int i, int value) {
            SetValue(i, Wla.ToWord(value));
        }
        public void SetValue(string s, string value) {
            GetValueReference(s).SetValue(value);
        }
        public void SetValue(string s, int i) {
            GetValueReference(s).SetValue(i);
        }

        public void SetNumValues(int n) {
            while (values.Count < n) {
                InsertValue(values.Count, ".");
            }
            while (values.Count > n)
                RemoveValue(values.Count-1);
        }

        // Removes a value, deletes the spacing prior to it
        public void RemoveValue(int i) {
            values.RemoveAt(i);
            spacing.RemoveAt(i+1);
            Modified = true;
        }
        public void InsertValue(int i, string value, string priorSpaces=" ") {
            values.Insert(i, value);
            spacing.Insert(i+1, priorSpaces);
            Modified = true;
        }

        public ValueReferenceGroup GetValueReferenceGroup() {
            return valueReferenceGroup;
        }
        public IList<ValueReference> GetValueReferences() {
            if (valueReferenceGroup == null)
                return null;
            return valueReferenceGroup.GetValueReferences();
        }
        public ValueReference GetValueReference(string name) {
            if (valueReferenceGroup == null)
                return null;
            return valueReferenceGroup.GetValueReference(name);
        }
        public void SetValueReferences(IList<ValueReference> references) {
            valueReferenceGroup = new ValueReferenceGroup(references);
            foreach (ValueReference r in valueReferenceGroup.GetValueReferences())
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
            parser.InsertComponentAfter(reference, this);
        }
        public void InsertIntoParserBefore(Data reference) {
            parser.InsertComponentBefore(reference, this);
        }

        // Data events
        public void AddDataModifiedHandler(EventHandler handler) {
            dataModifiedEvent += handler;
        }
        public void RemoveDataModifiedHandler(EventHandler handler) {
            dataModifiedEvent -= handler;
        }

        // Helper function for GetString
        string GetSpacingIndex(int i) {
            string space = spacing[i];
            if (space.Length == 0 && i != 0 && i != values.Count+1) space = " ";
            return space;
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

        public RgbData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing)
            : base(p, command, values, 2, parser, spacing)
        {
        }
    }
}
