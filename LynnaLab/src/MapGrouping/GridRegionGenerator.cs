namespace LynnaLab;

/// <summary>
/// Takes a TileGrid and a function which converts an index in the tilegrid to a "region index".
/// Tilegrid indices with the same "region index" are put into the same Region.
///
/// Except, not quite, because we don't want to allow regions that are disconnected to exist. So if
/// two disconnected points in the grid have the same region index, they actually become separate regions.
/// </summary>
public class GridRegionGenerator
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public GridRegionGenerator(TileGrid grid, Func<int, int> getRegion)
    {
        this.Grid = grid;
        this.getRegion = getRegion;

        Generate();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Func<int, int> getRegion;

    // ================================================================================
    // Properties
    // ================================================================================

    public TileGrid Grid { get; }
    public IReadOnlyList<GridRegion> Regions { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    // ================================================================================
    // Private methods
    // ================================================================================

    void Generate()
    {
        Dictionary<int, List<Point>> regionDictPass1 = new();

        // First pass: Group all indices with the same region index into the same region.
        for (int index = 0; index < Grid.MaxIndex; index++)
        {
            int region = getRegion(index);
            if (!regionDictPass1.TryGetValue(region, out var points))
            {
                points = new List<Point>();
                regionDictPass1[region] = points;
            }

            int x = index % Grid.Width;
            int y = index / Grid.Width;
            points.Add(new Point(x, y));
        }

        // Second pass: Regions that are not connected together should be split into separate regions.
        List<GridRegion> regions = new();

        Color[] colors = [Color.Blue, Color.DarkOrange, Color.Lime, Color.Red];
        int colorIndex = 0;

        foreach (var points in regionDictPass1.Values)
        {
            HashSet<Point> remainingPoints = new(points);

            while (remainingPoints.Count != 0)
            {
                Point start = remainingPoints.First();
                Queue<Point> queue = new();
                List<Point> connectedPoints = new();

                queue.Enqueue(start);
                remainingPoints.Remove(start);

                while (queue.Count != 0)
                {
                    Point p = queue.Dequeue();
                    connectedPoints.Add(p);

                    Point[] neighbors = new Point[]
                    {
                        new Point(p.X - 1, p.Y),
                        new Point(p.X + 1, p.Y),
                        new Point(p.X, p.Y - 1),
                        new Point(p.X, p.Y + 1),
                    };

                    foreach (Point neighbor in neighbors)
                    {
                        if (remainingPoints.Remove(neighbor))
                            queue.Enqueue(neighbor);
                    }
                }

                regions.Add(new GridRegion(connectedPoints, colors[colorIndex++]));
                colorIndex %= 4;
            }
        }

        Regions = regions;
    }
}
