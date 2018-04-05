using System;
using System.Collections.Generic;
namespace LynnaLab
{
    public partial class DocumentationDialog : Gtk.Dialog
    {
        Documentation documentation;

        Gtk.VBox VBox = new Gtk.VBox();

        public DocumentationDialog (Documentation _doc)
        {
            documentation = _doc;

            this.Add(VBox);

            Gtk.Label nameLabel = new Gtk.Label("<b>"+documentation.Name+"</b>");
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
            VBox.PackStart(descLabel, false, false, 0);


            // Display X and Y (TODO: just display all unrecognized fields?)
            AddGenericField("Y");
            AddGenericField("X");

            // Create SubID table
            if (subidEntries.Count > 0) {
                Gtk.Label valuesLabel = new Gtk.Label("\nValues:");
                valuesLabel.UseUnderline = false;
                valuesLabel.Xalign = 0;
                VBox.Add(valuesLabel);

                Gtk.Table subidTable = new Gtk.Table(2,(uint)subidEntries.Count*2,false);

                uint subidX=0;
                uint subidY=0;

                foreach (string key in subidEntries) {
                    string value = documentation.GetField(key);

                    Gtk.Label l1 = new Gtk.Label(key);
                    l1.UseUnderline = false;
                    l1.Xalign = 0;
                    l1.Yalign = 0;

                    Gtk.Label l2 = new Gtk.Label(value);
                    l2.UseUnderline = false;
                    l2.Wrap = true;
                    l2.Xalign = 0;
                    l2.Yalign = 0;

                    subidTable.Attach(l1, subidX+0,subidX+1, subidY,subidY+1, Gtk.AttachOptions.Fill, Gtk.AttachOptions.Fill, 4, 0);
                    subidTable.Attach(l2, subidX+2,subidX+3, subidY,subidY+1);

                    subidY++;
                    subidTable.Attach(new Gtk.HSeparator(), subidX+0,subidX+3, subidY,subidY+1, Gtk.AttachOptions.Fill, 0, 0, 0);
                    subidY++;
                }
                subidTable.Attach(new Gtk.VSeparator(), subidX+1,subidX+2, 0,subidTable.NRows, 0, Gtk.AttachOptions.Fill, 4, 0);

                Gtk.ScrolledWindow scrolledWindow = new Gtk.ScrolledWindow();
                scrolledWindow.AddWithViewport(subidTable);
                scrolledWindow.ShadowType = Gtk.ShadowType.EtchedIn;
                scrolledWindow.SetPolicy(Gtk.PolicyType.Automatic, Gtk.PolicyType.Automatic);
                subidTable.ShowAll();

                // Determine width/height to request on scrolledWindow
                Gtk.Requisition subidTableRequest = subidTable.SizeRequest();
                int width = Math.Min(subidTableRequest.Width+20, 700);
                width = Math.Max(width, 400);
                int height = Math.Min(subidTableRequest.Height+5, 400);
                height = Math.Max(height, 200);
                scrolledWindow.SetSizeRequest(width, height);

                VBox.PackStart(scrolledWindow, true, true, 0);
            }

            AddActionWidget(new Gtk.Button("gtk-ok"), 0);
            //SetSizeRequest(500, 500);

            ShowAll();
        }

        void AddGenericField(string field) {
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
