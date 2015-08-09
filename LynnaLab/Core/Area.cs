using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Area : ProjectIndexedDataType
    {
        FileParser areaFile;

        Data areaData;

        int flags1, flags2;
        int tilesetHeaderGroupIndex;
        int layoutGroup;
        int animationIndex;

        TilesetHeaderGroup tilesetHeaderGroup;
        AnimationGroup animationGroup;

        GraphicsState graphicsState;

        Bitmap[] tileImagesCache = new Bitmap[256];
        Bitmap fullCachedImage = new Bitmap(16*16,16*16);

        public delegate void TileModifiedHandler(int tile);
        public event TileModifiedHandler TileModifiedEvent;

        // For dynamically showing an animation
        int[] animationPos = new int[4];
        int[] animationCounter = new int[4];

        List<byte>[] usedTileList = new List<byte>[256];


        // If true, tiles which must be updated are always slated to be
        // redrawn, so they'll be ready when they're needed.
        public bool DrawInvalidatedTiles { get; set; }


        public GraphicsState GraphicsState {
            get { return graphicsState; }
        }

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
                return d.Values[0];
            }
            set {
                Data d = GetDataIndex(2);
                d.SetValue(0, value);
                SetUniqueGfx(Project.EvalToInt(value));
            }
        }
        public string MainGfxString {
            get {
                Data d = GetDataIndex(3);
                return d.Values[0];
            }
            set {
                Data d = GetDataIndex(3);
                d.SetValue(0, value);
                SetMainGfx(Project.EvalToInt(value));
            }
        }
        public string PaletteHeaderString {
            get {
                Data d = GetDataIndex(4);
                return d.Values[0];
            }
            set {
                Data d = GetDataIndex(4);
                d.SetValue(0, value);
                SetPaletteHeader(Project.EvalToInt(value));
            }
        }
            
        public int LayoutGroup {
            get { return layoutGroup; }
        }

        public Area(Project p, int i) : base(p, i) {
            areaFile = Project.GetFileWithLabel("areaData");

            areaData = areaFile.GetData("areaData", Index * 8);

            // Initialize graphics state
            graphicsState = new GraphicsState();
            // Global palettes
            PaletteHeaderGroup globalPaletteHeaderGroup = 
                Project.GetIndexedDataType<PaletteHeaderGroup>(0xf);
            graphicsState.AddPaletteHeaderGroup(globalPaletteHeaderGroup, PaletteGroupType.Common);

            Data data = areaData;
            flags1 = p.EvalToInt(data.Values[0]);

            data = data.Next;
            flags2 = p.EvalToInt(data.Values[0]);

            data = data.Next;
            SetUniqueGfx(Project.EvalToInt(data.Values[0]));

            data = data.Next;
            SetMainGfx(Project.EvalToInt(data.Values[0]));

            data = data.Next;
            SetPaletteHeader(Project.EvalToInt(data.Values[0]));

            data = data.Next;
            tilesetHeaderGroupIndex = p.EvalToInt(data.Values[0]);

            data = data.Next;
            layoutGroup = p.EvalToInt(data.Values[0]);

            data = data.Next;
            animationIndex = p.EvalToInt(data.Values[0]);


            tilesetHeaderGroup = Project.GetIndexedDataType<TilesetHeaderGroup>(tilesetHeaderGroupIndex);


            // Generate usedTileList for quick lookup of which metatiles use
            // which 4 gameboy tiles
            for (int j=0; j<256; j++)
                usedTileList[j] = new List<byte>();
            byte[] mappingsData = tilesetHeaderGroup.GetMappingsData();
            for (int j=0; j<256; j++) {
                // j = index of metatile
                bool[] used = new bool[256];
                for (int k=0; k<4; k++) {
                    int tile = mappingsData[j*8+k];
                    if (!used[tile]) {
                        usedTileList[tile].Add((byte)j);
                        used[tile] = true;
                    }
                }
            }



            // Animation
            if (animationIndex != 0xff) {
                animationGroup
                    = Project.GetIndexedDataType<AnimationGroup>(animationIndex);
                for (int j=0; j<animationGroup.NumAnimations; j++) {
                    Animation animation = animationGroup.GetAnimationIndex(j);
                    for (int k=0; k<animation.NumIndices; k++) {
                        graphicsState.AddGfxHeader(animation.GetGfxHeader(k), GfxHeaderType.Animation);
                    }
                }
            }
        }

        Data GetDataIndex(int i) {
            Data data = areaData;
            for (int j=0; j<i; j++)
                data = data.Next;
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
            GLib.Idle.Remove(handler);
            GLib.Idle.Add(handler);
        }
        public void DrawAllTiles() {
            tileUpdaterIndex = 0;
            tileUpdaterRedraw = false;
            GLib.IdleHandler handler = new GLib.IdleHandler(TileUpdater);
            GLib.Idle.Remove(handler);
            GLib.Idle.Add(handler);
        }

        bool tileUpdaterRedraw;
        int tileUpdaterIndex;
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
            return true;
        }

        public Bitmap GetTileImage(int index) {
            if (tileImagesCache[index] != null)
                return tileImagesCache[index];

            Bitmap image = new Bitmap(16,16);
            byte[] mappingsData = tilesetHeaderGroup.GetMappingsData();

            Graphics g = Graphics.FromImage(image);

            for (int y=0; y<2; y++) {
                for (int x=0; x<2; x++) {
                    int tileIndex = mappingsData[index*8+y*2+x];
                    int flags = mappingsData[index*8+y*2+x+4];

                    int tileOffset = 0x1000 + ((sbyte)tileIndex)*16;

                    byte[] src = new byte[16];
                    Array.Copy(graphicsState.VramBuffer[1], tileOffset, src, 0, 16);
                    Bitmap subImage = GbGraphics.TileToImage(src, GraphicsState.GetBackgroundPalettes()[flags&7], flags);

                    g.DrawImage(subImage, x*8, y*8);
                }
            }
            g.Dispose();

            g = Graphics.FromImage(fullCachedImage);
            g.DrawImage(image, (index%16)*16, (index/16)*16);

            tileImagesCache[index] = image;
            return image;
        }

        // This function doesn't guarantee to return a fully rendered image,
        // only what is currently available
        public Bitmap GetFullCachedImage() {
            return fullCachedImage;
        }

        // Returns a list of tiles which have changed
        public IList<byte> updateAnimations(int frames) {
            List<byte> retData = new List<byte>();
            if (animationGroup == null)
                return retData;

            for (int i=0; i<animationGroup.NumAnimations; i++) {
                Animation animation = animationGroup.GetAnimationIndex(i);
                animationCounter[i] -= frames;
                while (animationCounter[i] <= 0) {
                    int pos = animationPos[i];
                    animationCounter[i] += animation.GetCounter(pos);
                    GfxHeaderData header = animation.GetGfxHeader(pos);
                    graphicsState.AddGfxHeader(header, GfxHeaderType.Animation);
//                     Console.WriteLine(i + ":" + animationPos[i]);
                    animationPos[i]++;
                    if (animationPos[i] >= animation.NumIndices)
                        animationPos[i] = 0;

                    // Check which tiles changed
                    if (header.DestAddr >= 0x8800 && header.DestAddr < 0x9800 &&
                            header.DestBank == 1) {
                        for (int addr=header.DestAddr;
                                addr<header.DestAddr+
                                (Project.EvalToInt(header.Values[2])+1)*16;
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
            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Main);

            FileParser gfxHeaderFile = Project.GetFileWithLabel("gfxHeaderGroupTable");
            Data pointerData = gfxHeaderFile.GetData("gfxHeaderGroupTable", index*2);
            GfxHeaderData header = gfxHeaderFile.GetData(pointerData.Values[0])
                as GfxHeaderData;
            if (header != null) {
                bool next = true;
                while (next) {
                    graphicsState.AddGfxHeader(header, GfxHeaderType.Main);
                    next = false;
                    if (header.ShouldHaveNext()) {
                        GfxHeaderData nextHeader = header.Next as GfxHeaderData;
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
            graphicsState.RemoveGfxHeaderType(GfxHeaderType.Unique);
            if (index != 0) {
                FileParser uniqueGfxHeaderFile = Project.GetFileWithLabel("uniqueGfxHeaderGroupsStart");
                GfxHeaderData header
                    = uniqueGfxHeaderFile.GetData("uniqueGfxHeaderGroup" + index.ToString("x2"))
                    as GfxHeaderData;
                if (header != null) {
                    bool next = true;
                    while (next) {
                        graphicsState.AddGfxHeader(header, GfxHeaderType.Unique);
                        next = false;
                        if (header.ShouldHaveNext()) {
                            GfxHeaderData nextHeader = header.Next as GfxHeaderData;
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
    }
}
