namespace LynnaLib.Tests;

public class TestProject
{
    [Fact]
    public static void TestProject1()
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

    // ================================================================================
    // Static methods
    // ================================================================================

    public static Project LoadProject(Game game)
    {
        string dir = "../../../../oracles-disasm";
        ProjectConfig config = ProjectConfig.Load(dir);
        return new Project(dir, game, config);
    }
}
