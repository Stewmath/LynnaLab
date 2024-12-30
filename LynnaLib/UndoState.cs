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

    Stack<Transaction> transactions = new Stack<Transaction>();
    public Transaction constructingTransaction = new Transaction();

    int beginTransactionCalls = 0;

    // ================================================================================
    // Properties
    // ================================================================================

    public IEnumerable<Transaction> Transactions { get { return transactions; } }

    public bool UndoAvailable { get { return LastTransaction != null; } }

    Transaction LastTransaction
    {
        get
        {
            if (!constructingTransaction.Empty)
                return constructingTransaction;
            if (transactions.Count != 0)
                return transactions.Peek();
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

        if (transactions.Count == 0)
            return false;

        Transaction transaction = transactions.Pop();
        transaction.Undo();

        return true;
    }

    public void BeginTransaction(string description, bool merge)
    {
        if (beginTransactionCalls == 0)
        {
            FinalizeTransaction();

            if (merge && transactions.Count > 0 && transactions.Peek().description == description)
            {
                // Move the last commited transaction back into constructingTransaction so that
                // upcoming changes are merged into it
                constructingTransaction = transactions.Pop();
            }
            else
            {
                constructingTransaction.description = description;
            }
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
        }
    }

    public void RecordChange(Undoable source)
    {
        if (constructingTransaction.deltas.ContainsKey(source))
            return;
        var delta = new TransactionStateHolder<Undoable>(source);
        constructingTransaction.deltas[source] = delta;
    }

    public string GetLastTransactionDescription()
    {
        if (LastTransaction == null)
            return null;
        string desc = LastTransaction.description;
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
            transactions.Push(t);
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

    public bool Empty { get { return deltas.Count == 0; } }

    public string description = "Unlabelled";
    public Dictionary<object, TransactionDelta> deltas = new Dictionary<object, TransactionDelta>();


    public void Undo()
    {
        foreach (TransactionDelta delta in deltas.Values)
            delta.Rewind();
        foreach (TransactionDelta delta in deltas.Values)
            delta.InvokeModifiedEvents();
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
/// Interface for  classes which can capture state of an object at a given time and can move between
/// the initial and subsequent state on undo/redo operations.
/// At this time the only implementer is "TransactionStateHolder". Perhaps TransactionDelta could be
/// used again in the future for more efficiently storing the deltas rather than always storing the
/// complete initial & final states.
/// </summary>
public interface TransactionDelta
{
    public void CaptureFinalState();
    public void Rewind();
    public void InvokeModifiedEvents();
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

    public void Rewind()
    {
        Debug.Assert(finalState.Compare(instance.GetState()),
                     $"Expected:\n{ObjectDumper.Dump(finalState)}\nActual:\n{ObjectDumper.Dump(instance.GetState())}");
        instance.SetState(initialState);
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
/// </summary>
public abstract class TransactionState
{
    public abstract TransactionState Copy();
    public abstract bool Compare(TransactionState state);
}
