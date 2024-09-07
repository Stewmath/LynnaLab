namespace LynnaLib;

/// <summary>
/// A ValueReference which isn't directly tied to any data; instead it takes getter and setter
/// functions for data modifications.
/// A caveat about using this: if this is used as a layer on top of actual Data values, then if
/// those Data values are changed, the event handlers installed by "AddValueModifiedHandler"
/// won't trigger. They will only trigger if modifications are made through this class.
/// </summary>
public class AbstractIntValueReference : ValueReference
{

    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


    // ================================================================================
    // Constructors
    // ================================================================================


    public AbstractIntValueReference(Project project, string name, Func<int> getter, Action<int> setter, int maxValue, int minValue = 0, ValueReferenceType type = ValueReferenceType.Int,
            string constantsMappingString = null)
    : base(project, name, type, constantsMappingString)
    {
        this.getter = getter;
        this.setter = setter;

        base.MaxValue = maxValue;
        base.MinValue = minValue;
    }

    public AbstractIntValueReference(AbstractIntValueReference r)
    : base(r)
    {
        this.getter = r.getter;
        this.setter = r.setter;
    }

    public AbstractIntValueReference(ValueReference r, Func<int> getter = null, Action<int> setter = null)
    : base(r)
    {
        this.getter = getter;
        this.setter = setter;

        if (this.getter == null)
            this.getter = r.GetIntValue;
        if (this.setter == null)
            this.setter = r.SetValue;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Func<int> getter;
    Action<int> setter;

    // ================================================================================
    // Public methods
    // ================================================================================

    public override string GetStringValue()
    {
        return Wla.ToHex(GetIntValue(), 2);
    }
    public override int GetIntValue()
    {
        return getter();
    }
    public override void SetValue(string s)
    {
        SetValue(Project.EvalToInt(s));
    }
    public override void SetValue(int i)
    {
        if (i > MaxValue)
        {
            log.Warn(string.Format("Tried to set \"{0}\" to {1} (max value is {2})", Name, i, MaxValue));
            i = MaxValue;
        }
        if (i == GetIntValue())
            return;
        setter(i);
        RaiseModifiedEvent(null);
    }

    public override void Initialize()
    {
        throw new NotImplementedException();
    }

    public override ValueReference Clone()
    {
        return new AbstractIntValueReference(this);
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    /// <summary>
    /// Helper function to create a DataValueReference wrapped around a ValueReferenceDescriptor in
    /// a single function call.
    /// </summary>
    public static ValueReferenceDescriptor Descriptor(
        Project project,
        string name,
        Func<int> getter,
        Action<int> setter,
        int maxValue,
        int minValue = 0,
        ValueReferenceType type = ValueReferenceType.Int,
        bool editable = true,
        string constantsMappingString = null,
        string tooltip = null)
    {
        var vr = new AbstractIntValueReference(project, name, getter, setter,
                                               maxValue, minValue, type, constantsMappingString);
        var descriptor = new ValueReferenceDescriptor(vr, editable, tooltip);
        return descriptor;
    }
}
