using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Area : ProjectIndexedDataType
    {
        FileParser areaFile;

        Data areaData;

        int flags1, flags2;
        int layoutGroup;

        TilesetHeaderGroup tilesetHeaderGroup;
        AnimationGroup animationGroup;

        GraphicsState graphicsState;

        Bitmap[] tileImagesCache = new Bitmap[256];
        Bitmap fullCachedImage = new Bitmap(16*16,16*16);
        BitmapData fullCachedImageData;

        public delegate void TileModifiedHandler(int tile);
        public delegate void LayoutGroupModifiedHandler();
        public event TileModifiedHandler TileModifiedEvent;
        public event LayoutGroupModifiedHandler LayoutGroupModifiedEvent;

        // For dynamically showing an animation
        int[] animationPos = new int[4];
        int[] animationCounter = new int[4];

        List<byte>[] usedTileList = new List<byte>[256];


        // If true, tiles which must be updated are always slated to be
        // redrawn, so they'll be ready when they're needed.
        public bool DrawInvalidatedTiles { get; set; }

        // The GraphicsState which contains the data as it will be loaded into
        // vram.
        public GraphicsState GraphicsState {
            get { return graphicsState; }
        }

        // Following properties correspond to the 8 bytes defining the area.

        public int Flags1 {
            get { return flags1; }
            set {
                flags1 = value;
                Data data = GetDataIndex(0);
                data.SetValue(0,Wla.ToByte((byte)flags1));
            }
        }
        public int Flags2 {
            get { return flags2; }
            set {
                flags2 = value;
                Data data = GetDataIndex(1);
                data.SetValue(0,Wla.ToByte((byte)flags2));
            }
        }
        public string UniqueGfxString {
            get {
                Data d = GetDataIndex(2);
                return d.GetValue(0);
            }
            set {
                Data d = GetDataIndex(2);
                d.SetValue(0, value);
                SetUniqueGfx(Project.EvalToInt(value));
            }
        }
        public int UniqueGfx {
            get {
                return Project.EvalToInt(UniqueGfxString);
            }
            set {
                UniqueGfxString = Project.UniqueGfxMapping.ByteToString(value);
            }
        }
        public string MainGfxString {
            get {
                Data d = GetDataIndex(3);
                return d.GetValue(0);
            }
            set {
                Data d = GetDataIndex(3);
                d.SetValue(0, value);
                SetMainGfx(Project.EvalToInt(value));
            }
        }
        public int MainGfx {
            get {
                return Project.EvalToInt(MainGfxString);
            }
            set {
                MainGfxString = Project.MainGfxMapping.ByteToString(value);
            }
        }
        public string PaletteHeaderString {
            get {
                Data d = GetDataIndex(4);
                return d.GetValue(0);
            }
            set {
                Data d = GetDataIndex(4);
                d.SetValue(0, value);
                SetPaletteHeader(Project.EvalToInt(value));
            }
        }
        public int PaletteHeader {
            get {
                return Project.EvalToInt(PaletteHeaderString);
            }
            set {
                PaletteHeaderString = Project.PaletteHeaderMapping.ByteToString(value);
            }
        }
        public int TilesetIndex {
            get {
                Data d = GetDataIndex(5);
                return Project.EvalToInt(d.GetValue(0));
            }
            set {
                Data d = GetDataIndex(5);
                d.SetValue(0, Wla.ToByte((byte)value));
                SetTileset(value);
            }
        }
        public int LayoutGroup {
            get { return layoutGroup; }
            set {
                layoutGroup = value;
                Data d = GetDataIndex(6);
                d.SetValue(0, Wla.ToByte((byte)value));
                if (LayoutGroupModifiedEvent != null)
                    LayoutGroupModifiedEvent();
            }
        }
        public int AnimationIndex {
            get {
                Data d = GetDataIndex(7);
                return Project.EvalToInt(d.GetValue(0));
            }
            set {
                Data d = GetDataIndex(7);
                d.SetValue(0, Wla.ToByte((byte)value));
                SetAnimation((byte)value);
            }
        }

        internal Area(Project p, int i) : base(p, i) {
            areaFile = Project.GetFileWithLabel("areaData");

            areaData = areaFile.GetData("areaData", Index * 8);


            // If this is Seasons, it's possible that areaData does not point to 8 bytes as
            // expected, but instead to an "m_SeasonalData" macro.
            if (areaData.CommandLowerCase == "m_seasonalarea") {
                int season=0;
                areaData = Project.GetData(areaData.GetValue(0), season*8);
            }

            // Initialize graphics state
            graphicsState = new GraphicsState();
            // Global palettes
            PaletteHeaderGroup globalPaletteHeaderGroup = 
                Project.GetIndexedDataType<PaletteHeaderGroup>(0xf);
            graphicsState.AddPaletteHeaderGroup(globalPaletteHeaderGroup, PaletteGroupType.Common);

            Data data = areaData;
            flags1 = p.EvalToInt(data.GetValue(0));

            data = data.NextData;
            flags2 = p.EvalToInt(data.GetValue(0));

            data = data.NextData;
            SetUniqueGfx(Project.EvalToInt(data.GetValue(0)));

            data = data.NextData;
            SetMainGfx(Project.EvalToInt(data.GetValue(0)));

            data = data.NextData;
            SetPaletteHeader(Project.EvalToInt(data.GetValue(0)));

            data = data.NextData;
            SetTileset(Project.EvalToInt(data.GetValue(0)));

            data = data.NextData;
            layoutGroup = Project.EvalToInt(data.GetValue(0));

            data = data.NextData;
            SetAnimation((byte)Project.EvalToInt(data.GetValue(0)));

            if (Project.Config.ExpandedTilesets) {
                MemoryFileStream stream = Project.GetBinaryFile(
                        String.Format("gfx/{0}/gfx_tileset{1:x2}.bin", Project.GameString, Index));
                byte[] gfx = new byte[0x1000];
                stream.Read(gfx, 0, 0x1000);
                graphicsState.AddRawVram(1, 0x800, gfx);
            }
        }

        Data GetDataIndex(int i) {
            Data data = areaData;
            for (int j=0; j<i; j++)
                data = data.NextData;
            return data;
        }

        public void InvalidateAllTiles() {
            for (int i=0; i<256; i++)
                tileImagesCache[i] = null;
            if (DrawInvalidatedTiles)
                DrawAllTiles();
        }
        public void RedrawAllTiles() {
            tileUpdaterIndex = 0;
            tileUpdaterRedraw = true;
            GLib.IdleHandler handler = new GLib.IdleHandler(TileUpdater);
            GLib.Idle.Add(handler);
        }
        public void DrawAllTiles() {
            tileUpdaterIndex = 0;
            tileUpdaterRedraw = false;
            GLib.IdleHandler handler = new GLib.IdleHandler(TileUpdater);
            GLib.Idle.Add(handler);
        }

        bool tileUpdaterRedraw;
        int tileUpdaterIndex;

        // TileUpdater is called by GLib.IdleHandler, which just calls this "when it has time".
        bool TileUpdater() {
            if (tileUpdaterIndex == 256)
                return false;
            int numDrawnTiles = 0;
            while (tileUpdaterIndex < 256 && numDrawnTiles < 16) {
                if (tileUpdaterRedraw)
                    tileImagesCache[tileUpdaterIndex] = null;
                if (tileImagesCache[tileUpdaterIndex] == null)
                    numDrawnTiles++;
                GetTileImage(tileUpdaterIndex);

                if (TileModifiedEvent != null)
                    TileModifiedEvent(tileUpdaterIndex);
                tileUpdaterIndex++;
            }

            // Return true to indicate to GLib that it should call this again later
            return true;
        }

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
                MemoryFileStream stream = Project.GetBinaryFile(
                        String.Format("tileset_layouts/{0}/tilesetMappings{1:x2}.bin", Project.GameString, Index));
                stream.Seek(index * 8 + y * 2 + x, SeekOrigin.Begin);
                return (byte)stream.ReadByte();
            }
            else
                return tilesetHeaderGroup.GetMappingsData(index*8+y*2+x);
        }
        public void SetSubTileIndex(int index, int x, int y, byte value) {
            if (Project.Config.ExpandedTilesets) {
                // TODO
            }
            else
                tilesetHeaderGroup.SetMappingsData(index*8+y*2+x, value);
        }
        public byte GetSubTileFlags(int index, int x, int y) {
            if (Project.Config.ExpandedTilesets) {
                MemoryFileStream stream = Project.GetBinaryFile(
                        String.Format("tileset_layouts/{0}/tilesetMappings{1:x2}.bin", Project.GameString, Index));
                stream.Seek(index * 8 + 4 + y * 2 + x, SeekOrigin.Begin);
                return (byte)stream.ReadByte();
            }
            else
                return tilesetHeaderGroup.GetMappingsData(index*8+y*2+x+4);
        }
        public void SetSubTileFlags(int index, int x, int y, byte value) {
            if (Project.Config.ExpandedTilesets) {
                // TODO
            }
            else {
                tilesetHeaderGroup.SetMappingsData(index*8+y*2+x+4, value);
                tileImagesCache[index] = null;
                TileModifiedEvent(index);
            }
        }

        // Get the "basic collision" of a subtile (whether or not that part is
        // solid). This ignores the upper half of the collision data bytes and
        // assumes it is zero.
        public bool GetSubTileBasicCollision(int index, int x, int y) {
            if (Project.Config.ExpandedTilesets) {
                // TODO
                return false;
            }
            else {
                byte b = tilesetHeaderGroup.GetCollisionsData(index);
                byte i = (byte)(1<<(3-(x+y*2)));
                return (b&i) != 0;
            }
        }
        public void SetSubTileBasicCollision(int index, int x, int y, bool val) {
            if (Project.Config.ExpandedTilesets) {
                // TODO
            }
            else {
                byte b = tilesetHeaderGroup.GetCollisionsData(index);
                byte i = (byte)(1<<(3-(x+y*2)));
                b = (byte)(b & ~i);
                if (val)
                    b |= i;
                tilesetHeaderGroup.SetCollisionsData(index, b);
            }
        }

        // Get the full collision byte for a tile.
        public byte GetTileCollision(int index) {
            if (Project.Config.ExpandedTilesets) {
                // TODO
                return 0;
            }
            else
                return tilesetHeaderGroup.GetCollisionsData(index);
        }
        public void SetTileCollision(int index, byte val) {
            if (Project.Config.ExpandedTilesets) {
                // TODO
            }
            else
                tilesetHeaderGroup.SetCollisionsData(index, val);
        }

        // This function doesn't guarantee to return a fully rendered image,
        // only what is currently available
        public Bitmap GetFullCachedImage() {
            if (fullCachedImageData != null) {
                fullCachedImage.UnlockBits(fullCachedImageData);
                fullCachedImageData = null;
            }
            return fullCachedImage;
        }

        // Returns a list of tiles which have changed
        public IList<byte> UpdateAnimations(int frames) {
            List<byte> retData = new List<byte>();
            if (animationGroup == null)
                return retData;

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
                                tileImagesCache[metatile] = null;
                                retData.Add(metatile);
                            }
                        }
                    }
                }
            }

            if (TileModifiedEvent != null)
                foreach (byte b in retData)
                    TileModifiedEvent(b);

            if (DrawInvalidatedTiles)
                DrawAllTiles();

            return retData;
        }

        public override void Save() {
        }

        void SetMainGfx(int index) {
            if (Project.Config.ExpandedTilesets) // Field has no effect in this case
                return;

            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Main);

            FileParser gfxHeaderFile = Project.GetFileWithLabel("gfxHeaderTable");
            Data pointerData = gfxHeaderFile.GetData("gfxHeaderTable", index*2);
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

        void SetUniqueGfx(int index) {
            if (Project.Config.ExpandedTilesets) // Field has no effect in this case
                return;

            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Unique);
            if (index != 0) {
                FileParser uniqueGfxHeaderFile = Project.GetFileWithLabel("uniqueGfxHeadersStart");
                GfxHeaderData header
                    = uniqueGfxHeaderFile.GetData("uniqueGfxHeader" + index.ToString("x2"))
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

        void SetPaletteHeader(int index) {
            graphicsState.RemovePaletteGroupType(PaletteGroupType.Main);
            var paletteHeaderGroup =
                Project.GetIndexedDataType<PaletteHeaderGroup>(index);
            graphicsState.AddPaletteHeaderGroup(paletteHeaderGroup, PaletteGroupType.Main);
            InvalidateAllTiles();
        }

        void SetTileset(int index) {
            if (Project.Config.ExpandedTilesets) // Field has no effect in this case
                return;

            tilesetHeaderGroup = Project.GetIndexedDataType<TilesetHeaderGroup>(index);

            // Generate usedTileList for quick lookup of which metatiles use
            // which 4 gameboy tiles
            for (int j=0; j<256; j++)
                usedTileList[j] = new List<byte>();
            for (int j=0; j<256; j++) {
                // j = index of metatile
                bool[] used = new bool[256];
                for (int k=0; k<4; k++) {
                    int tile = tilesetHeaderGroup.GetMappingsData(j*8+k);
                    if (!used[tile]) {
                        usedTileList[tile].Add((byte)j);
                        used[tile] = true;
                    }
                }
            }

            InvalidateAllTiles();
        }

        void SetAnimation(byte index) {
            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Animation);
            // Animation
            if (index != 0xff) {
                animationGroup
                    = Project.GetIndexedDataType<AnimationGroup>(index);
            }
            else
                animationGroup = null;
            InvalidateAllTiles();
        }
    }
}
