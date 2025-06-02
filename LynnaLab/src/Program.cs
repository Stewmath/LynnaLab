// global using directives apply to all files in the project (C#10 feature).
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Numerics;
global using ImGuiNET;
global using LynnaLib;
global using Util;
global using OneOf;
global using OneOf.Types;

global using Debug = System.Diagnostics.Debug;

// Veldrid backend stuff
global using Interpolation = VeldridBackend.Interpolation;
global using Palette = VeldridBackend.VeldridPalette;
global using TextureBase = VeldridBackend.VeldridTextureBase;
global using RgbaTexture = VeldridBackend.VeldridRgbaTexture;
global using TextureWindow = VeldridBackend.VeldridTextureWindow;
global using TextureModifiedEventArgs = VeldridBackend.TextureModifiedEventArgs;

using System.Runtime.InteropServices;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace LynnaLab;

class Program
{
    public static Exception handlingException;

    /// <summary>
    /// Program entry point.
    /// </summary>
    static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 2)
                Top.Load(args[0], args[1]);
            else if (args.Length >= 1)
                Top.Load(args[0]);
            else
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string path = $"C:\\msys64\\home\\{Environment.UserName}\\oracles-disasm";
                    Top.Load(path, implicitWindowsOpen: true);
                }
                else
                    Top.Load();
            }

            while (!Top.Backend.Exited)
            {
                Top.Run();
            }
        }
        catch (Exception e)
        {
            // This is our generic exception handler - we print the exception to the console and
            // attempt to create a messagebox with SDL.

            // If we end up here twice then something must be wrong with our exception handler,
            // so just give up and throw it. (This is only relevant to the old imgui-based exception
            // handler, not the current SDL-based one.)
            if (handlingException != null)
                throw handlingException;

            handlingException = e;
            Console.WriteLine("Unhandled exception occurred!");
            Console.WriteLine(e);

            string message = "An unhandled exception occurred! Take a screenshot and show this to Stewmat.\n\nLynnaLab will now terminate. Click \"Save & exit\" to attempt to save your project before closing.\n\n" + e;
            int option = SDLUtil.SDLHelper.ShowErrorMessageBox(
                "Error",
                message,
                new string[] { "Save & exit", "Don't save" });

            if (option == 0)
                Top.SaveProject();

            return 1;
        }

        return 0;
    }
}
