using System;

namespace LynnaLab
{
    public abstract class Plugin
    {
        // Properties

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

        protected virtual Gtk.Widget Widget {
            get { throw new NotImplementedException(); }
        }


        // Methods

        public abstract void Init(PluginManager manager);

        public virtual void Exit() { throw new NotImplementedException(); }
        public virtual void Activate() { throw new NotImplementedException(); }
        public virtual Gtk.Widget Instantiate() { throw new NotImplementedException(); }

        public void SpawnWindow() {
            Gtk.Window w = new Gtk.Window(Name);
            w.Add(Instantiate());
            w.ShowAll();
        }
    }
}

