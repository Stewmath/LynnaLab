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

        public bool DarkenUsedDungeonRooms {
            get {
                return _darkenUsedDungeonRooms;
            }
            set {
                if (_darkenUsedDungeonRooms != value) {
                    _darkenUsedDungeonRooms = value;
                    base.ClearImageCache();
                    GenerateImage();
                }
            }
        }

        public HighlightingMinimap()
        {
        }

        public override void GenerateImage() {
            if (Map == null)
                return;

            roomsUsedInDungeons = Project.GetRoomsUsedInDungeons();

            base.GenerateImage();
        }

        protected override Bitmap GenerateTileImage(int x, int y) {
            Room room = GetRoom(x, y);
            Bitmap orig = room.GetImage();
            Bitmap newImage = orig;

            if (!DarkenUsedDungeonRooms || !roomsUsedInDungeons.Contains(room.Index))
                return orig;

            newImage = new Bitmap(orig.Width, orig.Height);

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
