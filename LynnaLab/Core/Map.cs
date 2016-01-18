using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public abstract class Map : ProjectIndexedDataType
    {
        public abstract int MapWidth { get; }
        public abstract int MapHeight { get; }
        public abstract int RoomWidth { get; }
        public abstract int RoomHeight { get; }

        internal Map(Project p, int i) : base(p, i) {
        }

        public abstract Room GetRoom(int x, int y, int floor=0);
    }
}
