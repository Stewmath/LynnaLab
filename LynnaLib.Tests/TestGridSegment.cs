using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LynnaLab;
using Util;

namespace LynnaLib.Tests;

public class TestGridSegment
{
    [Fact]
    public void GridSegmentTest()
    {
        AssertCoords(
            new List<Point> {
                new Point(0, 0),
            },
            new List<Vector2> {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f),
            });

        AssertCoords(
            new List<Point> {
                new Point(0, 0),
                new Point(1, 0),
            },
            new List<Vector2> {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(1.5f, -0.5f),
                new Vector2(1.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f),
            });
    }


    static void AssertCoords(List<Point> points, IEnumerable<Vector2> expected)
    {
        var segment = new GridRegion(points);
        var coords = segment.Polygon.Coordinates.ToList();
        AssertSequenceEqual(coords, expected);
    }

    static void AssertSequenceEqual(IEnumerable<Vector2> coords, IEnumerable<Vector2> expected)
    {
        Assert.True(coords.SequenceEqual(expected),
            $"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", coords)}]");
    }
}
