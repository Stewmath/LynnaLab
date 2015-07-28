using System;
using System.Drawing;
using System.IO;

namespace LynnaLab
{
	public class Area : ProjectIndexedDataType
	{
		FileParser areaFile;

		int flags1, flags2;
		int uniqueGfxHeaderIndex, gfxHeaderGroupIndex;
		int paletteHeaderGroupIndex;
		int tilesetHeaderGroupIndex;
		int layoutGroup;
		int animationIndex;

		GfxHeaderGroup gfxHeaderGroup;
		PaletteHeaderGroup paletteHeaderGroup;
		TilesetHeaderGroup tilesetHeaderGroup;

		GraphicsState graphicsState;

		public GraphicsState GraphicsState {
			get { return graphicsState; }
		}

		public Area(Project p, int i) : base(p, i) {
			areaFile = Project.GetFileWithLabel("areaData");

			Data areaData = areaFile.GetData("areaData", Index * 8);
			flags1 = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			flags2 = p.EvalToInt(areaData.Values[0]);

			areaData = areaData.Next;
			uniqueGfxHeaderIndex = p.EvalToInt(areaData.Values[0]);

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


			gfxHeaderGroup = Project.GetDataType<GfxHeaderGroup>(gfxHeaderGroupIndex);
            paletteHeaderGroup = Project.GetDataType<PaletteHeaderGroup>(paletteHeaderGroupIndex);
            tilesetHeaderGroup = Project.GetDataType<TilesetHeaderGroup>(tilesetHeaderGroupIndex);
            PaletteHeaderGroup globalPaletteHeaderGroup = 
                Project.GetDataType<PaletteHeaderGroup>(0xf);

			graphicsState = new GraphicsState();
			graphicsState.AddGfxHeaderGroup(gfxHeaderGroup);
            graphicsState.AddPaletteHeaderGroup(paletteHeaderGroup);
            // Global palettes
            graphicsState.AddPaletteHeaderGroup(globalPaletteHeaderGroup);
		}

        public Bitmap GetTileImage(int Index) {
            Bitmap image = new Bitmap(16,16);
            byte[] mappingsData = tilesetHeaderGroup.GetMappingsData();

            Graphics g = Graphics.FromImage(image);

            for (int y=0; y<2; y++) {
                for (int x=0; x<2; x++) {
                    int tileIndex = mappingsData[Index*8+y*2+x];
                    int flags = mappingsData[Index*8+y*2+x+4];

                    int tileOffset = 0x1000 + ((sbyte)tileIndex)*16;

                    byte[] src = new byte[16];
                    Array.Copy(graphicsState.VramBuffer[1], tileOffset, src, 0, 16);
                    Bitmap subImage = GbGraphics.TileToImage(src, GraphicsState.GetBackgroundPalettes()[flags&7], flags);

                    g.DrawImage(subImage, x*8, y*8);
                }
            }
            g.Dispose();

            return image;
        }

        public override void Save() {
        }
	}
}
