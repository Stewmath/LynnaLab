namespace LynnaLib;

/// <summary>
/// Keeps track of "transactions" that can be undone/redone.
///
/// Some assumptions are made here, relating to cases this class can't handle:
/// - Files are never created or deleted (be they FileParsers or MemoryFileStreams, etc)
/// </summary>
public class UndoState
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public UndoState()
    {

    }

    // ================================================================================
    // Variables
    // ================================================================================

    Stack<Transaction> undoStack = new();
    Stack<Transaction> redoStack = new();
    public Transaction constructingTransaction = new Transaction();

    int beginTransactionCalls = 0;

    // ================================================================================
    // Properties
    // ================================================================================

    public IEnumerable<Transaction> Transactions { get { return undoStack; } }

    public bool UndoAvailable { get { return GetUndoDescription() != null; } }
    public bool RedoAvailable { get { return GetRedoDescription() != null; } }

    Transaction LastTransaction
    {
        get
        {
            if (!constructingTransaction.Empty)
                return constructingTransaction;
            if (undoStack.Count != 0)
                return undoStack.Peek();
            else
                return null;
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public bool Undo()
    {
        if (!constructingTransaction.Empty)
            FinalizeTransaction();

        if (undoStack.Count == 0)
            return false;

        Transaction transaction = undoStack.Pop();
        transaction.Undo();
        redoStack.Push(transaction);

        return true;
    }

    public bool Redo()
    {
        if (redoStack.Count == 0)
            return false;

        Debug.Assert(constructingTransaction.Empty);

        Transaction transaction = redoStack.Pop();
        transaction.Redo();
        undoStack.Push(transaction);

        return true;
    }

    public void BeginTransaction(string description, bool merge)
    {
        if (beginTransactionCalls == 0)
        {
            FinalizeTransaction();

            if (merge && undoStack.Count > 0 && undoStack.Peek().description == description)
            {
                // Move the last commited transaction back into constructingTransaction so that
                // upcoming changes are merged into it
                constructingTransaction = undoStack.Pop();
            }
            else
            {
                constructingTransaction.description = description;
            }

            redoStack.Clear();
        }
        beginTransactionCalls++;
    }

    public void EndTransaction()
    {
        beginTransactionCalls--;

        if (beginTransactionCalls < 0)
            throw new Exception("EndTransaction called without a corresponding BeginTransaction");
        else if (beginTransactionCalls == 0)
        {
            FinalizeTransaction();
            redoStack.Clear();
        }
    }

    public void RecordChange(Undoable source)
    {
        if (constructingTransaction.deltas.ContainsKey(source))
            return;
        var delta = new TransactionStateHolder<Undoable>(source);
        constructingTransaction.deltas[source] = delta;
        redoStack.Clear();
    }

    public void OnRewind(string desc, Action onUndo, Action<bool> onRedo)
    {
        Rewindable r = new(desc, onUndo, onRedo);
        constructingTransaction.rewindables.Add(r);
        onRedo(false);
        redoStack.Clear();
    }

    public string GetUndoDescription()
    {
        if (LastTransaction == null)
            return null;
        string desc = LastTransaction.description;
        if (desc.Contains("#"))
            return desc.Substring(0, desc.IndexOf('#'));
        else
            return desc;
    }

    public string GetRedoDescription()
    {
        if (redoStack.Count == 0)
            return null;
        string desc = redoStack.Peek().description;
        if (desc.Contains("#"))
            return desc.Substring(0, desc.IndexOf('#'));
        else
            return desc;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Moves constructingTransactions into transactions list if it is non-empty. When this returns,
    /// constructingTransactions is guaranteed to be empty.
    /// </summary>
    void FinalizeTransaction()
    {
        var t = constructingTransaction;
        if (!t.Empty)
        {
            t.CaptureFinalState();
            undoStack.Push(t);
            redoStack.Clear();
        }
        constructingTransaction = new Transaction();
    }
}

/// <summary>
/// A "transaction" represents a set of changes that can be undone or redone. It keeps track of both
/// the initial state and the final state of everything that's modified so that this can go in
/// either direction.
/// </summary>
public class Transaction
{
    public Transaction() { }

    public bool Empty { get { return deltas.Count == 0 && rewindables.Count == 0; } }

    public string description = "Unlabelled";

    // TODO: "deltas" and "rewindables" should be one single list that can be traversed in the order
    // of the changes that occurred
    public Dictionary<object, TransactionDelta> deltas = new Dictionary<object, TransactionDelta>();
    public List<Rewindable> rewindables = new List<Rewindable>();


    public void Undo()
    {
        // Update all states (no events should be triggered by these)
        foreach (TransactionDelta delta in deltas.Values)
            delta.Undo();

        // Trigger events, invoke callbacks for updating non-tracked states, etc
        foreach (TransactionDelta delta in deltas.Values)
            delta.InvokeModifiedEvents();
        foreach (Rewindable r in ((IEnumerable<Rewindable>)rewindables).Reverse())
            r.Undo();
    }

    public void Redo()
    {
        // Update all states (no events should be triggered by these)
        foreach (TransactionDelta delta in deltas.Values)
            delta.Redo();

        // Trigger events, invoke callbacks for updating non-tracked states, etc
        foreach (TransactionDelta delta in deltas.Values)
            delta.InvokeModifiedEvents();
        foreach (Rewindable r in rewindables)
            r.Redo();
    }

    public void CaptureFinalState()
    {
        foreach (TransactionDelta delta in deltas.Values)
            delta.CaptureFinalState();
    }

    public void InvokeModifiedEvents()
    {
        // Invoke modified events after all the data is written. If we wanted to be really careful
        // we'd do this in the reverse order in which the data was modified. But these are
        // Dictionaries so there are no ordering guarantees.
        foreach (Transaction delta in deltas.Values)
        {
            delta.InvokeModifiedEvents();
        }
    }
}

/// <summary>
/// Interface for classes which can capture state of an object at a given time and can move between
/// the initial and subsequent state on undo/redo operations.
/// At this time the only implementer is "TransactionStateHolder". Perhaps TransactionDelta could be
/// used again in the future for more efficiently storing the deltas rather than always storing the
/// complete initial & final states.
/// </summary>
public interface TransactionDelta
{
    public void CaptureFinalState();
    public void Undo();
    public void Redo();
    public void InvokeModifiedEvents();
}

/// <summary>
/// Rewindable keeps track of actions to perform on undo and redo. Less frequently used than
/// TransactionDelta. Handy for knowing that you should invoke certain events on undo/redo
/// operations, for instance (ie. invoke a "chest added event" when undoing a chest removal).
/// </summary>
public class Rewindable
{
    public Rewindable(string description, Action u, Action<bool> r)
    {
        this.description = description;
        onUndo = u;
        onRedo = r;
    }

    Action onUndo;
    Action<bool> onRedo;
    string description;

    public void Undo()
    {
        LogHelper.GetLogger().Info("Undo: " + description);
        onUndo();
    }

    public void Redo()
    {
        LogHelper.GetLogger().Info("Redo: " + description);
        onRedo(true);
    }
}

/// <summary>
/// Implementation of "TransactionDelta" that works by storing the complete initial and final states.
/// </summary>
class TransactionStateHolder<C> : TransactionDelta where C : Undoable
{
    C instance;
    TransactionState initialState, finalState;

    public TransactionStateHolder(C instance)
    {
        this.instance = instance;
        initialState = instance.GetState().Copy();
    }

    public void CaptureFinalState()
    {
        finalState = instance.GetState().Copy();
    }

    public void Undo()
    {
        Debug.Assert(finalState.Compare(instance.GetState()),
                     $"Expected:\n{ObjectDumper.Dump(finalState)}\nActual:\n{ObjectDumper.Dump(instance.GetState())}");
        instance.SetState(initialState);
    }

    public void Redo()
    {
        Debug.Assert(initialState.Compare(instance.GetState()),
                     $"Expected:\n{ObjectDumper.Dump(initialState)}\nActual:\n{ObjectDumper.Dump(instance.GetState())}");
        instance.SetState(finalState);
    }

    public void InvokeModifiedEvents()
    {
        instance.InvokeModifiedEvent();
    }
}

/// <summary>
/// A class whose state can be controlled by the undo/redo mechanism through a "TransactionState" object
/// </summary>
public interface Undoable
{
    public TransactionState GetState();
    public void SetState(TransactionState state);
    public void InvokeModifiedEvent();
}

/// <summary>
/// Base class for state holders, used by classes that implement "Undoable".
/// Default method implementations work well ONLY if the implementer is a value type (struct).
/// </summary>
public interface TransactionState
{
    public TransactionState Copy()
    {
        return this;
    }
    public bool Compare(TransactionState state)
    {
        return this.Equals(state);
    }
}
