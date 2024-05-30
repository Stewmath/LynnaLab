using System;

using LynnaLib;

namespace LynnaLab
{
    public class PluginManager
    {
        readonly Type pluginType;
        readonly Plugin plugin;

        public Plugin Plugin
        {
            get { return plugin; }
        }

        public Project Project
        {
            get
            {
                return PluginCore.Project;
            }
        }

        PluginCore PluginCore { get; set; }
        public MainWindow MainWindow { get; set; }

        public PluginManager(PluginCore core, MainWindow window, Type type)
        {
            MainWindow = window;
            PluginCore = core;

            pluginType = type;
            plugin = Activator.CreateInstance(type) as Plugin;
            plugin.Init(this);
        }

        public Room GetActiveRoom()
        {
            return MainWindow.ActiveRoom;
        }
        public RoomLayout GetActiveRoomLayout()
        {
            return MainWindow.ActiveRoomLayout;
        }

        public Map GetActiveMap()
        {
            return MainWindow.ActiveMap;
        }
        public int GetMapSelectedX()
        {
            return MainWindow.MapSelectedX;
        }
        public int GetMapSelectedY()
        {
            return MainWindow.MapSelectedY;
        }
        public int GetMapSelectedFloor()
        {
            return MainWindow.MapSelectedFloor;
        }
    }
}

