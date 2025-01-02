// global using directives apply to all files in the project (C#10 feature).
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using ImGuiNET;
global using LynnaLib;
global using Util;

global using Debug = System.Diagnostics.Debug;


using System.Runtime.InteropServices;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace LynnaLab;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length >= 2)
            TopLevel.Load(args[0], args[1]);
        else if (args.Length >= 1)
            TopLevel.Load(args[0]);
        else
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string path = $"C:\\msys64\\home\\{Environment.UserName}\\oracles-disasm";
                TopLevel.Load(path);
            }
            else
                TopLevel.Load();
        }

        TopLevel.Run();
    }
}
