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
    bool barrierOn = false;
    bool doInProgress = false;

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
        Debug.Assert(!doInProgress);

        if (!constructingTransaction.Empty)
            FinalizeTransaction();

        if (undoStack.Count == 0)
            return false;

        doInProgress = true;

        Transaction transaction = undoStack.Pop();
        transaction.Undo();
        redoStack.Push(transaction);

        barrierOn = true;
        doInProgress = false;
        return true;
    }

    public bool Redo()
    {
        Debug.Assert(!doInProgress);

        if (redoStack.Count == 0)
            return false;

        doInProgress = true;

        Debug.Assert(constructingTransaction.Empty);

        Transaction transaction = redoStack.Pop();
        transaction.Redo();
        undoStack.Push(transaction);

        barrierOn = true;
        doInProgress = false;
        return true;
    }

    /// <summary>
    /// Any two transactions immediately before and after calling this cannot be merged. Helps to
    /// split up multiple undo-able operations that would normally be merged into one operation.
    /// </summary>
    public void InsertBarrier()
    {
        Debug.Assert(!doInProgress);
        Debug.Assert(beginTransactionCalls == 0);
        barrierOn = true;
    }

    public void ClearHistory()
    {
        Debug.Assert(!doInProgress);
        Debug.Assert(beginTransactionCalls == 0);

        undoStack.Clear();
        redoStack.Clear();
        constructingTransaction = new Transaction();
        barrierOn = false;
    }

    public void BeginTransaction(string description, bool merge)
    {
        Debug.Assert(!doInProgress);

        if (beginTransactionCalls == 0)
        {
            FinalizeTransaction();

            if (merge && !barrierOn && undoStack.Count > 0 && undoStack.Peek().description == description)
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
        Debug.Assert(!doInProgress);

        beginTransactionCalls--;

        if (beginTransactionCalls < 0)
            throw new Exception("EndTransaction called without a corresponding BeginTransaction");
        else if (beginTransactionCalls == 0)
        {
            FinalizeTransaction();
        }
    }

    /// <summary>
    /// Call this whenever an Undoable's state is about to change, so that we have a chance to
    /// record its initial state to roll it back later.
    /// In principle, we could do without this by storing the initial state of everything and
    /// checking for changes when a transaction is completed. But, this allows us to maintain a list
    /// of _exactly_ what has changed during the current transaction instead of having to check the
    /// state of every single Undoable in the project.
    /// If called multiple times within the same transaction, subsequent calls do nothing.
    /// </summary>
    public void CaptureInitialState(Undoable source)
    {
        Debug.Assert(!doInProgress);

        if (constructingTransaction.deltas.ContainsKey(source))
            return;
        var delta = new TransactionStateHolder<Undoable>(source);
        constructingTransaction.deltas[source] = delta;
        redoStack.Clear();
    }

    /// <summary>
    /// Registers a pair of functions that must be executed during an undo or redo.
    /// The redo function is also called immediately with "false" as the boolean parameter.
    /// This is not the main mechanism that undo/redo is implemented by, but it is quite useful for
    /// ensuring that modified events are triggered on data that is not explicitly tracked as a
    /// StateTransaction.
    /// </summary>
    public void OnRewind(string desc, Action onUndo, Action<bool> onRedo)
    {
        Debug.Assert(!doInProgress);

        Rewindable r = new(desc, onUndo, onRedo);
        constructingTransaction.rewindables.Add(r);
        redoStack.Clear();
        onRedo(false);
    }

    /// <summary>
    /// Like above, but only a single function is given for both undo & redo. It is called immediately.
    /// </summary>
    public void OnRewind(string desc, Action action)
    {
        Debug.Assert(!doInProgress);

        Rewindable r = new(desc, action, (_) => action());
        constructingTransaction.rewindables.Add(r);
        redoStack.Clear();
        action();
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
            barrierOn = false; // Safe to disable the barrier once the undo stack has an additional entry
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
            delta.InvokeModifiedEvents(true);
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
            delta.InvokeModifiedEvents(false);
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
    public void InvokeModifiedEvents(bool undo);
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

    public void InvokeModifiedEvents(bool undo)
    {
        if (undo)
            instance.InvokeModifiedEvent(finalState);
        else
            instance.InvokeModifiedEvent(initialState);
    }
}

/// <summary>
/// A class whose state can be controlled by the undo/redo mechanism through a "TransactionState" object
/// </summary>
public interface Undoable
{
    public TransactionState GetState();
    public void SetState(TransactionState state);
    public void InvokeModifiedEvent(TransactionState prevState);
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
