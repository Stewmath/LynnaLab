using System;
using System.Drawing;
using Gtk;

using LynnaLib;

namespace LynnaLab
{
    public partial class GfxViewer : TileGridViewer {

        override protected Bitmap Image {
            get { return image; }
        }

        Bitmap image;

        GraphicsState graphicsState;
        int offsetStart, offsetEnd;

        public GfxViewer() : base() {
            TileWidth = 8;
            TileHeight = 8;
            Width = 0;
            Height = 0;
            Scale = 2;
            Selectable = true;
            SelectedIndex = 0;
        }

        public void SetGraphicsState(GraphicsState state, int offsetStart, int offsetEnd, int width=-1, int scale=2)
        {
            GraphicsState.TileModifiedHandler tileModifiedHandler = delegate(int bank, int tile)
            {
                if (bank == -1 && tile == -1) // Full invalidation
                    redrawAll();
                else
                    draw(tile+bank*0x180);
            };

            if (graphicsState != null)
                graphicsState.RemoveTileModifiedHandler(tileModifiedHandler);
            if (state != null)
                state.AddTileModifiedHandler(tileModifiedHandler);

            graphicsState = state;

            int size = (offsetEnd-offsetStart)/16;
            if (width == -1)
                width = (int)Math.Sqrt(size);
            int height = size/width;

            this.offsetStart = offsetStart;
            this.offsetEnd = offsetEnd;

            Width = width;
            Height = height;
            TileWidth = 8;
            TileHeight = 8;
            Scale = scale;

            image = new Bitmap(Width*TileWidth,Height*TileHeight);

            redrawAll();
        }

        void redrawAll() {
            for (int i=offsetStart/16; i<offsetEnd/16; i++)
                draw(i);
        }

        void draw(int tile) {
            int offset = tile*16;

            if (!(offset >= offsetStart && offset < offsetEnd))
                return;

            int x = ((offset-offsetStart)/16)%Width;
            int y = ((offset-offsetStart)/16)/Width;

            int bank=0;
            if (offset >= 0x1800) {
                offset -= 0x1800;
                bank = 1;
            }
            byte[] data = new byte[16];
            Array.Copy(graphicsState.VramBuffer[bank], offset, data, 0, 16);
            Bitmap subImage = GbGraphics.TileToBitmap(data);
            Graphics g = Graphics.FromImage(image);
            g.DrawImage(subImage, x*8, y*8);

            QueueDraw();
        }
    }
}
