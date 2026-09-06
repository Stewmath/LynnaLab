using LynnaLab;
using Util;

namespace LynnaLib.Tests;

public class TestGridRegionGenerator
{
    [Fact]
    public void Generate_SplitsDisconnectedAreasAndAssignsColors()
    {
        int[,] grid =
        {
            { 0, 1, 0 },
            { 1, 1, 1 },
            { 0, 1, 0 },
        };

        var generator = new GridRegionGenerator(3, 3, (p) => grid[p.Y, p.X]);
        var regions = generator.Regions;

        Assert.Equal(5, regions.Count);
        Assert.Equal([1, 1, 1, 1, 5], regions.Select(r => r.Points.Count()).Order().ToArray());

        GridRegion plusRegion = regions.Single(r => r.Points.Count() == 5);
        GridPoint center = plusRegion.Points.Single(p => p.Point.X == 1 && p.Point.Y == 1);
        GridPoint top = plusRegion.Points.Single(p => p.Point.X == 1 && p.Point.Y == 0);

        Assert.False(center.TopEdge);
        Assert.False(center.RightEdge);
        Assert.False(center.BottomEdge);
        Assert.False(center.LeftEdge);

        Assert.True(top.TopEdge);
        Assert.True(top.RightEdge);
        Assert.False(top.BottomEdge);
        Assert.True(top.LeftEdge);
    }

    [Fact]
    public void Generate_HandlesCaseThatDefeatsGreedyColorPlacement()
    {
        // This layout is 4-colorable overall, but a naive greedy ordering can paint itself into a
        // corner. So this test should exercise backtracking in the coloring algorithm.
        int[,] grid =
        {
            { 1, 1, 1, 1 },
            { 2, 3, 0, 3 },
            { 0, 0, 2, 3 },
            { 2, 1, 1, 3 },
        };

        var generator = new GridRegionGenerator(4, 4, (p) => grid[p.Y, p.X]);
        var regions = generator.Regions;

        Assert.Equal(9, regions.Count);

        for (int i = 0; i < regions.Count; i++)
        for (int j = i + 1; j < regions.Count; j++)
        {
            if (AreAdjacent(regions[i], regions[j]))
                Assert.NotEqual(regions[i].Color, regions[j].Color);
        }
    }

    static bool AreAdjacent(GridRegion a, GridRegion b)
    {
        HashSet<Point> points = b.Points.Select(p => p.Point).ToHashSet();

        foreach (GridPoint gridPoint in a.Points)
        {
            Point p = gridPoint.Point;
            if (points.Contains(new Point(p.X - 1, p.Y)) ||
                points.Contains(new Point(p.X + 1, p.Y)) ||
                points.Contains(new Point(p.X, p.Y - 1)) ||
                points.Contains(new Point(p.X, p.Y + 1)))
                return true;
        }

        return false;
    }
}
