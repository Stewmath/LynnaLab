using System;

namespace LynnaLab
{
    public class PluginManager
    {
        readonly Type pluginType;
        readonly Plugin plugin;

        public Plugin Plugin {
            get { return plugin; }
        }

        public Project Project {
            get {
                return PluginCore.Project;
            }
        }

        PluginCore PluginCore {get;set;}
        public MainWindow MainWindow {get;set;}
        
        public PluginManager(PluginCore core, MainWindow window, Type type)
        {
            MainWindow = window;
            PluginCore = core;

            pluginType = type;
            plugin = Activator.CreateInstance(type) as Plugin;
            plugin.Init(this);
        }

        public Room GetActiveRoom() {
            return MainWindow.ActiveRoom;
        }
    }
}

