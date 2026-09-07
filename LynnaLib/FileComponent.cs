using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LynnaLib;

/// <summary>
///  This usually corresponds to a line in a file, but it can generally also be a logical unit
///  (ie. a documentation block).
/// </summary>
public abstract class FileComponent : TrackedProjectData
{
    protected FileComponentState state;
    protected int suppressUndoRecording = 0;

    // Private variables
    bool _modified;


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


    public FileComponent? Next
    {
        get
        {
            return FileParser?.GetNextFileComponent(this);
        }
    }
    public FileComponent? Prev
    {
        get
        {
            return FileParser?.GetPrevFileComponent(this);
        }
    }
    // This can be null, if the data is orphaned.
    public FileParser? FileParser
    {
        get { return state.parser?.Instance; }
    }

    protected List<string> Spacing
    {
        get { return state.spacing; }
        set { state.spacing = value; }
    }

    protected FileComponent(Project p, string id, FileComponentState state)
        : base(p, id)
    {
        this.state = state;
        _modified = false;
    }

    /// <summary>
    /// State-based constructor, for network transfer
    /// </summary>
    protected FileComponent(Project p, string id, TransactionState state)
        : base(p, id)
    {
        this.state = (FileComponentState)state;
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

    /// <summary>
    /// Called immediately before "GetString()" when the FileComponent is about to be written
    /// back to the file (or when generating the string).
    /// </summary>
    public virtual void AboutToSave() { }

    public abstract string GetString();

    // Attaching/detaching from a parser object
    public void Attach(FileParser p)
    {
        if (p == state.parser?.Instance)
            return;
        if (state.parser != null)
            throw new Exception("Must call 'Detach()' before calling 'Attach(parser)'.");
        if (p == null)
            throw new Exception("Can't attach to null parser");
        RecordChange();
        state.parser = new(p);
    }
    public virtual void Detach()
    {
        VerifyFileParserExists("Detach");
        RecordChange();
        FileParser.RemoveFileComponent(this);
        state.parser = null;
    }
    public void InsertIntoParserAfter(Data reference)
    {
        VerifyFileParserExists("InsertIntoParserAfter");
        FileParser.InsertComponentAfter(reference, this);
    }
    public void InsertIntoParserBefore(Data reference)
    {
        VerifyFileParserExists("InsertIntoParserBefore");
        FileParser.InsertComponentBefore(reference, this);
    }

    /// <summary>
    /// Always call this BEFORE any change is made that affects the state object. It will record
    /// the state before the change is made in order to be able to undo it.
    /// </summary>
    protected void RecordChange()
    {
        if (suppressUndoRecording != 0)
            return;
        state.CaptureInitialState(this);
        Modified = true;
        if (FileParser != null)
            FileParser.Modified = true;
    }

    [MemberNotNull(nameof(FileParser))]
    protected void VerifyFileParserExists(string error)
    {
        ArgumentNullException.ThrowIfNull(FileParser,
            $"Can't call {error} without being attached to a FileParser");
    }

    // ================================================================================
    // TrackedProjectData interface functions
    // ================================================================================

    public override TransactionState GetState()
    {
        return state;
    }

    public override void SetState(TransactionState state)
    {
        this.state = (FileComponentState)state;
        Modified = true;
        if (FileParser != null)
            FileParser.Modified = true;
    }

    public override void InvokeUndoEvents(TransactionState prevState)
    {
    }

    /// <summary>
    /// Holds the state of a FileComponent. Can be easily replaced for undo/redo actions.
    /// </summary>
    protected class FileComponentState : TransactionState
    {
        public InstanceResolver<FileParser>? parser;
        public List<string> spacing;
        public bool endsLine;
        public bool fake;

        public FileComponentState(FileParser? p, IList<string>? s)
        {
            parser = p == null ? null : new(p);
            spacing = s == null
                ? new List<string>()
                : new List<string>(s);

            this.endsLine = true;
            this.fake = false;
        }

        // For deserialization only
        [JsonConstructor]
        protected FileComponentState()
        {
            spacing = null!;
        }

        public virtual void CaptureInitialState(FileComponent parent)
        {
            parent.Project.TransactionManager.CaptureInitialState<FileComponentState>(parent);
        }
    }
}

public class StringFileComponent : FileComponent
{
    public StringFileComponent(string id, FileParser parser, string str, IList<string>? spacing)
        : base(parser.Project, id, new StringFileComponentState(parser, spacing, str))
    {
        if (this.Spacing == null)
            this.Spacing = new List<string>();
        while (this.Spacing.Count < 2)
            this.Spacing.Add("");
    }

    /// <summary>
    /// State-based constructor, for network transfer (located via reflection)
    /// </summary>
    private StringFileComponent(Project p, string id, TransactionState state)
        : base(p, id, state)
    {
    }

    // Properties
    StringFileComponentState State { get { return (StringFileComponentState)base.state; } }

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

        public StringFileComponentState(FileParser? p, IList<string>? spacing, string str)
            : base(p, spacing)
        {
            this.str = str;
        }

        // For deserialization only
        [JsonConstructor]
        protected StringFileComponentState() : base()
        {
            str = null!;
        }

        public override void CaptureInitialState(FileComponent parent)
        {
            parent.Project.TransactionManager.CaptureInitialState<StringFileComponentState>(parent);
        }
    }
}

public class Label : FileComponent
{
    class LabelState : FileComponent.FileComponentState
    {
        public string name;

        public LabelState(FileParser? p, IList<string>? s, string name)
            : base(p, s)
        {
            this.name = name;
        }

        // For deserialization only
        [JsonConstructor]
        protected LabelState() : base()
        {
            name = null!;
        }

        public override void CaptureInitialState(FileComponent parent)
        {
            parent.Project.TransactionManager.CaptureInitialState<LabelState>(parent);
        }
    }

    LabelState State { get { return (LabelState)base.state; } }

    public string Name
    {
        get { return State.name; }
    }

    public Label(string id, FileParser parser, string n, IList<string>? spacing = null)
        : base(parser.Project, id, new LabelState(parser, spacing, n))
    {
        if (spacing == null)
        {
            this.Spacing = new List<string> { "", "" };
        }
        while (this.Spacing.Count < 2)
            this.Spacing.Add("");
    }

    /// <summary>
    /// State-based constructor, for network transfer (located via reflection)
    /// </summary>
    private Label(Project p, string id, TransactionState state)
        : base(p, id, state)
    {
    }

    public override string GetString()
    {
        return Spacing[0] + Name + ":" + Spacing[1];
    }
}
