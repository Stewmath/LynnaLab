namespace LynnaLib;

/// <summary>
/// T1: The object type being counted
/// T2: The type that holds references to T1
/// </summary>
public class RefCounter<T1, T2> : TrackedProjectData
    where T1 : ProjectDataType
    where T2 : ProjectDataType
{
    // ================================================================================
    // Constructors
    // ================================================================================
    internal RefCounter(Project p, T1 instance)
        : base(p, ComputeIdentifier(instance))
    {
        state = new()
        {
            ReferencedObject = new(instance),
            Referrers = new(),
        };
    }

    /// <summary>
    /// State-based constructor, for network transfer (located via reflection)
    /// </summary>
    private RefCounter(Project p, string id, TransactionState s)
        : base(p, id)
    {
        this.state = (State)s;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    class State : TransactionState
    {
        public required InstanceResolver<T1> ReferencedObject { get; init; }
        public required HashSet<InstanceResolver<T2>> Referrers { get; init; }
    }

    State state;

    // ================================================================================
    // Properties
    // ================================================================================

    public int Count { get { return state.Referrers.Count; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public int AddReference(T2 referrer)
    {
        Project.TransactionManager.CaptureInitialState<State>(this);

        if (!state.Referrers.Add(new(referrer)))
            throw new Exception($"AddReference: Already contained a reference to {referrer}");
        return state.Referrers.Count;
    }

    public int RemoveReference(T2 referrer)
    {
        Project.TransactionManager.CaptureInitialState<State>(this);

        if (!state.Referrers.Remove(new(referrer)))
            throw new Exception($"AddReference: Already contained a reference to {referrer}");
        return state.Referrers.Count;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    // ================================================================================
    // TrackedProjectData implementation
    // ================================================================================

    public override TransactionState GetState()
    {
        return state;
    }

    public override void SetState(TransactionState state)
    {
        this.state = (State)state;
    }

    public override void InvokeUndoEvents(TransactionState oldState)
    {
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    public static string ComputeIdentifier(T1 instance)
    {
        return typeof(T2).ToString() + "::" + instance.FullIdentifier;
    }
}
