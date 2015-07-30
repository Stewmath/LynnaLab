using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab
{
    public class Room : ProjectIndexedDataType {
        // Actual width and height of room
        int width, height;
        // Width and height of file; large rooms always have an extra 0 at the
        // end of each row
        int fileWidth, fileHeight;

        byte[,] tiles;
        Area area;
        Bitmap cachedImage;

        public int Height
        {
            get { return height; }
        }
        public int Width
        {
            get { return width; }
        }
        public Area Area
        {
            get { return area; }
        }

        FileStream TileDataFile {
            get {
                string label = "room" + Index.ToString("X4").ToLower();
                FileParser parserFile = Project.GetFileWithLabel(label);
                Data data = parserFile.GetData(label);
                if (data.Command != "m_roomlayoutdata") {
                    throw new Exception("Expected label \"" + label + "\" to be followed by the m_RoomLayoutData macro.");
                }
                string roomString = data.Values[0] + ".bin";
                FileStream dataFile;
                try {
                    dataFile = Project.GetBinaryFile("rooms/small/" + roomString);
                }
                catch (FileNotFoundException ex) {
                    try {
                        dataFile = Project.GetBinaryFile("rooms/large/" + roomString);
                    }
                    catch (FileNotFoundException ex2) {
                        throw new FileNotFoundException("Couldn't find \"" + roomString + "\" in \"rooms/small\" or \"rooms/large\".");
                    }
                }
                return dataFile;
            }
        }

        public Room(Project p, int i) : base(p,i) {
            FileStream dataFile = TileDataFile;

            if (dataFile.Length == 80) { // Small map
                width = fileWidth = 10;
                height = fileHeight = 8;
                tiles = new byte[width,height];
                dataFile.Position = 0;
                for (int y=0; y<height; y++)
                    for (int x=0; x<width; x++) {
                        tiles[x,y] = (byte)dataFile.ReadByte();
                }
            }
            else if (dataFile.Length == 176) { // Large map
                width = 0xf;
                fileWidth = 0x10;
                height = fileHeight = 0xb;
                tiles = new byte[width,height];
                dataFile.Position = 0;
                for (int y=0; y<height; y++) {
                    for (int x=0; x<width; x++)
                        tiles[x,y] = (byte)dataFile.ReadByte();
                    dataFile.ReadByte();
                }
            }
            else
                throw new Exception("Size of file \"" + dataFile.Name + "\" was invalid!");

            int areaID = 0;
            FileStream groupAreasFile = Project.GetBinaryFile("rooms/group" + (Index>>8) + "Areas.bin");
            groupAreasFile.Position = Index&0xff;
            areaID = groupAreasFile.ReadByte() & 0x7f;

            area = Project.GetDataType<Area>(areaID);
        }

        public Bitmap GetImage() {
            if (cachedImage != null)
                return cachedImage;

            cachedImage = new Bitmap(width*16, height*16);
            Graphics g = Graphics.FromImage(cachedImage);

            for (int x=0; x<width; x++) {
                for (int y=0; y<height; y++) {
                    g.DrawImage(area.GetTileImage(tiles[x,y]), x*16, y*16);
                }
            }

            g.Dispose();

            return cachedImage;
        }

        public int GetTile(int x, int y) {
            return tiles[x,y];
        }
        public void SetTile(int x, int y, int value) {
            if (tiles[x,y] != value) {
                tiles[x,y] = (byte)value;
                if (cachedImage != null) {
                    Graphics g = Graphics.FromImage(cachedImage);
                    g.DrawImage(area.GetTileImage(value), x*16, y*16);
                    g.Dispose();
                }
                Modified = true;
            }
        }

        public override void Save() {
            if (Modified) {
                FileStream file = TileDataFile;
                file.Position = 0;
                for (int y=0; y<Height; y++) {
                    for (int x=0; x<Width; x++) {
                        file.WriteByte(tiles[x,y]);
                    }
                    for (int j=width; j<fileWidth; j++)
                        file.WriteByte(0);
                }

                Modified = false;
            }
        }
    }
}
