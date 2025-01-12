// global using directives apply to all files in the project (C#10 feature).
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using ImGuiNET;
global using LynnaLib;
global using Util;

global using Debug = System.Diagnostics.Debug;
global using ImageSharp = SixLabors.ImageSharp;

using System.Runtime.InteropServices;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace LynnaLab;

class Program
{
    public static Exception handlingException;

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

        while (!TopLevel.Backend.Exited)
        {
            try
            {
                TopLevel.Run();
            }
            catch (Exception e)
            {
                // If we end up here twice then something must be wrong with our exception handler,
                // so just give up and throw it.
                if (handlingException != null)
                    throw handlingException;

                handlingException = e;
                Console.WriteLine("Unhandled exception occurred!");
                Console.WriteLine(e);
                TopLevel.DisplayExceptionModal(e);
            }
        }
    }
}
