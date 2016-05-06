using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.ComponentModel;

namespace LynnaLab
{
    // An extension of Minimap that darkens rooms which are visible through
    // dungeons, effectively highlighting the "unmapped" rooms.
    [System.ComponentModel.ToolboxItem(true)]
    class HighlightingMinimap : Minimap
    {
        bool _darkenUsedDungeonRooms = true;

        HashSet<int> roomsUsedInDungeons;

        [BrowsableAttribute(false)]
        public bool DarkenUsedDungeonRooms {
            get {
                return _darkenUsedDungeonRooms;
            }
            set {
                if (_darkenUsedDungeonRooms != value) {
                    GenerateImage();
                    _darkenUsedDungeonRooms = value;
                }
            }
        }

        public HighlightingMinimap()
        {
        }

        protected override Image GenerateTileImage(int x, int y) {

            // Regenerate the "usedDungeonRooms" set when redrawing the whole
            // image (assumed to be starting at 0,0)
            if (roomsUsedInDungeons == null || (x == 0 && y == 0)) {
                roomsUsedInDungeons = Project.GetRoomsUsedInDungeons();
            }

            Room room = GetRoom(x, y);
            Bitmap orig = room.GetImage();

            if (!DarkenUsedDungeonRooms || !roomsUsedInDungeons.Contains(room.Index))
                return orig;

            Bitmap newImage = new Bitmap(orig.Width, orig.Height);

            const float ratio = (float)1.0/4;
            var matrix = new float[][] {
                new float[] { ratio,0,0,0,0 },
                new float[] { 0,ratio,0,0,0 },
                new float[] { 0,0,ratio,0,0 },
                new float[] { 0,0,0,1,0 },
                new float[] { 0,0,0,0,1 }
            };

            var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix(matrix));
            using (Graphics g = Graphics.FromImage(newImage)) {
                g.DrawImage(orig,
                        new Rectangle(0, 0, orig.Width, orig.Height),
                        0, 0, orig.Width, orig.Height,
                        GraphicsUnit.Pixel, attributes);
            }

            return newImage;
        }
    }
}
