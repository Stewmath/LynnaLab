using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;

namespace LynnaLab
{

    public class Data : FileComponent, ValueReferenceHandler
    {
        // Size in bytes
        // -1 if indeterminate? (consider getting rid of this, it's unreliable)
        protected int size;

        // Command is like .db, .dw, or a macro.
        string command;
        List<string> values;

        // Event called whenever data is modified
        LockableEvent<ValueModifiedEventArgs> dataModifiedEvent = new LockableEvent<ValueModifiedEventArgs>();

        // The ValueReferenceGroup provides an alternate method to access the data (via string
        // names)
        ValueReferenceGroup valueReferenceGroup;


        // Properties

        public string Command {
            get { return command; }
            set { command = value; }
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
                if (parser == null)
                    return null;
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
                if (parser == null)
                    return null;
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
        }


        public bool HasValue(string name) {
            if (valueReferenceGroup == null)
                return false;
            return valueReferenceGroup.HasValue(name);
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

        public virtual int GetNumValues() {
            return values.Count;
        }


        public virtual void SetValue(int i, string value) {
            if (values[i] != value) {
                values[i] = value;
                Modified = true;
                dataModifiedEvent.Invoke(this, null);
            }
        }
        public void SetByteValue(int i, byte value) {
            SetHexValue(i, value, 2);
        }
        public void SetWordValue(int i, int value) {
            SetHexValue(i, value, 4);
        }
        public void SetHexValue(int i, int value, int minDigits) {
            SetValue(i, Wla.ToHex(value, minDigits));
        }


        // Implementation of ValueReferenceHandler
        public virtual string GetValue(string s) { // Get a value based on a value reference name
            ValueReference r = GetValueReference(s);
            if (r == null)
                ThrowException(new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + s + "\"."));
            return r.GetStringValue();
        }
        public virtual int GetIntValue(string s) {
            ValueReference r = GetValueReference(s);
            if (r == null) {
                throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + s + "\".");
            }
            else {
                return r.GetIntValue();
            }
        }
        public virtual void SetValue(string s, string value) {
            GetValueReference(s).SetValue(value);
        }
        public virtual void SetValue(string s, int i) {
            GetValueReference(s).SetValue(i);
        }
        public virtual void AddValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler) {
            dataModifiedEvent += handler;
        }
        public virtual void RemoveValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler) {
            dataModifiedEvent -= handler;
        }
        // End of ValueReferenceHandler implementation


        public void SetNumValues(int n, string defaultValue) {
            while (values.Count < n) {
                InsertValue(values.Count, defaultValue);
            }
            while (values.Count > n)
                RemoveValue(values.Count-1);
        }

        // Removes a value, deletes the spacing prior to it
        public void RemoveValue(int i) {
            values.RemoveAt(i);
            spacing.RemoveAt(i+1);
            Modified = true;
            dataModifiedEvent.Invoke(this, null);
        }
        public void InsertValue(int i, string value, string priorSpaces=" ") {
            values.Insert(i, value);
            spacing.Insert(i+1, priorSpaces);
            Modified = true;
            dataModifiedEvent.Invoke(this, null);
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
            SetValueReferences(new ValueReferenceGroup(references));
        }
        public void SetValueReferences(ValueReferenceGroup group) {
            valueReferenceGroup = group;
            foreach (ValueReference r in valueReferenceGroup.GetValueReferences())
                r.SetHandler(this);
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

        public void LockModifiedEvents() {
            dataModifiedEvent.Lock();
        }

        public void UnlockModifiedEvents() {
            dataModifiedEvent.Unlock();
        }

        public void ClearAndUnlockModifiedEvents() {
            dataModifiedEvent.Clear();
            dataModifiedEvent.Unlock();
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
