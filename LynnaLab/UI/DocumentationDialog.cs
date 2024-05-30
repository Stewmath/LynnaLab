using System;
using System.Collections.Generic;

using LynnaLib;

namespace LynnaLab
{
    public partial class DocumentationDialog : Gtk.Dialog
    {
        Documentation documentation;

        Gtk.Box VBox = new Gtk.Box(Gtk.Orientation.Vertical, 0);

        public DocumentationDialog(Documentation _doc)
        {
            ContentArea.PackStart(VBox, true, true, 0);

            documentation = _doc;

            Gtk.Label nameLabel = new Gtk.Label("<b>" + documentation.Name + "</b>");
            nameLabel.Wrap = true;
            nameLabel.UseUnderline = false;
            nameLabel.UseMarkup = true;
            nameLabel.Xalign = 0.5f;
            VBox.PackStart(nameLabel, false, false, 10);

            string desc = documentation.Description;
            if (desc == null)
                desc = "";

            var subidEntries = documentation.Keys;

            Gtk.Label descLabel = new Gtk.Label(desc);
            descLabel.Wrap = true;
            descLabel.UseUnderline = false;
            descLabel.Xalign = 0;
            descLabel.WidthChars = 50;
            descLabel.MaxWidthChars = 50;
            VBox.PackStart(descLabel, false, false, 0);


            // Create SubID table
            if (subidEntries.Count > 0)
            {
                Gtk.Label valuesLabel = new Gtk.Label("\nValues:");
                valuesLabel.UseUnderline = false;
                valuesLabel.Xalign = 0;
                VBox.PackStart(valuesLabel, false, false, 0);

                Gtk.Grid subidTable = new Gtk.Grid();

                int subidX = 0;
                int subidY = 0;

                foreach (string key in subidEntries)
                {
                    string value = documentation.GetField(key);

                    Gtk.Label l1 = new Gtk.Label(key);
                    l1.UseUnderline = false;
                    l1.Hexpand = false;
                    l1.Xalign = 0;
                    l1.Yalign = 0;

                    Gtk.Label l2 = new Gtk.Label(value);
                    l2.UseUnderline = false;
                    l2.Wrap = true;
                    l2.Xalign = 0;
                    l2.Yalign = 0;
                    l2.Hexpand = true;
                    l2.WidthChars = 50;
                    l2.MaxWidthChars = 50;

                    subidTable.Attach(l1, subidX + 0, subidY, 1, 1);
                    subidTable.Attach(l2, subidX + 2, subidY, 1, 1);

                    subidY++;
                    subidTable.Attach(new Gtk.Separator(Gtk.Orientation.Horizontal),
                                      subidX + 0,
                                      subidY,
                                      3,
                                      1);
                    subidY++;
                }
                subidTable.Attach(new Gtk.Separator(Gtk.Orientation.Vertical),
                                  subidX + 1,
                                  0,
                                  1,
                                  subidEntries.Count * 2);

                Gtk.ScrolledWindow scrolledWindow = new Gtk.ScrolledWindow();
                scrolledWindow.Add(subidTable);
                scrolledWindow.ShadowType = Gtk.ShadowType.EtchedIn;
                scrolledWindow.SetPolicy(Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
                subidTable.ShowAll();

                VBox.PackStart(scrolledWindow, true, true, 0);

                this.SetDefaultSize(700, 600);
            }

            AddActionWidget(new Gtk.Button("gtk-ok"), 0);

            ShowAll();
        }

        protected override void OnResponse(Gtk.ResponseType response_id)
        {
            this.Dispose();
        }

        void AddGenericField(string field)
        {
            string value = documentation.GetField(field);
            if (value == null)
                return;

            Gtk.Label label = new Gtk.Label("\n<b>" + field + "</b>: " + value);
            label.UseUnderline = false;
            label.UseMarkup = true;
            label.Wrap = true;
            label.Xalign = 0;

            VBox.PackStart(label, false, false, 2);
        }
    }
}
