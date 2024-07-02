using System;
using System.IO;
using System.Collections.Generic;
using Util;

namespace LynnaLib
{
    public class Tileset : IDisposable
    {
        FileParser tilesetFile;

        // These two Data's are the same unless it's a seasonal tileset.
        // In that case, parentData points to the table of seasonal tilesets.
        // (m_SeasonalTileset)
        Data parentData;
        Data tilesetData;

        ValueReferenceGroup vrg;

        TilesetHeaderGroup tilesetHeaderGroup;
        AnimationGroup animationGroup;

        GraphicsState graphicsState;

        Bitmap[] tileImagesCache = new Bitmap[256];
        Bitmap fullCachedImage = new Bitmap(16 * 16, 16 * 16);

        WeakEventWrapper<ReloadableStream> gfxStreamEventWrapper
            = new WeakEventWrapper<ReloadableStream>();
        WeakEventWrapper<PaletteHeaderGroup> paletteEventWrapper
            = new WeakEventWrapper<PaletteHeaderGroup>();

        bool constructorFinished = false;

        // For dynamically showing an animation
        int[] animationPos = new int[4];
        int[] animationCounter = new int[4];

        List<byte>[] usedTileList = new List<byte>[256];

        // This is generally a reference to GLib.Idle.Add, but this library doesn't import GLib, so
        // it's up to the caller to set this to something with the "LazyTileRedraw" function. If
        // this is null, then tile updates are always fully redrawn before any method here returns.
        Action<Func<bool>> idleHandlerAdder = null;


        // Events

        public delegate void LayoutGroupModifiedHandler();

        // Invoked whenever the image of a tile is modified
        public event EventHandler<int> TileModifiedEvent;

        public event LayoutGroupModifiedHandler LayoutGroupModifiedEvent;
        public event EventHandler<EventArgs> PaletteHeaderGroupModifiedEvent;


        // Properties

        public Project Project { get; private set; }
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

        // The GraphicsState which contains the data as it will be loaded into
        // vram.
        public GraphicsState GraphicsState
        {
            get { return graphicsState; }
        }

        public bool IsSeasonal
        {
            get { return parentData.CommandLowerCase == "m_seasonaltileset"; }
        }

        // Following properties correspond to the 8 bytes defining the tileset.

        public bool SidescrollFlag
        {
            get { return vrg.GetIntValue("Sidescrolling") != 0; }
        }
        public bool MakuTreeFlag
        {
            get { return vrg.GetIntValue("Maku Tree") != 0; }
        }
        public bool SubrosiaFlag // Will only work in seasons of course
        {
            get { return vrg.GetIntValue("Subrosia") != 0; }
        }
        public bool SmallIndoorFlag // Will only work in seasons of course
        {
            get { return vrg.GetIntValue("Small Indoor Room") != 0; }
        }
        public int UniqueGfx
        {
            get
            {
                return vrg.GetIntValue("Unique Gfx");
            }
            set
            {
                vrg.SetValue("Unique Gfx", value);
            }
        }
        public int MainGfx
        {
            get
            {
                return vrg.GetIntValue("Main Gfx");
            }
            set
            {
                vrg.SetValue("Main Gfx", value);
            }
        }
        public int PaletteHeader
        {
            get
            {
                return vrg.GetIntValue("Palettes");
            }
            set
            {
                vrg.SetValue("Palettes", value);
            }
        }
        public PaletteHeaderGroup PaletteHeaderGroup
        {
            get
            {
                try
                {
                    return Project.GetIndexedDataType<PaletteHeaderGroup>(PaletteHeader);
                }
                catch (InvalidPaletteHeaderGroupException)
                {
                    return null;
                }
            }
        }
        public int TilesetLayoutIndex
        {
            get
            {
                return vrg.GetIntValue("Layout");
            }
            set
            {
                vrg.SetValue("Layout", value);
            }
        }
        public int LayoutGroup
        {
            get
            {
                return vrg.GetIntValue("Layout Group");
            }
            set
            {
                vrg.SetValue("Layout Group", value);
            }
        }
        public int AnimationIndex
        {
            get
            {
                return vrg.GetIntValue("Animations");
            }
            set
            {
                vrg.SetValue("Animations", value);
            }
        }

