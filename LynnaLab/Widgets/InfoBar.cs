using System;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    public enum InfoLevel {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    // A simple bar that can be put on the bottom of a widget to show warnings.
    // Like Gtk.StatusBar but has icons.
    public class InfoBar : Gtk.Box
    {
        string[] imageNames = {
            "gtk-dialog-info",
            "gtk-dialog-warning",
            "gtk-dialog-error"
        };

        List<Gtk.Widget> itemList = new List<Gtk.Widget>();


        public InfoBar() : base(Orientation.Vertical, 6) {
            Gtk.Separator separator = new Gtk.Separator(Orientation.Horizontal);
            base.Add(separator);
        }

        public void Push(InfoLevel level, string text) {
            Gtk.Box hbox = new Gtk.Box(Orientation.Horizontal, 6);

            Gtk.Image image = new Gtk.Image(imageNames[(int)level], Gtk.IconSize.SmallToolbar);
            hbox.Add(image);
            hbox.Add(new Gtk.Label(text));

            base.Add(hbox);
            itemList.Add(hbox);

            this.ShowAll();
        }

        public void RemoveAll() {
            foreach (Gtk.Widget widget in itemList) {
                base.Remove(widget);
            }
            itemList = new List<Gtk.Widget>();
        }
    }
}
