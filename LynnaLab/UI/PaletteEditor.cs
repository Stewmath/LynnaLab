using System;

using LynnaLib;
using Util;

namespace LynnaLab
{
    /// Provides an interface for editing palettes. Unfortunately this doesn't do very much about
    /// redundant data; the user will need to deal with that themselves, perhaps by de-duplicating
    /// the data in a text editor.
    public class PaletteEditor : Gtk.Box
    {
        // Variables

        PaletteHeaderGroup _paletteHeaderGroup;


        // Constuctors

        public PaletteEditor(MainWindow mainWindow) : base(Gtk.Orientation.Vertical, 0)
        {
            MainWindow = mainWindow;
        }


        // Properties

        public PaletteHeaderGroup PaletteHeaderGroup
        {
            get
            {
                return _paletteHeaderGroup;
            }
            set
            {
                _paletteHeaderGroup = value;
                UpdateButtons();
            }
        }

        private MainWindow MainWindow
        {
            get; set;
        }


        // Methods

        void UpdateButtons()
        {
            base.Foreach((c) =>
            {
                base.Remove(c);
                c.Dispose();
            });

            this.Spacing = 6;

            if (PaletteHeaderGroup != null)
            {
                PaletteHeaderGroup.Foreach((paletteHeader) =>
                {
                    Add(GenerateButtonsForData(paletteHeader));
                });
            }
            ShowAll();
        }

        Gtk.Widget GenerateButtonsForData(PaletteHeaderData data)
        {
            Gtk.Grid grid = new Gtk.Grid();

            if (data.IsResolvable)
            {
                Color[][] colors = data.GetPalettes();

                int row = 0;
                int col = 0;

                int numCols = 8; // Reduce this to sort colors into separate rows

                for (int i = 0; i < data.NumPalettes; i++)
                {
                    Gtk.Box box = new Gtk.Box(Gtk.Orientation.Vertical, 0);

                    for (int j = 0; j < 4; j++)
                    {
                        int paletteIndex = i;
                        int colorIndex = j;

                        var color = colors[i][j];
                        Gtk.ColorButton button = new Gtk.ColorButton(color.ToGdk());
                        button.ColorSet += (sender, args) =>
                        {
                            data.SetColor(paletteIndex, colorIndex, button.Rgba.FromGdk());
                        };

                        var contextMenu = new Gtk.Menu();

                        var copyItem = new Gtk.MenuItem("Copy");
                        copyItem.Activated += (_, _) =>
                        {
                            MainWindow.CopiedColor = button.Rgba.FromGdk();
                        };
                        contextMenu.Append(copyItem);

                        var pasteItem = new Gtk.MenuItem("Paste");
                        pasteItem.Activated += (_, _) =>
                        {
                            if (MainWindow.CopiedColor != null)
                            {
                                data.SetColor(paletteIndex, colorIndex, MainWindow.CopiedColor);
                                button.Rgba = MainWindow.CopiedColor.ToGdk();
                            }
                        };
                        contextMenu.Append(pasteItem);

                        contextMenu.ShowAll();

                        button.ButtonPressEvent += (_, args) =>
                        {
                            if (args.Event.Button == 3)
                            {
                                contextMenu.Popup();
                            }
                        };

                        box.Add(button);
                    }

                    Gtk.Frame frame = new Gtk.Frame();
                    frame.Label = (i + data.FirstPalette).ToString();
                    frame.LabelXalign = 0.5f;
                    frame.Add(box);
                    grid.Attach(frame, col, row, 1, 1);
                    col++;
                    if (col == numCols)
                    {
                        col = 0;
                        row++;
                    }
                }
            }

            grid.Halign = Gtk.Align.Center;

            Gtk.Frame outsideFrame = new Gtk.Frame();
            outsideFrame.Add(grid);
            outsideFrame.Label = data.PointerName
                        + " [" + (data.PaletteType == PaletteType.Background ? "BG" : "OBJ") + "]";
            outsideFrame.LabelXalign = 0.5f;
            outsideFrame.Halign = Gtk.Align.Center;

            return outsideFrame;
        }
    }
}
