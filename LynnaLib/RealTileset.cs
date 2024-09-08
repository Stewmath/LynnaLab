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

        // Data modified handlers
        GetDataIndex(2).AddModifiedEventHandler((sender, args) => OnUniqueGfxChanged());
        GetDataIndex(3).AddModifiedEventHandler((sender, args) => OnMainGfxChanged());
        GetDataIndex(4).AddModifiedEventHandler((sender, args) => OnPaletteHeaderChanged());
        GetDataIndex(5).AddModifiedEventHandler((sender, args) => OnTilesetLayoutChanged());
        GetDataIndex(6).AddModifiedEventHandler((sender, args) => OnLayoutGroupChanged());
        GetDataIndex(7).AddModifiedEventHandler((sender, args) => OnAnimationChanged());

        base.ReloadAll();
    }

    // ================================================================================
    // Variables
    // ================================================================================
    ValueReferenceGroup vrg;

    // These two Data's are the same unless it's a seasonal tileset.
    // In that case, parentData points to the table of seasonal tilesets.
    // (m_SeasonalTileset)
    Data parentData;
    Data tilesetData;


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

    public ValueReferenceGroup ValueReferenceGroup { get { return vrg; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    // Functions dealing with subtiles
    public override byte GetSubTileIndex(int index, int x, int y)
    {
        Debug.Assert(index >= 0 && index <= 0xff && x >= 0 && x <= 1 && y >= 0 && y <= 1);

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
        Debug.Assert(index >= 0 && index <= 0xff && x >= 0 && x <= 1 && y >= 0 && y <= 1);

        if (Project.Config.ExpandedTilesets)
        {
            MemoryFileStream stream = GetExpandedMappingsFile();
            stream.Seek(index * 8 + y * 2 + x, SeekOrigin.Begin);
            stream.WriteByte(value);
        }
        else
            TilesetHeaderGroup.SetMappingsData(index * 8 + y * 2 + x, value);

        base.GenerateUsedTileList();
        base.InvalidateTile(index);
    }
    public override byte GetSubTileFlags(int index, int x, int y)
    {
        Debug.Assert(index >= 0 && index <= 0xff && x >= 0 && x <= 1 && y >= 0 && y <= 1);

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
        Debug.Assert(index >= 0 && index <= 0xff && x >= 0 && x <= 1 && y >= 0 && y <= 1);

        if (Project.Config.ExpandedTilesets)
        {
            MemoryFileStream stream = GetExpandedMappingsFile();
            stream.Seek(index * 8 + 4 + y * 2 + x, SeekOrigin.Begin);
            stream.WriteByte(value);
        }
        else
        {
            TilesetHeaderGroup.SetMappingsData(index * 8 + y * 2 + x + 4, value);
        }
        base.InvalidateTile(index);
    }

    // Get the "basic collision" of a subtile (whether or not that part is
    // solid). This ignores the upper half of the collision data bytes and
    // assumes it is zero.
    public override bool GetSubTileBasicCollision(int index, int x, int y)
    {
        Debug.Assert(index >= 0 && index <= 0xff && x >= 0 && x <= 1 && y >= 0 && y <= 1);

        byte b = GetTileCollision(index);
        byte i = (byte)(1 << (3 - (x + y * 2)));
        return (b & i) != 0;
    }
    public override void SetSubTileBasicCollision(int index, int x, int y, bool val)
    {
        Debug.Assert(index >= 0 && index <= 0xff && x >= 0 && x <= 1 && y >= 0 && y <= 1);

        byte b = GetTileCollision(index);
        byte i = (byte)(1 << (3 - (x + y * 2)));
        b = (byte)(b & ~i);
        if (val)
            b |= i;
        SetTileCollision(index, b);
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
        var descList = new List<ValueReferenceDescriptor>();

        var addDescriptor = (ValueReference vr, string name, bool editable = true, string tooltip = null) =>
        {
            var descriptor = new ValueReferenceDescriptor(
                vr, name, editable, tooltip);
            descList.Add(descriptor);
        };


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

            addDescriptor(PastFlag, "Past", true,
                          "Set in the past. Determines which minimap comes up with the select button, maybe other stuff too. If a tileset can be used in both the present in the past, this is left unchecked, and the 'roomsInAltWorld' table is checked instead.");
            addDescriptor(UnderwaterFlag, "Underwater", true,
                          "Set in underwater rooms.");
        }
        else
        { // seasons
            SubrosiaFlag = new DataValueReference(GetDataIndex(1),
                        index: 0,
                        startBit: 7,
                        type: DataValueType.ByteBit);

            addDescriptor(SubrosiaFlag, "Subrosia", true,
                        "Set in subrosia. Determines which minimap comes up with the select button, maybe other stuff too. If a tileset can be used in both the overworld and subrosia, this is left unchecked, and the 'roomsInAltWorld' table is checked instead.");

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

        addDescriptor(SidescrollFlag, "Sidescrolling", true,
                      "Set in sidescrolling rooms.");
        addDescriptor(LargeIndoorFlag, "Large Indoor Room", true,
                      "Set in large, indoor rooms (which aren't real dungeons, ie. ambi's palace). Seems to disable certain properties of dungeons? (Ages only?)");
        addDescriptor(DungeonFlag, "Is Dungeon", true,
                      "Flag is set on dungeons, but also on any room which has a layout in the 'dungeons' tab, even if it's not a real dungeon (ie. ambi's palace). In that case set the 'Large Indoor Room' flag also.");
        addDescriptor(SmallIndoorFlag, "Small Indoor Room", true,
                      "Set in small indoor rooms.");
        addDescriptor(MakuTreeFlag, "Maku Tree", true,
                      "In Ages, this hardcodes the location on the minimap for the maku tree screens, and prevents harp use. Not sure if this does anything in Seasons?");
        addDescriptor(OutdoorFlag, "Outdoors", true,
                      "Affects whether you can use gale seeds, and other things. In Ages this must be checked for the minimap to update your position.");

        addDescriptor(DungeonIndex, "Dungeon Index", true,
                      "Dungeon index (should match value in the Dungeons tab; Dungeon bit must be set).");
        addDescriptor(CollisionType, "Collision Type", true,
                      ("Determines most collision behaviour aside from solidity (ie. water, holes). The meaning of the values differ between ages and seasons.\n\n"
                                   + (Project.Game == Game.Seasons
                                   ? "0: Overworld\n1: Indoors\n2: Maku Tree\n3: Indoors\n4: Dungeons\n5: Sidescrolling"
                                   : "0: Overworld\n1: Indoors\n2: Dungeons\n3: Sidescrolling\n4: Underwater\n5: Unused?")));

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

            addDescriptor(TilesetLayoutIndex, "Layout Index", true, null);
            addDescriptor(UniqueGfx, "Unique GFX Index", true, null);
            addDescriptor(MainGfx, "Main GFX Index", true, null);
            addDescriptor(LayoutGroup, "Layout Group", true,
                "Determines where to read the room layout from (ie. for value '2', it reads from the file 'room02XX.bin', even if the group number is not 2). In general, to prevent confusion, all rooms in the same overworld (or group) should use tilesets which have the same value for this.");
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

        addDescriptor(PaletteHeader, "Palettes", true, null);
        addDescriptor(AnimationIndex, "Animation Index", true, null);

        // NOTE: unused bit (byte 0, bit 7) has no ValueReference

        vrg = new ValueReferenceGroup(descList);
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
