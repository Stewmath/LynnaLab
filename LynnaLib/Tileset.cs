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

        // TODO: Update variables on undo/redo?

        AnimationGroup animationGroup;

        GraphicsState graphicsState;

        Bitmap[] tileImagesCache = new Bitmap[256];
        bool[] tileImagesDrawn = new bool[256];

        EventWrapper<ReloadableStream> gfxStreamEventWrapper
            = new EventWrapper<ReloadableStream>();
        EventWrapper<PaletteHeaderGroup> paletteEventWrapper
            = new EventWrapper<PaletteHeaderGroup>();

        // For dynamically showing an animation
        int[] animationPos = new int[4];
        int[] animationCounter = new int[4];

        List<byte>[] usedTileList = new List<byte>[256];

        int inhibitRedraw = 0;
        int inhibitUsedTileListUpdate = 0;


        // ================================================================================
        // Events
        // ================================================================================

        public delegate void LayoutGroupModifiedHandler();

        // Invoked whenever the image of a tile is modified
        public event EventHandler<int> TileModifiedEvent;

        public event LayoutGroupModifiedHandler LayoutGroupModifiedEvent;
        public event EventHandler<EventArgs> PaletteHeaderGroupModifiedEvent;

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
        public void InvalidateAllTiles()
        {
            if (inhibitRedraw != 0)
                return;
            for (int i = 0; i < 256; i++) {
                tileImagesDrawn[i] = false;
            }
            RequestRedraw();
        }

        // Trigger asynchronous redraw of all tiles that are marked as needing to be redrawn
        public void RequestRedraw()
        {
            if (Disposed || inhibitRedraw > 0)
                return;

            // Don't add handler again if it's already in progress
            if (tileUpdaterIndex != 256)
            {
                tileUpdaterIndex = 0;
                return;
            }

            tileUpdaterIndex = 0;
            Project.LazyInvoke(TileUpdater);
        }

        int tileUpdaterIndex = 256;

        // TileUpdater is called by the idle handler, which just calls this "when it has time".
        bool TileUpdater()
        {
            if (Disposed)
                return false;

            if (tileUpdaterIndex == 256)
            {
                TileModifiedEvent?.Invoke(this, -1);
                return false;
            }
            int numDrawnTiles = 0;
            while (tileUpdaterIndex < 256 && numDrawnTiles < 16)
            {
                if (!tileImagesDrawn[tileUpdaterIndex])
                {
                    numDrawnTiles++;
                    // Generate the image if it's not cached
                    GetTileBitmap(tileUpdaterIndex, invokeModifiedEvent:false);

                    // Do not invoke TileModifiedEvent here. We would not be in this function in the
                    // first place if we weren't updating many tiles at once. So only invoke it when
                    // all tiles are finished updating, with parameter -1, indicating that all tiles
                    // were redrawn.
                }
                tileUpdaterIndex++;
            }

            // Return true to indicate to GLib that it should call this again later
            return true;
        }

        // This returns an image for the specified tile index.
        public Bitmap GetTileBitmap(int index, bool invokeModifiedEvent=true)
        {
            if (tileImagesDrawn[index])
                return tileImagesCache[index];

            var image = tileImagesCache[index];

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

            TileDescription desc = new TileDescription(tl, tr, bl, br);
            GbGraphics.RenderTile(image, 0, 0, desc, GraphicsState.GetBackgroundPalettes());

            tileImagesDrawn[index] = true;
            image.MarkModified();
            if (invokeModifiedEvent)
                TileModifiedEvent?.Invoke(this, index);

            return image;
        }

        public ReadOnlySpan<byte> GetSubTileGfxBytes(int subTileIndex)
        {
            int tileOffset = 0x1000 + ((sbyte)subTileIndex) * 16;
            return graphicsState.VramBuffer[1].AsSpan().Slice(tileOffset, 16);
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
            GfxFileStream = source.GfxFileStream;
            LoadAllGfxData();
            InvalidateAllTiles();
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
            GetTileBitmap(index); // Redraw tile image
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
            PaletteHeaderGroupModifiedEvent?.Invoke(this, null);
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
