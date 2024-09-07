namespace LynnaLib;

/// <summary>
/// Wrapper over a ValueReference providing additional information for editing such as
/// documentation, tooltip, etc.
/// </summary>
public class ValueReferenceDescriptor
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ValueReferenceDescriptor(ValueReference valueReference,
                                    string name,
                                    bool editable = true,
                                    string tooltip = null)
    {
        this.valueReference = valueReference;
        this.Name = name;
        this.Editable = editable;
        this.Tooltip = tooltip;

        if (ValueReference.ConstantsMapping != null)
        {
            Documentation = valueReference.ConstantsMapping.OverallDocumentation;
            Documentation.Name = "Field: " + Name;
        }
    }

    /// <summary>
    /// Clones a ValueReferenceDescriptor by instantiating a new instance of its underlying
    /// ValueReference and using that for the new ValueReferenceDescriptor.
    /// </summary>
    public ValueReferenceDescriptor(ValueReferenceDescriptor desc)
    {
        this.valueReference = desc.ValueReference.Clone();
        this.Name = desc.Name;
        this.Editable = desc.Editable;
        this.Tooltip = desc.Tooltip;
        this.Documentation = desc.Documentation;
    }

    // ================================================================================
    // Variables
    // ================================================================================
    ValueReference valueReference;

    // ================================================================================
    // Properties
    // ================================================================================

    public Project Project { get { return valueReference.Project; } }
    public ValueReference ValueReference { get { return valueReference; } }

    public string Name { get; private set; }
    public bool Editable { get; private set; }
    public string Tooltip { get; private set; }

    // This documentation tends to change based on what the current value is...
    public Documentation Documentation { get; private set; }


    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    ///  Returns a field from documentation (ie. "@desc{An interaction}").
    /// </summary>
    public string GetDocumentationField(string name)
    {
        if (Documentation == null)
            return null;
        return Documentation.GetField(name);
    }


    // Passthrough functions to ValueReference for convenience

    public string GetStringValue()
    {
        return ValueReference.GetStringValue();
    }

    public int GetIntValue()
    {
        return ValueReference.GetIntValue();
    }

    public void SetValue(string s)
    {
        ValueReference.SetValue(s);
    }

    public void SetValue(int i)
    {
        ValueReference.SetValue(i);
    }
}
