namespace LynnaLib.Tests;

public class TestProject
{
    public static readonly string ProjectBasePath = "../../../../oracles-disasm";


    [Fact]
    public static void TestWarpCreationAndDeletion()
    {
        Project p = LoadProject(Game.Seasons);

        // Warp test: Adding/deleting position warps
        {
            WarpGroup group = p.GetRoom(0).GetWarpGroup();
            group.AddWarp(WarpSourceType.Position);
            Assert.Equal(4, group.Count);
            group.RemoveWarp(3);
            Assert.Equal(3, group.Count);
        }
    }

    [Fact]
    public static void TestWarpDataFilePristine()
    {
        // Creating & deleting warps should leave the file in exactly the same state as before.
        Project p = LoadProject(Game.Seasons);
        WarpGroup group = p.GetRoom(0).GetWarpGroup();
        Assert.Equal(3, group.Count);
        string initial = group.FileParser.GetOutputString();
        group.AddWarp(WarpSourceType.Position);
        group.AddWarp(WarpSourceType.Standard);
        group.AddWarp(WarpSourceType.Position);
        group.RemoveWarp(3);
        group.RemoveWarp(3);
        group.RemoveWarp(3);
        Assert.Equal(initial, group.FileParser.GetOutputString());
    }

    [Fact]
    public static void TestUndo()
    {
        Project p = LoadProject(Game.Ages);

        // undo, redo, undo of 1-tile change
        {
            var layout = p.GetRoomLayout(10, Season.None);
            Assert.Equal(98, layout.GetTile(5, 4));
            layout.SetTile(5, 4, 40);
            Assert.Equal(40, layout.GetTile(5, 4));
            p.TransactionManager.Undo();
            Assert.Equal(98, layout.GetTile(5, 4));
            p.TransactionManager.Redo();
            Assert.Equal(40, layout.GetTile(5, 4));
            p.TransactionManager.Undo();
            Assert.Equal(98, layout.GetTile(5, 4));
        }

        // undo/redo of object creation
        {
            var group = p.GetRoom(0).GetObjectGroup();
            Assert.Equal(0, group.GetNumObjects());
            group.AddObject(ObjectType.Interaction);
            Assert.Equal(1, group.GetNumObjects());
            p.TransactionManager.Undo();
            Assert.Equal(0, group.GetNumObjects());
            p.TransactionManager.Redo();
            Assert.Equal(1, group.GetNumObjects());
        }

        // More object creation undo/redo testing (this used to cause stale data errors with InstanceResolvers)
        {
            var group = p.GetRoom(1).GetObjectGroup();
            Assert.Equal(0, group.GetNumObjects());
            group.AddObject(ObjectType.Interaction);
            Assert.Equal(1, group.GetNumObjects());
            group.GetObject(0).SetX(0x50);
            p.TransactionManager.Undo();
            p.TransactionManager.Undo();
            p.TransactionManager.Redo();
            p.TransactionManager.Redo();
        }

        // undo/redo of warp creation
        {
            var group = p.GetRoom(0).GetWarpGroup();
            Assert.Equal(0, group.Count);
            group.AddWarp(WarpSourceType.Standard);
            Assert.Equal(1, group.Count);
            p.TransactionManager.Undo();
            Assert.Equal(0, group.Count);
            p.TransactionManager.Redo();
            Assert.Equal(1, group.Count);
        }
    }

    [Fact]
    public static void TestObjectWipe()
    {
        Project p = LoadProject(Game.Seasons);

        // Room contains shared object data, good for testing an edge case.
        var r = p.GetRoom(0x542);
        ObjectGroup group = r.GetObjectGroup();
        Assert.Equal(ObjectGroupType.Main, group.GetGroupType());
        Assert.Equal(5, group.GetAllGroups().Count());
        Assert.Equal(4, group.GetObjects().Count);

        group.Wipe();

        Assert.Equal(4, group.GetAllGroups().Count());
        Assert.Empty(group.GetObjects());
        foreach (ObjectGroup g in group.GetAllGroups())
            Assert.Empty(g.GetObjects());

        // Ensure that the respawning bush shared object data exists in another room still
        GameObject? obj = p.GetRoom(0x46a).GetObjectGroup().GetAllGroups().Where(
            (g) => g.GetGroupType() == ObjectGroupType.Shared).First().GetObject(0).GetGameObject();
        Assert.IsType<InteractionObject>(obj);
        Assert.Equal(0xc7, obj.ID);

        p.TransactionManager.Undo();

        Assert.Equal(ObjectGroupType.Main, group.GetGroupType());
        Assert.Equal(5, group.GetAllGroups().Count());
        Assert.Equal(4, group.GetObjects().Count);
    }

    [Fact]
    public static void TestWarpWipe()
    {
        Project p = LoadProject(Game.Ages);

        var r = p.GetRoom(0x68); // Shop entrance
        var g = r.GetWarpGroup();
        Assert.Equal(2, g.Count);
        g.Wipe();
        Assert.Equal(0, g.Count);
    }