        internal Tileset(Project p, int i, int season)
        {
            Project = p;
            Index = i;
            Season = season;

            tilesetFile = Project.GetFileWithLabel("tilesetData");

            parentData = tilesetFile.GetData("tilesetData", Index * 8);
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

            ConstructValueReferenceGroup();

            // Data modified handlers
            GetDataIndex(2).AddModifiedEventHandler((sender, args) => LoadUniqueGfx());
            GetDataIndex(3).AddModifiedEventHandler((sender, args) => LoadMainGfx());
            GetDataIndex(4).AddModifiedEventHandler((sender, args) => LoadPaletteHeader());
            GetDataIndex(5).AddModifiedEventHandler((sender, args) => LoadTilesetLayout());
            GetDataIndex(6).AddModifiedEventHandler((sender, args) =>
            {
                if (LayoutGroupModifiedEvent != null)
                    LayoutGroupModifiedEvent();
            });
            GetDataIndex(7).AddModifiedEventHandler((sender, args) => LoadAnimation());

            // Initialize graphics state
            graphicsState = new GraphicsState();

            // Global palettes
            PaletteHeaderGroup globalPaletteHeaderGroup =
                Project.GetIndexedDataType<PaletteHeaderGroup>(0xf);
            graphicsState.AddPaletteHeaderGroup(globalPaletteHeaderGroup, PaletteGroupType.Common);

            LoadTilesetLayout();
            LoadAllGfxData();
            LoadPaletteHeader();

            gfxStreamEventWrapper.Bind<EventArgs>("ExternallyModifiedEvent", (o, a) =>
            {
                LoadAllGfxData();
                InvalidateAllTiles();
            });
            paletteEventWrapper.Bind<EventArgs>("ModifiedEvent", OnPaletteDataModified);

            constructorFinished = true;
        }

        void LoadAllGfxData()
        {
            graphicsState.ClearGfx();

            if (Project.Config.ExpandedTilesets)
            {
                string name = null;
                if (IsSeasonal)
                    name = String.Format("gfx_tileset{0:x2}_{1}", Index, SeasonName);
                else
                    name = String.Format("gfx_tileset{0:x2}", Index);
                Stream stream = Project.LoadGfx(name);
                if (stream == null)
                    throw new ProjectErrorException("Couldn't find \"" + name + "\" in project.");
                byte[] gfx = new byte[0x1000];
                stream.Read(gfx, 0, 0x1000);
                graphicsState.AddRawVram(1, 0x800, gfx);
                gfxStreamEventWrapper.ReplaceEventSource(stream as ReloadableStream);
            }
            else
            {
                LoadUniqueGfx();
                LoadMainGfx();
            }

            LoadAnimation();
        }

        Data GetDataIndex(int i)
        {
            Data data = tilesetData;
            for (int j = 0; j < i; j++)
                data = data.NextData;
            return data;
        }

