namespace LynnaLib;

/// <summary>
/// Wrapper around a ValueReference that implicitly converts to an int (or back to a ValueReference).
/// </summary>
public class IntValueReferenceWrapper
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public IntValueReferenceWrapper(ValueReference vr)
    {
        this.ValueReference = vr;
    }

    // ================================================================================
    // Properties
    // ================================================================================

    public ValueReference ValueReference { get; private set; }

    // ================================================================================
    // Implicit conversion
    // ================================================================================
    public static implicit operator int(IntValueReferenceWrapper wrapper)
    {
        return wrapper.ValueReference.GetIntValue();
    }

    public static implicit operator ValueReference(IntValueReferenceWrapper wrapper)
    {
        return wrapper.ValueReference;
    }

    public static implicit operator IntValueReferenceWrapper(ValueReference vr)
    {
        return new IntValueReferenceWrapper(vr);
    }
}

/// <summary>
/// Wrapper around a ValueReference that implicitly converts to a bool (or back to a ValueReference).
/// </summary>
public class BoolValueReferenceWrapper
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public BoolValueReferenceWrapper(ValueReference vr)
    {
        this.ValueReference = vr;
    }

    // ================================================================================
    // Properties
    // ================================================================================

    public ValueReference ValueReference { get; private set; }

    // ================================================================================
    // Implicit conversion
    // ================================================================================
    public static implicit operator bool(BoolValueReferenceWrapper wrapper)
    {
        return wrapper.ValueReference.GetIntValue() != 0;
    }

    public static implicit operator ValueReference(BoolValueReferenceWrapper wrapper)
    {
        return wrapper.ValueReference;
    }

    public static implicit operator BoolValueReferenceWrapper(ValueReference vr)
    {
        return new BoolValueReferenceWrapper(vr);
    }
}
