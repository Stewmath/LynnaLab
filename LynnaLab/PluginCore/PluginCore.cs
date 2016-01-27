using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;

using System.Linq;
using System.Reflection;
using Microsoft.CSharp;

using Gtk;
using Atk;

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
            UnloadPlugins();

            var files = Directory.GetFiles("Plugins/", "*.cs");
            foreach (string s in files) {
                string code = File.ReadAllText(s);

                Type type = Compile(code, s);
                if (type != null)
                    pluginManagers.Add(new PluginManager(this, mainWindow, type));
            }
        }

        public void UnloadPlugins() {
            foreach (Plugin p in GetPlugins()) {
                p.Exit();
            }
            pluginManagers.Clear();
        }

        public IEnumerable<Plugin> GetPlugins() {
            return pluginManagers.Select(m => m.Plugin);
        }

        Type Compile(string code, string filename) {
            Type pluginType = null;

            var CompilerParams = new CompilerParameters();

            CompilerParams.GenerateInMemory = true;
            CompilerParams.TreatWarningsAsErrors = false;
            CompilerParams.GenerateExecutable = false;
            CompilerParams.CompilerOptions = "/optimize";

            // Get a list of all referenced assemblies, give them to the plugin
            AssemblyName[] assemblies = System.Reflection.Assembly.GetEntryAssembly().GetReferencedAssemblies();
            string[] references = new string[assemblies.Length+1];
            references[assemblies.Length] = "LynnaLab.exe";
            for (int i=0;i<assemblies.Length;i++) {
                Assembly asm = Assembly.Load(assemblies[i]);
                references[i] = asm.CodeBase.Replace("file:///", "/");
//                 Console.WriteLine("Reference: \"" + references[i] + "\"");
            }
            CompilerParams.ReferencedAssemblies.AddRange(references);

            var provider = new CSharpCodeProvider();
            CompilerResults compile = provider.CompileAssemblyFromSource(CompilerParams, code);

            if (compile.Errors.HasErrors) {
                string text = "Compile error: ";
                foreach (CompilerError ce in compile.Errors)
                {
                    text += ce.ToString() + '\n';
                }
                Gtk.MessageDialog d = new MessageDialog(null,
                        DialogFlags.DestroyWithParent,
                        MessageType.Error,
                        ButtonsType.Ok,
                        "There was an error compiling \"" + filename + "\":\n\n" + text);
                d.Run();
                d.Destroy();
                return null;
            }

            foreach (Module module in compile.CompiledAssembly.GetModules()) {
                foreach (Type type in module.GetTypes()) {
                    if (type.BaseType == typeof(Plugin)) {
                        Console.WriteLine(type + " implements Plugin");
                        pluginType = type;
                        break;
                    }
                }
                if (pluginType != null)
                    break;
            }

            return pluginType;
        }
    }

}
