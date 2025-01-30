using System.Diagnostics;
using System.IO;

namespace LynnaLib
{
    /// <summary>
    /// Represents a Tileset. Subclass is responsible for determining where the tileset data is
    /// sourced from.
    /// </summary>
    public abstract class Tileset : IDisposable
    {
        // ================================================================================
        // Variables
        // ================================================================================

        int inhibitRedraw = 0;
        int inhibitUsedTileListUpdate = 0;

        Bitmap[] tileImagesCache = new Bitmap[256];
        bool[] tileImagesDrawn = new bool[256];

        EventWrapper<ReloadableStream> gfxStreamEventWrapper
            = new EventWrapper<ReloadableStream>();
        EventWrapper<PaletteHeaderGroup> paletteEventWrapper
            = new EventWrapper<PaletteHeaderGroup>();

        // TODO: This class is not fully undo-proofed. It works on the hack-base branch with the
        // expanded tilesets patch which simplifies things.

        GraphicsState graphicsState;

        // Animation stuff below here - undo/redo not tested
        AnimationGroup animationGroup;

        int[] animationPos = new int[4];
        int[] animationCounter = new int[4];

        List<byte>[] usedTileList = new List<byte>[256];


        // ================================================================================
        // Events
        // ================================================================================

        public delegate void LayoutGroupModifiedHandler();

        // Invoked whenever the image of a tile is modified
        public event EventHandler<int> TileModifiedEvent;

        public event LayoutGroupModifiedHandler LayoutGroupModifiedEvent;

        // This is mainly used by FakeTileset instances
        public event Action<object> DisposedEvent;


        // ================================================================================
        // Properties
        // ================================================================================

        public Project Project { get; private set; }

        public bool Disposed { get { return tileImagesCache == null; } }

        /// <summary>
        /// The GraphicsState which contains the data as it will be loaded into vram.
        /// </summary>
        public GraphicsState GraphicsState
        {
            get { return graphicsState; }
        }

        public ValueReferenceGroup ValueReferenceGroup { get; private set; }

        // Following properties correspond to the 8 bytes defining the tileset.
        // Subclasses must set these up in their constructors.

        public BoolValueReferenceWrapper SidescrollFlag { get; protected set; }
        public BoolValueReferenceWrapper LargeIndoorFlag { get; protected set; }
        public BoolValueReferenceWrapper DungeonFlag { get; protected set; }
        public BoolValueReferenceWrapper SmallIndoorFlag { get; protected set; }
        public BoolValueReferenceWrapper MakuTreeFlag { get; protected set;}
        public BoolValueReferenceWrapper OutdoorFlag { get; protected set; }

        // Seasons-only flags
        public BoolValueReferenceWrapper SubrosiaFlag { get; protected set; }

        // Ages-only flags
        public BoolValueReferenceWrapper PastFlag { get; protected set; }
        public BoolValueReferenceWrapper UnderwaterFlag { get; protected set; }

        public IntValueReferenceWrapper UniqueGfx { get; protected set; }
        public IntValueReferenceWrapper MainGfx { get; protected set; }
        public IntValueReferenceWrapper PaletteHeader { get; protected set; }

        public IntValueReferenceWrapper TilesetLayoutIndex { get; protected set; }
        public IntValueReferenceWrapper LayoutGroup { get; protected set; }
        public IntValueReferenceWrapper AnimationIndex { get; protected set; }
        public IntValueReferenceWrapper DungeonIndex { get; protected set; }
        public IntValueReferenceWrapper CollisionType { get; protected set; }

        public Stream GfxFileStream { get; protected set; }

        // End of properties that subclass must set in constructor


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

        public abstract string TransactionIdentifier { get; }

        protected TilesetHeaderGroup TilesetHeaderGroup { get; private set; }

        // ================================================================================
        // Constructors
        // ================================================================================

