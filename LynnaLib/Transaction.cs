#nullable enable

namespace LynnaLib;

/// <summary>
/// Keeps track of "transactions" in a project that can be undone/redone. This relates to both
/// undo/redo and to network synchronization.
/// </summary>
public class TransactionManager
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TransactionManager(Project project, int creatorID)
    {
        Project = project;
        _creatorID = creatorID;

        if (creatorID == 0)
        {
            // This is really ugly. I just need to create this temporarily with an invalid CreatorID
            // because I know it will be recreated later.
            constructingTransaction = new Transaction(project, -1, transactionIDCounter++);
        }
        else
            constructingTransaction = new Transaction(project, CreatorID, transactionIDCounter++);
    }
    // ================================================================================
    // Events
    // ================================================================================

    public event Action<LinkedListNode<TransactionNode>>? TransactionAddedEvent;

    // ================================================================================
    // Variables
    // ================================================================================

    const int MAX_UNDOS = 256;

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    Stack<OneOf<Transaction, TransactionBarrier>> undoStack = new();
    Stack<OneOf<Transaction, TransactionBarrier>> redoStack = new();
    public Transaction constructingTransaction;

    int _creatorID = 0;
    int transactionIDCounter = 1;
    int beginTransactionCalls = 0;
    bool doInProgress = false;
    bool disallowUndoThisTransaction = false;

    // ================================================================================
    // Properties
    // ================================================================================

    Project Project { get; }

    // TODO
    public IEnumerable<Transaction>? Transactions { get { return null; } }

    public bool UndoAvailable { get { return GetUndoDescription() != null; } }
    public bool RedoAvailable { get { return GetRedoDescription() != null; } }

    /// <summary>
    /// Whether we're currently performing an undo OR a redo / applying a transaction
    /// </summary>
    public bool IsUndoing { get { return doInProgress; } }

    public int CreatorID
    {
        get
        {
            if (_creatorID == 0)
                throw new Exception("CreatorID unassigned");
            return _creatorID;
        }

        // This is a rather messy public setter. Ideally the CreatorID would be assigned during
        // initialization, but I couldn't quite arrange it that way. We want to ensure that no
        // transactions were created before assigning an ID, though.
        set
        {
            if (_creatorID != 0)
                throw new Exception("Tried to reassign CreatorID?");

            _creatorID = value;

            if (TransactionHistory.Count != 0 || !constructingTransaction.Empty)
                throw new Exception("Tried to assign transaction history after creating transactions?");

            constructingTransaction = new Transaction(Project, CreatorID, transactionIDCounter++);
        }
    }

    bool InTransaction { get { return beginTransactionCalls != 0; } }

    bool HaveIncompleteTransaction { get { return !constructingTransaction.Empty; } }


    // TODO: Make this non-public
    public DictionaryLinkedList<string, TransactionNode> TransactionHistory { get; } = new();

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Attempt to undo, return true on success.
    /// </summary>
    public bool Undo()
    {
        Debug.Assert(!doInProgress);

        if (!constructingTransaction.Empty)
        {
            // This can happen if some data changes without BeginTransaction() ever being called.
            // Ideally this never happens, but we ought to be able to handle this situation.
            log.Warn("Undoing an unfinished transaction");
            FinalizeTransaction();
        }

        return UndoHelper(undoStack, redoStack, redo: false);
    }

    public bool Redo()
    {
        Debug.Assert(!doInProgress);

        if (!constructingTransaction.Empty)
        {
            // Can't redo with an unfinished transaction - it will clear the redo stack anyway.
            log.Warn("Tried to redo with an unfinished transaction");
            FinalizeTransaction();
            return false;
        }

        return UndoHelper(redoStack, undoStack, redo: true);
    }

    /// <summary>
    /// Common implementation for undo & redo. This works by adding new nodes to the transaction
    /// history; undo does NOT remove any nodes from the history. Making the history additive
    /// simplifies synchronization over the network, and makes it possible to undo transactions out
    /// of order (or at least attempt to do so).
    /// </summary>
    bool UndoHelper(Stack<OneOf<Transaction, TransactionBarrier>> mainStack,
                    Stack<OneOf<Transaction, TransactionBarrier>> otherStack,
                    bool redo)
    {
        if (InTransaction)
            throw new Exception("Undo/Redo: Called before EndTransaction");

        doInProgress = true;

        // Pop barriers at the start of the undo stack
        while (mainStack.Count != 0 && mainStack.Peek().IsT1) // Is barrier
        {
            mainStack.Pop();
            otherStack.Push(new TransactionBarrier());
        }

        string? lastDescription = null;
        Transaction? failedTransaction = null;
        bool didAnything = false;

        while (mainStack.Count != 0)
        {
            var next = mainStack.Peek();

            bool shouldBreak = next.Match(
                (transaction) =>
                {
                    // If the transaction doesn't have the same name as the last one, don't keep undoing
                    if (lastDescription != null && lastDescription != transaction.Description)
                    {
                        return true;
                    }
                    // Attempt to unapply the transaction - if successful, add to the transaction history
                    else if (redo ? transaction.Apply() : transaction.Unapply())
                    {
                        TransactionNode newNode = new TransactionNode(transaction, apply: redo);
                        TransactionHistory.AddLast(newNode.NodeID, newNode);
                        lastDescription = transaction.Description;
                        TransactionAddedEvent?.Invoke(TransactionHistory.LastNode);

                        mainStack.Pop();
                        otherStack.Push(transaction);

                        didAnything = true;
                        return false;
                    }
                    // Failed to unapply
                    else
                    {
                        failedTransaction = transaction;
                        return true;
                    }
                },
                (barrier) => { return true; }
            );

            if (shouldBreak)
                break;
        }

        doInProgress = false;

        if (failedTransaction != null)
        {
            log.Info($"Undo/redo failed on transaction {failedTransaction.UniqueID}: {failedTransaction.Description}");
            return false;
        }
        return didAnything;
    }

    public string? GetUndoDescription()
    {
        // Find first entry in undo stack that's not a barrier
        Transaction? lastTransaction = null;
        foreach (var t in undoStack)
        {
            if (t.TryPickT0(out lastTransaction, out var _))
                break;
        }

        if (lastTransaction == null)
            return null;
        string desc = lastTransaction.Description;
        if (desc.Contains("#"))
            return desc.Substring(0, desc.IndexOf('#'));
        else
            return desc;
    }

    public string? GetRedoDescription()
    {
        // Find first entry in redo stack that's not a barrier
        Transaction? nextTransaction = null;
        foreach (var t in redoStack)
        {
            if (t.TryPickT0(out nextTransaction, out var _))
                break;
        }

        if (nextTransaction == null)
            return null;
        string desc = nextTransaction.Description;
        if (desc.Contains("#"))
            return desc.Substring(0, desc.IndexOf('#'));
        else
            return desc;
    }

    /// <summary>
    /// Any two transactions immediately before and after calling this cannot be merged. Helps to
    /// split up multiple undo-able operations that would normally be merged into one operation.
    /// </summary>
    public void InsertBarrier()
    {
        Debug.Assert(!doInProgress);
        Debug.Assert(beginTransactionCalls == 0);

        if (undoStack.Count == 0 || !undoStack.Peek().IsT1)
            undoStack.Push(new TransactionBarrier());
    }

    public void BeginTransaction(string description, bool merge, bool disallowUndo=false)
    {
        Debug.Assert(!doInProgress);

        if (beginTransactionCalls == 0)
        {
            if (FinalizeTransaction())
                log.Warn("BeginTransaction: There was an uncompleted transaction already.");

            if (!merge)
                InsertBarrier();

            constructingTransaction.Description = description;

            // Don't clear redoStack here. There is code that calls BeginTransaction even when it's
            // likely that nothing will actually happen.
        }
        beginTransactionCalls++;
        if (disallowUndo)
            disallowUndoThisTransaction = true;
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
            disallowUndoThisTransaction = false;
        }
    }

    /// <summary>
    /// Call this whenever a TrackedProjectData's state is about to change, so that we have a chance
    /// to record its initial state to roll it back later.
    ///
    /// In principle, we could do without this by storing the initial state of everything and
    /// checking for changes when a transaction is completed. But, this allows us to maintain a list
    /// of _exactly_ what has changed during the current transaction instead of having to check the
    /// state of every single TrackedProjectData in the project.
    ///
    /// If called multiple times within the same transaction, subsequent calls do nothing.
    ///
    /// Some objects call this in their constructors, which they really shouldn't be doing (see
    /// ObjectData due to tweaking its underlying data). It shouldn't have any effect in that case
    /// though, since the object will have already been registered with RegisterNewData. Therefore
    /// this will see that there's no need to capture the initial state because it didn't exist
    /// prior to the current transaction.
    /// </summary>
    public void CaptureInitialState<T>(TrackedProjectData source) where T : TransactionState
    {
        Debug.Assert(!doInProgress);

        if (!InTransaction)
        {
            log.Warn($"Called CaptureInitialState with '{source.FullIdentifier}' with no active transaction");
        }

        if (!source.Project.CheckHasDataType(source.GetType(), source.Identifier))
        {
            throw new Exception("CaptureInitialState called on unregistered data! Usually caused either by calling this from a constructor, or forgetting to call AddDataType with this data.");
        }
        
        constructingTransaction.AddDataIfMissing(source);
    }

    /// <summary>
    /// Called when a new ProjectDataType instance is created.
    /// </summary>
    public void RegisterNewData(Type type, TrackedProjectData data)
    {
        Debug.Assert(!doInProgress);

        if (!InTransaction)
        {
            log.Warn($"Called RegisterNewData with '{data.FullIdentifier}' with no active transaction");
        }

       constructingTransaction.RegisterNewData(type, data);
    }

    /// <summary>
    /// This only works if we're unregistering data that was added within the same transaction!
    /// Don't depend on this too much.
    /// </summary>
    public void UnregisterData(Type type, string identifier)
    {
        Debug.Assert(!doInProgress);

       constructingTransaction.UnregisterData(type, identifier);
    }

    // ================================================================================
    // Transaction history manipulation - used for networking
    // ================================================================================

    /// <summary>
    /// Return true if the transaction was successfully applied; false if it was not (state remains
    /// the same as before). Adds the transaction to the local transaction history if successful.
    ///
    /// If remove=true, this can remove ONLY the most recent transaction (and remove it from history).
    /// </summary>
    public bool ApplyTransactionNode(TransactionNode node, bool remove = false)
    {
        Debug.Assert(!doInProgress);

        if (beginTransactionCalls != 0)
            throw new Exception("ApplyTransaction: Can't call when a transaction is already active");

        if (HaveIncompleteTransaction)
        {
            // TODO: Find a more elegant way to handle this? Reluctant to call FinalizeTransaction()
            // since that will invoke a callback into networking code
            throw new Exception("ApplyTransaction: Can't apply with an incomplete transaction in progress");
        }

        doInProgress = true;

        bool retval;
        if (remove)
        {
            if (node.NodeID != TransactionHistory.Last?.NodeID)
                throw new Exception("ApplyTransaction: Tried to unapply a transaction that wasn't the most recent");

            if (node.Apply)
                retval = node.Transaction.Unapply();
            else
                retval = node.Transaction.Apply();

            if (retval)
                TransactionHistory.RemoveCertain(node.NodeID);
        }
        else
        {
            if (node.Apply)
                retval = node.Transaction.Apply();
            else
                retval = node.Transaction.Unapply();

            if (retval)
                TransactionHistory.AddLast(node.NodeID, node);
        }

        doInProgress = false;

        return retval;
    }

    /// <summary>
    /// Synchronize the current transaction state history with the given alternate history, applying
    /// as many undos and redos as necessary to reach that result.
    /// </summary>
    public void SynchronizeWith(DictionaryLinkedList<string, TransactionNode> remote, string? commonAncestor)
    {
        if (remote.Count == 0)
            throw new Exception("SynchronizeWith: Received empty history?");

        log.Debug($"SynchronizeWith: Last common ancestor: {commonAncestor}");

        // Optimization: Passed common ancestor may not account for client transactions made since
        // the last synchronization. See if there's a more recent common ancestor.
        {
            var rNode = commonAncestor == null ? remote.FirstNode : remote.Find(commonAncestor).Next;
            var lNode = commonAncestor == null ? TransactionHistory.FirstNode : TransactionHistory.Find(commonAncestor).Next;

            while (rNode != null && lNode != null && rNode.Value.NodeID == lNode.Value.NodeID)
            {
                commonAncestor = lNode.Value.NodeID;
                rNode = rNode.Next;
                lNode = lNode.Next;
            }
        }

        log.Debug($"SynchronizeWith: Adjusted last common ancestor: {commonAncestor}");

        if (commonAncestor == remote.Last.NodeID)
        {
            log.Debug("SynchronizeWith: Nothing to do.");
            return;
        }

        // Unapply transactions until we reach a common history.
        while (TransactionHistory.Last?.NodeID != commonAncestor)
        {
            if (TransactionHistory.Last == null)
                throw new Exception("SynchronizeWith: Couldn't find common ancestor");
            if (!ApplyTransactionNode(TransactionHistory.Last, remove: true))
                throw new Exception($"SynchronizeWith: Failed to unapply transaction: {TransactionHistory.Last.NodeID}");
        }

        // TransactionHistory.Last may be null here on the initial data transfer.

        if (TransactionHistory.Last?.NodeID != commonAncestor)
            throw new Exception("SynchronizeWith: Internal error finding common ancestor");

        // Determine the first node to apply
        LinkedListNode<TransactionNode>? firstNode = commonAncestor == null
            ? remote.FirstNode
            : remote.Find(commonAncestor)?.Next;

        if (firstNode == null)
            throw new Exception("SynchronizeWith: Couldn't find first node to apply.");

        // Sanity check: Ensure this really is a common ancestor
        string? localNodeID = TransactionHistory.Last?.NodeID;
        if (localNodeID != firstNode.Previous?.Value.NodeID)
            throw new Exception($"Couldn't find node '{localNodeID}' in remote transaction history");

        // Apply all new transactions.
        ApplyRange(firstNode, remote.LastNode);
    }

    /// <summary>
    /// Apply transactions up to "last" (include both first and last).
    /// </summary>
    void ApplyRange(LinkedListNode<TransactionNode> first, LinkedListNode<TransactionNode> last)
    {
        log.Debug($"ApplyRange: from {first.Value.NodeID} to {last.Value.NodeID}");

        if (first.List != last.List)
            throw new Exception("ApplyRange: Parameters must belong to the same list.");

        LinkedListNode<TransactionNode> node = first;

        while (node != last.Next)
        {
            Debug.Assert(node.Previous?.Value.NodeID == TransactionHistory.Last?.NodeID);

            TransactionNode transaction = node.Value;

            log.Debug($"Local applies transaction {transaction.NodeID}");

            if (!ApplyTransactionNode(transaction))
                throw new Exception("ApplyRange: Failed to apply transactions.");

            log.Debug($"Completed transaction {transaction.NodeID}");

            if (node.Next == null)
            {
                if (last.Next != null)
                    throw new Exception("ApplyRange: last came before first?");
                break;
            }
            node = node.Next;
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Moves constructingTransactions into transactions list if it is non-empty. When this returns,
    /// constructingTransactions is guaranteed to be empty.
    /// </summary>
    bool FinalizeTransaction()
    {
        var t = constructingTransaction;
        bool pushed = false;
        if (HaveIncompleteTransaction)
        {
            t.CaptureFinalState();
            TransactionNode node = new(t, apply: true);
            TransactionHistory.AddLast(node.NodeID, node);
            if (!disallowUndoThisTransaction)
            {
                undoStack.Push(t);
                redoStack.Clear();
            }
            pushed = true;
            TransactionAddedEvent?.Invoke(TransactionHistory.LastNode);
        }
        constructingTransaction = new Transaction(Project, CreatorID, transactionIDCounter++);
        return pushed;
    }
}

/// <summary>
/// History is tracked with "TransactionNode" instances; either an applicaion or de-application of a
/// transaction.
/// </summary>
public class TransactionNode
{
    public TransactionNode(Transaction transaction, bool apply)
    {
        this.Transaction = transaction;
        this.Apply = apply;
        this.applyIndex = this.Transaction.IncrementApplyCounter();
    }

    public TransactionNode(Project p, TransactionNodeDTO dto)
    {
        if (dto.Transaction == null)
            throw new Exception("Null transaction in DTO");
        this.Transaction = new Transaction(p, dto.Transaction);
        this.Apply = dto.Apply;
        this.applyIndex = dto.ApplyIndex;
    }

    // This is an index which uniquely identifies how many times we've undone / redone this
    // transaction, which is necessary to create a unique NodeID for the TransactionNode.
    int applyIndex;

    public Transaction Transaction { get; }

    public bool Apply { get; }

    public string Description
    {
        get { return Transaction.Description; }
    }

    /// <summary>
    /// Different from Transaction "UniqueID": Need to be able to distinguish between applying &
    /// unapplying the same transaction.
    /// </summary>
    public string NodeID
    {
        get
        {
            if (Apply)
                return $"Apply#{applyIndex}-{Transaction.UniqueID}";
            else
                return $"Unapply#{applyIndex}-{Transaction.UniqueID}";
        }
    }

    public TransactionNodeDTO AsDTO()
    {
        return new TransactionNodeDTO()
        {
            Transaction = Transaction.AsDTO(),
            Apply = Apply,
            ApplyIndex = applyIndex,
        };
    }
}

/// <summary>
/// Serializaiton for TransactionNode class
/// </summary>
public class TransactionNodeDTO
{
    // Need an empty constructor for deserialization
    public TransactionNodeDTO()
    {
    }

    public required TransactionDTO? Transaction { get; init; }
    public required bool Apply { get; init; }
    public required int ApplyIndex { get; init; }
}

/// <summary>
/// For the undo/redo stacks
/// </summary>
public class TransactionBarrier();

/// <summary>
/// A "transaction" represents a set of changes that can be undone or redone. It keeps track of both
/// the initial state and the final state of everything that's modified so that this can go in
/// either direction.
/// </summary>
public class Transaction
{
    public Transaction(Project project, int creatorID, int transactionID)
    {
        Project = project;
        CreatorID = creatorID;
        TransactionID = transactionID;

        if (creatorID == NetworkID.Unassigned)
            throw new Exception("Never assigned creatorID");
    }

    public Transaction(Project p, TransactionDTO transactionDTO)
    {
        if (transactionDTO.Description == null || transactionDTO.Deltas == null)
            throw new Exception("Invalid TransactionDTO: null values");
        this.Project = p;
        this.Description = transactionDTO.Description;
        this.CreatorID = transactionDTO.CreatorID;
        this.TransactionID = transactionDTO.TransactionID;
        this.deltas = new();
        foreach ((string key, TransactionStateHolderDTO holderDTO) in transactionDTO.Deltas)
        {
            this.deltas[key] = new TransactionStateHolder(p, holderDTO);
        }
        this.fromDTO = true;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    Dictionary<string, TransactionStateHolder> deltas = new();

    bool fromDTO = false;
    int applyCounter = 0;

    // ================================================================================
    // Properties
    // ================================================================================

    Project Project { get; }
    public string Description { get; set; } = "Unlabelled";
    public int CreatorID { get; }
    public int TransactionID { get; }

    public bool Empty { get { return deltas.Count == 0; } }
    public string UniqueID { get { return Transaction.GetUniqueID(CreatorID, TransactionID); } }

    // ================================================================================
    // Methods
    // ================================================================================

    /// <summary>
    /// Unapply changes made in this transaction (undo)
    /// </summary>
    public bool Unapply()
    {
        log.Debug("Transaction.Unapply: Begin");

        // Check if final states match up to be able to unapply this transaction
        foreach (TransactionStateHolder delta in deltas.Values)
        {
            if (!delta.CanUnapply())
            {
                log.Debug("Transaction failed, not unapplying");
                return false;
            }
        }

        // Update all states (no events should be triggered by these)
        foreach (TransactionStateHolder delta in deltas.Values)
            delta.Unapply();

        log.Debug("Transaction.Unapply: Updated all objects");

        // Trigger events, invoke callbacks for updating non-tracked states, etc
        foreach (TransactionStateHolder delta in deltas.Values)
            delta.InvokeModifiedEvents(true);

        log.Debug("Transaction.Unapply: Completed");

        return true;
    }

    /// <summary>
    /// Apply changes made in this transaction (redo)
    /// </summary>
    public bool Apply()
    {
        log.Debug("Transaction.Apply: Begin");

        // Check if initial states match up to be able to apply this transaction
        foreach (TransactionStateHolder delta in deltas.Values)
        {
            if (!delta.CanApply())
            {
                log.Debug("Transaction failed, not applying");
                return false;
            }
        }

        // Update all states (no events should be triggered by these)
        foreach (TransactionStateHolder delta in deltas.Values)
            delta.Apply();

        log.Debug("Transaction.Apply: Initializing new objects");

        // Invoke special function for newly created objects.
        foreach (TransactionStateHolder delta in deltas.Values)
        {
            if (delta.IsNewCreation)
                delta.InvokeInitializedFromTransfer();
        }

        log.Debug("Transaction.Apply: Updated all objects");

        // Trigger events, invoke callbacks for updating non-tracked states, etc
        foreach (TransactionStateHolder delta in deltas.Values)
            delta.InvokeModifiedEvents(false);

        log.Debug("Transaction.Apply: Completed");

        return true;
    }

    public void CaptureFinalState()
    {
        foreach (TransactionStateHolder delta in deltas.Values)
            delta.CaptureFinalState();
    }

    /// <summary>
    /// Use this instead of AddDataIfMissing for data that's been newly created.
    /// </summary>
    public void RegisterNewData(Type type, TrackedProjectData data)
    {
        if (deltas.ContainsKey(data.FullIdentifier))
        {
            throw new Exception(
                $"Transaction.RegisterNewData: Tried to create '{data.FullIdentifier}' which already existed!");
        }
        var delta = new TransactionStateHolder(data, newlyCreated: true);
        deltas.Add(data.FullIdentifier, delta);
    }

    /// <summary>
    /// This is very rarely called - currently only by the FileParser class when it "changes its
    /// mind" about what data to add.
    /// </summary>
    public void UnregisterData(Type type, string identifier)
    {
        var key = Project.GetFullIdentifier(type, identifier);
        if (!deltas.ContainsKey(key) || !deltas[key].IsNewCreation)
            throw new Exception("Tried to unregister data that we didn't add this transaction!");
        deltas.Remove(key);
    }

    /// <summary>
    /// Only use this for data that's been modified, NOT for newly created data.
    /// </summary>
    public void AddDataIfMissing(TrackedProjectData source)
    {
        if (deltas.ContainsKey(source.FullIdentifier))
            return;
        var delta = new TransactionStateHolder(source, newlyCreated: false);
        deltas[source.FullIdentifier] = delta;
    }

    /// <summary>
    /// This should return a number that has been used only once by this particular transaction. It
    /// is used to construct a unique "NodeID"; unique values for this are necessary for network
    /// communication to work.
    ///
    /// Must be careful regarding synchronization of these values, particularly since (at the time
    /// of writing) there could be multiple Transaction objects representing the same thing as they
    /// are sent over the network. However, if the use of this is limited to the transactions in the
    /// undo stack (local to the client that created the transaction), it should be ok.
    /// </summary>
    public int IncrementApplyCounter()
    {
        if (fromDTO)
            throw new Exception("IncrementApplyCounter: Can't call this from a DTO-initialized instance");
        return applyCounter++;
    }

    public TransactionDTO AsDTO()
    {
        Dictionary<string, TransactionStateHolderDTO> dtoDeltas = new();
        foreach ((string key, TransactionStateHolder holder) in this.deltas)
        {
            dtoDeltas.Add(key, holder.AsDTO());
        }
        TransactionDTO dto = new()
        {
            Description = Description,
            CreatorID = CreatorID,
            TransactionID = TransactionID,
            Deltas = dtoDeltas,
        };
        return dto;
    }

    // ================================================================================
    // Static methods
    // ================================================================================
    public static string GetUniqueID(int creatorID, int transactionID)
    {
        return $"{creatorID}-{transactionID}";
    }
}

/// <summary>
/// Transaction class in de/serializable form.
/// </summary>
public class TransactionDTO
{
    public required string Description { get; init; }
    public required int CreatorID { get; init; }
    public required int TransactionID { get; init; }
    public required Dictionary<string, TransactionStateHolderDTO> Deltas { get; init; }
}

/// <summary>
/// Stores the complete initial and final states for one object in a transaction.
/// </summary>
public class TransactionStateHolder
{
    public TransactionStateHolder(Project p, TransactionStateHolderDTO dto)
    {
        if (dto.FinalState == null || dto.Identifier == null || dto.InstanceType == null || dto.StateType == null)
            throw new Exception("Invalid TransactionStateHolderDTO: null values");

        this.Project = p;
        this.InstanceResolver = new InstanceResolver<TrackedProjectData>(p, dto.InstanceType, dto.Identifier, false);
        this._stateType = Project.GetStateType(dto.StateType);

        this.initialState = dto.InitialState;
        this.finalState = dto.FinalState;
    }

    public TransactionStateHolder(TrackedProjectData instance, bool newlyCreated)
    {
        this.Project = instance.Project;
        this.InstanceResolver = new(instance, resolveImplicitly: false);

        if (newlyCreated)
        {
            this.initialState = null;
        }
        else
        {
            this.initialState = Serialize();
        }
        this.finalState = null;
        //Console.WriteLine($"Initial state: {initialState}");
    }

    // ================================================================================
    // Variables
    // ================================================================================

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    readonly string? initialState; // May be null if object was created this transaction
    string? finalState; // May be null up until CaptureFinalState() is called

    Type? _stateType;

    // ================================================================================
    // Properties
    // ================================================================================

    /// <summary>
    /// If true, the TrackedProjectData in question was created by this transaction.
    /// </summary>
    public bool IsNewCreation { get { return initialState == null; } }

    Project Project { get; }
    InstanceResolver<TrackedProjectData> InstanceResolver { get; }

    Type StateType
    {
        get
        {
            if (_stateType == null)
                _stateType = Instance.GetState().GetType();
            return _stateType;
        }
    }

    // Don't resolve this until it's time to apply the transaction. (This may be unresolved for
    // transactions just sent over the network.)
    TrackedProjectData Instance { get { return InstanceResolver.Instance; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void CaptureFinalState()
    {
        finalState = Serialize();
        //Console.WriteLine($"Final state: {initialState}");
    }

    public bool CanUnapply()
    {
        // Object in question must already exist
        if (!Project.CheckHasDataType(InstanceResolver.InstanceType, InstanceResolver.Identifier))
            return false;

        // Used to ensure "InstanceResolver.ResolvedValue" here, but that doesn't work since this
        // could have just been received from the network, so it's unresolved.

        // Sanity check: Ensure that we don't have some weird situation where the InstanceResolver
        // is pointing to stale data!
        if (InstanceResolver.ResolvedValue && InstanceResolver.Instance != Project.GetDataType(
                InstanceResolver.InstanceType, InstanceResolver.Identifier, false))
        {
            throw new Exception($"CanUnapply: InstanceResolver mismatch on: {InstanceResolver.FullIdentifier}");
        }

        if (finalState == null)
            throw new Exception("CanUnapply: Never called CaptureFinalState() on this transaction?");

        InstanceResolver.Resolve(); // Must resolve in order to serialize

        string currentState = Serialize();
        if (finalState != currentState)
        {
            log.Info($"Couldn't unapply transaction on '{InstanceResolver.FullIdentifier}' due to final state mismatch.\nExpected:\n{finalState}\nActual:\n{currentState}");
            return false;
        }
        return true;
    }

    public bool CanApply()
    {
        // If it was created by this transaction, project must not have it
        if (initialState == null)
        {
            return !Project.CheckHasDataType(InstanceResolver.InstanceType, InstanceResolver.Identifier);
        }

        // Otherwise, project must have it
        if (!Project.CheckHasDataType(InstanceResolver.InstanceType, InstanceResolver.Identifier))
            return false;

        // It's entirely possible for the InstanceResolver to be pointing to stale data here, ie. if
        // "object creation" was undone, then redone, then another future operation depending on
        // that is redone. So, reresolve it. (In any case it must be in a resolved state before
        // serializing.)
        InstanceResolver.Reresolve();

        string currentState = Serialize();
        if (initialState != currentState)
        {
            log.Info($"Couldn't apply transaction on '{InstanceResolver.FullIdentifier}' due to initial state mismatch.\nExpected:\n{initialState}\nActual:\n{currentState}");
            return false;
        }
        return true;
    }

    public void Unapply()
    {
        if (finalState == null)
        {
            throw new Exception(
                $"Can't unapply transaction with no final state. Identifier: {InstanceResolver.FullIdentifier}");
        }

        // Not checking final state here, that's done in "CanUnapply".

        if (initialState == null)
        {
            // Object didn't exist prior to this transaction.

            // We'll unregister the data from the project, but first, let's determine the StateType
            // while we still have the opportunity (we may need to know it for redos later).
            _stateType = Instance.GetState().GetType();

            // Drop our reference to the object, maybe allow it to be garbage collected. (However
            // since duplicate transaction objects can be transmitted over the network, there may
            // still be other dangling references through their InstanceResolvers.)
            Project.RemoveDataType(InstanceResolver.InstanceType, InstanceResolver.Identifier, fromUndo: true);

            // Don't bother calling InstanceResolver.Unresolve() here. It doesn't fix the root of
            // the issue that InstanceResolvers in other instances of this class can point to stale
            // data. Deal with it as needed elsewhere.
        }
        else
        {
            InstanceResolver.Resolve(); // Must resolve in order to deserialize

            if (!Deserialize(initialState))
                throw new Exception("Couldn't deserialize: " + initialState);
        }
    }

    public void Apply()
    {
        log.Debug("Apply: " + InstanceResolver.FullIdentifier);
        if (finalState == null)
        {
            throw new Exception(
                $"Can't apply transaction with no final state. Identifier: {InstanceResolver.FullIdentifier}");
        }

        // Create it if it hasn't been created already
        if (initialState == null)
        {
            TrackedProjectData data = Project.AddExternalData(InstanceResolver.Identifier,
                                                              InstanceResolver.InstanceType,
                                                              StateType,
                                                              finalState);
            // InstanceResolver must update itself to point to the new data
            InstanceResolver.Reresolve();
        }
        else if (initialState != null)
        {
            // Not checking initial state here, that's done in "CanApply". Always call CanApply
            // before this!
            InstanceResolver.Reresolve(); // Should exist already
            if (!Deserialize(finalState))
                throw new Exception("Couldn't deserialize: " + finalState);
        }
    }

    public void InvokeModifiedEvents(bool undo)
    {
        if (undo)
        {
            // Not doing any callbacks for undoing the creation of something...
            if (initialState == null)
                return;

            if (finalState == null)
                throw new Exception("InvokeModifiedEvents: Never called CaptureFinalState()?");
            InvokeUndoEvents(finalState);
        }
        else
        {
            // NOTE: Should data which has just been created call InvokeUndoEvents? I don't know.
            // Some implementations can't handle null parameters (and some TransactionStates may be
            // structs which can't be null!)
            if (initialState == null)
                return;

            InvokeUndoEvents(initialState);
        }
    }

    public void InvokeInitializedFromTransfer()
    {
        Instance.OnInitializedFromTransfer();
    }

    public TransactionStateHolderDTO AsDTO()
    {
        return new TransactionStateHolderDTO()
        {
            InitialState = initialState,
            FinalState = finalState,
            Identifier = InstanceResolver.Identifier,
            InstanceType = InstanceResolver.InstanceType.FullName,
            StateType = StateType.FullName,
        };
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void InvokeUndoEvents(string prevStateStr)
    {
        try
        {
            TransactionState prevState = (TransactionState)Project.Deserialize(StateType, prevStateStr);
            Instance.InvokeUndoEvents(prevState);
        }
        catch (Exception)
        {
            throw;
        }
    }
    
    string Serialize()
    {
        try
        {
            //Helper.Assert(source.Instance.GetState() is T, "Internal error: TransactionState type mismatch");
            TransactionState state = Instance.GetState();
            return Project.Serialize(state.GetType(), state);
        }
        catch (NotSupportedException)
        {
            throw;
        }
    }

    bool Deserialize(string stateStr)
    {
        try
        {
            TransactionState state = (TransactionState)Project.Deserialize(StateType, stateStr);
            if (state == null)
                return false;
            Instance.SetState(state);
            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }
}

/// <summary>
/// TransactionStateHolder class in de/serializable form.
/// </summary>
public class TransactionStateHolderDTO
{
    public TransactionStateHolderDTO()
    {
    }

    public required string? InitialState { get; init; }
    public required string? FinalState { get; init; }
    public required string? Identifier { get; init; }
    public required string? InstanceType { get; init; }
    public required string? StateType { get; init; }
}


/// <summary>
/// Represents a class that can be de/serialized for undo/redo and network communication.
/// </summary>
public abstract class TrackedProjectData : ProjectDataType, Trackable
{
    public TrackedProjectData(Project p, string id) : base(p, id) {}

    public abstract TransactionState GetState();
    public abstract void SetState(TransactionState state);
    public abstract void InvokeUndoEvents(TransactionState oldState);

    /// <summary>
    /// This is invoked after all objects being transferred over have been initialized. (Most
    /// initialization should be done in the state-based constructor, but sometimes that may not be
    /// possible ie. if you need to resolve an InstanceResolver referencing something that may not
    /// have been created yet.
    ///
    /// Try to avoid using this if possible. It's vulnerable to issues regarding loading order.
    /// Finishing initialization outside of the constructor is really not ideal.
    /// </summary>
    public virtual void OnInitializedFromTransfer() {}
}

public abstract class TrackedIndexedProjectData : TrackedProjectData
{
    public TrackedIndexedProjectData(Project p, int index) : base(p, index.ToString())
    {
        this.Index = index;
    }

    public int Index { get; }
}

public interface ProjectDataInstantiator
{
   public static abstract ProjectDataType Instantiate(Project p, string id);
}

public interface IndexedProjectDataInstantiator
{
   public static abstract ProjectDataType Instantiate(Project p, int index);
}

public abstract class TrackedStream : TrackedProjectData, IStream
{
    public TrackedStream(Project p, string id) : base(p, id) {}

    public abstract long Length { get; }
    public abstract long Position { get; set; }

    public event EventHandler<StreamModifiedEventArgs>? ModifiedEvent;

    public abstract long Seek(long dest, System.IO.SeekOrigin origin = System.IO.SeekOrigin.Begin);
    public abstract int Read(byte[] buffer, int offset, int count);
    public abstract int ReadByte();
    public abstract ReadOnlySpan<byte> ReadAllBytes();

    public abstract void Write(byte[] buffer, int offset, int count);
    public abstract void WriteByte(byte value);
    public abstract void WriteAllBytes(ReadOnlySpan<byte> data);


    protected void InvokeModifiedEvent(StreamModifiedEventArgs args)
    {
        ModifiedEvent?.Invoke(this, args);
    }
}

public interface Trackable
{
    public Project Project { get; }

    /// <summary>
    /// </summary>
    public TransactionState GetState();

    /// <summary>
    /// Throws an exception if something goes wrong
    /// </summary>
    public void SetState(TransactionState newState);

    /// <summary>
    /// Called after all undos are finished so that external modified handlers may be invoked once
    /// everything's in a coherent state.
    ///
    /// Currently this is not invoked for undos of object creation (where initial state would be
    /// "null").
    ///
    /// Although this passes the previous transaction state, it is NOT SAFE to resolve any
    /// InstanceResolvers in them. If this is an undo of data being created, the InstanceResolver
    /// will no longer be able to find that data.
    /// </summary>
    public void InvokeUndoEvents(TransactionState prevState);
}

/// <summary>
/// Stub class to help with organization. Implementors of this are means to store "states" that can
/// be serialized with System.Text.Json so they can be sent over the network.
///
/// In general, when modifying a class's State, one should always call
/// "TransactionManager.CaptureInitialState" first, before it is modified. This is how the system
/// knows that something is about to be changed and to serialize its initial state. (Might be nice
/// if I could somehow architect the state classes such that accesses to its fields triggered this
/// automatically somehow... maybe a source generator could create a wrapper around it or
/// something?)
///
/// Serialization notes:
/// - All public fields and properties are serialized by default. (Setter must be public.)
/// - Readonly fields and properties are ignored. (Does this include properties with only a getter?)
/// - Private fields/properties can be serialized with [JsonInclude].
/// - Ignore fields/properties with [JsonIgnore].
///
/// Deserialization notes:
/// - Use [JsonRequired] to be more rigorous by requiring the fields to exist.
/// - It will error out if it provides fields that don't exist
/// - Check for null values even if deserialization succeeds, + any other needed sanity checks.
///
/// What to track as state:
/// - Anything that can change after initialization of the object in a way that can't be recomputed
/// after deserialization.
/// - Anything that's needed to properly instantiate an object after deserialization.
/// - Avoid putting "caches" in the state. It should be possible to re-compute that kind of thing
/// after deserialization. Anyway, it can create false "undo/redo" prompts when cache-like data is
/// tracked (simply from loading new data in without changing anything).
/// </summary>
public interface TransactionState
{
}
