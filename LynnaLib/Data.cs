using System;
using System.Collections.Generic;
using System.Linq;
using Util;

namespace LynnaLib
{
    // Args passed by the "AddModifiedEventHandler" function.
    public class DataModifiedEventArgs
    {
        // The index of the value changed (starting at 0), or -1 if potentially all values changed
        // (or size-changing events).
        public readonly int ValueIndex;

        public DataModifiedEventArgs(int valueIndex)
        {
            this.ValueIndex = valueIndex;
        }
    }

    public class Data : FileComponent
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Size in bytes
        // -1 if indeterminate? (consider getting rid of this, it's unreliable)
        readonly int size;

        // Event invoked whenever data is modified
        LockableEvent<DataModifiedEventArgs> dataModifiedEvent = new LockableEvent<DataModifiedEventArgs>();

        // TODO: replace above with this
        public event EventHandler<DataModifiedEventArgs> ModifiedEvent;

        // Event invoked when the "GetString" method is invoked (before it does anything) allowing
        // last-minute adjustments to the data
        public event EventHandler<EventArgs> ResolveEvent;


        // State (things that undo/redo can affect)

        private DataState State { get { return base.state as DataState; } }

        // Command is like .db, .dw, or a macro.
        public string Command
        {
            get { return State.command; }
            set { State.command = value; }
        }
        private List<string> Values
        {
            get { return State.values; }
            set { State.values = value; }
        }
        // If false, don't output the command, only the values
        public bool PrintCommand
        {
            get { return State.printCommand; }
            set { State.printCommand = value; }
        }



        // Properties

        public string CommandLowerCase
        {
            get { return Command.ToLower(); }
        }
        public int Size
        {
            get { return size; }
        }

        public Data NextData
        {
            get
            {
                if (FileParser == null)
                    return null;
                FileComponent c = this;
                do
                {
                    c = FileParser.GetNextFileComponent(c);
                    if (c is Data) return c as Data;
                } while (c != null);
                return c as Data;
            }
        }
        public Data LastData
        {
            get
            {
                if (FileParser == null)
                    return null;
                FileComponent c = this;
                do
                {
                    c = FileParser.GetPrevFileComponent(c);
                    if (c is Data) return c as Data;
                } while (c != null);
                return c as Data;
            }
        }


        // Constructor

        public Data(Project p, string command, IEnumerable<string> values, int size, FileParser parser, IList<string> spacing)
            : base(parser, spacing, () => new DataState())
        {
            base.SetProject(p);
            this.Command = command;
            if (values == null)
                this.Values = new List<string>();
            else
                this.Values = new List<string>(values);
            this.size = size;

            if (this.Spacing == null)
                this.Spacing = new List<string>();
            while (this.Spacing.Count < this.Values.Count + 2)
                this.Spacing.Add("");

            PrintCommand = true;

            dataModifiedEvent += (sender, args) => ModifiedEvent?.Invoke(sender, args);
        }


        public virtual string GetValue(int i)
        {
            if (i >= GetNumValues())
                throw new InvalidLookupException("Value " + i + " is out of range in Data object.");
            return Values[i];
        }
        public int GetIntValue(int i)
        {
            // TODO: error handling
            try
            {
                return Project.EvalToInt(GetValue(i));
            }
            catch (Exception)
            {
                throw;
            }
        }

        public virtual int GetNumValues()
        {
            return Values.Count;
        }


        public virtual void SetValue(int i, string value)
        {
            if (Values[i] != value)
            {
                RecordChange();
                Values[i] = value;
                dataModifiedEvent.Invoke(this, new DataModifiedEventArgs(i));
            }
        }
        public void SetByteValue(int i, byte value)
        {
            SetHexValue(i, value, 2);
        }
        public void SetWordValue(int i, int value)
        {
            SetHexValue(i, value, 4);
        }
        public void SetHexValue(int i, int value, int minDigits)
        {
            SetValue(i, Wla.ToHex(value, minDigits));
        }


