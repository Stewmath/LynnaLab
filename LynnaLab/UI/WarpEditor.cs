using System;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    // TODO: Fix crash when warpdest is too high
    public class WarpEditor : Gtk.Bin
    {
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

        WarpSourceGroup _warpSourceGroup;
        WarpDestGroup destGroup;
        WarpDestData destData;

        ValueReferenceEditor sourceEditor;

        int map;
        int _selectedIndex;


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
            get { return _selectedIndex; }
            set {
                SetWarpIndex(value);
            }
        }

        public WarpSourceGroup WarpSourceGroup {
            get { return _warpSourceGroup; }
            set {
                if (_warpSourceGroup != value) {
                    _warpSourceGroup = value;

                    if (warpSourceBox != null)
                        warpSourceBox.Dispose();
                    warpSourceBox = new WarpSourceBox(_warpSourceGroup);
                    warpSourceBox.AddTileSelectedHandler((sender, index) => {
                        SelectedIndex = index;
                    });
                    warpSourceBoxContainer.Add(warpSourceBox);
                    warpSourceBoxContainer.ShowAll();
                }
            }
        }

        public WarpSourceData SelectedWarpSource {
            get { return GetWarpSourceDataIndex(SelectedIndex); }
        }

        Project Project {
            get { return mainWindow.Project; }
        }


        // Methods

        public void SetMap(int group, int map) {
            WarpSourceGroup = Project.GetIndexedDataType<WarpSourceGroup>((group<<8) | map);

            this.map = map;

            SetWarpIndex(-1);
        }

        // Load the i'th warp in the current map.
        public void SetWarpIndex(int i) {
            if (i >= WarpSourceGroup.Count)
                i = WarpSourceGroup.Count-1;

            if (_selectedIndex == i)
                return;

            _selectedIndex = i;

            valueEditorContainer.Foreach((c) => c.Dispose());

            if (i == -1) {
                warpSourceFrame.Visible = false;
                warpSourceFrame.Hide();
                OnDestIndexChanged(-1,-1);
                return;
            }

            Gtk.HBox hbox = new Gtk.HBox();

            WarpSourceData warpSourceData = SelectedWarpSource;

            if (warpSourceData.WarpSourceType == WarpSourceType.Pointed)
                warpSourceTypeLabel.Text = "<b>Type</b>: Position Warp";
            else
                warpSourceTypeLabel.Text = "<b>Type</b>: Screen Warp";

            warpSourceTypeLabel.UseMarkup = true;

            OnDestIndexChanged(warpSourceData.DestGroup, warpSourceData.DestIndex);
            sourceEditor = new ValueReferenceEditor(Project, warpSourceData.ValueReferenceGroup);

            Gtk.Button newDestButton = new Gtk.Button("New/Unused\nDestination");
            newDestButton.Clicked += (sender, args) => {
                if (destGroup != null) {
                    WarpDestData data = destGroup.GetNewOrUnusedDestData();
                    SelectedWarpSource.SetDestData(data);
                }
            };

            sourceEditor.AddDataModifiedHandler(OnSourceDataModified);
            sourceEditor.AddWidgetToSide("Dest Index", newDestButton, height:2);

            hbox.Add(sourceEditor);
            valueEditorContainer.Add(hbox);
            warpSourceFrame.ShowAll();

            warpSourceBox.SelectedIndex = i;

            if (SelectedWarpEvent != null)
                SelectedWarpEvent(this, null);
        }

        // Gets the index corresponding to a spin button value.
        WarpSourceData GetWarpSourceDataIndex(int i) {
            return WarpSourceGroup.GetWarpSource(i);
        }

        void OnDestIndexChanged(int group, int index) {
            if (group == -1 || group >= Project.GetNumGroups() || index == -1)
            {
                destGroup = null;
                destData = null;
                warpDestFrame.Hide();
                return;
            }

            // Check for change
            if (destGroup == Project.GetIndexedDataType<WarpDestGroup>(group)
                    && destData == destGroup?.GetWarpDest(index))
                return;

            destGroup = Project.GetIndexedDataType<WarpDestGroup>(group);

            if (index >= destGroup.GetNumWarpDests()) {
                destGroup = null;
                destData = null;
                warpDestFrame.Hide();
                return;
            }

            destEditorContainer.Foreach((c) => c.Dispose());

            destData = destGroup.GetWarpDest(index);
            ValueReferenceEditor editor = new ValueReferenceEditor(Project,destData.ValueReferenceGroup);

            destInfoLabel.Text = "Group " + group + " Index " + Wla.ToHex(index, 2) + ": ";
            int numReferences = destData.GetNumReferences()-1;
            if (numReferences == 0)
                destInfoLabel.Text += "Used by no other sources";
            else if (numReferences == 1)
                destInfoLabel.Text += "Used by <span foreground=\"red\">" + numReferences + " other source</span>";
            else
                destInfoLabel.Text += "Used by <span foreground=\"red\">" + numReferences + " other sources</span>";
            destInfoLabel.UseMarkup = true;

            destEditorContainer.Add(editor);
            destEditorContainer.ShowAll();
            warpDestFrame.ShowAll();
        }

        void OnJumpToDestClicked(object sender, EventArgs e) {
            if (destData != null) {
                mainWindow.SetRoom((destData.DestGroup.Index<<8) | destData.Map);
            }
        }

        void OnSourceDataModified() {
            WarpSourceData data = SelectedWarpSource;
            OnDestIndexChanged(data.DestGroup, data.DestIndex);
        }



        class WarpSourceBox : SelectionBox {
            public WarpSourceBox(WarpSourceGroup warpSourceGroup) {
                this.WarpSourceGroup = warpSourceGroup;
                WarpSourceGroup.AddModifiedHandler(OnWarpSourceGroupModified);
                OnWarpSourceGroupModified(null, null);
            }


            public WarpSourceGroup WarpSourceGroup { get; private set; }



            protected override void OnDestroyed() {
                WarpSourceGroup.RemoveModifiedHandler(OnWarpSourceGroupModified);
                base.OnDestroyed();
            }

            void OnWarpSourceGroupModified(object sender, EventArgs args) {
                MaxIndex = WarpSourceGroup.Count - 1;
                QueueDraw();
            }


            // SelectionBox overrides

            protected override void OnMoveSelection(int oldIndex, int newIndex) {
                // Can't be moved; do nothing
            }

            protected override void ShowPopupMenu(Gdk.EventButton ev) {
                Gtk.Menu menu = new Gtk.Menu();
                {
                    Gtk.MenuItem item = new Gtk.MenuItem("Add standard warp");
                    menu.Append(item);

                    item.Activated += (sender, args) => {
                        SelectedIndex = WarpSourceGroup.AddWarpSource(WarpSourceType.Standard);
                    };
                }

                {
                    Gtk.MenuItem item = new Gtk.MenuItem("Add specific-position warp");
                    menu.Append(item);

                    item.Activated += (sender, args) => {
                        SelectedIndex = WarpSourceGroup.AddWarpSource(WarpSourceType.Pointed);
                    };
                }

                if (HoveringIndex != -1) {
                    menu.Append(new Gtk.SeparatorMenuItem());

                    Gtk.MenuItem deleteItem = new Gtk.MenuItem("Delete");
                    deleteItem.Activated += (sender, args) => {
                        if (SelectedIndex != -1)
                            WarpSourceGroup.RemoveWarpSource(SelectedIndex);
                    };
                    menu.Append(deleteItem);
                }

                menu.AttachToWidget(this, null);
                menu.ShowAll();
                menu.Popup(null, null, null, IntPtr.Zero, ev.Button, ev.Time);
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
