using System;

namespace LynnaLab
{
    public abstract class Plugin
    {
        public abstract String Name {
            get;
        }
        public abstract String Tooltip {
            get;
        }
        public abstract bool IsDockable {
            get;
        }
        public abstract string Category {
            get;
        }

        public abstract void Init(PluginManager manager);
        public abstract void Exit();
        public abstract void Clicked();
    }
}

