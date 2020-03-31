using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using LynnaLab;

namespace Plugins
{
    // This plugin "smoothens" similar tiles that are supposed to flow
    // together, such as the various kinds of paths.
    // It reads from an xml file to figure out what pieces "go together".
    public class AutoSmoother : Plugin
    {
        PluginManager manager;

        public override String Name { get { return "AutoSmoother"; } }
        public override String Tooltip { get { return "Smoothen Automatically"; } }
        public override bool IsDockable { get { return false; } }
        public override string Category { get { return "Action"; } }

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
            int area = room.Area.Index;

            var reader = new XmlTextReader(Helper.GetResourceStream("LynnaLab.Resources.AutoSmoother.xml"));

            Smoother smoother = new Smoother();
            bool validArea = false;

            string name = "";
            while (reader.Read()) 
            {
                string s;

                switch (reader.NodeType) 
                {
                    case XmlNodeType.Element:
                        name = reader.Name;
                        switch(name) {
                            case "area":
                                s = reader.GetAttribute("index");
                                List<int> ints = GetIntList(s);
                                validArea = ints.Contains(area);
                                break;
                        }
                        break;
                    case XmlNodeType.Text:
                        if (!validArea)
                            continue;

                        s = reader.Value.Trim();
                        switch(name) {
                            case "base":
                                {
                                    IEnumerable<int> values = Regex.Split(s, @"\s+")
                                        .Select(a => Convert.ToInt32(a,16));
                                    int c=0;
                                    foreach (int i in values) {
                                        smoother.baseTiles[c/3,c%3] = i;
                                        c++;
                                    }
                                }
                                break;
                            case "horizontal":
                                {
                                    IEnumerable<int> values = Regex.Split(s,@"\s+")
                                        .Select(a => Convert.ToInt32(a,16));
                                    int c=0;
                                    foreach (int i in values) {
                                        smoother.hTiles[c] = i;
                                        c++;
                                    }
                                }
                                break;
                            case "vertical":
                                {
                                    IEnumerable<int> values = Regex.Split(s,@"\s+")
                                        .Select(a => Convert.ToInt32(a,16));
                                    int c=0;
                                    foreach (int i in values) {
                                        smoother.vTiles[c] = i;
                                        c++;
                                    }
                                }
                                break;
                            case "friendly":
                                {
                                    s = Regex.Replace(s, @"\s","");
                                    List<int> ints = GetIntList(s);
                                    foreach (int i in ints) {
                                        smoother.ignoreTiles.Add(i);
                                    }
                                }
                                break;
                        }
                        break;
                    case XmlNodeType.EndElement:
                        switch(reader.Name) {
                            case "area":
                                validArea = false;
                                break;
                            case "smoother":
                                smoother.Apply(manager);
                                smoother = new Smoother();
                                break;
                        }
                        name = "";
                        break;
                }
            }
        }

        // Convert xml string into a list of ints
        // ie "13,15-18" => {13,15,16,17,18}
        List<int> GetIntList(string s) {
            if (s == "")
                return new List<int>();

            Func<string,Tuple<int,int>> f = str =>
            {
                int i = str.IndexOf('-');
                if (i == -1) {
                    int n = Convert.ToInt32(str,16);
                    return new Tuple<int,int>(n,n);
                }
                int n1 = Convert.ToInt32(str.Substring(0,i),16);
                int n2 = Convert.ToInt32(str.Substring(i+1),16);
                return new Tuple<int,int>(n1,n2);
            };

            int ind = s.IndexOf(',');
            if (ind == -1)  {
                var ret = new List<int>();
                var tuple = f(s);
                for (int i=tuple.Item1;i<=tuple.Item2;i++) {
                    ret.Add(i);
                }
                return ret;
            }
            List<int> ret2 = GetIntList(s.Substring(0,ind));
            ret2.AddRange(GetIntList(s.Substring(ind+1)));
            return ret2;
        }
    }

    class Smoother {
        public int[,] baseTiles = new int[3,3];
        public int[] hTiles = new int[2];
        public int[] vTiles = new int[2];
        public List<int> ignoreTiles = new List<int>();
        protected HashSet<int> tiles = new HashSet<int>();

        public Smoother() {
            baseTiles[0,0] = -1;
        }

        protected void AssembleTiles() {
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

        public void Apply(PluginManager manager) {
            if (baseTiles[0,0] == -1)
                return;

            AssembleTiles();

            Room room = manager.GetActiveRoom();
            for (int y=0; y<room.Height; y++) {
                for (int x=0; x<room.Width; x++) {
                    int t = GetTile(x, y, manager);
                    if (t != room.GetTile(x,y))
                        room.SetTile(x, y, t);
                }
            }
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