        // Alternative method to access data fields (this data feeds the GUI directly)
        void ConstructValueReferenceGroup()
        {
            var list = new List<ValueReference>();

            if (Project.GameString == "ages")
            {
                list.AddRange(new ValueReference[] {
                    new DataValueReference(GetDataIndex(1),
                            name: "Past",
                            index: 0,
                            startBit: 7,
                            type: DataValueType.ByteBit,
                            tooltip: "Set in the past. Determines which minimap comes up with the select button, maybe other stuff too. If a tileset can be used in both the present in the past, this is left unchecked, and the 'roomsInAltWorld' table is checked instead."),
                    new DataValueReference(GetDataIndex(1),
                            name: "Underwater",
                            index: 0,
                            startBit: 6,
                            type: DataValueType.ByteBit,
                            tooltip: "Set in underwater rooms.")
                });
            }
            else
            { // seasons
                list.AddRange(new ValueReference[] {
                    new DataValueReference(GetDataIndex(1),
                            name: "Subrosia",
                            index: 0,
                            startBit: 7,
                            type: DataValueType.ByteBit,
                            tooltip: "Set in subrosia. Determines which minimap comes up with the select button, maybe other stuff too. If a tileset can be used in both the overworld and subrosia, this is left unchecked, and the 'roomsInAltWorld' table is checked instead."),
                    new DataValueReference(GetDataIndex(1),
                            name: "Bit 6 (0x40)",
                            index: 0,
                            startBit: 6,
                            type: DataValueType.ByteBit,
                            tooltip: "Likely unused.")
                });
            }
            list.AddRange(new ValueReference[] {
                new DataValueReference(GetDataIndex(1),
                        name: "Sidescrolling",
                        index: 0,
                        startBit: 5,
                        type: DataValueType.ByteBit,
                        tooltip: "Set in sidescrolling rooms."),
                new DataValueReference(GetDataIndex(1),
                        name: "Large Indoor Room",
                        index: 0,
                        startBit: 4,
                        type: DataValueType.ByteBit,
                        tooltip: "Set in large, indoor rooms (which aren't real dungeons, ie. ambi's palace). Seems to disable certain properties of dungeons? (Ages only?)"),
                new DataValueReference(GetDataIndex(1),
                        name: "Dungeon",
                        index: 0,
                        startBit: 3,
                        type: DataValueType.ByteBit,
                        tooltip: "Flag is set on dungeons, but also on any room which has a layout in the 'dungeons' tab, even if it's not a real dungeon (ie. ambi's palace). In that case set the 'Large Indoor Room' flag also."),
                new DataValueReference(GetDataIndex(1),
                        name: "Small Indoor Room",
                        index: 0,
                        startBit: 2,
                        type: DataValueType.ByteBit,
                        tooltip: "Set in small indoor rooms."),
                new DataValueReference(GetDataIndex(1),
                        name: "Maku Tree",
                        index: 0,
                        startBit: 1,
                        type: DataValueType.ByteBit,
                        tooltip: "In Ages, this hardcodes the location on the minimap for the maku tree screens, and prevents harp use. Not sure if this does anything in Seasons?"),
                new DataValueReference(GetDataIndex(1),
                        name: "Outdoors",
                        index: 0,
                        startBit: 0,
                        type: DataValueType.ByteBit,
                        tooltip: "Affects whether you can use gale seeds, and other things. In Ages this must be checked for the minimap to update your position."),
            });

            list.AddRange(new ValueReference[] {
                new DataValueReference(GetDataIndex(0),
                        name: "Dungeon Index",
                        index: 0,
                        startBit: 0,
                        endBit: 3,
                        type: DataValueType.ByteBits,
                        tooltip: "Dungeon index (should match value in the Dungeons tab; Dungeon bit must be set)."),
                new DataValueReference(GetDataIndex(0),
                        name: "Collision Type",
                        index: 0,
                        startBit: 4,
                        endBit: 6,
                        type: DataValueType.ByteBits,
                        tooltip: ("Determines most collision behaviour aside from solidity (ie. water, holes). The meaning of the values differ between ages and seasons.\n\n"
                                       + (Project.Game == Game.Seasons
                                       ? "0: Overworld\n1: Indoors\n2: Maku Tree\n3: Indoors\n4: Dungeons\n5: Sidescrolling"
                                       : "0: Overworld\n1: Indoors\n2: Dungeons\n3: Sidescrolling\n4: Underwater\n5: Unused?")))
            });

            // These fields do nothing with the expanded tilesets patch.
            if (!Project.Config.ExpandedTilesets)
            {
                list.AddRange(new ValueReference[] {
                    new DataValueReference(GetDataIndex(2),
                        name: "Unique Gfx",
                        index: 0,
                        type: DataValueType.Byte,
                        constantsMappingString: "UniqueGfxMapping",
                        useConstantAlias: true),
                    new DataValueReference(GetDataIndex(3),
                        name: "Main Gfx",
                        index: 0,
                        type: DataValueType.Byte,
                        constantsMappingString: "MainGfxMapping",
                        useConstantAlias: true),
                    new DataValueReference(GetDataIndex(5),
                        name: "Layout",
                        index: 0,
                        type: DataValueType.Byte),
                    new DataValueReference(GetDataIndex(6),
                        name: "Layout Group",
                        index: 0,
                        type: DataValueType.Byte,
                        maxValue: Project.NumLayoutGroups - 1,
                        tooltip: "Determines where to read the room layout from (ie. for value '2', it reads from the file 'room02XX.bin', even if the group number is not 2). In general, to prevent confusion, all rooms in the same overworld (or group) should use tilesets which have the same value for this."),
                });
            }

            list.AddRange(new ValueReference[] {
                new DataValueReference(GetDataIndex(4),
                        name: "Palettes",
                        index: 0,
                        type: DataValueType.Byte,
                        constantsMappingString: "PaletteHeaderMapping",
                        useConstantAlias: true),
                new DataValueReference(GetDataIndex(7),
                        name: "Animations",
                        index: 0,
                        type: DataValueType.Byte),
            });

            // list.AddRange(new ValueReference[] {
            //     new DataValueReference(GetDataIndex(0),
            //             name: "Unused(?) Bit",
            //             index: 0,
            //             startBit: 7,
            //             type: DataValueType.ByteBit),
            // });


            vrg = new ValueReferenceGroup(list);

            vrg["Palettes"].ModifiedEvent += (sender, args) => PaletteHeaderGroupModifiedEvent?.Invoke(this, null);
        }


