﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Gtk;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace LynnaLab
{
    class MainClass
    {

        public static void Main(string[] args)
        {
            NUnitTestClass.RunTests();

            Application.Init();

#if (!DEBUG)
            try {
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => HandleException(e.ExceptionObject);
                GLib.ExceptionManager.UnhandledException += (args) => HandleException(args.ExceptionObject);
#endif

            MainWindow win;
            if (args.Length >= 1)
                win = new MainWindow(args[0]);
            else
                win = new MainWindow();
            Application.Run();

#if (!DEBUG)
            }
            catch (Exception e) {
                HandleException(e);
            }
#endif
        }

        static void HandleException(object exception)
        {
            string outputString = "An unhandled exception occurred:\n\n";

            outputString += exception.ToString();

            Console.WriteLine(outputString);

            Gtk.MessageDialog d = new MessageDialog(null,
                                        DialogFlags.DestroyWithParent,
                                        MessageType.Error,
                                        ButtonsType.Ok,
                                        outputString);
            d.Run();
            d.Dispose();
            Environment.Exit(1);
        }
    }
}
