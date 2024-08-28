using System;

using System.Runtime.InteropServices;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace LynnaLab
{
    class Program
    {
        static void Main(string[] args)
        {
            var backend = new VeldridBackend.VeldridBackend();

            TopLevel topLevel;
            if (args.Length >= 2)
                topLevel = new TopLevel(backend, args[0], args[1]);
            else if (args.Length >= 1)
                topLevel = new TopLevel(backend, args[0]);
            else
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string path = $"C:\\msys64\\home\\{Environment.UserName}\\oracles-disasm";
                    topLevel = new TopLevel(backend, path);
                }
                else
                    topLevel = new TopLevel(backend);
            }

            topLevel.Run();
        }
    }
}
