using System;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class DungeonEditor : Gtk.Bin
    {
        Minimap minimap;

        Dungeon _dungeon;

        public Dungeon Dungeon {
            get {
                return _dungeon;
            }
            set {
                _dungeon = value;
                minimap.Project = _dungeon.Project;
                minimap.SetDungeon(_dungeon);
            }
        }

        public DungeonEditor()
        {
            this.Build();

            minimap = new Minimap();

            vbox2.PackStart(minimap);
        }
    }
}

