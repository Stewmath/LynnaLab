using System;
using System.Drawing;
using Gtk;

namespace LynnaLab
{
    public partial class GfxViewer : TileGridSelector {

        override protected Bitmap Image {
            get { return image; }
        }

        Bitmap image;

        public GfxViewer() : base() {
            TileWidth = 8;
            TileHeight = 8;
            Width = 0;
            Height = 0;
            Scale = 2;
        }

        public void SetGraphicsState(GraphicsState state, int offsetStart, int offsetEnd, int width=-1, int scale=2) {
            int size = (offsetEnd-offsetStart)/16;
            if (width == -1)
                width = (int)Math.Sqrt(size);
            Console.WriteLine("size " + size);
            Console.WriteLine("width " + width);
            int height = size/width;
            Console.WriteLine("height " + height);

            image = new Bitmap(width*8,height*8);

            for (int x=0;x<width;x++) {
                for (int y=0;y<height;y++) {
                    int offset = offsetStart+x*16+y*16*width;
                    int bank=0;
                    if (offset >= 0x1800) {
                        offset -= 0x1800;
                        bank = 1;
                    }
                    Console.WriteLine("Draw at " + x + "," + y);
                    byte[] data = new byte[16];
                    Array.Copy(state.VramBuffer[bank], offset, data, 0, 16);
                    Bitmap subImage = GbGraphics.TileToImage(data);
                    Graphics g = Graphics.FromImage(image);
                    g.DrawImage(subImage, x*8, y*8);
                }
            }

            Width = width;
            Height = height;
            TileWidth = 8;
            TileHeight = 8;
            Scale = scale;
        }
    }
}
