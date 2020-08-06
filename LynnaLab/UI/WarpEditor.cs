using System;
using System.Collections.Generic;
using Gtk;
using Util;

namespace LynnaLab
{
    public class WarpEditor : Gtk.Bin
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        public static readonly Cairo.Color WarpSourceColor = CairoHelper.ConvertColor(186, 8, 206, 0xc0);

        // Events
        public event EventHandler<EventArgs> SelectedWarpEvent;


        // GUI stuff
        Gtk.Box mainVBox;
        Gtk.Label destInfoLabel, warpSourceTypeLabel;
        Gtk.Container valueEditorContainer, destEditorContainer, warpSourceBoxContainer;
        Gtk.Frame warpSourceFrame, warpDestFrame;
        Gtk.Box infoBarHolder;
        InfoBar infoBar;
        WarpSourceBox warpSourceBox;

        MainWindow mainWindow;


        // Variables

        WarpGroup _warpGroup;
        Warp _selectedWarp;

        ValueReferenceEditor sourceEditor;

        int map;


        // Constructor

        public WarpEditor(MainWindow main) {
            mainWindow = main;

            Gtk.Builder builder = new Builder();
            builder.AddFromString(Helper.ReadResourceFile("LynnaLab.Glade.WarpEditor.ui"));
            builder.Autoconnect(this);

            mainVBox = (Gtk.Box)builder.GetObject("mainVBox");
            destInfoLabel = (Gtk.Label)builder.GetObject("destInfoLabel");
            warpSourceTypeLabel = (Gtk.Label)builder.GetObject("warpSourceTypeLabel");
            valueEditorContainer = (Gtk.Container)builder.GetObject("valueEditorContainer");
            destEditorContainer = (Gtk.Container)builder.GetObject("destEditorContainer");
            warpSourceBoxContainer = (Gtk.Container)builder.GetObject("warpSourceBoxContainer");
            warpSourceFrame = (Gtk.Frame)builder.GetObject("warpSourceFrame");
            warpDestFrame = (Gtk.Frame)builder.GetObject("warpDestFrame");
            infoBarHolder = (Gtk.Box)builder.GetObject("infoBarHolder");

            base.Child = (Gtk.Widget)builder.GetObject("WarpEditor");

            infoBar = new InfoBar();
            infoBarHolder.Add(infoBar);
        }


        // Properties

        public int SelectedIndex {
            get { return GetWarpIndex(SelectedWarp); }
            set {
                SetWarpIndex(value);
            }
        }

        public WarpGroup WarpGroup {
            get { return _warpGroup; }
            set {
                if (_warpGroup != value) {
                    _warpGroup = value;

                    if (warpSourceBox != null)
                        warpSourceBox.Dispose();
                    warpSourceBox = new WarpSourceBox(_warpGroup);
                    warpSourceBox.AddTileSelectedHandler((sender, index) => {
                        SelectedIndex = index;
                    });
                    warpSourceBoxContainer.Add(warpSourceBox);
                    warpSourceBoxContainer.ShowAll();
                }
            }
        }

        public Warp SelectedWarp {
            get { return _selectedWarp; }
        }

        Project Project {
            get { return mainWindow.Project; }
        }


        // Methods

        public void SetMap(int group, int map) {
            WarpGroup = Project.GetIndexedDataType<WarpGroup>((group<<8) | map);

            this.map = map;

            SetWarpIndex(-1);
        }

        // Load the i'th warp in the current map.
        public void SetWarpIndex(int i) {
            if (i >= WarpGroup.Count) {
                log.Warn(string.Format("Tried to select warp index {0} (highest is {1})", i, WarpGroup.Count-1));
                i = WarpGroup.Count-1;
            }

            SetSelectedWarp(GetWarpIndex(i));
        }

        // This can be a warp that isn't in the warp list (as when editing a warp destination).
        public void SetSelectedWarp(Warp warp) {
            if (_selectedWarp == warp)
                return;
            _selectedWarp = warp;

            valueEditorContainer.Foreach((c) => c.Dispose());

            if (warp == null) {
                warpSourceFrame.Hide();
                return;
            }

            int index = GetWarpIndex(warp);
            warpSourceBox.SelectedIndex = index;

            Gtk.HBox hbox = new Gtk.HBox();

            if (warp.WarpSourceType == WarpSourceType.Pointed)
                warpSourceTypeLabel.Text = "<b>Type</b>: Position Warp";
            else
                warpSourceTypeLabel.Text = "<b>Type</b>: Screen Warp";

            warpSourceTypeLabel.UseMarkup = true;

            sourceEditor = new ValueReferenceEditor(Project, warp.ValueReferenceGroup);

            valueEditorContainer.Add(sourceEditor);
            warpSourceFrame.ShowAll();

            if (SelectedWarpEvent != null)
                SelectedWarpEvent(this, null);
        }

        // Gets the index corresponding to a spin button value.
        Warp GetWarpIndex(int i) {
            if (i == -1)
                return null;
            return WarpGroup.GetWarp(i);
        }

        int GetWarpIndex(Warp warp) {
            return WarpGroup.IndexOf(warp);
        }


        class WarpSourceBox : SelectionBox {
            public WarpSourceBox(WarpGroup warpGroup) {
                this.WarpGroup = warpGroup;
                WarpGroup.AddModifiedHandler(OnWarpGroupModified);
                OnWarpGroupModified(null, null);
            }


            public WarpGroup WarpGroup { get; private set; }



            protected override void OnDestroyed() {
                WarpGroup.RemoveModifiedHandler(OnWarpGroupModified);
                base.OnDestroyed();
            }

            void OnWarpGroupModified(object sender, EventArgs args) {
                MaxIndex = WarpGroup.Count - 1;
                QueueDraw();
            }


            // SelectionBox overrides

            protected override void OnMoveSelection(int oldIndex, int newIndex) {
                // Can't be moved; do nothing
            }

            protected override void ShowPopupMenu(Gdk.Event ev) {
                Gtk.Menu menu = new Gtk.Menu();
                {
                    Gtk.MenuItem item = new Gtk.MenuItem("Add standard warp");
                    menu.Append(item);

                    item.Activated += (sender, args) => {
                        SelectedIndex = WarpGroup.AddWarp(WarpSourceType.Standard);
                    };
                }

                {
                    Gtk.MenuItem item = new Gtk.MenuItem("Add specific-position warp");
                    menu.Append(item);

                    item.Activated += (sender, args) => {
                        SelectedIndex = WarpGroup.AddWarp(WarpSourceType.Pointed);
                    };
                }

                if (HoveringIndex != -1) {
                    menu.Append(new Gtk.SeparatorMenuItem());

                    Gtk.MenuItem deleteItem = new Gtk.MenuItem("Delete");
                    deleteItem.Activated += (sender, args) => {
                        if (SelectedIndex != -1)
                            WarpGroup.RemoveWarp(SelectedIndex);
                    };
                    menu.Append(deleteItem);
                }

                menu.AttachToWidget(this, null);
                menu.ShowAll();
                menu.PopupAtPointer(ev);
            }

            protected override void TileDrawer(int index, Cairo.Context cr) {
                if (index > MaxIndex)
                    return;
                cr.SetSourceColor(WarpSourceColor);
                cr.Rectangle(0, 0, TileWidth, TileHeight);
                cr.Fill();

                cr.SetSourceColor(new Cairo.Color(1.0, 1.0, 1.0));
                CairoHelper.DrawText(cr, index.ToString("X"), 12, 0, 0, TileWidth, TileHeight);
            }
        }
    }
}
