using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;

using System.Linq;
using System.Reflection;
using Microsoft.CSharp;

using Gtk;

namespace LynnaLab
{
    public class PluginCore
    {
        MainWindow mainWindow;
        readonly List<PluginManager> pluginManagers = new List<PluginManager>();

        readonly string[] referencedAssemblies;

        public Project Project {
            get {
                return mainWindow.Project;
            }
        }

        public PluginCore(MainWindow window)
        {
            mainWindow = window;

            // Force all referenced assemblies to load so that I can find them
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            loadedAssemblies
                .SelectMany(x => x.GetReferencedAssemblies())
                .Distinct()
                .Where(y => loadedAssemblies.Any((a) => a.FullName == y.FullName) == false)
                .ToList()
                .ForEach(x => loadedAssemblies.Add(AppDomain.CurrentDomain.Load(x)));

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            List<string> references = new List<string>();
            references.Add("LynnaLab.exe");

            for (int i=0;i<assemblies.Length;i++) {
                try {
                    if (Environment.OSVersion.Platform == PlatformID.Win32Windows
                        || Environment.OSVersion.Platform == PlatformID.Win32NT
                        || Environment.OSVersion.Platform == PlatformID.Win32S
                        || Environment.OSVersion.Platform == PlatformID.WinCE) {
                        references.Add(assemblies[i].CodeBase.Replace("file:///", ""));
                    }
                    else {
                        references.Add(assemblies[i].CodeBase.Replace("file:///", "/"));
                    }
                    Console.WriteLine("Reference: \"" + references[references.Count-1] + "\"");
                }
                catch (System.NotSupportedException) {
                    Console.WriteLine("1 reference failed: " + assemblies[i]);
                }
            }
            referencedAssemblies = references.ToArray();
        }

        public void ReloadPlugins() {
            UnloadPlugins();

            try {
                var files = Directory.GetFiles("Plugins/", "*.cs");
                foreach (string s in files) {
                    string code = File.ReadAllText(s);

                    Type type = Compile(code, s);
                    if (type != null)
                        pluginManagers.Add(new PluginManager(this, mainWindow, type));
                }
            }
            catch (DirectoryNotFoundException e) {
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

            CompilerParams.ReferencedAssemblies.AddRange(referencedAssemblies);

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
