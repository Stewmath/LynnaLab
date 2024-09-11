namespace LynnaLib;

/// <summary>
/// Similar to AbstractIntValueReference
/// </summary>
public class AbstractBoolValueReference : AbstractIntValueReference
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


    // ================================================================================
    // Constuctors
    // ================================================================================

    public AbstractBoolValueReference(Project project,
                                      Func<bool> getter, Action<bool> setter,
                                      ValueReferenceType type = ValueReferenceType.Bool,
                                      string constantsMappingString = null)
        : base(
            project,
            getter: () => getter() ? 1 : 0,
            setter: (v) => setter(v != 0 ? true : false),
            maxValue: 1,
            type: type,
            constantsMappingString: constantsMappingString)
    { }


    // ================================================================================
    // Public methods
    // ================================================================================

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
        Func<bool> getter,
        Action<bool> setter,
        ValueReferenceType type = ValueReferenceType.Int,
        bool editable = true,
        string constantsMappingString = null,
        string tooltip = null)
    {
        var vr = new AbstractBoolValueReference(project, getter, setter, type,
                                                constantsMappingString);
        var descriptor = new ValueReferenceDescriptor(vr, name, editable, tooltip);
        return descriptor;
    }
}
