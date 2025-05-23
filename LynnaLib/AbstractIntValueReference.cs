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


    public AbstractIntValueReference(Project project, Func<int> getter, Action<int> setter, int maxValue, int minValue = 0, ValueReferenceType type = ValueReferenceType.Int,
            string constantsMappingString = null)
    : base(project, type, constantsMappingString)
    {
        this.getter = getter;
        this.setter = setter;

        base.MaxValue = maxValue;
        base.MinValue = minValue;
    }

    public AbstractIntValueReference(ValueReference r, int maxValue, int minValue = 0, Func<int> getter = null, Action<int> setter = null)
        : base(r.Project, r.ValueType, r.ConstantsMappingString)
    {
        this.MaxValue = maxValue;
        this.MinValue = minValue;
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
        base.BeginTransaction();
        SetValue(Project.Eval(s));
        base.EndTransaction();
    }
    public override void SetValue(int i)
    {
        if (i > MaxValue)
        {
            log.Warn(string.Format("Tried to set value to {0} (max value is {1})", i, MaxValue));
            i = MaxValue;
        }
        if (i < MinValue)
        {
            log.Warn(string.Format("Tried to set value to {0} (min value is {1})", i, MinValue));
            i = MinValue;
        }
        if (i == GetIntValue())
            return;

        base.BeginTransaction();
        setter(i);
        RaiseModifiedEvent(null);
        base.EndTransaction();
    }

    public override void Initialize()
    {
        throw new NotImplementedException();
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
        var vr = new AbstractIntValueReference(project, getter, setter,
                                               maxValue, minValue, type, constantsMappingString);
        var descriptor = new ValueReferenceDescriptor(vr, name, editable, tooltip);
        return descriptor;
    }
}
