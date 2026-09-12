namespace LynnaLab;

/// <summary>
/// A GridRegion consists of a bunch of GridPoints, which are just Points that also specify which
/// directions are edges, if any.
/// </summary>
public class GridPoint
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public GridPoint(Point point, bool topEdge, bool rightEdge, bool bottomEdge, bool leftEdge)
    {
        this.Point = point;
        this.TopEdge = topEdge;
        this.RightEdge = rightEdge;
        this.BottomEdge = bottomEdge;
        this.LeftEdge = leftEdge;
    }

    // ================================================================================
    // Properties
    // ================================================================================

    public Point Point { get; }

    public int X { get { return Point.X; } }
    public int Y { get { return Point.Y; } }

    public bool TopEdge { get; }
    public bool RightEdge { get; }
    public bool BottomEdge { get; }
    public bool LeftEdge { get; }
}

/// <summary>
/// A contiguous, but non-rectangular section of a grid. All Points contained in the segment are
/// connected through direct neighbors.
/// </summary>
public class GridRegion
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public GridRegion(int regionID, IEnumerable<Point> points, Color color)
    {
        List<Point> pointList = points.ToList();
        HashSet<Point> pointSet = pointList.ToHashSet();

        this.Points = pointList.Select(p => new GridPoint(
            p,
            !pointSet.Contains(new Point(p.X, p.Y - 1)),
            !pointSet.Contains(new Point(p.X + 1, p.Y)),
            !pointSet.Contains(new Point(p.X, p.Y + 1)),
            !pointSet.Contains(new Point(p.X - 1, p.Y)))).ToList();
        this.Color = color;
        this.RegionID = regionID;
    }

    // ================================================================================
    // Properties
    // ================================================================================

    public IEnumerable<GridPoint> Points { get; }

    public Color Color { get; }

    /// <summary>
    /// A unique region ID value. NOT equivalent to the underlying value that the region uses (ie.
    /// tileset index) because multiple regions can have the same tileset (if detached from each
    /// other).
    /// </summary>
    public int RegionID { get; }
}
