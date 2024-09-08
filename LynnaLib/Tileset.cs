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

        AnimationGroup animationGroup;

        GraphicsState graphicsState;

        Bitmap[] tileImagesCache = new Bitmap[256];

        EventWrapper<ReloadableStream> gfxStreamEventWrapper
            = new EventWrapper<ReloadableStream>();
        EventWrapper<PaletteHeaderGroup> paletteEventWrapper
            = new EventWrapper<PaletteHeaderGroup>();

        // For dynamically showing an animation
        int[] animationPos = new int[4];
        int[] animationCounter = new int[4];

        List<byte>[] usedTileList = new List<byte>[256];

        int inhibitRedraw = 0;


        // ================================================================================
        // Events
        // ================================================================================

        public delegate void LayoutGroupModifiedHandler();

        // Invoked whenever the image of a tile is modified
        public event EventHandler<int> TileModifiedEvent;

        public event LayoutGroupModifiedHandler LayoutGroupModifiedEvent;
        public event EventHandler<EventArgs> PaletteHeaderGroupModifiedEvent;


        // ================================================================================
        // Properties
        // ================================================================================

        public Project Project { get; private set; }

        /// <summary>
        /// The GraphicsState which contains the data as it will be loaded into vram.
        /// </summary>
        public GraphicsState GraphicsState
        {
            get { return graphicsState; }
        }

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
        protected Stream GfxFileStream { get; set; } // Subclass must set this in constructor

        // ================================================================================
        // Constructors
        // ================================================================================

        internal Tileset(Project p)
        {
            Project = p;

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

            // Subclass constructor should call ReloadAll() after their constructors are finished to
            // begin loading the graphics.
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
            for (int i = 0; i < 256; i++) {
                tileImagesCache[i]?.Dispose();
                tileImagesCache[i] = null;
            }
            RequestRedraw();
        }

        // Trigger asynchronous redraw of all tiles that are marked as needing to be redrawn
        public void RequestRedraw()
        {
            if (inhibitRedraw > 0)
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
            if (tileUpdaterIndex == 256)
            {
                TileModifiedEvent?.Invoke(this, -1);
                return false;
            }
            int numDrawnTiles = 0;
            while (tileUpdaterIndex < 256 && numDrawnTiles < 16)
            {
                if (tileImagesCache[tileUpdaterIndex] == null)
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

            tileImagesCache[index] = image;
            if (invokeModifiedEvent)
                TileModifiedEvent?.Invoke(this, index);

            return image;
        }

        // Functions dealing with subtiles
        public abstract byte GetSubTileIndex(int index, int x, int y);
        public abstract void SetSubTileIndex(int index, int x, int y, byte value);
        public abstract byte GetSubTileFlags(int index, int x, int y);
        public abstract void SetSubTileFlags(int index, int x, int y, byte value);
        public abstract bool GetSubTileBasicCollision(int index, int x, int y);
        public abstract void SetSubTileBasicCollision(int index, int x, int y, bool value);
        public abstract byte GetTileCollision(int index);
        public abstract void SetTileCollision(int index, byte value);

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
            return tileImagesCache[tileIndex] != null;
        }

        public void Dispose()
        {
            graphicsState = null;
            foreach (Bitmap b in tileImagesCache)
                b?.Dispose();
            tileImagesCache = null;
            paletteEventWrapper.UnbindAll();
        }

        // ================================================================================
        // Protected methods
        // ================================================================================

        // Generate usedTileList for quick lookup of which metatiles use which 4 gameboy tiles.
        protected void GenerateUsedTileList()
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

        protected void InvalidateTile(int index)
        {
            tileImagesCache[index]?.Dispose();
            tileImagesCache[index] = null;
            GetTileBitmap(index); // Redraw tile image
            TileModifiedEvent?.Invoke(this, index);
        }

        protected void LoadAllGfxData()
        {
            graphicsState.ClearGfx();

            if (Project.Config.ExpandedTilesets)
            {
                byte[] gfx = new byte[0x1000];
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
        protected void ReloadAll()
        {
            inhibitRedraw++;
            OnTilesetLayoutChanged();
            LoadAllGfxData();
            OnPaletteHeaderChanged();
            inhibitRedraw--;
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
    }
}
