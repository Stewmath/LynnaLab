using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Tileset : ProjectIndexedDataType
    {
        FileParser tilesetFile;
        Data tilesetData;
        ValueReferenceGroup vrg;

        TilesetHeaderGroup tilesetHeaderGroup;
        AnimationGroup animationGroup;

        GraphicsState graphicsState;

        Bitmap[] tileImagesCache = new Bitmap[256];
        Bitmap fullCachedImage = new Bitmap(16*16,16*16);
        BitmapData fullCachedImageData;

        bool constructorFinished = false;

        public delegate void TileModifiedHandler(int tile);
        public delegate void LayoutGroupModifiedHandler();
        // Invoked whenever the image of a tile is modified
        public event TileModifiedHandler TileModifiedEvent;

        public event LayoutGroupModifiedHandler LayoutGroupModifiedEvent;

        // For dynamically showing an animation
        int[] animationPos = new int[4];
        int[] animationCounter = new int[4];

        List<byte>[] usedTileList = new List<byte>[256];


        // The GraphicsState which contains the data as it will be loaded into
        // vram.
        public GraphicsState GraphicsState {
            get { return graphicsState; }
        }

        // Following properties correspond to the 8 bytes defining the tileset.

        public bool SidescrollFlag {
            get { return vrg.GetIntValue("Sidescrolling") != 0 ? true : false; }
        }
        public int UniqueGfx {
            get {
                return vrg.GetIntValue("Unique Gfx");
            }
            set {
                vrg.SetValue("Unique Gfx", value);
            }
        }
        public int MainGfx {
            get {
                return vrg.GetIntValue("Main Gfx");
            }
            set {
                vrg.SetValue("Main Gfx", value);
            }
        }
        public int PaletteHeader {
            get {
                return vrg.GetIntValue("Palettes");
            }
            set {
                vrg.SetValue("Palettes", value);
            }
        }
        public int TilesetLayoutIndex {
            get {
                return vrg.GetIntValue("Layout");
            }
            set {
                vrg.SetValue("Layout", value);
            }
        }
        public int LayoutGroup {
            get {
                return vrg.GetIntValue("Layout Group");
            }
            set {
                vrg.SetValue("Layout Group", value);
            }
        }
        public int AnimationIndex {
            get {
                return vrg.GetIntValue("Animations");
            }
            set {
                vrg.SetValue("Animations", value);
            }
        }

        internal Tileset(Project p, int i) : base(p, i) {
            tilesetFile = Project.GetFileWithLabel("tilesetData");

            tilesetData = tilesetFile.GetData("tilesetData", Index * 8);


            // If this is Seasons, it's possible that tilesetData does not point to 8 bytes as
            // expected, but instead to an "m_SeasonalData" macro.
            if (tilesetData.CommandLowerCase == "m_seasonaltileset") {
                int season=0; // TODO: season switching
                tilesetData = Project.GetData(tilesetData.GetValue(0), season*8);
            }

            ConstructValueReferenceGroup();

            // Data modified handlers
            GetDataIndex(2).AddModifiedEventHandler((sender, args) => LoadUniqueGfx());
            GetDataIndex(3).AddModifiedEventHandler((sender, args) => LoadMainGfx());
            GetDataIndex(4).AddModifiedEventHandler((sender, args) => LoadPaletteHeader());
            GetDataIndex(5).AddModifiedEventHandler((sender, args) => LoadTilesetLayout());
            GetDataIndex(6).AddModifiedEventHandler((sender, args) => {
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

            constructorFinished = true;
        }

        void LoadAllGfxData() {
            graphicsState.ClearGfx();

            if (Project.Config.ExpandedTilesets) {
                string name = String.Format("gfx_tileset{1:x2}", Project.GameString, Index);
                Stream stream = Project.LoadGfx(name);
                if (stream == null)
                    throw new ProjectErrorException("Couldn't find \"" + name + "\" in project.");
                byte[] gfx = new byte[0x1000];
                stream.Read(gfx, 0, 0x1000);
                graphicsState.AddRawVram(1, 0x800, gfx);
            }
            else {
                LoadUniqueGfx();
                LoadMainGfx();
            }

            LoadAnimation();
        }

        Data GetDataIndex(int i) {
            Data data = tilesetData;
            for (int j=0; j<i; j++)
                data = data.NextData;
            return data;
        }

        // Alternative method to access data fields (this data feeds the GUI directly)
        void ConstructValueReferenceGroup() {
            // TODO: save main gfx, unique gfx, palette as strings, not ints
            var list = new List<ValueReference>();

            if (Project.GameString == "ages") {
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
            else { // seasons
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
                            type: DataValueType.ByteBit)
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
                        name: "Maku tree",
                        index: 0,
                        startBit: 1,
                        type: DataValueType.ByteBit,
                        tooltip: "Hardcodes location on the minimap for maku tree screens. Ages only?"),
                new DataValueReference(GetDataIndex(1),
                        name: "Outdoors",
                        index: 0,
                        startBit: 0,
                        type: DataValueType.ByteBit,
                        tooltip: "Does various things. In Ages this must be checked for the minimap to update your position."),
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
                        tooltip: "Determines most collision behaviour aside from solidity (ie. water, holes)")
            });

            // These fields do nothing with the expanded tilesets patch.
            if (!Project.Config.ExpandedTilesets) {
                list.AddRange(new ValueReference[] {
                    new DataValueReference(GetDataIndex(2),
                            name: "Unique Gfx",
                            index: 0,
                            type: DataValueType.Byte,
                            constantsMappingString: "UniqueGfxMapping"),
                    new DataValueReference(GetDataIndex(3),
                            name: "Main Gfx",
                            index: 0,
                            type: DataValueType.Byte,
                            constantsMappingString: "MainGfxMapping"),
                    new DataValueReference(GetDataIndex(5),
                            name: "Layout",
                            index: 0,
                            type: DataValueType.Byte),
                });
            }

            list.AddRange(new ValueReference[] {
                new DataValueReference(GetDataIndex(4),
                        name: "Palettes",
                        index: 0,
                        type: DataValueType.Byte,
                        constantsMappingString: "PaletteHeaderMapping"),
                new DataValueReference(GetDataIndex(6),
                        name: "Layout Group",
                        index: 0,
                        type: DataValueType.Byte,
                        tooltip: "Determines where to read the room layout from (ie. for value '2', it reads from the file 'room02XX.bin', even if the group number is not 2). In general, to prevent confusion, all rooms in the same overworld (or group) should use tilesets which have the same value for this."),
                new DataValueReference(GetDataIndex(7),
                        name: "Animations",
                        index: 0,
                        type: DataValueType.Byte),
            });
            list.AddRange(new ValueReference[] {
                new DataValueReference(GetDataIndex(0),
                        name: "Unused(?) Bit",
                        index: 0,
                        startBit: 7,
                        type: DataValueType.ByteBit),
            });


            vrg = new ValueReferenceGroup(list);
        }


        // Clear all tile image caches. This is called when certain major fields of the tileset are
        // modified. This will trigger an asynchronous redraw of the tileset image (unless called
        // from the constructor), which is costly.
        public void InvalidateAllTiles() {
            for (int i=0; i<256; i++)
                tileImagesCache[i] = null;
            if (constructorFinished)
                DrawAllTiles();
        }
        // Trigger asynchronous redraw of all tiles that are marked as needing to be redrawn
        public void DrawAllTiles() {
            tileUpdaterIndex = 0;
            GLib.IdleHandler handler = new GLib.IdleHandler(TileUpdater);
            GLib.Idle.Add(handler);
        }

        int tileUpdaterIndex;

        // TileUpdater is called by GLib.IdleHandler, which just calls this "when it has time".
        bool TileUpdater() {
            if (tileUpdaterIndex == 256)
                return false;
            int numDrawnTiles = 0;
            while (tileUpdaterIndex < 256 && numDrawnTiles < 16) {
                if (tileImagesCache[tileUpdaterIndex] == null) {
                    numDrawnTiles++;
                    GetTileImage(tileUpdaterIndex); // Will generate the image if it's not cached
                    if (TileModifiedEvent != null)
                        TileModifiedEvent(tileUpdaterIndex);
                }
                tileUpdaterIndex++;
            }

            // Return true to indicate to GLib that it should call this again later
            return true;
        }

        // This returns an image for the specified tile index, and also updates the "master" tileset
        // image if said tile has been changed.
        public Bitmap GetTileImage(int index) {
            if (tileImagesCache[index] != null)
                return tileImagesCache[index];

            Bitmap image = new Bitmap(16,16);

            Graphics g = Graphics.FromImage(image);

            for (int y=0; y<2; y++) {
                for (int x=0; x<2; x++) {
                    int tileIndex = GetSubTileIndex(index,x,y);
                    int flags = GetSubTileFlags(index,x,y);

                    int tileOffset = 0x1000 + ((sbyte)tileIndex)*16;

                    byte[] src = new byte[16];
                    Array.Copy(graphicsState.VramBuffer[1], tileOffset, src, 0, 16);
                    Bitmap subImage = GbGraphics.TileToBitmap(src, GraphicsState.GetBackgroundPalettes()[flags&7], flags);

                    g.DrawImageUnscaled(subImage, x*8, y*8);
                }
            }
            g.Dispose();

//             if (fullCachedImageData == null)
//                 fullCachedImageData = fullCachedImage.LockBits(
//                         new Rectangle(0, 0, fullCachedImage.Width, fullCachedImage.Height),
//                         ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
//             GbGraphics.QuickDraw(fullCachedImageData, image, (index%16)*16, (index/16)*16);
            g = Graphics.FromImage(fullCachedImage);
            g.DrawImageUnscaled(image, (index%16)*16, (index/16)*16);
            g.Dispose();

            tileImagesCache[index] = image;
            return image;
        }

        // Functions dealing with subtiles
        public byte GetSubTileIndex(int index, int x, int y) {
            if (Project.Config.ExpandedTilesets) {
                MemoryFileStream stream = GetExpandedMappingsFile();
                stream.Seek(index * 8 + y * 2 + x, SeekOrigin.Begin);
                return (byte)stream.ReadByte();
            }
            else
                return tilesetHeaderGroup.GetMappingsData(index*8+y*2+x);
        }
        public void SetSubTileIndex(int index, int x, int y, byte value) {
            if (Project.Config.ExpandedTilesets) {
                MemoryFileStream stream = GetExpandedMappingsFile();
                stream.Seek(index * 8 + y * 2 + x, SeekOrigin.Begin);
                stream.WriteByte(value);
            }
            else
                tilesetHeaderGroup.SetMappingsData(index*8+y*2+x, value);

            GenerateUsedTileList();
            tileImagesCache[index] = null;
            GetTileImage(index); // Redraw tile image
            TileModifiedEvent(index);
        }
        public byte GetSubTileFlags(int index, int x, int y) {
            if (Project.Config.ExpandedTilesets) {
                MemoryFileStream stream = GetExpandedMappingsFile();
                stream.Seek(index * 8 + 4 + y * 2 + x, SeekOrigin.Begin);
                return (byte)stream.ReadByte();
            }
            else
                return tilesetHeaderGroup.GetMappingsData(index*8+y*2+x+4);
        }
        public void SetSubTileFlags(int index, int x, int y, byte value) {
            if (Project.Config.ExpandedTilesets) {
                MemoryFileStream stream = GetExpandedMappingsFile();
                stream.Seek(index * 8 + 4 + y * 2 + x, SeekOrigin.Begin);
                stream.WriteByte(value);
            }
            else {
                tilesetHeaderGroup.SetMappingsData(index*8+y*2+x+4, value);
            }
            tileImagesCache[index] = null;
            GetTileImage(index); // Redraw tile image
            TileModifiedEvent(index);
        }

        // Get the "basic collision" of a subtile (whether or not that part is
        // solid). This ignores the upper half of the collision data bytes and
        // assumes it is zero.
        public bool GetSubTileBasicCollision(int index, int x, int y) {
            byte b = GetTileCollision(index);
            byte i = (byte)(1<<(3-(x+y*2)));
            return (b&i) != 0;
        }
        public void SetSubTileBasicCollision(int index, int x, int y, bool val) {
            byte b = GetTileCollision(index);
            byte i = (byte)(1<<(3-(x+y*2)));
            b = (byte)(b & ~i);
            if (val)
                b |= i;
            SetTileCollision(index, b);
        }

        // Get the full collision byte for a tile.
        public byte GetTileCollision(int index) {
            if (Project.Config.ExpandedTilesets) {
                MemoryFileStream stream = GetExpandedCollisionsFile();
                stream.Seek(index, SeekOrigin.Begin);
                return (byte)stream.ReadByte();
            }
            else
                return tilesetHeaderGroup.GetCollisionsData(index);
        }
        public void SetTileCollision(int index, byte val) {
            if (Project.Config.ExpandedTilesets) {
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
        public Bitmap GetFullCachedImage() {
            if (fullCachedImageData != null) {
                fullCachedImage.UnlockBits(fullCachedImageData);
                fullCachedImageData = null;
            }
            return fullCachedImage;
        }

        // Returns a list of tiles which have changed
        public IList<byte> UpdateAnimations(int frames) {
            HashSet<byte> changedTiles = new HashSet<byte>();
            if (animationGroup == null)
                return new List<byte>();

            for (int i=0; i<animationGroup.NumAnimations; i++) {
                Animation animation = animationGroup.GetAnimationIndex(i);
                animationCounter[i] -= frames;
                while (animationCounter[i] <= 0) {
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
                            header.DestBank == 1) {
                        for (int addr=header.DestAddr;
                                addr<header.DestAddr+
                                (Project.EvalToInt(header.GetValue(2))+1)*16;
                                addr++) {
                            int tile = (addr-0x8800)/16;
                            tile -= 128;
                            if (tile < 0)
                                tile += 256;
                            foreach (byte metatile in usedTileList[tile]) {
                                changedTiles.Add(metatile);
                            }
                        }
                    }
                }
            }

            foreach (int t in changedTiles) {
                // Refresh the image of each animated metatile
                tileImagesCache[t] = null;
                GetTileImage(t);

                if (TileModifiedEvent != null)
                    TileModifiedEvent(t);
            }

            return new List<byte>(changedTiles);
        }

        public void ResetAnimation() {
            if (!graphicsState.HasGfxHeaderType(GfxHeaderType.Animation))
                return;

            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Animation);

            for (int i=0; i<4; i++) {
                animationPos[i] = 0;
                animationCounter[i] = 0;
            }

            InvalidateAllTiles();
        }

        public ValueReferenceGroup GetValueReferenceGroup() {
            return vrg;
        }

        public override void Save() {
        }

        void LoadMainGfx() {
            if (Project.Config.ExpandedTilesets) // Field has no effect in this case
                return;

            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Main);

            FileParser gfxHeaderFile = Project.GetFileWithLabel("gfxHeaderTable");
            Data pointerData = gfxHeaderFile.GetData("gfxHeaderTable", MainGfx*2);
            GfxHeaderData header = gfxHeaderFile.GetData(pointerData.GetValue(0))
                as GfxHeaderData;
            if (header != null) {
                bool next = true;
                while (next) {
                    graphicsState.AddGfxHeader(header, GfxHeaderType.Main);
                    next = false;
                    if (header.ShouldHaveNext) {
                        GfxHeaderData nextHeader = header.NextData as GfxHeaderData;
                        if (nextHeader != null) {
                            header = nextHeader;
                            next = true;
                        }
                        // Might wanna print a warning if no next value is found
                    }
                }
            }
            InvalidateAllTiles();
        }

        void LoadUniqueGfx() {
            if (Project.Config.ExpandedTilesets) // Field has no effect in this case
                return;

            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Unique);
            if (UniqueGfx != 0) {
                FileParser uniqueGfxHeaderFile = Project.GetFileWithLabel("uniqueGfxHeadersStart");
                GfxHeaderData header
                    = uniqueGfxHeaderFile.GetData("uniqueGfxHeader" + UniqueGfx.ToString("x2"))
                    as GfxHeaderData;
                if (header != null) {
                    bool next = true;
                    while (next) {
                        graphicsState.AddGfxHeader(header, GfxHeaderType.Unique);
                        next = false;
                        if (header.ShouldHaveNext) {
                            GfxHeaderData nextHeader = header.NextData as GfxHeaderData;
                            if (nextHeader != null) {
                                header = nextHeader;
                                next = true;
                            }
                            // Might wanna print a warning if no next value is found
                        }
                    }
                }
            }
            InvalidateAllTiles();
        }

        void LoadPaletteHeader() {
            graphicsState.RemovePaletteGroupType(PaletteGroupType.Main);
            var paletteHeaderGroup =
                Project.GetIndexedDataType<PaletteHeaderGroup>(PaletteHeader);
            graphicsState.AddPaletteHeaderGroup(paletteHeaderGroup, PaletteGroupType.Main);
            InvalidateAllTiles();
        }

        void LoadTilesetLayout() {
            if (!Project.Config.ExpandedTilesets) {
                tilesetHeaderGroup = Project.GetIndexedDataType<TilesetHeaderGroup>(TilesetLayoutIndex);
                InvalidateAllTiles();
            }

            GenerateUsedTileList();
        }

        // Generate usedTileList for quick lookup of which metatiles use which 4 gameboy tiles.
        void GenerateUsedTileList() {
            for (int j=0; j<256; j++)
                usedTileList[j] = new List<byte>();
            for (int j=0; j<256; j++) {
                // j = index of metatile
                bool[] used = new bool[256];
                for (int k=0; k<4; k++) {
                    int tile = GetSubTileIndex(j, k%2, k/2);
                    if (!used[tile]) {
                        usedTileList[tile].Add((byte)j);
                        used[tile] = true;
                    }
                }
            }
        }

        void LoadAnimation() {
            if (AnimationIndex != 0xff)
                animationGroup = Project.GetIndexedDataType<AnimationGroup>(AnimationIndex);
            else
                animationGroup = null;

            ResetAnimation();
        }


        MemoryFileStream GetExpandedMappingsFile() {
            return Project.GetBinaryFile(
                    String.Format("tileset_layouts/{0}/tilesetMappings{1:x2}.bin", Project.GameString, Index));
        }

        MemoryFileStream GetExpandedCollisionsFile() {
            return Project.GetBinaryFile(
                    String.Format("tileset_layouts/{0}/tilesetCollisions{1:x2}.bin", Project.GameString, Index));
        }
    }
}
