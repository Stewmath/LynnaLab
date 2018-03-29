using System;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class ComboBoxFromConstants : Gtk.Bin
    {
        public event EventHandler Changed;

        ConstantsMapping mapping;

        // Array of actual text from ConstantsMapping (because the string is changed around
        // a little bit)
        string[] keyText;

        public int Active {
            get { return combobox1.Active; }
            set {
                combobox1.Active = value;
                if (mapping != null && Active != -1)
                    spinButton.Value = mapping.GetIndexByte(combobox1.Active);
            }
        }
        public int ActiveValue {
            get { return spinButton.ValueAsInt; }
            set {
                if (mapping != null)
                    Active = mapping.IndexOf((byte)value);
                spinButton.Value = value;
            }
        }
        public string ActiveText {
            get {
                if (combobox1.Active == -1)
                    return "";
                return keyText[combobox1.Active];
            }
            set {
                if (mapping != null) {
                    Active = mapping.IndexOf(value);
                    if (Active != -1)
                        spinButton.Value = mapping.GetIndexByte(combobox1.Active);
                }
            }
        }
        public Documentation Documentation {
            get {
                if (mapping == null)
                    return null;
                return new Documentation("", "", mapping.GetAllValuesWithDescriptions());
            }
        }


        // TODO: pass in a label which it will update with the name from the combobox?
        public ComboBoxFromConstants()
        {
            this.Build();

            // When clicking the "help" button, create a popup with documentation for
            // possible values. (It checks for a "@values" field in the documentation.)
            Gtk.Button helpButton = new Gtk.Button("?");
            helpButton.Clicked += delegate(object sender, EventArgs e) {
                if (Active == -1)
                    return;

                if (Documentation == null)
                    return;

                Gtk.Dialog d = new Gtk.Dialog();
                Gtk.Label nameLabel = new Gtk.Label("<b>"+Documentation.Name+"</b>");
                nameLabel.Wrap = true;
                nameLabel.UseUnderline = false;
                nameLabel.UseMarkup = true;
                nameLabel.Xalign = 0.5f;
                d.VBox.PackStart(nameLabel, false, false, 10);

                string desc = Documentation.Description;
                if (desc == null)
                    desc = "";
                IList<Tuple<string,string>> subidEntries = null;

                subidEntries = Documentation.ValueList;

                if (subidEntries != null && subidEntries.Count > 0) {
                    desc += "\n\nValues:";
                }

                Gtk.Label descLabel = new Gtk.Label(desc);
                descLabel.Wrap = true;
                descLabel.UseUnderline = false;
                descLabel.Xalign = 0;
                d.VBox.PackStart(descLabel, false, false, 0);

                if (subidEntries != null && subidEntries.Count > 0) {
                    Gtk.Table subidTable = new Gtk.Table(2,(uint)subidEntries.Count*2,false);

                    uint subidX=0;
                    uint subidY=0;

                    foreach (var tup in subidEntries) {
                        Gtk.Label l1 = new Gtk.Label(tup.Item1);
                        l1.UseUnderline = false;
                        l1.Xalign = 0;
                        l1.Yalign = 0;

                        Gtk.Label l2 = new Gtk.Label(tup.Item2);
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

                    d.VBox.PackStart(scrolledWindow, true, true, 0);
                }

                d.AddActionWidget(new Gtk.Button("gtk-ok"), 0);
                //d.SetSizeRequest(500, 500);

                d.ShowAll();

                d.Run();
                d.Destroy();
            };

            hbox1.PackStart(helpButton, false, false, 0);
        }

        public void SetConstantsMapping(ConstantsMapping mapping) {
            this.mapping = mapping;
            keyText = new string[mapping.GetAllStrings().Count];

            int i=0;
            foreach (string key in mapping.GetAllStrings()) {
                string text = mapping.RemovePrefix(key);
                int value = mapping.StringToByte(key);
                combobox1.AppendText(Wla.ToByte((byte)value) + ": " + text);

                keyText[i] = key;
                i++;
            }
        }

        protected void OnCombobox1Changed(object sender, EventArgs e)
        {
            if (Changed != null)
                Changed(sender, e);

            if (mapping == null || combobox1.Active == -1)
                return;
            spinButton.Value = mapping.GetIndexByte(combobox1.Active);
        }

        protected void OnSpinButtonValueChanged (object sender, EventArgs e)
        {
            if (Changed != null)
                Changed(sender, e);

            if (mapping == null)
                return;
            combobox1.Active = mapping.IndexOf((byte)spinButton.ValueAsInt);
        }
    }
}
