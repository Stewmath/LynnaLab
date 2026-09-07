namespace LynnaLib;

/// <summary>
/// Represents one Maple Location table (ie. table for Ricky).
/// </summary>
public class MapleLocationTable : ProjectDataType, ProjectDataInstantiator
{
    // ================================================================================
    // Constructors
    // ================================================================================
    private MapleLocationTable(Project project, string label)
        : base(project, label)
    {
        this.Label = label;
    }

    static ProjectDataType ProjectDataInstantiator.Instantiate(Project p, string id)
    {
        return new MapleLocationTable(p, id);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================
    public string Label { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Return true if Maple appears at this point.
    /// </summary>
    public bool IsPresent(Point point)
    {
        var (data, bitmask) = GetDataAndBitmask(point);

        int value = data.GetIntValue(0);
        return (value & bitmask) == 0;
    }

    /// <summary>
    /// Update whether Maple appears at this location.
    /// </summary>
    public void SetPresent(Point point, bool present)
    {
        var (data, bitmask) = GetDataAndBitmask(point);

        int value = data.GetIntValue(0);
        if (present)
            value &= ~bitmask;
        else
            value |= bitmask;

        data.SetValue(0, "%" + Convert.ToString(value, 2).PadLeft(8, '0'));
    }

    /// <summary>
    /// Call "action" whenever any data in this table is modified. Returns a delegate to unregister
    /// the event.
    /// </summary>
    public Action RegisterModifiedEvent(Action action)
    {
        EventHandler<DataModifiedEventArgs> handler = (_, _) => action();
        foreach (Data data in GetAllData())
        {
            data.AddModifiedEventHandler(handler);
        }

        Action unregister = () =>
        {
            foreach (Data data in GetAllData())
            {
                data.RemoveModifiedEventHandler(handler);
            }
        };

        return unregister;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Note: Returned bitmask assumes reversed bit order (dbrev macro).
    /// </summary>
    (Data, int) GetDataAndBitmask(Point point)
    {
        if (!point.InRange(0, 0, 16, 16))
            throw new ArgumentException($"MapleLocationData.SetPresent: Invalid point {point}");

        Data data = Project.GetData(Label);
        for (int r = 0; r < point.Y * 2 + (point.X / 8); r++)
        {
            if (data.NextData == null)
                throw new AssemblyErrorException("Couldn't load maple location data?");
            data = data.NextData;
        }

        if (data.Command != "dbrev")
            throw new AssemblyErrorException("MapleLocationData: Expected 'dbrev' for data");
        if (data.GetNumValues() != 1)
            throw new AssemblyErrorException("MapleLocationData: Unexpected number of values for dbrev");

        int bitmask = (1 << (7 - (point.X % 8)));

        return (data, bitmask);
    }

    IEnumerable<Data> GetAllData()
    {
        Data data = Project.GetData(Label);
        yield return data;
        for (int r = 0; r < 31; r++)
        {
            if (data.NextData == null)
                throw new AssemblyErrorException("Couldn't load maple location data?");
            data = data.NextData;
            yield return data;
        }
    }
}