        internal Tileset(Project p)
        {
            Project = p;

            // Initialize blank tile images
            for (int i = 0; i < 256; i++)
            {
                tileImagesCache[i] = new Bitmap(16, 16);
            }

            // Initialize graphics state
            graphicsState = new GraphicsState();

            // Global palettes
            PaletteHeaderGroup globalPaletteHeaderGroup =
                Project.GetIndexedDataType<PaletteHeaderGroup>(0xf);
            graphicsState.AddPaletteHeaderGroup(globalPaletteHeaderGroup, PaletteGroupType.Common);

            gfxStreamEventWrapper.Bind<EventArgs>("ExternallyModifiedEvent", (o, a) =>
            {
                LoadAllGfxData();
                InvalidateAllTiles();
            });
            paletteEventWrapper.Bind<EventArgs>("ModifiedEvent", OnPaletteDataModified);

            // Subclass constructor should call SubclassInitializationFinished() after their
            // constructors are finished to begin loading the graphics.
            // Ideally we'd have all the information we need already within this constructor, but
            // subclasses are providing data through protected properties which aren't initialized
            // yet. This is probably against some kind of C# design pattern.
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        // Clear all tile image caches. This is called when certain major fields of the tileset are
        // modified. This will trigger an asynchronous redraw of the tileset image (unless called
        // from the constructor), which is costly.
        internal void InvalidateAllTiles()
        {
            if (inhibitRedraw != 0)
                return;
            for (int i = 0; i < 256; i++) {
                tileImagesDrawn[i] = false;
            }
            TileModifiedEvent?.Invoke(this, -1);
        }

        /// <summary>
        /// Returns a TileDescription struct that has all the data needed to render a tile.
        /// </summary>
        public TileDescription GetTileDescription(int index)
        {
            // Draw the tile
            var getSubTileDescription = (int index, int x, int y) =>
            {
                int tileIndex = GetSubTileIndex(index, x, y);
                byte flags = GetSubTileFlags(index, x, y);
                return new SubTileDescription(GetSubTileGfxBytes(tileIndex), flags);
            };

            SubTileDescription tl = getSubTileDescription(index, 0, 0);
            SubTileDescription tr = getSubTileDescription(index, 1, 0);
            SubTileDescription bl = getSubTileDescription(index, 0, 1);
            SubTileDescription br = getSubTileDescription(index, 1, 1);

            return new TileDescription(tl, tr, bl, br);
        }

        /// <summary>
        /// This returns an image for the specified tile index. This used to be the basis for
        /// rendering in LynnaLab, but now a lot of stuff is done GPU-side, whereas this is
        /// CPU-based.
        /// </summary>
        public Bitmap GetTileBitmap(int index)
        {
            if (tileImagesDrawn[index])
                return tileImagesCache[index];

            var image = tileImagesCache[index];

            TileDescription desc = GetTileDescription(index);
            GbGraphics.RenderTile(image, 0, 0, desc, GraphicsState.GetBackgroundPalettes());

            tileImagesDrawn[index] = true;
            image.MarkModified();

            return image;
        }

        public ReadOnlySpan<byte> GetSubTileGfxBytes(int subTileIndex)
        {
            int tileOffset = 0x1000 + ((sbyte)subTileIndex) * 16;
            return graphicsState.VramBuffer[1].AsSpan().Slice(tileOffset, 16);
        }

        public byte[] GetTileMapBytes()
        {
            return GetTileData(false);
        }
        public byte[] GetTileFlagBytes()
        {
            return GetTileData(true);
        }
        private byte[] GetTileData(bool flags)
        {
            byte[] data = new byte[32 * 32];

            for (int t=0; t<256; t++)
            {
                int tx = t % 16;
                int ty = t / 16;

                for (int x=0; x<2; x++)
                {
                    for (int y=0; y<2; y++)
                    {
                        if (flags)
                            data[(ty * 2 + y) * 32 + (tx * 2 + x)] = GetSubTileFlags(t, x, y);
                        else
                            data[(ty * 2 + y) * 32 + (tx * 2 + x)] = GetSubTileIndex(t, x, y);
                    }
                }
            }

            return data;
        }

        // Functions dealing with subtiles
        public abstract byte GetSubTileIndex(int index, int x, int y);
        public abstract void SetSubTileIndex(int index, int x, int y, byte value);
        public abstract byte GetSubTileFlags(int index, int x, int y);
        public abstract void SetSubTileFlags(int index, int x, int y, byte value);
        public abstract byte GetTileCollision(int index);
        public abstract void SetTileCollision(int index, byte value);


        /// <summary>
        /// Get the "basic collision" of a subtile (whether or not that part is
        /// solid). This ignores the upper half of the collision data bytes and
        /// assumes it is zero.
        /// </summary>
        public bool GetSubTileBasicCollision(int index, int x, int y)
        {
            VerifySubTileParams(index, x, y);

            byte b = GetTileCollision(index);
            byte i = (byte)(1 << (3 - (x + y * 2)));
            return (b & i) != 0;
        }
        /// <summary>
        /// Sets the "basic collision" of a subtile. If the collision value >= 0x10, then this
        /// discards the upper bits. One should probably avoid calling this in that situation.
        /// </summary>
        public void SetSubTileBasicCollision(int index, int x, int y, bool val)
        {
            VerifySubTileParams(index, x, y);

            byte b = (byte)(GetTileCollision(index) & 0x0f);
            byte i = (byte)(1 << (3 - (x + y * 2)));
            b = (byte)(b & ~i);
            if (val)
                b |= i;
            SetTileCollision(index, b);
        }

        public int GetSubTilePalette(int index, int x, int y)
        {
            VerifySubTileParams(index, x, y);
            return GetSubTileFlags(index, x, y) & 7;
        }

        public void SetSubTilePalette(int index, int x, int y, int palette)
        {
            VerifySubTileParams(index, x, y);
            Debug.Assert(palette >= 0 && palette <= 7);

            byte flags = GetSubTileFlags(index, x, y);
            flags = (byte)((flags & (~7)) | palette);
            SetSubTileFlags(index, x, y, flags);
        }


        // Returns a list of tiles which have changed
        // NOTE: This code is stale, won't work with new gpu-based rendering
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
                                (Project.Eval(header.GetValue(2)) + 1) * 16;
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
                tileImagesDrawn[t] = false;
                GetTileBitmap(t);

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

        /// <summary>
        /// Returns true if the given tileIndex has a cached Bitmap image that is up-to-date
        /// </summary>
        public bool TileIsRendered(int tileIndex)
        {
            return tileImagesDrawn[tileIndex];
        }

        /// <summary>
        /// Clones all data fields from another tileset into this tileset.
        /// Typically used to load a FakeTileset's data into a RealTileset.
        /// </summary>
        public void LoadFrom(Tileset source)
        {
            inhibitRedraw++;

            LoadSubTileIndices(source);
            LoadSubTileFlags(source);
            LoadGraphics(source);
            LoadCollisions(source);
            LoadProperties(source);

            inhibitRedraw--;

            InvalidateAllTiles();
            GenerateUsedTileList();
        }

        /// <summary>
        /// Replace the tile index list with the values from the given tileset source
        /// </summary>
        public void LoadSubTileIndices(Tileset source)
        {
            inhibitUsedTileListUpdate++;
            for (int tile = 0; tile < 256; tile++)
            {
                for (int subtile = 0; subtile < 4; subtile++)
                {
                    var (x, y) = (subtile % 2, subtile / 2);
                    SetSubTileIndex(tile, x, y, source.GetSubTileIndex(tile, x, y));
                }
            }
            inhibitUsedTileListUpdate--;
            GenerateUsedTileList();
        }

        /// <summary>
        /// Replace the tile flag list with the values from the given tileset source
        /// </summary>
        public void LoadSubTileFlags(Tileset source)
        {
            inhibitRedraw++;

            for (int tile = 0; tile < 256; tile++)
            {
                for (int subtile = 0; subtile < 4; subtile++)
                {
                    var (x, y) = (subtile % 2, subtile / 2);
                    SetSubTileFlags(tile, x, y, source.GetSubTileFlags(tile, x, y));
                }
            }

            inhibitRedraw--;
            InvalidateAllTiles();
        }

        /// <summary>
        /// Replace the graphics with the data from the source.
        /// TODO: Write change to file
        /// </summary>
        public void LoadGraphics(Tileset source)
        {
            if (GfxFileStream != source.GfxFileStream)
            {
                Stream origStream = GfxFileStream;
                Stream newStream = source.GfxFileStream;

                if (this is RealTileset) // Only real tilesets matter to the undo system
                {
                    Project.UndoState.OnRewind("Load tileset graphics", () =>
                    { // On undo
                        GfxFileStream = origStream;
                        LoadAllGfxData();
                        InvalidateAllTiles();
                    }, (isRedo) => // On redo / right now
                    {
                        GfxFileStream = newStream;
                        LoadAllGfxData();
                        InvalidateAllTiles();
                    });
                }
                else
                {
                    GfxFileStream = newStream;
                    LoadAllGfxData();
                    InvalidateAllTiles();
                }
            }
        }

        /// <summary>
        /// Replace the collision data with the data from the source
        /// </summary>
        public void LoadCollisions(Tileset source)
        {
            for (int tile = 0; tile < 256; tile++)
            {
                SetTileCollision(tile, source.GetTileCollision(tile));
            }
        }

        /// <summary>
        /// Load all "property" fields from another tileset (see GenerateDescriptors function).
        /// </summary>
        public void LoadProperties(Tileset source)
        {
            inhibitRedraw++;

            foreach (var vr in ValueReferenceGroup.GetDescriptors())
            {
                vr.SetValue(source.ValueReferenceGroup[vr.Name].GetIntValue());
            }

            inhibitRedraw--;
            InvalidateAllTiles();
        }

        public void Dispose()
        {
            graphicsState.ClearGfx();
            graphicsState = null;
            foreach (Bitmap b in tileImagesCache)
                b?.Dispose();
            tileImagesCache = null;
            paletteEventWrapper.UnbindAll();
            gfxStreamEventWrapper.UnbindAll();
            DisposedEvent?.Invoke(this);
        }

        // ================================================================================
        // Protected methods
        // ================================================================================

        // Generate usedTileList for quick lookup of which metatiles use which 4 gameboy tiles.
        protected void GenerateUsedTileList()
        {
            if (inhibitUsedTileListUpdate != 0)
                return;
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

        protected void InvalidateTile(int index)
        {
            if (inhibitRedraw != 0)
                return;
            tileImagesDrawn[index] = false;
            TileModifiedEvent?.Invoke(this, index);
        }

        protected void LoadAllGfxData()
        {
            graphicsState.ClearGfx();

            if (Project.Config.ExpandedTilesets)
            {
                byte[] gfx = new byte[0x1000];
                GfxFileStream.Seek(0, SeekOrigin.Begin);
                GfxFileStream.Read(gfx, 0, 0x1000);
                graphicsState.AddRawVram(1, 0x800, gfx);
                gfxStreamEventWrapper.ReplaceEventSource(GfxFileStream as ReloadableStream);
            }
            else
            {
                OnUniqueGfxChanged();
                OnMainGfxChanged();
            }

            OnAnimationChanged();
        }

        protected void OnMainGfxChanged()
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

        protected void OnUniqueGfxChanged()
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

        protected void OnPaletteHeaderChanged()
        {
            ReloadPalettes();
        }

        protected void OnTilesetLayoutChanged()
        {
            if (!Project.Config.ExpandedTilesets)
            {
                TilesetHeaderGroup = Project.GetIndexedDataType<TilesetHeaderGroup>(TilesetLayoutIndex);
                InvalidateAllTiles();
            }

            GenerateUsedTileList();
        }

        protected void OnAnimationChanged()
        {
            if (AnimationIndex < Project.NumAnimations)
                animationGroup = Project.GetIndexedDataType<AnimationGroup>(AnimationIndex);
            else
                animationGroup = null;

            ResetAnimation();
        }

        protected void OnLayoutGroupChanged()
        {
            LayoutGroupModifiedEvent?.Invoke();
        }

        /// <summary>
        /// Subclasses should call this at the end of their constructors
        /// </summary>
        protected void SubclassInitializationFinished()
        {
            // Set inhibitRedraw because we want to delay drawing until it's requested, and some of
            // the functions called here would otherwise trigger redraws
            inhibitRedraw++;

            OnTilesetLayoutChanged();
            LoadAllGfxData();
            OnPaletteHeaderChanged();

            GenerateDescriptors();
            InstallModifiedHandlers();

            inhibitRedraw--;
        }

        protected void VerifySubTileParams(int index, int x, int y)
        {
            Debug.Assert(index >= 0 && index <= 0xff && x >= 0 && x <= 1 && y >= 0 && y <= 1,
                         $"Invalid subtile params: {index}, {x}, {y}");
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Palette data modified (NOT the palette header index but the data it references)
        /// </summary>
        void OnPaletteDataModified(object sender, EventArgs args)
        {
            ReloadPalettes();
        }

        void ReloadPalettes()
        {
            paletteEventWrapper.ReplaceEventSource(PaletteHeaderGroup);
            graphicsState.RemovePaletteGroupType(PaletteGroupType.Main);
            if (PaletteHeaderGroup != null)
                graphicsState.AddPaletteHeaderGroup(PaletteHeaderGroup, PaletteGroupType.Main);
            InvalidateAllTiles();
        }

        /// <summary>
        /// Generate ValueReferenceDescriptors for all the ValueReferences. Called once at the end
        /// of initialization.
        /// </summary>
        void GenerateDescriptors()
        {
            var descList = new List<ValueReferenceDescriptor>();

            var addDescriptor = (ValueReference vr, string name, bool editable = true, string tooltip = null) =>
            {
                var descriptor = new ValueReferenceDescriptor(
                    vr, name, editable, tooltip);
                descList.Add(descriptor);
            };


            if (Project.Game == Game.Ages)
            {
                addDescriptor(PastFlag, "Past", true,
                          "Set in the past. Determines which minimap comes up with the select button, maybe other stuff too. If a tileset can be used in both the present in the past, this is left unchecked, and the 'roomsInAltWorld' table is checked instead.");
                addDescriptor(UnderwaterFlag, "Underwater", true,
                    "Set in underwater rooms.");
            }
            else // Seasons
            {
                addDescriptor(SubrosiaFlag, "Subrosia", true,
                            "Set in subrosia. Determines which minimap comes up with the select button, maybe other stuff too. If a tileset can be used in both the overworld and subrosia, this is left unchecked, and the 'roomsInAltWorld' table is checked instead.");
            }

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

            if (!Project.Config.ExpandedTilesets)
            {
                addDescriptor(TilesetLayoutIndex, "Layout Index", true, null);
                addDescriptor(UniqueGfx, "Unique GFX Index", true, null);
                addDescriptor(MainGfx, "Main GFX Index", true, null);
                addDescriptor(LayoutGroup, "Layout Group", true,
                    "Determines where to read the room layout from (ie. for value '2', it reads from the file 'room02XX.bin', even if the group number is not 2). In general, to prevent confusion, all rooms in the same overworld (or group) should use tilesets which have the same value for this.");
            }

            addDescriptor(PaletteHeader, "Palettes", true, null);
            addDescriptor(AnimationIndex, "Animation Index", true, null);

            ValueReferenceGroup = new ValueReferenceGroup(descList);
            ValueReferenceGroup.EnableTransactions($"Edit tileset#{TransactionIdentifier}", true);
        }

        /// <summary>
        /// Install modified handlers onto the ValueReferences. Called once at the end of initialization.
        /// </summary>
        void InstallModifiedHandlers()
        {
            // These ones may be null due to only being used on the master branch
            UniqueGfx?.ValueReference.AddValueModifiedHandler(
                (sender, args) => OnUniqueGfxChanged());
            MainGfx?.ValueReference.AddValueModifiedHandler(
                (sender, args) => OnMainGfxChanged());
            TilesetLayoutIndex?.ValueReference.AddValueModifiedHandler(
                (sender, args) => OnTilesetLayoutChanged());
            LayoutGroup?.ValueReference.AddValueModifiedHandler(
                (sender, args) => OnLayoutGroupChanged());

            // These ones should never be null
            PaletteHeader.ValueReference.ModifiedEvent += (_, _) => OnPaletteHeaderChanged();
            AnimationIndex.ValueReference.ModifiedEvent += (_, _) => OnAnimationChanged();
        }
    }
}
