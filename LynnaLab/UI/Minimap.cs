using System;
using System.Drawing;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public class Minimap : TileGridSelector
    {
        public Project Project {
            get {
                if (dungeon == null)
                    return null;
                return dungeon.Project;
            }
        }
        public Dungeon Dungeon {
            get {
                return dungeon;
            }
            set {
                dungeon = value;
                QueueDraw();
            }
        }

        protected override Bitmap Image {
            get {
                if (dungeon == null)
                    return null;
                double scale = 1.0/8;
                Bitmap image = new Bitmap((int)(roomWidth*16*8*scale), (int)(roomHeight*16*8*scale));
                Graphics g = Graphics.FromImage(image);

                for (int x=0; x<8; x++) {
                    for (int y=0; y<8; y++) {
                        Room room = Dungeon.GetRoom(0, x, y);
                        Bitmap img = room.GetImage();
                        g.DrawImage(img, (int)(x*roomWidth*16*scale), (int)(y*roomHeight*16*scale),
                                (int)(roomWidth*16*scale), (int)(roomHeight*16*scale));
                    }
                }
                g.Dispose();
                return image;
            }
        }

        Dungeon dungeon;
        int roomWidth, roomHeight;

        public Minimap()
        {
            roomWidth = 15;
            roomHeight = 11;
        }

        protected override bool OnButtonPressEvent(Gdk.EventButton ev)
        {
            // Insert button press handling code here.
            return base.OnButtonPressEvent(ev);
        }

        protected override bool OnExposeEvent(Gdk.EventExpose ev)
        {
            base.OnExposeEvent(ev);
            
            return true;
        }

        protected override void OnSizeAllocated(Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated(allocation);
            // Insert layout code here.
        }

        protected override void OnSizeRequested(ref Gtk.Requisition requisition)
        {
            base.OnSizeRequested(ref requisition);
        }
    }
}

