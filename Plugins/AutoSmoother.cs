using System;
using System.Collections.Generic;
using System.Linq;
using LynnaLab;

namespace Plugins
{
    // This plugin "smoothens" similar tiles that are supposed to flow
    // together, such as the various kinds of paths.
    public class AutoSmoother : Plugin
    {
        // Edit this if areas are modified.
        Smoother[] smoothers = {
            new Smoother(
                    new int[,] {
                    // 3x3 grid: includes corners, sides, center
                    {0x10,0x11,0x12},
                    {0x16,0x1b,0x17},
                    {0x13,0x14,0x15}},
                    // 2x1 arrangement
                    new int[] {0x19,0x19},
                    // 1x2 arrangement
                    new int[] {0x18,0x18},
                    // Tiles to not modify but use for adjacency checks
                    new int[] {0x1a}
                    )};

        PluginManager manager;

        public override String Name {
            get {
                return "AutoSmoother";
            }
        }
        public override String Tooltip {
            get {
                return "Smoothen Automatically";
            }
        }
        public override bool IsDockable {
            get {
                return false;
            }
        }

        Project Project {
            get {
                return manager.Project;
            }
        }

        public override void Init(PluginManager manager) {
            this.manager = manager;
        }
        public override void Exit() {
        }

        public override void Clicked() {

            Room room = manager.GetActiveRoom();
            foreach (Smoother s in smoothers) {
                for (int y=0; y<room.Height; y++) {
                    for (int x=0; x<room.Width; x++) {
                        int t = s.GetTile(x, y, manager);
                        if (t != room.GetTile(x,y))
                            room.SetTile(x, y, t);
                    }
                }
            }
        }
    }

    class Smoother {
        protected int[,] baseTiles;
        protected int[] hTiles;
        protected int[] vTiles;
        protected int[] ignoreTiles;
        protected HashSet<int> tiles = new HashSet<int>();

        public Smoother(int[,] baseTiles, int[] hTiles, int[] vTiles, int[] ignoreTiles) {
            this.baseTiles = baseTiles;
            this.hTiles = hTiles;
            this.vTiles = vTiles;
            this.ignoreTiles = ignoreTiles;

            for (int y=0;y<3;y++) {
                for (int x=0;x<3;x++)
                    tiles.Add(baseTiles[y,x]);
            }
            tiles.Add(hTiles[0]);
            tiles.Add(hTiles[1]);
            tiles.Add(vTiles[0]);
            tiles.Add(vTiles[1]);
            foreach (int i in ignoreTiles)
                tiles.Add(i);
        }

        public virtual int GetTile(int x, int y, PluginManager manager) {
            Room room = manager.GetActiveRoom();
            int t = room.GetTile(x,y);
            if (!tiles.Contains(t) || ignoreTiles.Contains(t))
                return t;

            Func<int,bool> f = a => {
                return tiles.Contains(a);
            };

            return GetTileBy(x, y, manager, f);
        }

        protected int GetTileBy(int x, int y, PluginManager manager, Func<int,bool> checker) {
            Map map = manager.GetActiveMap();
            Room room = manager.GetActiveRoom();

            // g takes x/y, corrects for room wrapping, returns the tile to check
            Func<int,int,int> g = (a,b) =>
            {
                int roomX = manager.GetMapSelectedX();
                int roomY = manager.GetMapSelectedY();
                int floor = manager.GetMapSelectedFloor();

                if (a < 0) {
                    if (roomX == 0)
                        return -1;
                    roomX--;
                    a += map.RoomWidth;
                }
                else if (a >= map.RoomWidth) {
                    if (roomX == map.MapWidth-1)
                        return -1;
                    roomX++;
                    a -= map.RoomWidth;
                }
                if (b < 0) {
                    if (roomY == 0)
                        return -1;
                    roomY--;
                    b += map.RoomHeight;
                }
                else if (b >= map.RoomHeight) {
                    if (roomY == map.MapHeight-1)
                        return -1;
                    roomY++;
                    b -= map.RoomHeight;
                }
                return map.GetRoom(roomX,roomY,floor).GetTile(a,b);
            };

            Func<int,bool> fx = (a) => checker(g(a,y));
            Func<int,bool> fy = (b) => checker(g(x,b));

            int xi;
            if (fx(x-1) && fx(x+1))
                xi = 1;
            else if (fx(x-1))
                xi = 2;
            else if (fx(x+1))
                xi = 0;
            else
                xi = -1;

            int yi;
            if (fy(y-1) && fy(y+1))
                yi = 1;
            else if (fy(y-1))
                yi = 2;
            else if (fy(y+1))
                yi = 0;
            else
                yi = -1;

            if (xi == -1 && yi == -1)
                return baseTiles[1,1];
            if (xi == -1) {
                return vTiles[yi == 2 ? 1 : 0];
            }
            else if (yi == -1) {
                return hTiles[xi == 2 ? 1 : 0];
            }
            return baseTiles[yi,xi];
        }
    }

    // A smoother where the only "edges" are water
//     class WaterEdgeSmoother : Smoother {
//         public WaterEdgeSmoother(int[,] baseTiles, int[] hTiles, int[] vTiles, int[] ignoreTiles)
//             : base(baseTiles, hTiles, vTiles, ignoreTiles)
//         {
//         }
// 
//         public override int GetTile(int x, int y, PluginManager manager) {
//             Func<int,int,bool> f = (a,b) => {
//                 if (a < 0) {
//                     if (room.Index%mapWidth == 0)
//                         return false;
//                 }
//             };
//         }
//     }
}
