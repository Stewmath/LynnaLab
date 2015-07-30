using System;
using System.Drawing;

namespace LynnaLab
{
	public class GraphicsState
	{
		byte[][] vramBuffer = { new byte[0x2000], new byte[0x2000] };
		byte[][] wramBuffer = { new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000], 
			new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000] };
		Color[][][] paletteBuffer;

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

		public void AddGfxHeaderGroup(GfxHeaderGroup group) {
			GfxHeaderData header = group.FirstGfxHeader;

			bool next = true;
			while (next) {
				if ((header.DestAddr & 0xe000) == 0x8000) {
					int bank = header.DestBank & 1;
					int dest = header.DestAddr & 0x1fff;
                    header.GfxFile.Position = 0;
					header.GfxFile.Read(vramBuffer[bank], dest, 0x2000 - dest);
				} else if ((header.DestAddr & 0xf000) == 0xd000) {
					int bank = header.DestBank & 7;
					int dest = header.DestAddr & 0x0fff;
                    header.GfxFile.Position = 0;
					header.GfxFile.Read(wramBuffer[bank], dest, 0x1000 - dest);
				}

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
	}
}
