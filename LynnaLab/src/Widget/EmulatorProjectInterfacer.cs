namespace LynnaLab;

public class EmulatorProjectInterfacer
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public EmulatorProjectInterfacer(Project p)
    {
        Project = p;

        SpecialObjectClass = new()
        {
            Offset = 0,
            Name = "Special Object",
            Plural = "Special Objects",
            ConstantsMapping = Project.SpecialObjectMapping
        };
        ItemObjectClass = new()
        {
            Offset = 0,
            Name = "Item",
            Plural = "Items",
            ConstantsMapping = Project.ItemMapping
        };
        InteractionObjectClass = new()
        {
            Offset = 0x40,
            Name = "Interaction",
            Plural = "Interactions",
            ConstantsMapping = Project.InteractionMapping
        };
        EnemyObjectClass = new()
        {
            Offset = 0x80,
            Name = "Enemy",
            Plural = "Enemies",
            ConstantsMapping = Project.EnemyMapping
        };
        PartObjectClass = new()
        {
            Offset = 0xc0,
            Name = "Part",
            Plural = "Parts",
            ConstantsMapping = Project.PartMapping
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================

    public Project Project { get; }

    public EmulatorObjectClass SpecialObjectClass { get; }
    public EmulatorObjectClass ItemObjectClass { get; }
    public EmulatorObjectClass InteractionObjectClass { get; }
    public EmulatorObjectClass EnemyObjectClass { get; }
    public EmulatorObjectClass PartObjectClass { get; }

    public ConstantsMapping MusicMapping { get { return Project.MusicMapping; } }
    public ConstantsMapping SfxMapping { get { return Project.SfxMapping; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public EmulatorObjectClass GetObjectClass(int addr)
    {
        switch (addr & 0xc0)
        {
            case 0x00:
                return addr < 0xd200 ? SpecialObjectClass : ItemObjectClass;
            case 0x40:
                return InteractionObjectClass;
            case 0x80:
                return EnemyObjectClass;
            case 0xc0:
                return PartObjectClass;
            default:
                throw new NotImplementedException();
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    // ================================================================================
    // Static methods
    // ================================================================================
}


public class EmulatorObjectClass
{
    public required int Offset { get; init; }
    public required string Name { get; init; }
    public required string Plural { get; init; }
    public required LynnaLib.ConstantsMapping ConstantsMapping { get; init; }

    public string GetName(int id, bool withPrefix=true)
    {
        string s = ConstantsMapping.ByteToString(id);
        if (withPrefix)
            return s;
        return ConstantsMapping.RemovePrefix(s);
    }
}
