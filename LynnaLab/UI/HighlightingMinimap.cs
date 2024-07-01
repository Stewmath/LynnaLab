using System;
using System.Collections.Generic;
using System.ComponentModel;

using LynnaLib;

namespace LynnaLab
{
    // An extension of Minimap that darkens rooms which are visible through
    // dungeons, effectively highlighting the "unmapped" rooms.
    [System.ComponentModel.ToolboxItem(true)]
    class HighlightingMinimap : Minimap
    {
        bool _darkenUsedDungeonRooms = true;

        public bool DarkenUsedDungeonRooms
        {
            get
            {
                return _darkenUsedDungeonRooms;
            }
            set
            {
                if (_darkenUsedDungeonRooms != value)
                {
                    _darkenUsedDungeonRooms = value;
                    QueueDraw();
                }
            }
        }

        public HighlightingMinimap()
        {
        }

        protected override void TileDrawer(int index, Cairo.Context cr)
        {
            if (Map == null)
                return;

            int roomIndex = (Map.MainGroup << 8) | index;

            base.TileDrawer(index, cr);

            if (DarkenUsedDungeonRooms && Project.RoomUsedInDungeon(roomIndex))
            {
                cr.SetSourceRGB(0, 0, 0);
                cr.PaintWithAlpha(0.8);
            }
        }
    }
}
