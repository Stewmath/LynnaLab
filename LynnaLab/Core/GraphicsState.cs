using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    // The lower the value, the lower the priority
    public enum GfxHeaderType {
        Main = 0,
        Unique,
        Animation
    };
    public enum PaletteGroupType{
        Common = 0,
        Main,
    };

	public class GraphicsState
	{
        public delegate void TileModifiedHandler(int bank, int tile);
        event TileModifiedHandler tileModifiedEvent;

        bool gfxModified, palettesModified;

		byte[][] vramBuffer;
		byte[][] wramBuffer;
		Color[][][] paletteBuffer;

        // Parallel lists
        List<GfxHeaderData> gfxHeaderDataList = new List<GfxHeaderData>();
        List<GfxHeaderType> gfxHeaderDataTypes = new List<GfxHeaderType>();

        List<PaletteHeaderGroup> paletteHeaderGroupList = new List<PaletteHeaderGroup>();
        List<PaletteGroupType> paletteHeaderGroupTypes = new List<PaletteGroupType>();

		public byte[][] VramBuffer {
			get { 
                if (gfxModified)
                    RegenerateBuffers();
                return vramBuffer;
            }
		}
		public byte[][] WramBuffer {
			get {
                if (gfxModified)
                    RegenerateBuffers();
                return wramBuffer;
            }
		}

		public GraphicsState()
		{
            RegenerateBuffers();
            RegeneratePalettes();
		}

        public Color[][] GetPalettes(PaletteType type) {
            if (palettesModified)
                RegeneratePalettes();
            return paletteBuffer[(int)type];
        }

        public Color[][] GetBackgroundPalettes() {
            return GetPalettes(PaletteType.Background);
        }
        public Color[][] GetSpritePalettes() {
            return GetPalettes(PaletteType.Sprite);
        }

        public void AddGfxHeader(GfxHeaderData header, GfxHeaderType group) {
            int i = 0;
            while (i < gfxHeaderDataTypes.Count && gfxHeaderDataTypes[i] <= group)
                i++;
            gfxHeaderDataList.Insert(i, header);
            gfxHeaderDataTypes.Insert(i, group);
            if (!gfxModified && i == gfxHeaderDataTypes.Count-1)
                LoadGfxHeader(header);
            else
                gfxModified = true;

            CheckGfxHeaderTilesToUpdate(header);
        }

        public void RemoveGfxHeaderType(GfxHeaderType type) {
            for (int i=0; i<gfxHeaderDataList.Count; i++) {
                if (gfxHeaderDataTypes[i] == type) {
                    GfxHeaderData header = gfxHeaderDataList[i];
                    gfxHeaderDataTypes.RemoveAt(i);
                    gfxHeaderDataList.RemoveAt(i);

                    CheckGfxHeaderTilesToUpdate(header);

                    i--;
                }
            }
            gfxModified = true;
        }

        public void AddPaletteHeaderGroup(PaletteHeaderGroup group, PaletteGroupType type) {
            int i = 0;
            while (i < paletteHeaderGroupList.Count && paletteHeaderGroupTypes[i] <= type)
                i++;
            paletteHeaderGroupList.Insert(i, group);
            paletteHeaderGroupTypes.Insert(i, type);
            if (!palettesModified && i == paletteHeaderGroupList.Count-1)
                LoadPaletteHeaderGroup(group);
            else
                palettesModified = true;
        }
        public void RemovePaletteGroupType(PaletteGroupType type) {
            for (int i=0; i<paletteHeaderGroupList.Count; i++) {
                if (paletteHeaderGroupTypes[i] == type) {
                    paletteHeaderGroupTypes.RemoveAt(i);
                    paletteHeaderGroupList.RemoveAt(i);
                    i--;
                }
            }
            palettesModified = true;
        }

        public void AddTileModifiedHandler(TileModifiedHandler handler) {
            tileModifiedEvent += handler;
        }
        public void RemoveTileModifiedHandler(TileModifiedHandler handler) {
            tileModifiedEvent -= handler;
        }

        void LoadGfxHeader(GfxHeaderData header) {
            if ((header.DestAddr & 0xe000) == 0x8000) {
                int bank = header.DestBank & 1;
                int dest = header.DestAddr & 0x1fff;
                header.GfxStream.Position = 0;
                header.GfxStream.Read(vramBuffer[bank], dest, 0x2000 - dest);
            } else if ((header.DestAddr & 0xf000) == 0xd000) {
                int bank = header.DestBank & 7;
                int dest = header.DestAddr & 0x0fff;
                header.GfxStream.Position = 0;
                header.GfxStream.Read(wramBuffer[bank], dest, 0x1000 - dest);
            }
        }
        void LoadPaletteHeaderGroup(PaletteHeaderGroup group) {
            PaletteHeaderData header = group.FirstPaletteHeader;
            bool next = true;
            while (next) {
                RgbData data = header.Data;
                for (int i=header.FirstPalette; i<header.FirstPalette+header.NumPalettes; i++) {
                    for (int j=0; j<4; j++) {
                        paletteBuffer[(int)header.PaletteType][i][j] = data.Color;
                        data = data.NextData as RgbData;
                    }
                }

                next = false;

				if (header.ShouldHaveNext()) {
                    PaletteHeaderData nextHeader = header.NextData as PaletteHeaderData;
                    if (nextHeader != null) {
						header = nextHeader;
						next = true;
					}
					// Might wanna print a warning if no next value is found
				}
            }
        }

        void RegenerateBuffers() {
            vramBuffer = new byte[2][];
            vramBuffer[0] = new byte[0x2000];
            vramBuffer[1] = new byte[0x2000];

            wramBuffer = new byte[8][];
            for (int i=0; i<8; i++)
                wramBuffer[i] = new byte[0x1000];

            foreach (GfxHeaderData header in gfxHeaderDataList) {
                LoadGfxHeader(header);
            }

            gfxModified = false;
        }
        void RegeneratePalettes() {
            paletteBuffer = new Color[2][][];
            for (int i=0; i<2; i++) {
                paletteBuffer[i] = new Color[8][];
                for (int j=0; j<8; j++) {
                    paletteBuffer[i][j] = new Color[4];
                    for (int k=0; k<4; k++)
                        paletteBuffer[i][j][k] = Color.FromArgb(0,0,0);
                }
            }

            foreach (PaletteHeaderGroup group in paletteHeaderGroupList) {
                LoadPaletteHeaderGroup(group);
            }
            palettesModified = false;
        }

        void CheckGfxHeaderTilesToUpdate(GfxHeaderData header) {
            if (tileModifiedEvent == null)
                return;
            if (header.DestAddr < 0x8000 || header.DestAddr > 0x9fff)
                return;

            for (int t=0; t<header.GetBlockCount(); t++) {
                int tile = t+(header.DestAddr-0x8000)/16;
                tileModifiedEvent(header.DestBank, tile);
            }
        }
	}
}
