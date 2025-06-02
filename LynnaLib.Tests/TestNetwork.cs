using System.Net;

using Util;

namespace LynnaLib.Tests;

public class TestNetwork
{
    static readonly IPEndPoint serverAddress = IPEndPoint.Parse("127.0.0.1:9898");

    public TestNetwork()
    {
    }

    [Fact]
    public static async Task TestNetworkTransfer()
    {
        // Load project 1
        Project p1 = TestProject.LoadProject(Game.Seasons);

        Func<Action, Task> invoker = (a) => { a(); return Task.CompletedTask; };

        // Start server on project 1
        ServerController server = ServerController.CreateServer(p1, serverAddress, invoker);
        var serverTask = server.RunUntilClosed();
        ConnectionController? serverConn = null;

        server.ConnectionRequestedEvent += (_, conn) =>
        {
            server.AcceptConnection(conn);
            serverConn = conn;
        };

        // Connect new client to project 1 (create project 2)
        var (p2, clientConn) = await ConnectionController.CreateForClientAsync(serverAddress, invoker);

        Task clientRunTask = clientConn.RunUntilClosed();

        await clientConn.WaitUntilSynchronizedAsync();

        if (serverConn == null)
            throw new Exception("Server never accepted connection?");

        Func<Task> synchronize = () => Helper.WhenAllWithExceptions(
            [clientConn.WaitUntilSynchronizedAsync(), serverConn.WaitUntilSynchronizedAsync()]);

        await synchronize();

        p2.FinalizeLoad();

        // Some sanity checks to see if the project data was transferred successfully
        CompareBoth(p1, p2, (p) => p.GetRoomLayout(0, 0).GetTile(0, 0), 0xb8);
        CompareBoth(p1, p2, (p) => p.GetRoomLayout(0, 0).Tileset.Index, 0x11);
        CompareBoth(p1, p2, (p) => p.GetRoom(0).GetObjectGroup().GetObject(0).GetGameObject().ID, 0xb3);
        CompareBoth(p1, p2, (p) => p.GetRoom(0).GetWarpGroup().GetWarp(0).DestRoom.Index, 0x4ba);
        CompareBoth(p1, p2, (p) => p.GetRoom(0x406).Chest?.TreasureIndex, 0x0500);

        // Modifying p1 room layout affects p2
        p1.BeginTransaction("A");
        p1.GetRoomLayout(0x103, Season.None).SetTile(3, 3, 0x50);
        p1.EndTransaction();
        await synchronize();
        Assert.Equal(0x50, p2.GetRoomLayout(0x103, Season.None).GetTile(3, 3));

        // Test chest deletion
        p2.GetRoom(0xf9).Chest.Delete();
        await synchronize();
        Assert.Null(p1.GetRoom(0xf9).Chest);

        // Test warp deletion
        p1.GetRoom(0).GetWarpGroup().GetWarp(2).Remove();
        await synchronize();
        Assert.Equal(2, p2.GetRoom(0).GetWarpGroup().Count);

        // Test pausing outgoing packets
        serverConn.PauseOutgoingPackets = true;
        p1.GetRoomLayout(3, Season.Winter).SetTile(0, 0, 51);
        await Task.Delay(100);
        Assert.Equal(162, p2.GetRoomLayout(3, Season.Winter).GetTile(0, 0));
        serverConn.PauseOutgoingPackets = false;
        await synchronize();
        Assert.Equal(51, p2.GetRoomLayout(3, Season.Winter).GetTile(0, 0));

        // Test simultaneous edits: Server should get priority
        serverConn.PauseOutgoingPackets = true;
        p1.GetRoomLayout(0x300, Season.None).SetTile(3, 3, 51);
        p2.GetRoomLayout(0x300, Season.None).SetTile(3, 3, 56);
        serverConn.PauseOutgoingPackets = false;
        await synchronize();
        Assert.Equal(51, p1.GetRoomLayout(0x300, Season.None).GetTile(3, 3));
        Assert.Equal(51, p2.GetRoomLayout(0x300, Season.None).GetTile(3, 3));

        // Undo/redo: Client does something, undoes, redoes
        {
            RoomLayout sRoom = p1.GetRoomLayout(0x498, Season.None);
            RoomLayout cRoom = p2.GetRoomLayout(0x498, Season.None);
            Assert.Equal(17, cRoom.GetTile(4, 2));
            cRoom.SetTile(4, 2, 56);
            await synchronize();
            Assert.Equal(56, sRoom.GetTile(4, 2));
            p2.TransactionManager.Undo();
            Assert.Equal(17, cRoom.GetTile(4, 2));
            await synchronize();
            Assert.Equal(17, sRoom.GetTile(4, 2));
            p2.TransactionManager.Redo();
            Assert.Equal(56, cRoom.GetTile(4, 2));
            await synchronize();
            Assert.Equal(56, sRoom.GetTile(4, 2));
        }

        // Undo/redo: Client does something, server does something else, then client undoes
        {
            // Client does something
            RoomLayout sRoom = p1.GetRoomLayout(0x453, Season.None);
            RoomLayout cRoom = p2.GetRoomLayout(0x453, Season.None);
            Assert.Equal(161, cRoom.GetTile(4, 2));
            cRoom.SetTile(4, 2, 56);
            await synchronize();
            Assert.Equal(56, sRoom.GetTile(4, 2));

            // Server does something in a different room
            Assert.Equal(45, p1.GetRoomLayout(0x203, Season.None).GetTile(2, 1));
            p1.GetRoomLayout(0x203, Season.None).SetTile(2, 1, 56);
            Assert.Equal(56, p1.GetRoomLayout(0x203, Season.None).GetTile(2, 1));
            await synchronize();
            Assert.Equal(56, p2.GetRoomLayout(0x203, Season.None).GetTile(2, 1));

            // Client does undo, then redo
            p2.TransactionManager.Undo();
            Assert.Equal(161, cRoom.GetTile(4, 2));
            await synchronize();
            Assert.Equal(161, sRoom.GetTile(4, 2));
            p2.TransactionManager.Redo();
            Assert.Equal(56, cRoom.GetTile(4, 2));
            await synchronize();
            Assert.Equal(56, sRoom.GetTile(4, 2));

            // Server's changes to the other room are preserved
            Assert.Equal(56, p1.GetRoomLayout(0x203, Season.None).GetTile(2, 1));
            Assert.Equal(56, p2.GetRoomLayout(0x203, Season.None).GetTile(2, 1));
        }

        // Client/server both create the same chest
        {
            var sRoom = p1.GetRoom(0x60);
            var cRoom = p2.GetRoom(0x60);

            Assert.Null(sRoom.Chest);
            serverConn.PauseOutgoingPackets = true;
            sRoom.AddChest();
            sRoom.Chest?.ValueReferenceGroup.SetValue("X", 3);
            cRoom.AddChest();
            cRoom.Chest?.ValueReferenceGroup.SetValue("X", 4);
            serverConn.PauseOutgoingPackets = false;
            await synchronize();
            Assert.Equal(3, sRoom.Chest?.ValueReferenceGroup.GetIntValue("X"));
            Assert.Equal(3, cRoom.Chest?.ValueReferenceGroup.GetIntValue("X"));
        }

        // TODO: Test applying transaction when there's an unfinished transaction

        p1.Close();
        //p2.Close();
    }

    static void CompareBoth<T>(Project p1, Project p2, Func<Project, T?> getter, T? expected)
    {
        T? t1 = getter(p1);
        T? t2 = getter(p2);
        Assert.Equal(expected, t1);
        Assert.Equal(expected, t2);
    }
}
