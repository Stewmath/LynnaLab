using System.Diagnostics;
using System.IO;

namespace LynnaLib;

/// <summary>
/// Subclass of Tileset representing a tileset derived directly from the game's data. Changes to the
/// tileset will directly modify that data.
///
/// A RealTileset object is guaranteed to uniquely represent either:
/// - A tileset index (for tilesets with no season)
/// - A tileset index + a season number (for tilesets with a season)
/// </summary>
public class RealTileset : Tileset
{
    // ================================================================================
    // Constructors
    // ================================================================================
    internal RealTileset(Project p, int i, int season)
        : base(p)
    {
        Index = i;
        Season = season;

        parentData = Project.GetData("tilesetData", Index * 8);
        tilesetData = parentData;

        if (IsSeasonal && Season == -1)
        {
            throw new ProjectErrorException("Specified season for non-seasonal tileset");
        }
        else if (!IsSeasonal && Season != -1)
        {
            throw new ProjectErrorException("No season specified for seasonal tileset");
        }

        // If this is Seasons, it's possible that tilesetData does not point to 8 bytes as
        // expected, but instead to an "m_SeasonalData" macro.
        if (IsSeasonal)
        {
            tilesetData = Project.GetData(parentData.GetValue(0), Season * 8);
        }

        {
            string gfxFileName = null;
            if (IsSeasonal)
                gfxFileName = String.Format("gfx_tileset{0:x2}_{1}", Index, SeasonName);
            else
                gfxFileName = String.Format("gfx_tileset{0:x2}", Index);
            base.GfxFileStream = Project.LoadGfx(gfxFileName);

            if (base.GfxFileStream == null)
                throw new ProjectErrorException("Couldn't find \"" + gfxFileName + "\" in project.");
        }

        ConstructValueReferenceGroup();

        if (Project.Config.ExpandedTilesets)
        {
            // Watch for changes to the tileset mappings (tile indices + flags)
            var stream = GetExpandedMappingsFile();
            stream.ModifiedEvent += (_, args) =>
            {
                for (int i = (int)args.modifiedRangeStart / 8; i < (args.modifiedRangeEnd + 7) / 8; i++)
                    base.InvalidateTile(i);
                base.GenerateUsedTileList();
            };
        }

        base.SubclassInitializationFinished();
    }

    // ================================================================================
    // Variables
    // ================================================================================
    // These two Data's are the same unless it's a seasonal tileset.
    // In that case, parentData points to the table of seasonal tilesets.
    // (m_SeasonalTileset)
    readonly Data parentData;
    readonly Data tilesetData;


    // ================================================================================
    // Properties
    // ================================================================================

    public int Index { get; private set; }
    public int Season { get; private set; } // Tilesets with the same index but different season differ

    public string SeasonName
    {
        get
        {
            switch (Season)
            {
                case 0:
                    return "spring";
                case 1:
                    return "summer";
                case 2:
                    return "autumn";
                case 3:
                    return "winter";
                default:
                    throw new ProjectErrorException("Invalid season: " + Season);
            }
        }
    }

