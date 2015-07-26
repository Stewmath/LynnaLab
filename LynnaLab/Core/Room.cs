using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab
{
    public class Room {
        Project project;
        int index;

        int width, height;
        byte[,] tiles;
        Area area;

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

        public Room(Project p, int i) {
            project = p;
            index = i;

            FileStream dataFile;
            string roomString = "room" + i.ToString("X4").ToLower() + ".bin";
            try {
                dataFile = project.GetBinaryFile("maps/small/" + roomString);
            }
            catch (FileNotFoundException ex) {
                try {
                    dataFile = project.GetBinaryFile("maps/large/" + roomString);
                }
                catch (FileNotFoundException ex2) {
                    throw new FileNotFoundException("Couldn't find \"" + roomString + "\" in \"maps/small\" or \"maps/large\".");
                }
            }

            if (dataFile.Length == 80) { // Small map
                width = 10;
                height = 8;
                tiles = new byte[width,height];
                dataFile.Position = 0;
                for (int y=0; y<height; y++)
                    for (int x=0; x<width; x++) {
                        tiles[x,y] = (byte)dataFile.ReadByte();
                }
            }
            else if (dataFile.Length == 176) { // Large map
                width = 0xf;
                height = 0xb;
                tiles = new byte[width,height];
                dataFile.Position = 0;
                for (int y=0; y<height; y++) {
                    for (int x=0; x<width; x++)
                        tiles[x,y] = (byte)dataFile.ReadByte();
                    dataFile.ReadByte();
                }
            }
            else
                throw new Exception("Size of file \"" + roomString + "\" was invalid!");

            int areaID = 0;
            FileStream groupAreasFile = project.GetBinaryFile("maps/group" + (index>>8) + "Areas.bin");
            groupAreasFile.Position = index&0xff;
            areaID = groupAreasFile.ReadByte() & 0x7f;

            area = new Area(project, areaID);
        }

        public Bitmap GetImage() {
            Bitmap image = new Bitmap(width*16, height*16);
            Graphics g = Graphics.FromImage(image);

            for (int x=0; x<width; x++) {
                for (int y=0; y<height; y++) {
                    g.DrawImage(area.GetTileImage(tiles[x,y]), x*16, y*16);
                }
            }

            return image;
        }
    }
}
