namespace LynnaLib;

/// <summary>
/// A "fake" tileset whose data is largely derived from another "source" tileset, but which can have
/// certain properties modified. Basically it is used when one needs to have a "dummy" tileset which
/// does not represent any actual data and is safe to modify without consequence.
/// </summary>
public class FakeTileset : Tileset
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public FakeTileset(Tileset source)
        : base(source.Project)
    {
        ConstructValueReferences(source);
        LoadSubTileIndices(source);
        LoadSubTileFlags(source);
        LoadGraphics(source);

        base.SubclassInitializationFinished();
    }

    // ================================================================================
    // Variables
    // ================================================================================
    byte[] tileCollisions = new byte[256];
    byte[,,] tileIndices = new byte[256,2,2];
    byte[,,] tileFlags = new byte[256,2,2];

    // ================================================================================
    // Properties
    // ================================================================================

    public override string TransactionIdentifier { get { return $"faketileset"; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    // Functions dealing with subtiles
    public override byte GetSubTileIndex(int index, int x, int y)
    {
        VerifySubTileParams(index, x, y);
        return tileIndices[index,x,y];
    }
    public override void SetSubTileIndex(int index, int x, int y, byte value)
    {
        VerifySubTileParams(index, x, y);

        tileIndices[index,x,y] = value;

        base.GenerateUsedTileList();
        base.InvalidateTile(index);
    }
    public override byte GetSubTileFlags(int index, int x, int y)
    {
        VerifySubTileParams(index, x, y);
        return tileFlags[index,x,y];
    }
    public override void SetSubTileFlags(int index, int x, int y, byte value)
    {
        VerifySubTileParams(index, x, y);

        tileFlags[index,x,y] = value;

        base.InvalidateTile(index);
    }

    public override byte GetTileCollision(int index)
    {
        return tileCollisions[index];
    }
    public override void SetTileCollision(int index, byte val)
    {
        tileCollisions[index] = val;
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Set up all the ValueReferences making up the underlying tileset data.
    /// For FakeTileset this simply creates read-only references to the "source" tileset's data.
    /// </summary>
    void ConstructValueReferences(Tileset source)
    {
        // Lambda to make a local copy of a ValueReference that takes the initial value of the
        // ValueReference, but then can be modified independantly.
        Func<ValueReference, ValueReference> cloneBoolValueReference = (vr) =>
        {
            bool value = vr.GetIntValue() != 0;
            return new AbstractBoolValueReference(
                Project,
                getter: () => value,
                setter: (v) =>
                {
                    value = v;
                }
            );
        };
        // Same as above but for int values
        Func<ValueReference, ValueReference> cloneIntValueReference = (vr) =>
        {
            int value = vr.GetIntValue();
            return new AbstractIntValueReference(
                Project,
                getter: () => value,
                setter: (v) =>
                {
                    value = v;
                },
                maxValue: vr.MaxValue,
                minValue: vr.MinValue,
                constantsMappingString: vr.ConstantsMappingString
            );
        };

        if (Project.Game == Game.Ages)
        {
            PastFlag = cloneBoolValueReference(source.PastFlag);
            UnderwaterFlag = cloneBoolValueReference(source.UnderwaterFlag);
        }
        else
        { // seasons
            SubrosiaFlag = cloneBoolValueReference(source.SubrosiaFlag);
        }

        SidescrollFlag = cloneBoolValueReference(source.SidescrollFlag);
        LargeIndoorFlag = cloneBoolValueReference(source.LargeIndoorFlag);
        DungeonFlag = cloneBoolValueReference(source.DungeonFlag);
        SmallIndoorFlag = cloneBoolValueReference(source.SmallIndoorFlag);
        MakuTreeFlag = cloneBoolValueReference(source.MakuTreeFlag);
        OutdoorFlag = cloneBoolValueReference(source.OutdoorFlag);

        DungeonIndex = cloneIntValueReference(source.DungeonIndex);
        CollisionType = cloneIntValueReference(source.CollisionType);

        // These fields do nothing with the expanded tilesets patch.
        if (!Project.Config.ExpandedTilesets)
        {
            TilesetLayoutIndex = cloneIntValueReference(source.TilesetLayoutIndex);
            UniqueGfx = cloneIntValueReference(source.UniqueGfx);
            MainGfx = cloneIntValueReference(source.MainGfx);
            LayoutGroup = cloneIntValueReference(source.LayoutGroup);
        }

        PaletteHeader = cloneIntValueReference(source.PaletteHeader);
        AnimationIndex = cloneIntValueReference(source.AnimationIndex);
    }
}