        // Call this with a non-null argument (should be like GLib.Idle.Add) to arrange for tile
        // redraws to be done lazily.
        public void LazyTileRedraw(Action<Func<bool>> idleHandlerAdder)
        {
            this.idleHandlerAdder = idleHandlerAdder;
        }

        // Clear all tile image caches. This is called when certain major fields of the tileset are
        // modified. This will trigger an asynchronous redraw of the tileset image (unless called
        // from the constructor), which is costly.
        public void InvalidateAllTiles()
        {
            for (int i = 0; i < 256; i++) {
                tileImagesCache[i]?.Dispose();
                tileImagesCache[i] = null;
            }
            if (constructorFinished)
                DrawAllTiles();
        }
        // Trigger asynchronous redraw of all tiles that are marked as needing to be redrawn
        public void DrawAllTiles()
        {
            if (idleHandlerAdder != null)
            {
                tileUpdaterIndex = 0;
                idleHandlerAdder(TileUpdater);
            }
            else
            {
                for (int i = 0; i < 256; i++)
                {
                    GetTileImage(i);
                }
            }
        }

        int tileUpdaterIndex = 256;

        // TileUpdater is called by GLib.IdleHandler, which just calls this "when it has time".
        bool TileUpdater()
        {
            if (tileUpdaterIndex == 256)
                return false;
            int numDrawnTiles = 0;
            while (tileUpdaterIndex < 256 && numDrawnTiles < 16)
            {
                if (tileImagesCache[tileUpdaterIndex] == null)
                {
                    numDrawnTiles++;
                    GetTileImage(tileUpdaterIndex); // Will generate the image if it's not cached
                }
                tileUpdaterIndex++;
            }

            // Return true to indicate to GLib that it should call this again later
            return true;
        }

