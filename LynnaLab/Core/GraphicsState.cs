using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    public enum GfxHeaderGroup {
        Main = 0,
        Unique,
        Animation
    };

	public class GraphicsState
	{
		byte[][] vramBuffer;
		byte[][] wramBuffer;
		Color[][][] paletteBuffer;

        // Parallel lists
        List<GfxHeaderData> gfxHeaderDataList = new List<GfxHeaderData>();
        List<GfxHeaderGroup> gfxHeaderDataGroups = new List<GfxHeaderGroup>();

		public byte[][] VramBuffer {
			get { return vramBuffer; }
		}
		public byte[][] WramBuffer {
			get { return wramBuffer; }
		}

		public GraphicsState()
		{
            paletteBuffer = new Color[2][][];
            for (int i=0; i<2; i++) {
                paletteBuffer[i] = new Color[8][];
                for (int j=0; j<8; j++)
                    paletteBuffer[i][j] = new Color[4];
            }

            RegenerateBuffers();
		}

        public Color[][] GetPalettes(PaletteType type) {
            return paletteBuffer[(int)type];
        }

        public Color[][] GetBackgroundPalettes() {
            return GetPalettes(PaletteType.Background);
        }
        public Color[][] GetSpritePalettes() {
            return GetPalettes(PaletteType.Sprite);
        }

        public void AddGfxHeader(GfxHeaderData header, GfxHeaderGroup group) {
            int i = 0;
            while (i < gfxHeaderDataGroups.Count && gfxHeaderDataGroups[i] <= group)
                i++;
            gfxHeaderDataList.Insert(i, header);
            gfxHeaderDataGroups.Insert(i, group);
            LoadGfxHeader(header);
        }

        public void RemoveGfxHeaderGroup(GfxHeaderGroup group) {
            for (int i=0; i<gfxHeaderDataList.Count; i++) {
                if (gfxHeaderDataGroups[i] == group) {
                    gfxHeaderDataGroups.RemoveAt(i);
                    gfxHeaderDataList.RemoveAt(i);
                    i--;
                }
            }
            RegenerateBuffers();
        }

        public void AddPaletteHeaderGroup(PaletteHeaderGroup group) {
            PaletteHeaderData header = group.FirstPaletteHeader;

            bool next = true;
            while (next) {
                RgbData data = header.Data;
                for (int i=header.FirstPalette; i<header.FirstPalette+header.NumPalettes; i++) {
                    for (int j=0; j<4; j++) {
                        paletteBuffer[(int)header.PaletteType][i][j] = data.Color;
                        data = data.Next as RgbData;
                    }
                }

                next = false;

				if (header.ShouldHaveNext()) {
                    PaletteHeaderData nextHeader = header.Next as PaletteHeaderData;
                    if (nextHeader != null) {
						header = nextHeader;
						next = true;
					}
					// Might wanna print a warning if no next value is found
				}
            }
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
        }
	}
}
