namespace LynnaLab;

/// <summary>
/// A contiguous, but non-rectangular section of a grid. All Points contained in the segment are
/// connected through direct neighbors.
/// </summary>
public class GridRegion
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public GridRegion(IEnumerable<Point> Points, Color color)
    {
        this.Points = Points.ToList();
        this.Polygon = GeneratePolygon(this.Points);
        this.Color = color;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================

    public IEnumerable<Point> Points { get; }

    /// <summary>
    /// A polygon outlining the entire segment. Point (0, 0) in the grid considers its top-left
    /// corner to be (0, 0), and the bottom-right to be (1, 1).
    /// </summary>
    public Polygon Polygon { get; }

    public Color Color { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    // ================================================================================
    // Private methods
    // ================================================================================

    struct DirectedEdge
    {
        public DirectedEdge(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }

        public Vector2 Start { get; }
        public Vector2 End { get; }
    }

    // TODO: How does this handle "islands" in the polygon? Need multiple polygons?
    static Polygon GeneratePolygon(IEnumerable<Point> points)
    {
        var pointSet = new HashSet<Point>(points);
        if (pointSet.Count == 0)
            return new Polygon(new List<Vector2>());

        var edges = new List<DirectedEdge>();

        foreach (var p in pointSet)
        {
            float x = p.X;
            float y = p.Y;

            Vector2 tl = new Vector2(x - 0.0f, y - 0.0f);
            Vector2 tr = new Vector2(x + 1.0f, y - 0.0f);
            Vector2 br = new Vector2(x + 1.0f, y + 1.0f);
            Vector2 bl = new Vector2(x - 0.0f, y + 1.0f);

            if (!pointSet.Contains(new Point(p.X, p.Y - 1)))
                edges.Add(new DirectedEdge(tl, tr));
            if (!pointSet.Contains(new Point(p.X + 1, p.Y)))
                edges.Add(new DirectedEdge(tr, br));
            if (!pointSet.Contains(new Point(p.X, p.Y + 1)))
                edges.Add(new DirectedEdge(br, bl));
            if (!pointSet.Contains(new Point(p.X - 1, p.Y)))
                edges.Add(new DirectedEdge(bl, tl));
        }

        var nextEdges = new Dictionary<Vector2, Queue<DirectedEdge>>();
        foreach (var edge in edges)
        {
            if (!nextEdges.ContainsKey(edge.Start))
                nextEdges[edge.Start] = new Queue<DirectedEdge>();
            nextEdges[edge.Start].Enqueue(edge);
        }

        var start = edges.OrderBy(e => e.Start.Y).ThenBy(e => e.Start.X).First();
        var coords = new List<Vector2>();
        var current = start;

        while (true)
        {
            coords.Add(current.Start);

            if (current.End == start.Start)
                break;

            current = nextEdges[current.End].Dequeue();
        }

        // Remove collinear points
        if (coords.Count >= 3)
        {
            var simplified = new List<Vector2>();
            for (int i = 0; i < coords.Count; i++)
            {
                Vector2 prev = coords[(i - 1 + coords.Count) % coords.Count];
                Vector2 curr = coords[i];
                Vector2 next = coords[(i + 1) % coords.Count];

                Vector2 dir1 = curr - prev;
                Vector2 dir2 = next - curr;
                if (MathF.Sign(dir1.X) == MathF.Sign(dir2.X) && MathF.Sign(dir1.Y) == MathF.Sign(dir2.Y))
                    continue;

                simplified.Add(curr);
            }
            coords = simplified;
        }

        return new Polygon(coords);
    }
}
