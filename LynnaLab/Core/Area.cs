using System;
using System.Drawing;
using System.IO;

namespace LynnaLab
{
    public class Area : ProjectIndexedDataType
    {
        FileParser areaFile;

        int flags1, flags2;
        int uniqueGfxHeaderGroupIndex, gfxHeaderGroupIndex;
        int paletteHeaderGroupIndex;
        int tilesetHeaderGroupIndex;
        int layoutGroup;
        int animationIndex;

        GfxHeaderGroup gfxHeaderGroup;
        PaletteHeaderGroup paletteHeaderGroup;
        TilesetHeaderGroup tilesetHeaderGroup;

        GraphicsState graphicsState;

        Bitmap[] tileImagesCache = new Bitmap[256];

        public GraphicsState GraphicsState {
            get { return graphicsState; }
        }
        public int LayoutGroup {
            get { return layoutGroup; }
        }

        public Area(Project p, int i) : base(p, i) {
            areaFile = Project.GetFileWithLabel("areaData");

            Data areaData = areaFile.GetData("areaData", Index * 8);
            flags1 = p.EvalToInt(areaData.Values[0]);

            areaData = areaData.Next;
            flags2 = p.EvalToInt(areaData.Values[0]);

            areaData = areaData.Next;
            uniqueGfxHeaderGroupIndex = p.EvalToInt(areaData.Values[0]);

            areaData = areaData.Next;
            gfxHeaderGroupIndex = p.EvalToInt(areaData.Values[0]);

            areaData = areaData.Next;
            paletteHeaderGroupIndex = p.EvalToInt(areaData.Values[0]);

            areaData = areaData.Next;
            tilesetHeaderGroupIndex = p.EvalToInt(areaData.Values[0]);

            areaData = areaData.Next;
            layoutGroup = p.EvalToInt(areaData.Values[0]);

            areaData = areaData.Next;
            animationIndex = p.EvalToInt(areaData.Values[0]);


            gfxHeaderGroup = Project.GetIndexedDataType<GfxHeaderGroup>(gfxHeaderGroupIndex);
            paletteHeaderGroup = Project.GetIndexedDataType<PaletteHeaderGroup>(paletteHeaderGroupIndex);
            tilesetHeaderGroup = Project.GetIndexedDataType<TilesetHeaderGroup>(tilesetHeaderGroupIndex);
            PaletteHeaderGroup globalPaletteHeaderGroup = 
                Project.GetIndexedDataType<PaletteHeaderGroup>(0xf);

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
                AnimationGroup animationGroup
                    = Project.GetIndexedDataType<AnimationGroup>(animationIndex);
                for (int j=0; j<animationGroup.NumAnimations; j++) {
                    Animation animation = animationGroup.GetAnimationIndex(j);
                    for (int k=0; k<animation.NumIndices; k++) {
                        graphicsState.AddGfxHeader(animation.GetGfxHeader(k));
                    }
                }
            }
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

        public override void Save() {
        }
    }
}