        // This returns an image for the specified tile index, and also updates the "master" tileset
        // image if said tile has been changed.
        public Bitmap GetTileImage(int index)
        {
            if (tileImagesCache[index] != null)
                return tileImagesCache[index];

            var image = new Bitmap(16, 16);

            // Draw the tile
            using (Cairo.Context cr = image.CreateContext())
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        int tileIndex = GetSubTileIndex(index, x, y);
                        int flags = GetSubTileFlags(index, x, y);

                        int tileOffset = 0x1000 + ((sbyte)tileIndex) * 16;

                        byte[] src = new byte[16];
                        Array.Copy(graphicsState.VramBuffer[1], tileOffset, src, 0, 16);
                        using (var subImage = GbGraphics.TileToBitmap(
                            src,
                            GraphicsState.GetBackgroundPalettes()[flags & 7],
                            flags))
                        {
                            cr.SetSourceSurface(subImage, x * 8, y * 8);
                            cr.Paint();
                        }
                    }
                }
            }

            // Update the full tileset image
            using (Cairo.Context cr = fullCachedImage.CreateContext())
            {
                cr.SetSourceSurface(image, (index % 16) * 16, (index / 16) * 16);
                cr.Paint();
            }

            tileImagesCache[index] = image;

            TileModifiedEvent?.Invoke(this, index);
            return image;
        }

        // Functions dealing with subtiles
        public byte GetSubTileIndex(int index, int x, int y)
        {
            if (Project.Config.ExpandedTilesets)
            {
                MemoryFileStream stream = GetExpandedMappingsFile();
                stream.Seek(index * 8 + y * 2 + x, SeekOrigin.Begin);
                return (byte)stream.ReadByte();
            }
            else
                return tilesetHeaderGroup.GetMappingsData(index * 8 + y * 2 + x);
        }
        public void SetSubTileIndex(int index, int x, int y, byte value)
        {
            if (Project.Config.ExpandedTilesets)
            {
                MemoryFileStream stream = GetExpandedMappingsFile();
                stream.Seek(index * 8 + y * 2 + x, SeekOrigin.Begin);
                stream.WriteByte(value);
            }
            else
                tilesetHeaderGroup.SetMappingsData(index * 8 + y * 2 + x, value);

            GenerateUsedTileList();
            tileImagesCache[index]?.Dispose();
            tileImagesCache[index] = null;
            GetTileImage(index); // Redraw tile image
            TileModifiedEvent?.Invoke(this, index);
        }
        public byte GetSubTileFlags(int index, int x, int y)
        {
            if (Project.Config.ExpandedTilesets)
            {
                MemoryFileStream stream = GetExpandedMappingsFile();
                stream.Seek(index * 8 + 4 + y * 2 + x, SeekOrigin.Begin);
                return (byte)stream.ReadByte();
            }
            else
                return tilesetHeaderGroup.GetMappingsData(index * 8 + y * 2 + x + 4);
        }
        public void SetSubTileFlags(int index, int x, int y, byte value)
        {
            if (Project.Config.ExpandedTilesets)
            {
                MemoryFileStream stream = GetExpandedMappingsFile();
                stream.Seek(index * 8 + 4 + y * 2 + x, SeekOrigin.Begin);
                stream.WriteByte(value);
            }
            else
            {
                tilesetHeaderGroup.SetMappingsData(index * 8 + y * 2 + x + 4, value);
            }
            tileImagesCache[index]?.Dispose();
            tileImagesCache[index] = null;
            GetTileImage(index); // Redraw tile image
            TileModifiedEvent?.Invoke(this, index);
        }

        // Get the "basic collision" of a subtile (whether or not that part is
        // solid). This ignores the upper half of the collision data bytes and
        // assumes it is zero.
        public bool GetSubTileBasicCollision(int index, int x, int y)
        {
            byte b = GetTileCollision(index);
            byte i = (byte)(1 << (3 - (x + y * 2)));
            return (b & i) != 0;
        }
        public void SetSubTileBasicCollision(int index, int x, int y, bool val)
        {
            byte b = GetTileCollision(index);
            byte i = (byte)(1 << (3 - (x + y * 2)));
            b = (byte)(b & ~i);
            if (val)
                b |= i;
            SetTileCollision(index, b);
        }

        // Get the full collision byte for a tile.
        public byte GetTileCollision(int index)
        {
            if (Project.Config.ExpandedTilesets)
            {
                MemoryFileStream stream = GetExpandedCollisionsFile();
                stream.Seek(index, SeekOrigin.Begin);
                return (byte)stream.ReadByte();
            }
            else
                return tilesetHeaderGroup.GetCollisionsData(index);
        }
        public void SetTileCollision(int index, byte val)
        {
            if (Project.Config.ExpandedTilesets)
            {
                MemoryFileStream stream = GetExpandedCollisionsFile();
                stream.Seek(index, SeekOrigin.Begin);
                stream.WriteByte(val);
            }
            else
                tilesetHeaderGroup.SetCollisionsData(index, val);
        }

        // This function doesn't guarantee to return a fully rendered image, only what is currently
        // available. Call "DrawAllTiles" to begin drawing the full image (though it will run
        // asynchronously).
        // The full image is not drawn after initialization, but it IS updated properly when any
        // properties of the tileset are modified. This is because it is inefficient to draw the
        // full tileset image for every single tileset when drawing the minimap.
        public Bitmap GetFullCachedImage()
        {
            return fullCachedImage;
        }

        // Returns a list of tiles which have changed
        public IList<byte> UpdateAnimations(int frames)
        {
            HashSet<byte> changedTiles = new HashSet<byte>();
            if (animationGroup == null)
                return new List<byte>();

            for (int i = 0; i < animationGroup.NumAnimations; i++)
            {
                Animation animation = animationGroup.GetAnimationIndex(i);
                animationCounter[i] -= frames;
                while (animationCounter[i] <= 0)
                {
                    animationPos[i]++;
                    if (animationPos[i] >= animation.NumIndices)
                        animationPos[i] = 0;
                    int pos = animationPos[i];
                    animationCounter[i] += animation.GetCounter(pos);
                    GfxHeaderData header = animation.GetGfxHeader(pos);
                    graphicsState.AddGfxHeader(header, GfxHeaderType.Animation);
                    //                     Console.WriteLine(i + ":" + animationPos[i]);

                    // Check which tiles changed
                    if (header.DestAddr >= 0x8800 && header.DestAddr < 0x9800 &&
                            header.DestBank == 1)
                    {
                        for (int addr = header.DestAddr;
                                addr < header.DestAddr +
                                (Project.EvalToInt(header.GetValue(2)) + 1) * 16;
                                addr++)
                        {
                            int tile = (addr - 0x8800) / 16;
                            tile -= 128;
                            if (tile < 0)
                                tile += 256;
                            foreach (byte metatile in usedTileList[tile])
                            {
                                changedTiles.Add(metatile);
                            }
                        }
                    }
                }
            }

            foreach (int t in changedTiles)
            {
                // Refresh the image of each animated metatile
                tileImagesCache[t]?.Dispose();
                tileImagesCache[t] = null;
                GetTileImage(t);

                TileModifiedEvent?.Invoke(this, t);
            }

            return new List<byte>(changedTiles);
        }

        public void ResetAnimation()
        {
            if (!graphicsState.HasGfxHeaderType(GfxHeaderType.Animation))
                return;

            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Animation);

            for (int i = 0; i < 4; i++)
            {
                animationPos[i] = 0;
                animationCounter[i] = 0;
            }

            InvalidateAllTiles();
        }

        public ValueReferenceGroup GetValueReferenceGroup()
        {
            return vrg;
        }

        public IList<Room> GetReferences()
        {
            var references = new List<Room>();
            for (int g=0; g<Project.NumGroups; g++)
            {
                for (int r=0; r<0x100; r++)
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

        public void Dispose()
        {
            graphicsState = null;
            foreach (Bitmap b in tileImagesCache)
                b?.Dispose();
            tileImagesCache = null;
            fullCachedImage.Dispose();
            fullCachedImage = null;
            paletteEventWrapper.UnbindAll();
        }


        void LoadMainGfx()
        {
            if (Project.Config.ExpandedTilesets) // Field has no effect in this case
                return;

            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Main);

            GfxHeaderData header = Project.GetData($"gfxHeader{MainGfx:x2}") as GfxHeaderData;
            if (header != null)
            {
                while (true)
                {
                    graphicsState.AddGfxHeader(header, GfxHeaderType.Main);
                    Data next = header.NextData;
                    if (next.CommandLowerCase == "m_gfxheaderend")
                        break;
                    GfxHeaderData nextHeader = next as GfxHeaderData;
                    if (nextHeader != null)
                    {
                        header = nextHeader;
                    }
                    else
                    {
                        throw new ProjectErrorException($"Malformed GFX header 0x{MainGfx:x2}?");
                    }
                }
            }
            InvalidateAllTiles();
        }

        void LoadUniqueGfx()
        {
            if (Project.Config.ExpandedTilesets) // Field has no effect in this case
                return;

            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Unique);
            if (UniqueGfx != 0)
            {
                GfxHeaderData header =
                    Project.GetData($"uniqueGfxHeader{UniqueGfx:x2}") as GfxHeaderData;
                if (header != null)
                {
                    while (true)
                    {
                        graphicsState.AddGfxHeader(header, GfxHeaderType.Unique);
                        Data next = header.NextData;
                        if (next.CommandLowerCase == "m_gfxheaderend")
                            break;
                        GfxHeaderData nextHeader = header.NextData as GfxHeaderData;
                        if (nextHeader != null)
                        {
                            header = nextHeader;
                        }
                        else
                        {
                            throw new ProjectErrorException($"Malformed unique GFX header 0x{UniqueGfx:x2}?");
                        }
                    }
                }
            }
            InvalidateAllTiles();
        }

        void LoadPaletteHeader()
        {
            paletteEventWrapper.ReplaceEventSource(PaletteHeaderGroup);
            graphicsState.RemovePaletteGroupType(PaletteGroupType.Main);
            if (PaletteHeaderGroup != null)
                graphicsState.AddPaletteHeaderGroup(PaletteHeaderGroup, PaletteGroupType.Main);
            InvalidateAllTiles();
        }

        void LoadTilesetLayout()
        {
            if (!Project.Config.ExpandedTilesets)
            {
                tilesetHeaderGroup = Project.GetIndexedDataType<TilesetHeaderGroup>(TilesetLayoutIndex);
                InvalidateAllTiles();
            }

            GenerateUsedTileList();
        }

        // Generate usedTileList for quick lookup of which metatiles use which 4 gameboy tiles.
        void GenerateUsedTileList()
        {
            for (int j = 0; j < 256; j++)
                usedTileList[j] = new List<byte>();
            for (int j = 0; j < 256; j++)
            {
                // j = index of metatile
                bool[] used = new bool[256];
                for (int k = 0; k < 4; k++)
                {
                    int tile = GetSubTileIndex(j, k % 2, k / 2);
                    if (!used[tile])
                    {
                        usedTileList[tile].Add((byte)j);
                        used[tile] = true;
                    }
                }
            }
        }

        void LoadAnimation()
        {
            if (AnimationIndex < Project.NumAnimations)
                animationGroup = Project.GetIndexedDataType<AnimationGroup>(AnimationIndex);
            else
                animationGroup = null;

            ResetAnimation();
        }

        void OnPaletteDataModified(object sender, EventArgs args)
        {
            // TODO: This will cause a full redraw of all tiles. Can we do better? (ie. only signal
            // that the tiles are invalidated and don't redraw them until they're requested)
            LoadPaletteHeader();
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
}
