using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Gtk;

[assembly: log4net.Config.XmlConfigurator(Watch=true)]

namespace LynnaLab
{
    class MainClass
    {

        public static void Main (string[] args)
        {
            NUnitTestClass.RunTests ();

            Application.Init ();
            MainWindow win;
            if (args.Length >= 1)
                win = new MainWindow(args[0]);
            else
                win = new MainWindow ();
            win.Show ();
            Application.Run ();
        }
    }
}
