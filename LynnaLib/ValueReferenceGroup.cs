namespace LynnaLib;

/// <summary>
/// This class contains a list of ValueReferences and allows you to look them up by name to get
/// or set them.
/// </summary>
public class ValueReferenceGroup
{
    readonly IList<ValueReferenceDescriptor> descriptors;
    readonly LockableEvent<ValueModifiedEventArgs> lockableModifiedEvent = new LockableEvent<ValueModifiedEventArgs>();

    string transactionDescription;
    bool transactionMerge;


    public ValueReferenceGroup(IList<ValueReferenceDescriptor> refs)
    {
        lockableModifiedEvent += (sender, args) => ModifiedEvent?.Invoke(sender, args);
        descriptors = new List<ValueReferenceDescriptor>();
        foreach (var desc in refs)
        {
            descriptors.Add(desc);

            desc.ValueReference.AddValueModifiedHandler(
                (sender, args) => lockableModifiedEvent?.Invoke(sender, args));
        }
    }


    public event EventHandler<ValueModifiedEventArgs> ModifiedEvent;



    // Properties

    public Project Project
    {
        get
        {
            if (descriptors.Count == 0)
                return null;
            return descriptors[0].Project;
        }
    }

    public int Count
    {
        get { return descriptors.Count; }
    }


    // Indexers

    public ValueReferenceDescriptor this[int i]
    {
        get { return descriptors[i]; }
    }
    public ValueReferenceDescriptor this[string name]
    {
        get
        {
            return GetDescriptor(name);
        }
    }


    // Public methods

    public IList<ValueReferenceDescriptor> GetDescriptors()
    {
        return descriptors;
    }
    public ValueReferenceDescriptor GetDescriptor(string name)
    {
        foreach (ValueReferenceDescriptor desc in descriptors)
        {
            if (desc.Name == name)
            {
                return desc;
            }
        }
        throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
    }

    public int GetIndexOf(ValueReferenceDescriptor r)
    {
        int i = 0;
        foreach (ValueReferenceDescriptor s in descriptors)
        {
            if (s.Name == r.Name)
                return i;
            i++;
        }
        return -1;
    }

    public bool HasValue(string name)
    {
        foreach (var r in descriptors)
            if (r.Name == name)
                return true;
        return false;
    }


    public string GetValue(string name)
    {
        return GetDescriptor(name).GetStringValue();
    }
    public int GetIntValue(string name)
    {
        ValueReferenceDescriptor desc = GetDescriptor(name);
        return desc.GetIntValue();
    }

    public void SetValue(string name, string value)
    {
        ValueReferenceDescriptor desc = GetDescriptor(name);
        desc.SetValue(value);
    }
    public void SetValue(string name, int value)
    {
        ValueReferenceDescriptor desc = GetDescriptor(name);
        desc.SetValue(value);
    }

    // TODO: remove these, use the public event instead
    public void AddValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler)
    {
        ModifiedEvent += handler;
    }
    public void RemoveValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler)
    {
        ModifiedEvent -= handler;
    }

    /// Call this to prevent events from firing until EndAtomicOperation is called.
    public void BeginAtomicOperation()
    {
        lockableModifiedEvent.Lock();
        // TODO: Would be ideal if this also locked events for the ValueReferences themselves
    }

    public void EndAtomicOperation()
    {
        lockableModifiedEvent.Unlock();
    }

    public void CopyFrom(ValueReferenceGroup vrg)
    {
        BeginTransaction();
        BeginAtomicOperation();

        foreach (var desc in descriptors)
        {
            desc.SetValue(vrg.GetValue(desc.Name));
        }

        EndAtomicOperation();
        EndTransaction();
    }

    public void EnableTransactions(string description, bool merge = false)
    {
        transactionDescription = $"{description}#All fields";
        transactionMerge = merge;
        foreach (var descriptor in descriptors)
        {
            string desc = $"{description}#{descriptor.Name}";
            descriptor.ValueReference.EnableTransactions(desc, merge);
        }
    }


    // ================================================================================
    // Protected methods
    // ================================================================================

    // ================================================================================
    // Private methods
    // ================================================================================

    void BeginTransaction()
    {
        if (transactionDescription != null)
            Project.BeginTransaction(transactionDescription, transactionMerge);
    }

    void EndTransaction()
    {
        if (transactionDescription != null)
            Project.EndTransaction();
    }
}