    [Fact]
    public static void TestDuplicateWarpSources()
    {
        // The maku tree top variants in Seasons reuse a warp source (warpSource77ef). It's the only
        // place that does that! How annoying!
        Project p = LoadProject(Game.Seasons);

        WarpGroup g1 = p.GetRoom(0x26b).GetWarpGroup();
        WarpGroup g2 = p.GetRoom(0x21b).GetWarpGroup();

        // WarpGroup should automatically create separate WarpSourceData instances for each
        Assert.NotEqual(g1.GetWarp(0).SourceData, g2.GetWarp(0).SourceData);

        // But the data itself should be identical
        Assert.Equal(g1.GetWarp(0).SourceData.GetString(), g2.GetWarp(0).SourceData.GetString());

        // Make sure both of the two PositionWarps were duplicated, not just the one
        Assert.Equal(2, g1.Count);
        Assert.Equal(2, g2.Count);

        // In a normal room: Simply adding or removing a warp should not trigger the code that
        // duplicates the warp data.
        WarpGroup g = p.GetRoom(0xf6).GetWarpGroup(); // Bipin & Blossom's house
        WarpSourceData d = g.GetWarp(0).SourceData;
        Assert.Equal(WarpSourceType.Position, d.WarpSourceType);
        g.AddWarp(WarpSourceType.Position);
        Assert.Equal(d, g.GetWarp(0).SourceData);
    }

    [Fact]
    public static void TestWipeSeasons()
    {
        TestWipeEverything(Game.Seasons);
    }
    [Fact]
    public static void TestWipeAges()
    {
        TestWipeEverything(Game.Ages);
    }

    // Destroy all... Kill all...
    static void TestWipeEverything(Game game)
    {
        Project p = LoadProject(game);

        for (int r = 0; r < p.NumRooms; r++)
        {
            Room room = p.GetRoom(r);
            room.GetObjectGroup().Wipe();
            room.GetWarpGroup().Wipe();
        }

        for (int r = 0; r < p.NumRooms; r++)
        {
            Room room = p.GetRoom(r);
            Assert.Equal(0, room.GetObjectGroup().GetNumObjects());
            Assert.Equal(0, room.GetWarpGroup().Count);
        }
    }

    [Fact]
    public static void TestRegisterEventOnAll()
    {
        Project p = LoadProject(Game.Seasons);

        int tilesetCalled = 0;
        int musicCalled = 0;

        void tilesetCallback()
        {
            tilesetCalled++;
        }
        void musicCallback()
        {
            musicCalled++;
        }

        var unregisterTileset = p.RegisterEventOnAllRooms("Tileset", tilesetCallback);
        var unregisterMusic = p.RegisterEventOnAllRooms("Music", musicCallback);

        Assert.Equal(0, tilesetCalled);
        Assert.Equal(0, musicCalled);

        p.BeginTransaction("Transaction");

        p.GetRoom(0).TilesetIndex = 0xff;
        p.GetRoom(0).SetMusicID(0xff);

        // No callback until EndTransaction
        Assert.Equal(0, tilesetCalled);
        Assert.Equal(0, musicCalled);

        // Callback happens after EndTransaction
        p.EndTransaction();
        Assert.Equal(1, tilesetCalled);
        Assert.Equal(1, musicCalled);

        // Multiple callbacks don't mix each other up
        p.BeginTransaction("Transaction");
        p.GetRoom(1).TilesetIndex = 0xfe;
        p.EndTransaction();
        Assert.Equal(2, tilesetCalled);
        Assert.Equal(1, musicCalled);

        // Multiple writes only cause one callback
        p.BeginTransaction("Transaction");
        p.GetRoom(5).SetMusicID(0xfd);
        p.GetRoom(6).SetMusicID(0xfd);
        p.EndTransaction();
        Assert.Equal(2, tilesetCalled);
        Assert.Equal(2, musicCalled);

        // Unregistration works
        unregisterTileset();
        p.BeginTransaction("Transaction");
        p.GetRoom(7).TilesetIndex = 0xfc;
        p.GetRoom(7).SetMusicID(0xfc);
        p.EndTransaction();
        Assert.Equal(2, tilesetCalled);
        Assert.Equal(3, musicCalled);
    }

    [Fact]
    public static void TestMapleLocations()
    {
        Project p = LoadProject(Game.Ages);

        MapleLocationTable maple = p.GetMapleLocationTable(Companion.Moosh, false);
        Assert.False(maple.IsPresent(new(0, 0)));
        Assert.True(maple.IsPresent(new(1, 0)));
        Assert.True(maple.IsPresent(new(0, 6)));
        Assert.False(maple.IsPresent(new(4, 6)));

        maple.SetPresent(new(0, 0), true);
        Assert.True(maple.IsPresent(new(0, 0)));
        maple.SetPresent(new(0, 6), false);
        Assert.False(maple.IsPresent(new(0, 6)));
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    public static Project LoadProject(Game game)
    {
        ProjectConfig config = ProjectConfig.Load(ProjectBasePath);
        return new Project(ProjectBasePath, game, config);
    }
}
