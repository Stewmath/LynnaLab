#nullable enable

using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LynnaLib;

public enum NetworkRole
{
    Standalone = 0, // Not connected to other instances (could become Server later, but not Client)
    Server = 1, // This is the server
    Client = 2, // This is a client
}

enum NetworkCommand
{
    ServerHello = 1,
    ClientHello,
    NewTransactions,
    Ack,
}

// This is a class instead of an enum since we don't want to have to cast the values manually
public class NetworkID
{
    public const int Unassigned = 0;
    public const int Server = 1;
    // Anything beyond here is a valid ID for clients
}

unsafe struct PacketHeader
{
    const int MAGIC_STRING_SIZE = 8;
    public const string MAGIC_STRING = "LL_PACKT"; // Encodes to exactly 8 bytes

    public PacketHeader()
    {
        byte[] magicBytes = System.Text.Encoding.UTF8.GetBytes(MAGIC_STRING);
        Debug.Assert(magicBytes.Length == MAGIC_STRING_SIZE);

        fixed (byte* ptr = magic)
        {
            Marshal.Copy(magicBytes, 0, (nint)ptr, MAGIC_STRING_SIZE);
        }
    }

    public fixed byte magic[MAGIC_STRING_SIZE];
    public int senderID;
    public int bodySize;
    public NetworkCommand command;


    public unsafe string GetMagic()
    {
        fixed (byte* ptr = magic)
        {
            return System.Text.Encoding.UTF8.GetString(ptr, PacketHeader.MAGIC_STRING.Length);
        }
    }

    public static unsafe PacketHeader FromBytes(byte[] data)
    {
        if (data.Length < Unsafe.SizeOf<PacketHeader>())
            throw new Exception();
        fixed (byte* ptr = data)
        {
            return (PacketHeader)Marshal.PtrToStructure<PacketHeader>((nint)ptr);
        }
    }
}

class ServerHelloPacket
{
    public required int ServerID { get; init; } // ID of the sender
    public required int AssignedID { get; init; } // ID that the sender assigned to the receiver
    public required string VersionString { get; init; }
}

class ClientHelloPacket
{
    public required int ClientID { get; init; } // ID of the sender (should be what it was assigned)
    public required string VersionString { get; init; }
}

class NewTransactionPacket
{
    public required List<TransactionNodeDTO> AppliedTransactions;
    public required string? PrevTransaction;
}

class AckPacket
{
    public required List<string> acceptedTransactions;
    public required List<string> rejectedTransactions;
}

/// <summary>
/// The purpose of this class is to allow networking functions to interface with the project. It
/// cannot be done directly due to threading issues. The constructor takes a function which invokes
/// an action on the "main thread", whatever that means to the user, so that operations which affect
/// the project state happen in a controlled way.
/// </summary>
public class NetworkInterfacer
{
    public NetworkInterfacer(Project project, Func<Action, Task> executeOnMainThread)
    {
        this.project = project;
        this.executeOnMainThread = executeOnMainThread;

        // TODO: Unsubscribe on deletion
        UndoState.TransactionAddedEvent += (t) =>
            TransactionAddedEvent?.Invoke(t.Value, t.Previous?.Value.NodeID);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Project project;
    Func<Action, Task> executeOnMainThread;

    // ================================================================================
    // Properties
    // ================================================================================

    public Project Project { get { return project; } }
    public UndoState UndoState { get { return project.UndoState; } }

    // ================================================================================
    // Events
    // ================================================================================

    public event Action<TransactionNode, string?>? TransactionAddedEvent;

    // ================================================================================
    // Methods
    // ================================================================================

    /// <summary>
    /// This function executes on the main thread and gives the caller access to the UndoState. This
    /// is the only safe way to access it.
    /// </summary>
    public async Task DoOnMainThread(Action<UndoState> action)
    {
        await executeOnMainThread(() => action(UndoState));
    }

    /// <summary>
    /// Checks if at least one transaction exists in the project history. (If it doesn't, this must
    /// be a client that hasn't received the initial packet from the server yet.)
    /// </summary>
    public async Task<bool> HasInitialTransaction()
    {
        bool retval = false;
        await executeOnMainThread(() => retval = UndoState.TransactionHistory.Count != 0);
        return retval;
    }

    public async Task AssignID(int id)
    {
        await executeOnMainThread(() => UndoState.CreatorID = id);
    }

    public TransactionNode CreateTransactionNode(TransactionNodeDTO dto)
    {
        // It should be ok to call this without executeOnMainThread() since it doesn't really do
        // anything with the project in the constructor... Just don't do anything silly like call
        // Transaction.Apply() from a networking thread.
        return new TransactionNode(project, dto);
    }
}

/// <summary>
/// Managing a server with potentially multiple client connections. Assume public methods could be
/// called from any thread.
/// </summary>
public class ServerController
{
    private ServerController(IPEndPoint endPoint, NetworkInterfacer interfacer)
    {
        TcpListener listener = new(endPoint);

        this.NetworkInterfacer = interfacer;
        this.listener = listener;
        this.activeConnections = new();
        this.IsAlive = true;
    }

