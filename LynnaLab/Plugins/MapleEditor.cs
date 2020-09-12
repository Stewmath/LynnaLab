using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using LynnaLab;

namespace Plugins
{
    // This plugin edits maple's appearance locations.
    public class MapleEditor : Plugin
    {
        PluginManager manager;
        MyMinimap minimap;

        Project Project {
            get {
                return manager.Project;
            }
        }

        public override String Name { get { return "Maple Editor"; } }
        public override String Tooltip { get { return "Edit Maple appearance locations"; } }
        public override bool IsDockable { get { return false; } }
        public override string Category { get { return "Window"; } }

        public override void Init(PluginManager manager) {
            this.manager = manager;
        }
        public override void Exit() {
        }

        public override void Clicked() {
            Gtk.Window w = new Gtk.Window("Maple Appearance Locations");
            minimap = new MyMinimap();

            var minimapContainer = new Gtk.Alignment(1.0f,1.0f,1.0f,1.0f);

            Gtk.ComboBox comboBox = new Gtk.ComboBox(
                    new string[] {
                    "Present (Ricky)",
                    "Present (Dimitri)",
                    "Present (Moosh)",
                    "Past"
                    });

            comboBox.Changed += (a,b) => {
                int i = comboBox.Active;
                Data data;
                Map map;

                if (i == 3) {
                    data = Project.GetData("maplePastLocations");
                    map = Project.GetIndexedDataType<WorldMap>(1);
                }
                else {
                    data = Project.GetData(Project.GetData("maplePresentLocationsTable", i*2).GetValue(0));
                    map = Project.GetIndexedDataType<WorldMap>(0);
                }

                minimap.Width = map.MapWidth;
                minimap.Height = map.MapHeight;

                minimap.SetData(data);
                minimap.SetMap(map);
                minimap.Selectable = false;

                minimapContainer.Foreach((c) => minimapContainer.Remove(c));
                minimapContainer.Add(minimap);
                minimapContainer.ShowAll();
            };
            if (manager.GetActiveMap().MainGroup == 1)
                comboBox.Active = 3;
            else
                comboBox.Active = 0;

            Gtk.VBox vbox = new Gtk.VBox();
            vbox.Add(comboBox);
            vbox.Add(minimapContainer);

            w.Add(vbox);
            w.ShowAll();
        }

        class MyMinimap : Minimap {
            Data bitData;
            bool dragSet;

            public MyMinimap() : base(1.0/4) {
                base.HoverColor = new Cairo.Color(1.0, 1.0, 1.0);

                Action<int,int> OnDragged = (x, y) => {
                    x /= TileWidth;
                    y /= TileHeight;
                    SetSelected(x, y, dragSet);
                };

                this.ButtonPressEvent += delegate(object o, Gtk.ButtonPressEventArgs args) {
                    int x,y;
                    Gdk.ModifierType state;
                    args.Event.Window.GetPointer(out x, out y, out state);
                    if (IsInBounds(x, y)) {
                        if (state.HasFlag(Gdk.ModifierType.Button1Mask)) {
                            dragSet = true;
                            OnDragged(x,y);
                        }
                        else if (state.HasFlag(Gdk.ModifierType.Button3Mask)) {
                            dragSet = false;
                            OnDragged(x,y);
                        }
                    }
                };
                this.MotionNotifyEvent += delegate(object o, Gtk.MotionNotifyEventArgs args) {
                    int x,y;
                    Gdk.ModifierType state;
                    args.Event.Window.GetPointer(out x, out y, out state);
                    if (IsInBounds(x, y)) {
                        if (state.HasFlag(Gdk.ModifierType.Button1Mask) || state.HasFlag(Gdk.ModifierType.Button3Mask))
                            OnDragged(x, y);
                    }
                };
            }

            public void SetData(Data d) {
                bitData = d;
            }

            bool GetSelected(int x, int y) {
                Data data = bitData;
                int i = y*Width+x;
                while (i >= 8) {
                    data = data.NextData;
                    i-=8;
                }
                return (data.GetIntValue(0) & (0x80>>i)) != 0;
            }
            void SetSelected(int x, int y, bool val) {
                Data data = bitData;
                int i = y*Width+x;
                while (i >= 8) {
                    data = data.NextData;
                    i-=8;
                }
                int bit = (0x80>>i);
                if (val)
                    data.SetValue(0, Wla.ToBinary(data.GetIntValue(0) | bit));
                else
                    data.SetValue(0, Wla.ToBinary(data.GetIntValue(0) & ~bit));
                QueueDrawArea(x*TileWidth, y*TileHeight, TileWidth, TileHeight);
            }

            protected override void TileDrawer(int index, Cairo.Context cr) {
                base.TileDrawer(index, cr);

                int x = index % Width;
                int y = index / Width;

                cr.Save();
                if (GetSelected(x,y)) {
                    cr.SetSourceRGB(1.0, 0, 0);
                    //cr.Rectangle(0, 0, TileWidth, TileHeight);
                    cr.PaintWithAlpha(0.4);

                    CairoHelper.DrawRectOutline(cr, 1, new Cairo.Rectangle(0, 0, TileWidth, TileHeight));
                }
                cr.Restore();
            }

            /*
            protected override bool OnExposeEvent(Gdk.EventExpose ev)
            {
                base.OnExposeEvent(ev);
                Gdk.Window win = ev.Window;

                using (Graphics g = Gtk.DotNet.Graphics.FromDrawable(win)) {
                    for (int x=0;x<Width;x++) {
                        for (int y=0;y<Height;y++) {
                            if (GetSelected(x,y)) {
                                Color c = Color.Red;
                                c = Color.FromArgb(0x80, c.R,c.G,c.B);
                                g.FillRectangle(new SolidBrush(c), x*TileWidth, y*TileHeight, TileWidth, TileHeight);
                                g.DrawRectangle(new Pen(Color.Red), x*TileWidth, y*TileHeight, TileWidth-1, TileHeight-1);
                            }
                        }
                    }

                    g.DrawRectangle(new Pen(Color.Red), HoveringX*TileWidth, HoveringY*TileHeight, TileWidth-1, TileHeight-1);
                }

                return true;
            }
*/
        }
    }
}