    public bool IsSeasonal
    {
        get { return parentData.CommandLowerCase == "m_seasonaltileset"; }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    // Functions dealing with subtiles
    public override byte GetSubTileIndex(int index, int x, int y)
    {
        VerifySubTileParams(index, x, y);

        if (Project.Config.ExpandedTilesets)
        {
            MemoryFileStream stream = GetExpandedMappingsFile();
            stream.Seek(index * 8 + y * 2 + x, SeekOrigin.Begin);
            return (byte)stream.ReadByte();
        }
        else
            return TilesetHeaderGroup.GetMappingsData(index * 8 + y * 2 + x);
    }
    public override void SetSubTileIndex(int index, int x, int y, byte value)
    {
        VerifySubTileParams(index, x, y);

        if (Project.Config.ExpandedTilesets)
        {
            MemoryFileStream stream = GetExpandedMappingsFile();
            stream.Seek(index * 8 + y * 2 + x, SeekOrigin.Begin);
            stream.WriteByte(value);
        }
        else
        {
            TilesetHeaderGroup.SetMappingsData(index * 8 + y * 2 + x, value);
            base.GenerateUsedTileList();
            base.InvalidateTile(index);
        }
    }
    public override byte GetSubTileFlags(int index, int x, int y)
    {
        VerifySubTileParams(index, x, y);

        if (Project.Config.ExpandedTilesets)
        {
            MemoryFileStream stream = GetExpandedMappingsFile();
            stream.Seek(index * 8 + 4 + y * 2 + x, SeekOrigin.Begin);
            return (byte)stream.ReadByte();
        }
        else
            return TilesetHeaderGroup.GetMappingsData(index * 8 + y * 2 + x + 4);
    }
    public override void SetSubTileFlags(int index, int x, int y, byte value)
    {
        VerifySubTileParams(index, x, y);

        if ((value & 0x08) == 0)
            LogHelper.GetLogger().Warn($"Tileset {Index:X2} tile {index:X2}: Wrote subtile flags with bank bit unset");

        if (Project.Config.ExpandedTilesets)
        {
            MemoryFileStream stream = GetExpandedMappingsFile();
            stream.Seek(index * 8 + 4 + y * 2 + x, SeekOrigin.Begin);
            stream.WriteByte(value);
        }
        else
        {
            TilesetHeaderGroup.SetMappingsData(index * 8 + y * 2 + x + 4, value);
            base.InvalidateTile(index);
        }
    }

    // Get the full collision byte for a tile.
    public override byte GetTileCollision(int index)
    {
        if (Project.Config.ExpandedTilesets)
        {
            MemoryFileStream stream = GetExpandedCollisionsFile();
            stream.Seek(index, SeekOrigin.Begin);
            return (byte)stream.ReadByte();
        }
        else
            return TilesetHeaderGroup.GetCollisionsData(index);
    }
    public override void SetTileCollision(int index, byte val)
    {
        if (Project.Config.ExpandedTilesets)
        {
            MemoryFileStream stream = GetExpandedCollisionsFile();
            stream.Seek(index, SeekOrigin.Begin);
            stream.WriteByte(val);
        }
        else
            TilesetHeaderGroup.SetCollisionsData(index, val);
    }

    public IList<Room> GetReferences()
    {
        var references = new List<Room>();
        for (int g = 0; g < Project.NumGroups; g++)
        {
            for (int r = 0; r < 0x100; r++)
            {
                Room room = Project.GetIndexedDataType<Room>((g << 8) | r);
                if (room.Group != room.ExpectedGroup)
                    continue;
                if (room.TilesetIndex == Index)
                    references.Add(room);
            }
        }
        return references;
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    Data GetDataIndex(int i)
    {
        Data data = tilesetData;
        for (int j = 0; j < i; j++)
            data = data.NextData;
        return data;
    }

    /// <summary>
    /// Set up all the ValueReferences making up the underlying tileset data.
    /// </summary>
    void ConstructValueReferenceGroup()
    {
        if (Project.GameString == "ages")
        {
            PastFlag = new DataValueReference(
                    GetDataIndex(1),
                    index: 0,
                    startBit: 7,
                    type: DataValueType.ByteBit);
            UnderwaterFlag = new DataValueReference(
                    GetDataIndex(1),
                    index: 0,
                    startBit: 6,
                    type: DataValueType.ByteBit);
        }
        else
        { // seasons
            SubrosiaFlag = new DataValueReference(GetDataIndex(1),
                        index: 0,
                        startBit: 7,
                        type: DataValueType.ByteBit);
            // NOTE: Seasons unused bit (byte 1, bit 6) has no ValueReference.
        }

        SidescrollFlag = new DataValueReference(GetDataIndex(1),
                    index: 0,
                    startBit: 5,
                    type: DataValueType.ByteBit);
        LargeIndoorFlag = new DataValueReference(GetDataIndex(1),
                    index: 0,
                    startBit: 4,
                    type: DataValueType.ByteBit);
        DungeonFlag = new DataValueReference(GetDataIndex(1),
                    index: 0,
                    startBit: 3,
                    type: DataValueType.ByteBit);
        SmallIndoorFlag = new DataValueReference(GetDataIndex(1),
                    index: 0,
                    startBit: 2,
                    type: DataValueType.ByteBit);
        MakuTreeFlag = new DataValueReference(GetDataIndex(1),
                    index: 0,
                    startBit: 1,
                    type: DataValueType.ByteBit);
        OutdoorFlag = new DataValueReference(GetDataIndex(1),
                    index: 0,
                    startBit: 0,
                    type: DataValueType.ByteBit);

        DungeonIndex = new DataValueReference(GetDataIndex(0),
                    index: 0,
                    startBit: 0,
                    endBit: 3,
                    type: DataValueType.ByteBits);
        CollisionType = new DataValueReference(GetDataIndex(0),
                    index: 0,
                    startBit: 4,
                    endBit: 6,
                    type: DataValueType.ByteBits);

        // These fields do nothing with the expanded tilesets patch.
        if (!Project.Config.ExpandedTilesets)
        {
            TilesetLayoutIndex = new DataValueReference(GetDataIndex(5),
                    index: 0,
                    type: DataValueType.Byte);
            UniqueGfx = new DataValueReference(GetDataIndex(2),
                    index: 0,
                    type: DataValueType.Byte,
                    constantsMappingString: "UniqueGfxMapping");
            MainGfx = new DataValueReference(GetDataIndex(3),
                    index: 0,
                    type: DataValueType.Byte,
                    constantsMappingString: "MainGfxMapping");
            LayoutGroup = new DataValueReference(GetDataIndex(6),
                    index: 0,
                    type: DataValueType.Byte,
                    maxValue: Project.NumLayoutGroups - 1);
        }

        PaletteHeader = new IntValueReferenceWrapper(
            new DataValueReference(GetDataIndex(4),
                    index: 0,
                    type: DataValueType.Byte,
                    constantsMappingString: "PaletteHeaderMapping"));
        AnimationIndex = new IntValueReferenceWrapper(
            new DataValueReference(GetDataIndex(7),
                                   index: 0,
                                   type: DataValueType.Byte));

        // NOTE: unused bit (byte 0, bit 7) has no ValueReference
    }

    MemoryFileStream GetExpandedMappingsFile()
    {
        if (IsSeasonal)
        {
            return Project.GetBinaryFile(
                String.Format("tileset_layouts_expanded/{0}/tilesetMappings{1:x2}_{2}.bin",
                              Project.GameString, Index, SeasonName));
        }
        else
        {
            return Project.GetBinaryFile(
            String.Format("tileset_layouts_expanded/{0}/tilesetMappings{1:x2}.bin",
                          Project.GameString, Index));
        }
    }

    MemoryFileStream GetExpandedCollisionsFile()
    {
        if (IsSeasonal)
        {
            return Project.GetBinaryFile(
                String.Format("tileset_layouts_expanded/{0}/tilesetCollisions{1:x2}_{2}.bin",
                              Project.GameString, Index, SeasonName));
        }
        else
        {
            return Project.GetBinaryFile(
                String.Format("tileset_layouts_expanded/{0}/tilesetCollisions{1:x2}.bin",
                              Project.GameString, Index));
        }
    }
}
