namespace LynnaLib;

public enum ValueReferenceType
{
    String = 0,
    Int,
    Bool
}

// This is a stub for now
public class ValueModifiedEventArgs : EventArgs
{
}


/// <summary>
/// A ValueReference is a reference to some kind of data, usually a "Data" instance, but could also
/// be from a file stream or something more abstract.
/// </summary>
public abstract class ValueReference
{

    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    // ================================================================================
    // Constuctors
    // ================================================================================

    public ValueReference(Project project,
                          ValueReferenceType type, string constantsMappingString)
    {
        Project = project;
        ValueType = type;

        if (constantsMappingString != null)
        {
            this.ConstantsMappingString = constantsMappingString;
            constantsMapping = (ConstantsMapping)typeof(Project).GetField(ConstantsMappingString)
                .GetValue(Project);
        }
    }


    // ================================================================================
    // Variables
    // ================================================================================

    ConstantsMapping constantsMapping;


    // ================================================================================
    // Properties
    // ================================================================================

    public Project Project
    {
        get; private set;
    }
    public int MaxValue { get; protected set; }
    public int MinValue { get; protected set; }

    // TODO: Move this to ValueReferenceDescriptor
    public ValueReferenceType ValueType { get; protected set; }


    // Other properties

    public string ConstantsMappingString { get; private set; }
    public ConstantsMapping ConstantsMapping { get { return constantsMapping; } }

    public event EventHandler<ValueModifiedEventArgs> ModifiedEvent;


    // ================================================================================
    // Public methods
    // ================================================================================

    public abstract string GetStringValue();
    public abstract int GetIntValue();
    public abstract void SetValue(string s);
    public abstract void SetValue(int i);

    // TODO: Remove these functions in favor of just using the Modified event
    public void AddValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler)
    {
        ModifiedEvent += handler;
    }
    public void RemoveValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler)
    {
        ModifiedEvent -= handler;
    }

    // Subclasses must call this to raise the event
    protected void RaiseModifiedEvent(ValueModifiedEventArgs args)
    {
        ModifiedEvent?.Invoke(this, args);
    }

    // Sets the value to its default.
    public abstract void Initialize();
}
