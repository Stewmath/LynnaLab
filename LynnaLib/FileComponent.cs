using System;
using System.Collections.Generic;

namespace LynnaLib
{
    /// <summary>
    ///  This usually corresponds to a line in a file, but it can generally also be a logical unit
    ///  (ie. a documentation block).
    /// </summary>
    public abstract class FileComponent : Undoable
    {
        protected FileComponentState state;
        protected int suppressUndoRecording = 0;

        // Private variables
        bool _modified;
        Project _project;


        // Properties

        // True if a newline comes after this data
        // Should probably add RecordChange() to the setter? But this breaks things... not so
        // important as it's only ever written to during initial parsing...
        public bool EndsLine
        {
            get { return state.endsLine; }
            set { state.endsLine = value; }
        }

        // True if it's an internally-made object, not to be written back to
        // the file
        public bool Fake
        {
            get { return state.fake; }
            set { state.fake = value; }
        }

        public bool Modified
        {
            get { return _modified; }
            set { _modified = value; }
        }


        public FileComponent Next
        {
            get
            {
                return FileParser?.GetNextFileComponent(this);
            }
        }
        public FileComponent Prev
        {
            get
            {
                return FileParser?.GetPrevFileComponent(this);
            }
        }
        public FileParser FileParser
        {
            get { return state.parser; }
        }
        public Project Project
        {
            get { return _project; }
        }

        protected List<string> Spacing
        {
            get { return state.spacing; }
            set { state.spacing = value; }
        }

        protected FileComponent(FileParser parser, IList<string> spacing, Func<FileComponentState> stateConstructor)
        {
            state = stateConstructor();
            state.constructorFunc = stateConstructor;
            if (parser != null)
                _project = parser.Project;
            EndsLine = true;
            Fake = false;
            if (spacing != null)
                state.spacing = new List<string>(spacing);
            this.state.parser = parser;
            _modified = false;
        }

        public string GetSpacing(int index)
        {
            return Spacing[index];
        }
        public void SetSpacing(int index, string space)
        {
            // TODO: RecordChange()?
            Spacing[index] = space;
        }

        public abstract string GetString();

        // Attaching/detaching from a parser object
        public void Attach(FileParser p)
        {
            if (p == state.parser)
                return;
            if (state.parser != null)
                throw new Exception("Must call 'Detach()' before calling 'Attach(parser)'.");
            RecordChange();
            state.parser = p;
        }
        public void Detach()
        {
            RecordChange();
            FileParser.RemoveFileComponent(this);
            state.parser = null;
        }
        public void InsertIntoParserAfter(Data reference)
        {
            FileParser.InsertComponentAfter(reference, this);
        }
        public void InsertIntoParserBefore(Data reference)
        {
            FileParser.InsertComponentBefore(reference, this);
        }

        protected void SetProject(Project p)
        {
            _project = p;
        }

        /// <summary>
        /// Always call this BEFORE any change is made that affects the state object. It will record
        /// the state before the change is made in order to be able to undo it.
        /// </summary>
        protected void RecordChange()
        {
            if (suppressUndoRecording != 0)
                return;
            Project.UndoState.RecordChange(this);
            Modified = true;
            if (FileParser != null)
                FileParser.Modified = true;
        }

        // ================================================================================
        // Undoable interface functions
        // ================================================================================

        public TransactionState GetState()
        {
            return state;
        }

        public void SetState(TransactionState state)
        {
            this.state = (FileComponentState)state.Copy();
            Modified = true;
            if (FileParser != null)
                FileParser.Modified = true;
        }

        public virtual void InvokeModifiedEvent()
        {
        }

        /// <summary>
        /// Holds the state of a FileComponent. Can be easily replaced for undo/redo actions.
        /// </summary>
        protected class FileComponentState : TransactionState
        {
            public FileParser parser;
            public List<string> spacing;
            public bool endsLine;
            public bool fake;
            public Func<FileComponentState> constructorFunc; // Constructs an instance of the derived class

            public virtual void CopyFrom(FileComponentState state)
            {
                parser = state.parser;
                spacing = new List<string>(state.spacing);
                endsLine = state.endsLine;
                fake = state.fake;
                constructorFunc = state.constructorFunc;
            }

            public virtual TransactionState Copy()
            {
                FileComponentState copy = constructorFunc();
                copy.CopyFrom(this);
                return copy;
            }

            public virtual bool Compare(TransactionState obj)
            {
                if (!(obj is FileComponentState state))
                    return false;
                return parser == state.parser
                    && spacing.SequenceEqual(state.spacing)
                    && endsLine == state.endsLine
                    && fake == state.fake;
            }
        }
    }

    public class StringFileComponent : FileComponent
    {
        public StringFileComponent(FileParser parser, string s, IList<string> spacing)
            : base(parser, spacing, () => new StringFileComponentState())
        {
            State.str = s;
            if (this.Spacing == null)
                this.Spacing = new List<string>();
            while (this.Spacing.Count < 2)
                this.Spacing.Add("");
        }

        // Properties
        StringFileComponentState State { get { return base.state as StringFileComponentState; } }

        // Methods

        public override string GetString()
        {
            return Spacing[0] + State.str + Spacing[1];
        }

        public void SetString(string s)
        {
            RecordChange();
            State.str = s;
        }

        class StringFileComponentState : FileComponentState
        {
            public string str;

            public override void CopyFrom(FileComponentState state)
            {
                str = (state as StringFileComponentState).str;
                base.CopyFrom(state);
            }

            public override bool Compare(TransactionState obj)
            {
                if (!(obj is StringFileComponentState state))
                    return false;
                return base.Compare(obj) && str == state.str;
            }
        }
    }

    public class Label : FileComponent
    {
        readonly string name;

        public string Name
        {
            get { return name; }
        }

        public Label(FileParser parser, string n, IList<string> spacing = null)
            : base(parser, spacing, () => new FileComponentState())
        {
            name = n;
            if (spacing == null)
            {
                this.Spacing = new List<string> { "", "" };
            }
            while (this.Spacing.Count < 2)
                this.Spacing.Add("");
        }

        public override string GetString()
        {
            return Spacing[0] + name + ":" + Spacing[1];
        }
    }
}