    /// <summary>
    /// Try to start a server for the given project; throws a SocketException on failure.
    /// </summary>
    public static ServerController CreateServer(Project project, IPEndPoint endPoint, Func<Action, Task> executeOnMainThread)
    {
        NetworkInterfacer interfacer = new(project, executeOnMainThread);
        ServerController controller = new(endPoint, interfacer);
        return controller;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    TcpListener listener;
    int nextClientID = 2; // ID to assign to next client that connects
    bool stop = false;
    object mylock = new();

    // Connections that have been approved
    List<ConnectionController> activeConnections;
    // Connection requests that haven't been confirmed yet
    List<ConnectionController> pendingConnections = new();
    Queue<ConnectionController> acceptedConnectionQueue = new();

    TaskCompletionSource acceptedConnection = new(TaskCreationOptions.RunContinuationsAsynchronously);
    CancellationTokenSource? cancelSource;

    // ================================================================================
    // Properties
    // ================================================================================

    public bool IsAlive { get; private set; } // Whether the server is listening

    public NetworkInterfacer NetworkInterfacer { get; }
    public NetworkRole Role { get { return NetworkRole.Server; } }
    public EndPoint ListenEndPoint { get { return listener.LocalEndpoint; } }

    // ================================================================================
    // Events
    // ================================================================================

    // These can be executed on any thread!
    public event EventHandler<ConnectionController>? ConnectionRequestedEvent;

    public event Action<object, ConnectionController, Exception?>? ClientDisconnectedEvent;

    // ================================================================================
    // Methods
    // ================================================================================

    public IList<ConnectionController> GetConnections()
    {
        // Must copy the list, don't risk threading problems
        return new List<ConnectionController>(activeConnections);
    }

    public async Task RunUntilClosed()
    {
        cancelSource = new();

        try
        {
            listener.Start();

            Task<ConnectionController> connectionRequestedTask = WaitForConnectionAsync(cancelSource.Token);
            Task connectionAcceptedTask = acceptedConnection.Task;
            Dictionary<Task, ConnectionController> taskList = new();

            while (!stop)
            {
                var task = await Task.WhenAny([connectionRequestedTask, connectionAcceptedTask, ..taskList.Keys]);

                if (task == connectionRequestedTask)
                {
                    if (task.IsFaulted)
                        await task;
                    if (task.IsCanceled)
                        break;
                    var conn = connectionRequestedTask.Result;
                    log.Info("Connection request from: " + conn.RemoteEndPoint);
                    pendingConnections.Add(conn);
                    ConnectionRequestedEvent?.Invoke(this, conn);
                    connectionRequestedTask = WaitForConnectionAsync(cancelSource.Token);
                }
                else if (task == connectionAcceptedTask)
                {
                    if (task.IsFaulted)
                        await task;
                    if (task.IsCanceled)
                        break;

                    lock (mylock)
                    {
                        while (acceptedConnectionQueue.Count != 0)
                        {
                            var c = acceptedConnectionQueue.Dequeue();
                            log.Info("Accepting connection from: " + c.RemoteEndPoint);
                            taskList.Add(c.RunUntilClosed(cancelSource.Token), c);
                            activeConnections.Add(c);
                        }
                        connectionAcceptedTask = acceptedConnection.Task;
                    }
                }
                else
                {
                    var conn = taskList[task];
                    log.Info("Server dropped connection: " + conn.RemoteEndPoint);
                    taskList.Remove(task);
                    lock (mylock)
                    {
                        activeConnections.Remove(conn);
                    }

                    if (task.IsFaulted)
                    {
                        log.Error(task.Exception);
                    }

                    ClientDisconnectedEvent?.Invoke(this, conn, task.Exception);
                }
            }
        }
        finally
        {
            log.Debug("Server stopped, cleaning up.");

            cancelSource.Dispose();
            cancelSource = null;

            lock (mylock)
            {
                listener.Stop();

                foreach (ConnectionController conn in (ConnectionController[])[..activeConnections, ..pendingConnections, ..acceptedConnectionQueue])
                {
                    conn.Close();
                }

                activeConnections = new();
                pendingConnections = new();
                acceptedConnectionQueue = new();
            }
        }
    }

    public void AcceptConnection(ConnectionController conn)
    {
        lock (mylock)
        {
            conn.Accept();
            if (!pendingConnections.Remove(conn))
                throw new Exception("AcceptConnection: Connection wasn't in our list of pending connections.");
            acceptedConnectionQueue.Enqueue(conn);

            acceptedConnection.SetResult();
            acceptedConnection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void RejectConnection(ConnectionController conn)
    {
        lock (mylock)
        {
            if (!pendingConnections.Remove(conn))
                throw new Exception("RejectConnection: Connection wasn't in our list of pending connections.");
        }
        conn.Close();
    }

    public void Stop()
    {
        if (cancelSource == null)
            throw new Exception("Can't stop server that hasn't started");

        stop = true;
        cancelSource.Cancel();
    }

    /// <summary>
    /// Always call this from the main thread.
    /// </summary>
    public void QueueTransactionsForAllClients(List<TransactionNode> nodes, string? lastNodeID)
    {
        lock (mylock)
        {
            foreach (ConnectionController conn in activeConnections)
            {
                if (conn.SentInitialProjectState)
                    conn.QueueTransactions(nodes, lastNodeID, true);
            }
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    async Task<ConnectionController> WaitForConnectionAsync(CancellationToken ct)
    {
        TcpClient client;

        try
        {
            client = await listener.AcceptTcpClientAsync(ct);
        }
        catch (SocketException)
        {
            IsAlive = false;
            throw;
        }

        log.Info("Connection request from: " + client.Client.RemoteEndPoint);

        ConnectionController c;
        lock (mylock)
        {
            c = ConnectionController.CreateForServer(this, NetworkInterfacer, client, nextClientID++);
        }

        return c;
    }
}

/// <summary>
/// There is one instance of this per connection. Deals with high-level management of packets. Maybe
/// merge this with NetworkListener later on.
///
/// Since the LynnaLab GUI accesses this class directly, be careful with threading. Event callbacks
/// should execute on the main thread rather than a networking thread.
/// </summary>
public class ConnectionController
{
    // ================================================================================
    // Constructors
    // ================================================================================

    private ConnectionController(NetworkInterfacer interfacer, TcpClient client, ServerController? server, int remoteID, NetworkRole role)
    {
        this.Connection = new NetworkListener(this, client, OnPacketReceived);
        this.RemoteID = remoteID;
        this.Role = role;
        this.networkInterfacer = interfacer;
        this.RemoteEndPoint = client.Client.RemoteEndPoint!;
        this.Server = server;

        transactionQueue = Channel.CreateUnbounded<(List<TransactionNode> nodeList, string? lastNodeID)>();

        if (role == NetworkRole.Server)
        {
            packetHandler = new ServerPacketHandler(this, Connection, interfacer, () => packetLoopIterated);
        }
        else if (role == NetworkRole.Client)
        {
            packetHandler = new ClientPacketHandler(this, Connection, interfacer, () => packetLoopIterated);
            acceptedConnection = true;
        }
        else
        {
            throw new Exception("Invalid role: " + role);
        }

        this.Connection.ClosedEvent += () =>
        {
            ConnectionClosedEvent?.Invoke(closeException);
        };
    }

    /// <summary>
    /// Create a ConnectionController FROM a client TO the server. Throws a SocketException on failure.
    /// </summary>
    public async static Task<(Project, ConnectionController)> CreateForClientAsync(
        IPEndPoint endPoint, Func<Action, Task> executeOnMainThread, CancellationToken cancellationToken=default)
    {
        log.Info($"Attempting to connect to {endPoint}...");

        TcpClient client = new();
        await client.ConnectAsync(endPoint, cancellationToken);

        log.Info($"Connection established to {endPoint}.");

        Project p = new Project();
        NetworkInterfacer interfacer = new(p, executeOnMainThread);

        ConnectionController c = new(interfacer, client, null, NetworkID.Server, NetworkRole.Client);
        return (p, c);
    }

    /// <summary>
    /// Create a ConnectionController FROM the server TO a client.
    /// </summary>
    public static ConnectionController CreateForServer(ServerController server, NetworkInterfacer interfacer, TcpClient client, int remoteID)
    {
        ConnectionController c = new(interfacer, client, server, remoteID, NetworkRole.Server);
        return c;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    protected static readonly log4net.ILog log = LogHelper.GetLogger();

    protected bool receivedHello = false;

    int outstandingAckCounter = 0;

    NetworkInterfacer networkInterfacer;

    // A pseudo-task that completes once every time the event loop iterates
    TaskCompletionSource packetLoopIterated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    Channel<(List<TransactionNode> nodeList, string? lastNodeID)> transactionQueue;
    IPacketHandler packetHandler;

    bool ranBegin = false;
    bool acceptedConnection = false; // For server-side connections, this is false until accepted
    Exception? closeException;

    // ================================================================================
    // Properties
    // ================================================================================

    public JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Our ID
    /// </summary>
    public int LocalID { get { return packetHandler.LocalID; } }

    /// <summary>
    /// ID corresponding to the client on the other end of this connection
    /// </summary>
    public int RemoteID { get; }

    public bool Closed { get { return Connection.Closed; } }

    public bool PauseOutgoingPackets
    {
        get { return Connection.PauseOutgoingPackets; }
        set { Connection.PauseOutgoingPackets = value; }
    }

    public int TotalSentBytes { get { return Connection.TotalSentBytes; } }
    public int TotalReceivedBytes { get { return Connection.TotalReceivedBytes; } }

    public int OutstandingAcks { get { return outstandingAckCounter; } }
    public int RejectedTransactions { get { return packetHandler.RejectedTransactions; } }

    public NetworkRole Role { get; }

    /// <summary>
    /// Only valid if this is on the server end.
    /// </summary>
    public ServerController? Server { get; }

    /// <summary>
    /// Server only: true if we sent the initial state to this client.
    /// </summary>
    public bool SentInitialProjectState { get { return ((ServerPacketHandler)packetHandler).SentInitialProjectState; } }

    NetworkListener Connection { get; }

    // ================================================================================
    // Events
    // ================================================================================

    // This will be invoked on the main thread when the remote rejects transactions.
    public event Action<IEnumerable<string>>? TransactionsRejectedEvent;

    public event Action<Exception?>? ConnectionClosedEvent;

    // ================================================================================
    // Methods
    // ================================================================================

    public async Task RunUntilClosed(CancellationToken cancellationToken = default)
    {
        if (ranBegin)
            throw new Exception();

        ranBegin = true;

        try
        {
            if (Role == NetworkRole.Server)
            {
                Debug.Assert(this.RemoteID != NetworkID.Unassigned && this.RemoteID != NetworkID.Server);
                await Connection.SendServerHelloPacket(this.RemoteID);
            }
            else
            {
                Debug.Assert(this.RemoteID == NetworkID.Server);
            }

            Task packetTask = Task.CompletedTask;
            Task transactionTask = Task.CompletedTask;

            while (true)
            {
                {
                    // Just in case of race conditions, make sure that we have a new task ready
                    // before we call TrySetResult
                    var p = packetLoopIterated;
                    packetLoopIterated = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    p.TrySetResult();
                }

                Task t = await Task.WhenAny([packetTask, transactionTask]);

                if (t.IsCanceled)
                    break;

                if (t == packetTask)
                {
                    if (packetTask.IsFaulted)
                    {
                        // Typically, packetTask.Exception might be an EndOfStreamException here if
                        // somebody closed the connection, though it could be a lot of things. Use
                        // "await" to throw the exception.
                        await packetTask;
                    }
                    else if (packetTask.IsCanceled)
                        break;
                    packetTask = Connection.HandlePacketAsync(cancellationToken);
                }
                else if (t == transactionTask)
                {
                    if (t.IsFaulted)
                        await t; // Throw exception
                    (List<TransactionNode> nodeList, string? lastNodeID) tup;
                    if (transactionQueue.Reader.TryRead(out tup))
                    {
                        // Insert a Task.Delay() here to simulate network latency effects
                        await SendTransactions(tup.nodeList, tup.lastNodeID);
                    }
                    transactionTask = transactionQueue.Reader.WaitToReadAsync(cancellationToken).AsTask();
                }
                else
                    throw new Exception();
            }

            packetLoopIterated.TrySetCanceled();
        }
        catch (Exception e)
        {
            log.Error($"Error on connection: {RemoteEndPoint}");
            log.Error(e);
            packetLoopIterated.TrySetException(e);
            closeException = e;
            Close();
        }
        finally
        {
            Close();
        }

        // TODO: Cleanup?
    }

    public void Accept()
    {
        if (acceptedConnection)
            throw new Exception(FormatLog("Connection accepted multiple times?"));
        acceptedConnection = true;
    }

    public void Close()
    {
        Connection.Close();
    }

    public async Task WaitUntilSynchronizedAsync()
    {
        await packetHandler.WaitUntilSynchronizedAsync();
    }

    public async Task WaitUntilAcceptedAsync(CancellationToken ct)
    {
        await packetHandler.WaitUntilAcceptedAsync(ct);
    }

    /// <summary>
    /// Queue a transaction to be sent over the network. It's particularly important for the server
    /// to always use this method when sending transactions - because it also has an event handler
    /// which calls this. We must ensure that the transaction history is sent in the correct order.
    ///
    /// This should always be called from the main thread, to avoid sending transactions in a weird
    /// order (ie. if receiving transactions from the network as the local instance also creates
    /// transactions).
    /// </summary>
    public void QueueTransactions(List<TransactionNode> transactions, string? lastNodeID, bool incAckCounter)
    {
        if (!transactionQueue.Writer.TryWrite((transactions, lastNodeID)))
            throw new Exception("Channel write failure");
        if (incAckCounter)
        {
            outstandingAckCounter += transactions.Count;
        }
    }

    /// <summary>
    /// Called upon receiving AckPackets.
    /// </summary>
    public async Task AcknowledgeAcks(int count)
    {
        await networkInterfacer.DoOnMainThread((_) =>
        {
            outstandingAckCounter -= count;
        });
    }

    protected string FormatLog(string message)
    {
        return packetHandler.FormatLog(message);
    }

    // NOTE: Not doing anything with the CancellationToken since the things it awaits for generally
    // shouldn't be awaiting for too long (ie. sending packets) or doing things on the main thread).
    private async Task OnPacketReceived(PacketHeader header, string data, CancellationToken ct)
    {
        log.Debug(FormatLog($"Received packet from sender {header.senderID}: " + header.command));
        //log.Debug("Packet body: " + data);

        // Do not accept any packets until connection is accepted, for security reasons.
        if (!acceptedConnection)
            throw new NetworkException(FormatLog("Received packet before connection was accepted."));

        if (!receivedHello
            && header.command != NetworkCommand.ServerHello
            && header.command != NetworkCommand.ClientHello)
        {
            throw new NetworkException("Expected hello before any other packets");
        }

        if (receivedHello && header.senderID != RemoteID)
        {
            throw new NetworkException(
                $"Received packet with wrong ID (expected: {RemoteID}, got: {header.senderID})");
        }

        switch (header.command)
        {
            case NetworkCommand.ServerHello:
                await packetHandler.HandleServerHello(Deserialize<ServerHelloPacket>(data));
                receivedHello = true;
                break;
            case NetworkCommand.ClientHello:
                await packetHandler.HandleClientHello(Deserialize<ClientHelloPacket>(data));
                receivedHello = true;
                break;
            case NetworkCommand.NewTransactions:
                await packetHandler.HandleNewTransaction(Deserialize<NewTransactionPacket>(data));
                break;
            case NetworkCommand.Ack:
                var rejected = await packetHandler.HandleAckPacket(Deserialize<AckPacket>(data));
                if (rejected.Count != 0)
                {
                    await networkInterfacer.DoOnMainThread(
                        (_) => TransactionsRejectedEvent?.Invoke(rejected.Select((t) => t.Description)));
                }
                break;
            default:
                throw new NetworkException($"Unhandled network command: {header.command}");
        }
    }

    private async Task SendTransactions(List<TransactionNode> nodeList, string? lastNodeID)
    {
        List<TransactionNodeDTO> dtoList = new();

        foreach (TransactionNode node in nodeList)
        {
            dtoList.Add(node.AsDTO());
        }

        NewTransactionPacket packet = new()
        {
            AppliedTransactions = dtoList,
            PrevTransaction = lastNodeID,
        };

        await Connection.SendPacket<NewTransactionPacket>(NetworkCommand.NewTransactions, packet);
    }

    private T Deserialize<T>(string data)
    {
        return JsonSerializer.Deserialize<T>(data, SerializerOptions)
            ?? throw new DeserializationException("Null result");
    }

}

/// <summary>
/// There are two types of IPacketHandler: ServerPacketHandler and ClientPacketHandler. They deal
/// with each of the various packet types a bit differently.
/// </summary>
interface IPacketHandler
{
    /// <summary>
    /// Wait until server accepts client. (No-op for the server side.)
    /// </summary>
    Task WaitUntilAcceptedAsync(CancellationToken ct);

    /// <summary>
    /// Wait until the remote is up to date with our changes.
    /// </summary>
    Task WaitUntilSynchronizedAsync();

    /// <summary>
    /// Client receives a server hello packet. It will respond with a client hello packet.
    /// </summary>
    Task HandleServerHello(ServerHelloPacket packet);

    /// <summary>
    /// Server receives a client hello packet.
    /// </summary>
    Task HandleClientHello(ClientHelloPacket packet);

    /// <summary>
    /// Invoked when there's a new transaction from over the network.
    /// </summary>
    Task HandleNewTransaction(NewTransactionPacket packet);

    Task<IList<TransactionNode>> HandleAckPacket(AckPacket packet);

    string FormatLog(string message);

    public int LocalID { get; }
    public int RejectedTransactions { get; }
}

/// <summary>
/// Base class for ServerPacketHandler and ClientPacketHandler
/// </summary>
abstract class PacketHandlerBase
{
    public PacketHandlerBase(ConnectionController parent, NetworkListener connection, NetworkInterfacer networkInterfacer, Func<TaskCompletionSource> mainLoopIterationTaskGetter, int initialLocalID)
    {
        this.Parent = parent;
        this.Connection = connection;
        this.NetworkInterfacer = networkInterfacer;
        this.mainLoopIterationTaskGetter = mainLoopIterationTaskGetter;
        this.LocalID = initialLocalID;

        var onTransactionAdded = (TransactionNode node, string? prevNodeID) =>
        {
            if (EnableSendingTransactions)
                QueueTransaction(node, prevNodeID);
        };

        NetworkInterfacer.TransactionAddedEvent += onTransactionAdded;

        connection.ClosedEvent += () =>
        {
            NetworkInterfacer.TransactionAddedEvent -= onTransactionAdded;
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    Func<TaskCompletionSource> mainLoopIterationTaskGetter;

    object mylock = new();

    // ================================================================================
    // Properties
    // ================================================================================

    public bool Closed { get { return Parent.Closed; } }

    public int LocalID { get; private set; }
    public int RejectedTransactions { get; protected set; } = 0;

    protected int OutstandingAcks { get { return Parent.OutstandingAcks; } }
    protected NetworkListener Connection { get; }
    protected int RemoteID { get { return Parent.RemoteID; } }

    protected TaskCompletionSource MainLoopIterationTask { get { return mainLoopIterationTaskGetter(); } }

    protected NetworkInterfacer NetworkInterfacer { get; }

    protected abstract bool EnableSendingTransactions { get; }

    protected ConnectionController Parent { get; }


    // ================================================================================
    // Methods
    // ================================================================================

    /// <summary>
    /// Always call this from the main thread.
    /// </summary>
    protected void QueueTransactions(LinkedListNode<TransactionNode> start, LinkedListNode<TransactionNode> end)
    {
        log.Debug(FormatLog($"Queueing transactions: {start.Value.NodeID} to {end.Value.NodeID}"));

        List<TransactionNode> nodeList = new();
        LinkedListNode<TransactionNode> node = start;

        while (true)
        {
            nodeList.Add(node.Value);

            if (node == end)
                break;
            else if (node.Next == null)
                throw new Exception("SendTransactions: Node traversal error");

            node = node.Next;
        }

        Parent.QueueTransactions(nodeList, start.Previous?.Value.NodeID, true);
    }

    public string FormatLog(string message)
    {
        if (RemoteID == NetworkID.Unassigned)
            return $"From new remote: {message}";
        else
            return $"Conn {LocalID}->{RemoteID}: {message}";
    }

    /// <summary>
    /// Always call this from the main thread.
    /// </summary>
    protected void QueueTransaction(TransactionNode node, string? lastNodeID)
    {
        lock (mylock)
        {
            Parent.QueueTransactions([node], lastNodeID, true);
        }
    }

    protected async Task SendAcks(IEnumerable<TransactionNode> acks, IEnumerable<TransactionNode> nacks)
    {
        List<string> accepted = new(), rejected = new();

        foreach (TransactionNode n in acks)
            accepted.Add(n.NodeID);

        foreach (TransactionNode n in nacks)
            rejected.Add(n.NodeID);

        AckPacket packet = new()
        {
            acceptedTransactions = accepted,
            rejectedTransactions = rejected,
        };

        await Connection.SendPacket(NetworkCommand.Ack, packet);
    }

    protected async Task AssignID(int id)
    {
        if (LocalID != NetworkID.Unassigned)
            throw new Exception("ClientController: Tried to assign an ID multiple times?");
        if (id <= NetworkID.Server)
            throw new Exception("ClientController: Invalid ID: " + id);
        LocalID = id;

        await NetworkInterfacer.AssignID(id);
    }
}

/// <summary>
/// How to deal with packets received by the server
/// </summary>
class ServerPacketHandler : PacketHandlerBase, IPacketHandler
{
    public ServerPacketHandler(ConnectionController parent, NetworkListener connection, NetworkInterfacer interfacer, Func<TaskCompletionSource> mainLoopIterationTask)
        : base(parent, connection, interfacer, mainLoopIterationTask, NetworkID.Server)
    {

    }

    // ================================================================================
    // Variables
    // ================================================================================

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    bool initialStateSent = false;

    // ================================================================================
    // Properties
    // ================================================================================

    public bool SentInitialProjectState { get { return initialStateSent; } }
    protected override bool EnableSendingTransactions { get { return initialStateSent; } }

    // ================================================================================
    // Methods
    // ================================================================================

    public Task WaitUntilAcceptedAsync(CancellationToken ct)
    {
        throw new Exception("WaitUntilAcceptedAsync: N/A for servers");
    }

    public async Task WaitUntilSynchronizedAsync()
    {
        while (true)
        {
            if (OutstandingAcks == 0)
                return;

            await base.MainLoopIterationTask.Task;
        }
    }

    public async Task SendInitialProjectState()
    {
        await NetworkInterfacer.DoOnMainThread((undoState) =>
        {
            var history = undoState.TransactionHistory;
            base.QueueTransactions(history.FirstNode, history.LastNode);
            initialStateSent = true;
        });
    }

    public Task HandleServerHello(ServerHelloPacket packet)
    {
        throw new NetworkException("Server received a server hello packet?");
    }

    public async Task HandleClientHello(ClientHelloPacket packet)
    {
        log.Debug(FormatLog($"Received client hello packet - Client ID: {packet.ClientID}"));

        if (packet.VersionString != Helper.GetVersionString())
        {
            throw new NetworkException($"Invalid client version: got '{packet.VersionString}', expected '{Helper.GetVersionString()}'");
        }

        if (packet.ClientID != RemoteID)
        {
            throw new NetworkException(
                $"Client returned an ID ({packet.ClientID}) different from what we assigned ({RemoteID}).");
        }

        await SendInitialProjectState();
    }

    public async Task HandleNewTransaction(NewTransactionPacket packet)
    {
        List<TransactionNode> accepted = new();
        List<TransactionNode> rejected = new();

        bool rejectedAny = false;

        // Do this on the main thread, so that no changes to the transaction history are made in the meantime.
        await NetworkInterfacer.DoOnMainThread((undoState) =>
        {
            foreach (TransactionNodeDTO dto in packet.AppliedTransactions)
            {
                TransactionNode node = NetworkInterfacer.CreateTransactionNode(dto);

                if (rejectedAny)
                {
                    rejected.Add(node);
                    continue;
                }

                string? lastNodeID = undoState.TransactionHistory.Last?.NodeID;
                bool applied = undoState.ApplyTransactionNode(node);

                if (applied)
                {
                    if (Parent.Server == null)
                        throw new Exception("ServerPacketHandler: Used on a non-server connection?");

                    // Success: Let all clients know that we've applied this packet.
                    Parent.Server.QueueTransactionsForAllClients([node], lastNodeID);
                    accepted.Add(node);
                }
                else
                {
                    rejectedAny = true;
                    rejected.Add(node);
                }
            }
        });

        await base.SendAcks(accepted, rejected);
        RejectedTransactions += rejected.Count;
    }

    public async Task<IList<TransactionNode>> HandleAckPacket(AckPacket packet)
    {
        if (packet.rejectedTransactions.Count != 0)
            throw new NetworkException("Client unexpectedly rejected a transaction?");

        await Parent.AcknowledgeAcks(packet.acceptedTransactions.Count + packet.rejectedTransactions.Count);

        if (OutstandingAcks < 0)
            throw new NetworkException("HandleAckPacket: Received more acks than expected");

        return [];
    }
}

/// <summary>
/// How to deal with packets received by a client
/// </summary>
class ClientPacketHandler : PacketHandlerBase, IPacketHandler
{
    public ClientPacketHandler(ConnectionController parent, NetworkListener connection, NetworkInterfacer interfacer, Func<TaskCompletionSource> mainLoopIterationTask)
        : base(parent, connection, interfacer, mainLoopIterationTask, NetworkID.Unassigned)
    {
    }

    // ================================================================================
    // Variables
    // ================================================================================

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    // The NodeID of the last common ancestor transaction between our local transaction history and
    // the server transaction history.
    string? lastCommonAncestor;

    bool receivedHello = false;

    // List of server transactions that we eventually need to send an ACK for.
    List<TransactionNode> transactionsToAck = new();

    // ================================================================================
    // Properties
    // ================================================================================

    protected override bool EnableSendingTransactions { get { return true; } }

    // State of remote instance, as we know it
    DictionaryLinkedList<string, TransactionNode> ServerTransactionHistory { get; set; } = new();

    // ================================================================================
    // Methods
    // ================================================================================

    public async Task WaitUntilAcceptedAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource();

        // Registering callback to wait till CancellationToken triggers.
        // https://stackoverflow.com/questions/13741694/construct-task-from-waithandle-wait

        ThreadPool.RegisterWaitForSingleObject(
            waitObject: ct.WaitHandle,
            callBack:(o, timeout) => { tcs.TrySetResult(); },
            state: null,
            timeout: TimeSpan.FromSeconds(60),
            executeOnlyOnce: true);

        while (true)
        {
            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
                ct.ThrowIfCancellationRequested();
            }
            if (receivedHello)
            {
                tcs.TrySetCanceled();
                return;
            }

            Task t = await Task.WhenAny([base.MainLoopIterationTask.Task, tcs.Task]);

            if (t.IsFaulted)
                await t;
            else if (t.IsCanceled)
                throw new TaskCanceledException();
        }
    }

    /// <summary>
    /// Unlike for the server, the client must ensure that we have at least one transaction, to
    /// ensure that the initial synchronization with the server has completed. After that, we just
    /// check the acks like the server implementation.
    /// </summary>
    public async Task WaitUntilSynchronizedAsync()
    {
        log.Debug(FormatLog("Waiting for remote synchronization"));

        while (true)
        {
            if (await NetworkInterfacer.HasInitialTransaction() && OutstandingAcks == 0)
                return;

            await base.MainLoopIterationTask.Task;
        }
    }

    public async Task HandleServerHello(ServerHelloPacket packet)
    {
        log.Debug(FormatLog(
                        $"Received server hello packet - Remote ID: {packet.ServerID}, Assigned ID: {packet.AssignedID}"));

        if (packet.VersionString != Helper.GetVersionString())
        {
            throw new NetworkException($"Invalid server version: got '{packet.VersionString}', expected '{Helper.GetVersionString()}'");
        }
        if (packet.ServerID != NetworkID.Server)
            throw new NetworkException($"Invalid ID from server: {packet.ServerID}");
        if (packet.AssignedID <= NetworkID.Server)
            throw new NetworkException($"Invalid assigned ID: {packet.AssignedID}");

        await base.AssignID(packet.AssignedID);

        receivedHello = true;

        await Connection.SendClientHelloPacket();
    }

    public Task HandleClientHello(ClientHelloPacket packet)
    {
        throw new NetworkException("Client received client hello?");
    }

    public async Task HandleNewTransaction(NewTransactionPacket packet)
    {
        if (packet.AppliedTransactions == null)
        {
            throw new NetworkException(FormatLog("NewTransactionPacket: Null transaction"));
        }

        if (packet.PrevTransaction != ServerTransactionHistory.Last?.NodeID)
        {
            throw new NetworkException($"Packet expected to come after '{packet.PrevTransaction}', but last transaction was {ServerTransactionHistory.Last?.NodeID}");
        }

        // Update server transaction history. (Every packet we receive from the server is assumed to
        // be part of the "canonical" transaction history, so our local representation of its
        // history should always be accurate.)
        foreach (TransactionNodeDTO dto in packet.AppliedTransactions)
        {
            TransactionNode node = NetworkInterfacer.CreateTransactionNode(dto);

            ServerTransactionHistory.AddLast(node.NodeID, node);

            transactionsToAck.Add(node);

            log.Debug(FormatLog($"Server applies transaction {node.NodeID}"));
        }

        // If all our submitted transactions are acked by the server, then synchronize state with
        // the server. Otherwise do nothing (state is not synchronized until we receive ACKs or
        // NACKs from the server for all transactions that we've submitted.)
        await SynchronizeWithServerIfNoOutstandingTransactions();
    }

    public async Task<IList<TransactionNode>> HandleAckPacket(AckPacket packet)
    {
        await Parent.AcknowledgeAcks(packet.acceptedTransactions.Count + packet.rejectedTransactions.Count);
        RejectedTransactions += packet.rejectedTransactions.Count;

        List<TransactionNode> rejectedTransactionList = new();

        if (packet.rejectedTransactions.Count != 0)
        {
            await NetworkInterfacer.DoOnMainThread((undoState) =>
            {
                foreach (string p in packet.rejectedTransactions)
                {
                    // The rejected transactions should exist in our TransactionHistory up until we
                    // call "SynchronizeWithServer"
                    TransactionNode? node = undoState.TransactionHistory.Find(p)?.Value;
                    if (node == null)
                        throw new NetworkException($"Couldn't find transaction '{p}' in local history");
                    rejectedTransactionList.Add(node);
                }
            });
        }

        if (OutstandingAcks < 0)
            throw new NetworkException("HandleAckPacket: Received more ACKs than expected");
        else
        {
            // Only once we know the server has processed all of our requested transactions do we
            // synchronize our state with the server, in order to prevent our changes from
            // disappearing and reappearing.
            await SynchronizeWithServerIfNoOutstandingTransactions();
        }

        return rejectedTransactionList;
    }

    private async Task SynchronizeWithServerIfNoOutstandingTransactions()
    {
        bool synchronized = false;

        // Do the outstanding ack check on the main thread - just in case new transaction sneak in
        // on the other thread.
        await NetworkInterfacer.DoOnMainThread((undoState) =>
        {
            if (OutstandingAcks != 0)
                return;

            undoState.SynchronizeWith(ServerTransactionHistory, lastCommonAncestor);
            lastCommonAncestor = ServerTransactionHistory.Last.NodeID;
            synchronized = true;
        });

        if (synchronized)
        {
            // Send ACKs for all new packets from the server.
            await base.SendAcks(transactionsToAck, []);
            transactionsToAck.Clear();
        }
    }
}

// Methods to get, send packets
class NetworkListener
{
    public NetworkListener(ConnectionController parent, TcpClient client, Func<PacketHeader, string, CancellationToken, Task> packetHandler)
    {
        this.parent = parent;
        this.stream = client.GetStream();
        this.tcpClient = client;
        this.packetHandler = packetHandler;
        this.headerSize = Unsafe.SizeOf<PacketHeader>();
        this.pauseFinishedTask = new();
        pauseFinishedTask.SetResult();
    }

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    private JsonSerializerOptions SerializerOptions { get { return parent.SerializerOptions; } }


    const int BUFFER_SIZE = 10000;

    readonly int headerSize;

    ConnectionController parent;
    NetworkStream stream;
    TcpClient tcpClient;
    Func<PacketHeader, string, CancellationToken, Task> packetHandler;

    bool _pauseOutgoingPackets = false;
    TaskCompletionSource pauseFinishedTask;


    public bool PauseOutgoingPackets
    {
        get
        {
            return _pauseOutgoingPackets;
        }
        set
        {
            if (_pauseOutgoingPackets == value)
                return;
            if (value)
            {
                pauseFinishedTask = new();
                _pauseOutgoingPackets = true;
            }
            else
            {
                _pauseOutgoingPackets = false;
                pauseFinishedTask.SetResult();
            }
        }
    }

    public EndPoint? RemoteEndPoint { get { return tcpClient.Client.RemoteEndPoint; } }

    public int TotalSentBytes { get; private set; } = 0;
    public int TotalReceivedBytes { get; private set; } = 0;

    public bool Closed { get; private set; } = false;


    public event Action? ClosedEvent;


    public void Close()
    {
        if (!Closed)
        {
            Closed = true;
            log.Debug("Connection closed: " + RemoteEndPoint);
            stream.Close();
            tcpClient.Close();
            ClosedEvent?.Invoke();
        }
    }

    public async Task HandlePacketAsync(CancellationToken ct = default)
    {
        try
        {
            byte[] buffer = new byte[headerSize];
            await stream.ReadExactlyAsync(buffer, 0, headerSize, ct);

            TotalReceivedBytes += headerSize;

            log.Debug("Receive header: " + System.Text.Encoding.UTF8.GetString(buffer));

            PacketHeader recvHeader = PacketHeader.FromBytes(buffer);

            string magic = recvHeader.GetMagic();
            if (magic != PacketHeader.MAGIC_STRING)
                throw new NetworkException($"Magic string mismatch: '{magic}' != '{PacketHeader.MAGIC_STRING}'");

            if (recvHeader.bodySize < 0)
                throw new NetworkException($"Invalid body size from header: {recvHeader.bodySize}");

            if (!Enum.IsDefined<NetworkCommand>(recvHeader.command))
                throw new NetworkException($"Invalid network command: {recvHeader.command}");

            buffer = new byte[recvHeader.bodySize];
            await stream.ReadExactlyAsync(buffer, 0, recvHeader.bodySize, ct); // TODO: Sanity check on size?

            TotalReceivedBytes += recvHeader.bodySize;

            string recvBody;

            // Decompress the body
            using (MemoryStream compressedStream = new MemoryStream(buffer))
            using (MemoryStream decompressedStream = new MemoryStream())
            using (GZipStream zipStream = new(compressedStream, CompressionMode.Decompress))
            {
                zipStream.CopyTo(decompressedStream);
                recvBody = System.Text.Encoding.UTF8.GetString(decompressedStream.ToArray());
            }

            await packetHandler(recvHeader, recvBody, ct);
        }
        catch (Exception)
        {
            // Probably an EndOfStream exception for when someone closed the stream.
            throw;
        }
    }

    public async Task SendServerHelloPacket(int assignedID)
    {
        log.Debug("Sending server hello packet");

        ServerHelloPacket packet = new()
        {
            ServerID = parent.LocalID,
            AssignedID = assignedID,
            VersionString = Helper.GetVersionString(),
        };
        await SendPacket(NetworkCommand.ServerHello, packet);
    }

    public async Task SendClientHelloPacket()
    {
        log.Debug("Sending client hello packet");

        ClientHelloPacket packet = new()
        {
            ClientID = parent.LocalID,
            VersionString = Helper.GetVersionString(),
        };
        await SendPacket(NetworkCommand.ClientHello, packet);
    }

    public async Task SendPacket<T>(NetworkCommand command, T packet)
    {
        string body = JsonSerializer.Serialize<T>(packet, SerializerOptions);
        await SendPacket(command, body);
    }

    async Task SendPacket(NetworkCommand command, string body)
    {
        if (PauseOutgoingPackets)
            await pauseFinishedTask.Task;

        byte[] buffer = EncodePacket(command, body);
        await stream.WriteAsync(buffer);
        TotalSentBytes += buffer.Length;
    }

    unsafe byte[] EncodePacket(NetworkCommand command, string body)
    {
        if (parent.LocalID == NetworkID.Unassigned)
            throw new NetworkException("Can't send packet without an assigned ID");

        byte[] bodyEncoded = System.Text.Encoding.UTF8.GetBytes(body);
        byte[] buffer;
        int compressedSize;

        // Compress the body
        using (MemoryStream compressedStream = new())
        using (GZipStream zipStream = new(compressedStream, CompressionLevel.Optimal, true))
        {
            zipStream.Write(bodyEncoded);
            zipStream.Close();
            compressedSize = (int)compressedStream.Length;
            TotalSentBytes += compressedSize;
            buffer = new byte[headerSize + compressedSize];
            Array.Copy(compressedStream.ToArray(), 0, buffer, headerSize, compressedStream.Length);
        }

        // Build the header, write it to the buffer
        PacketHeader header = new();
        header.bodySize = compressedSize;
        header.command = command;
        header.senderID = parent.LocalID;

        fixed (byte* ptr = buffer)
        {
            Marshal.StructureToPtr<PacketHeader>(header, (nint)ptr, false);
        }

        return buffer;
    }
}
