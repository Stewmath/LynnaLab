using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;

using System.Linq;
using System.Reflection;
using Microsoft.CSharp;

using Gtk;

using LynnaLib;

namespace LynnaLab
{
    public class PluginCore
    {
        MainWindow mainWindow;
        readonly List<PluginManager> pluginManagers = new List<PluginManager>();

        public Project Project {
            get {
                return mainWindow.Project;
            }
        }

        public PluginCore(MainWindow window)
        {
            mainWindow = window;
        }

        public void ReloadPlugins() {
            pluginManagers.Clear();

            foreach (Module module in Assembly.GetExecutingAssembly().GetModules()) {
                foreach (Type type in module.GetTypes()) {
                    if (type.BaseType == typeof(Plugin)) {
                        Console.WriteLine(type + " implements Plugin");
                        pluginManagers.Add(new PluginManager(this, mainWindow, type));
                    }
                }
            }
        }

        public IEnumerable<Plugin> GetPlugins() {
            return pluginManagers.Select(m => m.Plugin);
        }
    }

}