        public virtual void AddModifiedEventHandler(EventHandler<DataModifiedEventArgs> handler)
        {
            dataModifiedEvent += handler;
        }
        public virtual void RemoveModifiedEventHandler(EventHandler<DataModifiedEventArgs> handler)
        {
            dataModifiedEvent -= handler;
        }


        public void SetNumValues(int n, string defaultValue)
        {
            while (Values.Count < n)
            {
                InsertValue(Values.Count, defaultValue);
            }
            while (Values.Count > n)
                RemoveValue(Values.Count - 1);
        }

        // Removes a value, deletes the spacing prior to it
        public void RemoveValue(int i)
        {
            RecordChange();
            Values.RemoveAt(i);
            Spacing.RemoveAt(i + 1);
            dataModifiedEvent.Invoke(this, new DataModifiedEventArgs(-1));
        }
        public void InsertValue(int i, string value, string priorSpaces = " ")
        {
            RecordChange();
            Values.Insert(i, value);
            Spacing.Insert(i + 1, priorSpaces);
            dataModifiedEvent.Invoke(this, new DataModifiedEventArgs(-1));
        }

        public override string GetString()
        {
            ResolveEvent?.Invoke(this, null);

            string s = "";
            if (PrintCommand)
                s = GetSpacingIndex(0) + Command;

            int spacingIndex = 1;
            for (int i = 0; i < Values.Count; i++)
            {
                s += GetSpacingIndex(spacingIndex++);
                s += Values[i];
            }
            s += GetSpacingIndex(spacingIndex++);

            return s;
        }

        public void LockModifiedEvents()
        {
            dataModifiedEvent.Lock();
        }

        public void UnlockModifiedEvents()
        {
            dataModifiedEvent.Unlock();
        }

        public void ClearAndUnlockModifiedEvents()
        {
            dataModifiedEvent.Clear();
            dataModifiedEvent.Unlock();
        }

        // Returns data which comes "offset" bytes after this data.
        // Generally only works with ".db" and ".dw" data, otherwise there is no size defined.
        public Data GetDataAtOffset(int offset)
        {
            return FileParser.GetData(this, offset);
        }

        public override void InvokeModifiedEvent()
        {
            dataModifiedEvent.Invoke(this, new DataModifiedEventArgs(-1));
        }


        // Helper function for GetString
        string GetSpacingIndex(int i)
        {
            string space = Spacing[i];
            if (space.Length == 0 && i != 0 && i != Values.Count + 1) space = " ";
            return space;
        }

        public void ThrowException(Exception e)
        {
            throw e;
        }

        class DataState : FileComponent.FileComponentState
        {
            public string command;
            public List<string> values;
            public bool printCommand;

            public override void CopyFrom(FileComponentState state)
            {
                DataState ds = state as DataState;
                command = ds.command;
                values = new List<string>(ds.values);
                printCommand = ds.printCommand;
                base.CopyFrom(state);
            }

            public override bool Compare(TransactionState obj)
            {
                if (!(obj is DataState state))
                    return false;
                return base.Compare(obj)
                    && command == state.command
                    && values.SequenceEqual(state.values)
                    && printCommand == state.printCommand;
            }
        }
    }

    public class RgbData : Data
    {

        public Color Color
        {
            get
            {
                return Color.FromRgb(
                        Project.EvalToInt(GetValue(0)) * 8,
                        Project.EvalToInt(GetValue(1)) * 8,
                        Project.EvalToInt(GetValue(2)) * 8);
            }
            set
            {
                SetByteValue(0, (byte)(value.R >> 3));
                SetByteValue(1, (byte)(value.G >> 3));
                SetByteValue(2, (byte)(value.B >> 3));
            }
        }

        public RgbData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing)
            : base(p, command, values, 2, parser, spacing)
        {
        }
    }
}
