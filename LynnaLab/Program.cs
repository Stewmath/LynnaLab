using System;
using Gtk;

namespace LynnaLab
{
    class MainClass
    {
        public static void Main (string[] args)
        {
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
