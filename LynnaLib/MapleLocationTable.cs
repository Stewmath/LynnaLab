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
        if (!point.InRange(0, 0, 16, 16))
            throw new Exception($"MapleLocationData.IsPresent: Invalid point {point}");

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

        int value = data.GetIntValue(0);

        return ((1 << (7 - (point.X % 8))) & value) == 0;
    }
}
