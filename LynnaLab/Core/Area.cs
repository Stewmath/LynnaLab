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
        int gfxHeaderGroupIndex;
        int paletteHeaderGroupIndex;
        int tilesetHeaderGroupIndex;
        int layoutGroup;
        int animationIndex;

        GfxHeaderGroup gfxHeaderGroup;
        PaletteHeaderGroup paletteHeaderGroup;
        TilesetHeaderGroup tilesetHeaderGroup;
        AnimationGroup animationGroup;

        GraphicsState graphicsState;

        Bitmap[] tileImagesCache = new Bitmap[256];

        public delegate void TileModifiedHandler(int tile);
        public event TileModifiedHandler TileModifiedEvent;

        // For dynamically showing an animation
        int[] animationPos = new int[4];
        int[] animationCounter = new int[4];

        List<byte>[] usedTileList = new List<byte>[256];

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
            }
        }
            
        public int LayoutGroup {
            get { return layoutGroup; }
        }

        public Area(Project p, int i) : base(p, i) {
            areaFile = Project.GetFileWithLabel("areaData");

            areaData = areaFile.GetData("areaData", Index * 8);
            Data data = areaData;
            flags1 = p.EvalToInt(data.Values[0]);

            data = data.Next;
            flags2 = p.EvalToInt(data.Values[0]);

            data = data.Next;
            int uniqueGfxHeaderGroupIndex = p.EvalToInt(data.Values[0]);

            data = data.Next;
            gfxHeaderGroupIndex = p.EvalToInt(data.Values[0]);

            data = data.Next;
            paletteHeaderGroupIndex = p.EvalToInt(data.Values[0]);

            data = data.Next;
            tilesetHeaderGroupIndex = p.EvalToInt(data.Values[0]);

            data = data.Next;
            layoutGroup = p.EvalToInt(data.Values[0]);

            data = data.Next;
            animationIndex = p.EvalToInt(data.Values[0]);


            gfxHeaderGroup = Project.GetIndexedDataType<GfxHeaderGroup>(gfxHeaderGroupIndex);
            paletteHeaderGroup = Project.GetIndexedDataType<PaletteHeaderGroup>(paletteHeaderGroupIndex);
            tilesetHeaderGroup = Project.GetIndexedDataType<TilesetHeaderGroup>(tilesetHeaderGroupIndex);
            PaletteHeaderGroup globalPaletteHeaderGroup = 
                Project.GetIndexedDataType<PaletteHeaderGroup>(0xf);


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


            graphicsState = new GraphicsState();
            graphicsState.AddGfxHeaderGroup(gfxHeaderGroup);
            graphicsState.AddPaletteHeaderGroup(paletteHeaderGroup);
            // Global palettes
            graphicsState.AddPaletteHeaderGroup(globalPaletteHeaderGroup);

            // Unique gfx headers
            if (uniqueGfxHeaderGroupIndex != 0) {
                FileParser uniqueGfxHeaderFile = Project.GetFileWithLabel("uniqueGfxHeaderGroupsStart");
                GfxHeaderData header = uniqueGfxHeaderFile.GetData("uniqueGfxHeaderGroup" + uniqueGfxHeaderGroupIndex.ToString("x2"))
                    as GfxHeaderData;
                if (header != null) {
                    bool next = true;
                    while (next) {
                        graphicsState.AddGfxHeader(header);
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

            // Animation
            if (animationIndex != 0xff) {
                animationGroup
                    = Project.GetIndexedDataType<AnimationGroup>(animationIndex);
                for (int j=0; j<animationGroup.NumAnimations; j++) {
                    Animation animation = animationGroup.GetAnimationIndex(j);
                    for (int k=0; k<animation.NumIndices; k++) {
                        graphicsState.AddGfxHeader(animation.GetGfxHeader(k));
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
            if (TileModifiedEvent != null) {
                tileUpdaterIndex = 0;
                GLib.IdleHandler handler = new GLib.IdleHandler(TileUpdater);
                GLib.Idle.Remove(handler);
                GLib.Idle.Add(handler);
            }
        }

        int tileUpdaterIndex;
        bool TileUpdater() {
            if (tileUpdaterIndex == 256)
                return false;
            tileImagesCache[tileUpdaterIndex] = null;
            GetTileImage(tileUpdaterIndex);
            if (TileModifiedEvent != null)
                TileModifiedEvent(tileUpdaterIndex++);
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

            tileImagesCache[index] = image;
            return image;
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
                    graphicsState.AddGfxHeader(header);
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
            return retData;
        }

        public override void Save() {
        }
    }
}
