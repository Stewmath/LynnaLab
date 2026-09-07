#nullable enable
namespace LynnaLab;

/// <summary>
/// Takes a function which converts a grid coordinate to a "region index". Grid positions with the
/// same "region index" are put into the same Region.
///
/// Except, not quite, because we don't want to allow regions that are disconnected to exist. So if
/// two disconnected points in the grid have the same region index, they actually become separate regions.
/// </summary>
public class GridRegionGenerator
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public GridRegionGenerator(int width, int height, Func<Point, int> getRegion, bool consistentColors=true)
    {
        this.Width = width;
        this.Height = height;
        this.getRegion = getRegion;

        Regenerate(consistentColors);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Func<Point, int> getRegion;

    // ================================================================================
    // Properties
    // ================================================================================

    public int Width { get; }
    public int Height { get; }

    public IReadOnlyList<GridRegion> Regions { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    [MemberNotNull(nameof(Regions))]
    public void Regenerate(bool consistentColors)
    {
        Dictionary<int, List<Point>> regionDictPass1 = new();

        // First pass: Group all indices with the same region index into the same region.
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int region = getRegion(new Point(x, y));
            if (!regionDictPass1.TryGetValue(region, out var points))
            {
                points = new List<Point>();
                regionDictPass1[region] = points;
            }

            points.Add(new Point(x, y));
        }

        // Second pass: Regions that are not connected together should be split into separate regions.
        List<List<Point>> regionPoints = new();
        Dictionary<Point, int> pointToRegion = new();

        foreach (var points in regionDictPass1.Values)
        {
            HashSet<Point> remainingPoints = new(points);

            while (remainingPoints.Count != 0)
            {
                Point start = remainingPoints.First();
                Queue<Point> queue = new();
                List<Point> connectedPoints = new();
                int regionId = regionPoints.Count;

                queue.Enqueue(start);
                remainingPoints.Remove(start);

                while (queue.Count != 0)
                {
                    Point p = queue.Dequeue();
                    connectedPoints.Add(p);
                    pointToRegion[p] = regionId;

                    foreach (Point neighbor in GetNeighbors(p))
                    {
                        if (remainingPoints.Remove(neighbor))
                            queue.Enqueue(neighbor);
                    }
                }

                regionPoints.Add(connectedPoints);
            }
        }

        // Third pass: Create adjacency matrix / graph edges.

        Dictionary<int, HashSet<int>> adjacency = new();
        for (int i = 0; i < regionPoints.Count; i++)
            adjacency[i] = new HashSet<int>();

        for (int regionId = 0; regionId < regionPoints.Count; regionId++)
        {
            foreach (Point p in regionPoints[regionId])
            {
                foreach (Point neighbor in GetNeighbors(p))
                {
                    if (pointToRegion.TryGetValue(neighbor, out int neighborRegionId) && neighborRegionId != regionId)
                        adjacency[regionId].Add(neighborRegionId);
                }
            }
        }

        // Fourth pass: Assign colors.

        if (consistentColors)
            AssignConsistentColors(regionPoints, adjacency);
        else
            AssignNonOverlappingColors(regionPoints, adjacency);
    }

    public GridRegion GetRegionForPoint(Point p)
    {
        return Regions.First(region => region.Points.Any(gridPoint => gridPoint.Point == p));
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Using this coloring algorithm: Any two regions that share the same value have the same
    /// color. But this does not guarantee that adjacent regions have different colors.
    /// </summary>
    [MemberNotNull(nameof(Regions))]
    void AssignConsistentColors(List<List<Point>> regionPoints, Dictionary<int, HashSet<int>> adjacency)
    {
        Color[] colors = [
            Color.Cyan,
            Color.DarkOrange,
            Color.Lime,
            Color.Red,
            Color.Azure,
            Color.Yellow,
            Color.LightBlue,
            Color.Magenta,
            Color.DeepPink,
            Color.Purple,
            Color.Green,
        ];
        int colorIndex = 0;
        int[] assignedColors = Enumerable.Repeat(-1, regionPoints.Count).ToArray();

        for (int r = 0; r < regionPoints.Count; r++)
        {
            if (assignedColors[r] != -1)
                continue;
            assignedColors[r] = colorIndex;
            colorIndex = (colorIndex + 1) % colors.Length;

            int value = getRegion(regionPoints[r].First());

            for (int i = r + 1; i < regionPoints.Count; i++)
            {
                if (value == getRegion(regionPoints[i].First()))
                    assignedColors[i] = assignedColors[r];
            }
        }

        Regions = Enumerable.Range(0, regionPoints.Count)
            .Select(regionId => new GridRegion(
                regionId,
                regionPoints[regionId],
                colors[assignedColors[regionId]]))
            .ToList();
    }

    /// <summary>
    /// Assign colors using a solution to the map coloring problem so that no adjacent regions have
    /// the same color. I'm not really using this because it seems more important to ensure that
    /// detached regions with the same value share a color, which this doesn't guarantee.
    /// </summary>
    [MemberNotNull(nameof(Regions))]
    void AssignNonOverlappingColors(List<List<Point>> regionPoints, Dictionary<int, HashSet<int>> adjacency)
    {
        Color[] colors = [Color.Cyan, Color.DarkOrange, Color.Lime, Color.Red];
        int[] assignedColors = Enumerable.Repeat(-1, regionPoints.Count).ToArray();

        List<int> orderedRegionIds = Enumerable.Range(0, regionPoints.Count)
            .OrderByDescending(id => adjacency[id].Count)
            .ToList();

        if (!TryAssignColors(adjacency, orderedRegionIds, colors.Length, assignedColors))
            throw new InvalidOperationException("Unable to assign a color to all adjacent regions with the available palette.");

        Regions = Enumerable.Range(0, regionPoints.Count)
            .Select(regionId => new GridRegion(
                regionId,
                regionPoints[regionId],
                colors[assignedColors[regionId]]))
            .ToList();
    }

    bool TryAssignColors(Dictionary<int, HashSet<int>> adjacency, List<int> orderedRegionIds, int numColors, int[] assignedColors, int orderIndex=0)
    {
        if (orderIndex == orderedRegionIds.Count)
            return true;

        int regionId = orderedRegionIds[orderIndex];
        HashSet<int> usedColors = adjacency[regionId]
            .Where(neighborId => assignedColors[neighborId] != -1)
            .Select(neighborId => assignedColors[neighborId])
            .ToHashSet();

        for (int colorIndex = 0; colorIndex < numColors; colorIndex++)
        {
            if (usedColors.Contains(colorIndex))
                continue;

            assignedColors[regionId] = colorIndex;
            if (TryAssignColors(adjacency, orderedRegionIds, numColors, assignedColors, orderIndex + 1))
                return true;
        }

        assignedColors[regionId] = -1;
        return false;
    }

    IEnumerable<Point> GetNeighbors(Point p)
    {
        yield return new Point(p.X - 1, p.Y);
        yield return new Point(p.X + 1, p.Y);
        yield return new Point(p.X, p.Y - 1);
        yield return new Point(p.X, p.Y + 1);
    }
}
