using System;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    public class WarpEditor : Gtk.Bin
    {
        // Events
        public event EventHandler<EventArgs> SelectedWarpEvent;

        // Properties

        Project Project {
            get { return mainWindow.Project; }
        }

        public bool SyncWithMainWindow {
            get { return syncCheckButton.Active; }
        }

        public int SelectedIndex {
            get { return _selectedIndex; }
            set {
                if (_selectedIndex != value) {
                    _selectedIndex = value;
                    SetWarpIndex(_selectedIndex);
                }
            }
        }


        // GUI stuff
        Gtk.Box mainVBox;
        Gtk.Button addScreenWarpButton, addPositionWarpButton;
        Gtk.CheckButton syncCheckButton;
        SpinButtonHexadecimal roomSpinButton;
        Gtk.SpinButton indexSpinButton;
        Gtk.Label destInfoLabel, warpSourceTypeLabel;
        Gtk.Alignment valueEditorContainer, destEditorContainer;
        Gtk.Frame warpSourceFrame, warpDestFrame;
        Gtk.Box infoBarHolder;
        InfoBar infoBar;

        MainWindow mainWindow;


        // Variables

        WarpSourceGroup sourceGroup;
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
            addScreenWarpButton = (Gtk.Button)builder.GetObject("addScreenWarpButton");
            addPositionWarpButton = (Gtk.Button)builder.GetObject("addPositionWarpButton");
            syncCheckButton = (Gtk.CheckButton)builder.GetObject("syncCheckButton");
            indexSpinButton = (Gtk.SpinButton)builder.GetObject("indexSpinButton");
            destInfoLabel = (Gtk.Label)builder.GetObject("destInfoLabel");
            warpSourceTypeLabel = (Gtk.Label)builder.GetObject("warpSourceTypeLabel");
            valueEditorContainer = (Gtk.Alignment)builder.GetObject("valueEditorContainer");
            destEditorContainer = (Gtk.Alignment)builder.GetObject("destEditorContainer");
            warpSourceFrame = (Gtk.Frame)builder.GetObject("warpSourceFrame");
            warpDestFrame = (Gtk.Frame)builder.GetObject("warpDestFrame");
            infoBarHolder = (Gtk.Box)builder.GetObject("infoBarHolder");

            roomSpinButton = new SpinButtonHexadecimal();
            roomSpinButton.Digits = 3;
            ((Gtk.Box)builder.GetObject("roomSpinButtonHolder")).Add(roomSpinButton);

            base.Child = (Gtk.Widget)builder.GetObject("WarpEditor");

            addScreenWarpButton.Image = new Gtk.Image(Stock.Add, Gtk.IconSize.Button);
            addPositionWarpButton.Image = new Gtk.Image(Stock.Add, Gtk.IconSize.Button);

            roomSpinButton.Adjustment.Lower = 0;
            roomSpinButton.Adjustment.Upper = 0;
            roomSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                SetMap(roomSpinButton.ValueAsInt>>8, roomSpinButton.ValueAsInt&0xff);
            };
            indexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                SetWarpIndex(indexSpinButton.ValueAsInt);
            };

            infoBar = new InfoBar();
            infoBarHolder.Add(infoBar);
        }

        public void SetMap(int group, int map) {
            roomSpinButton.Adjustment.Upper = Project.GetNumRooms()-1; // Couldn't find a good place for this

            roomSpinButton.Value = (group<<8) | map;
            sourceGroup = Project.GetIndexedDataType<WarpSourceGroup>((group<<8) | map);

            this.map = map;

            UpdateIndexSpinButtonRange();
            SetWarpIndex(0);
        }

        // Load the i'th warp in the current map.
        public void SetWarpIndex(int i) {
            if (i > indexSpinButton.Adjustment.Upper)
                i = (int)indexSpinButton.Adjustment.Upper;

            _selectedIndex = i;
            indexSpinButton.Value = i;

            valueEditorContainer.Remove(valueEditorContainer.Child);

            if (i == -1) {
                warpSourceFrame.Visible = false;
                warpSourceFrame.Hide();
                SetDestIndex(-1,-1);
                return;
            }

            Gtk.HBox hbox = new Gtk.HBox();

            WarpSourceData warpSourceData = GetWarpSourceDataIndex(i);

            if (warpSourceData.WarpSourceType == WarpSourceType.Pointed)
                warpSourceTypeLabel.Text = "<b>Type</b>: Position Warp";
            else
                warpSourceTypeLabel.Text = "<b>Type</b>: Screen Warp";

            warpSourceTypeLabel.UseMarkup = true;

            SetDestIndex(warpSourceData.DestGroup, warpSourceData.DestIndex);
            sourceEditor = new ValueReferenceEditor(Project, warpSourceData.ValueReferenceGroup);

            Alignment a = new Alignment(0,0,0,0);
            a.Add(sourceEditor);
            hbox.Add(a);

            sourceEditor.AddDataModifiedHandler(delegate() {
                SetDestIndex(warpSourceData.DestGroup, warpSourceData.DestIndex);
            });

            valueEditorContainer.Add(hbox);
            warpSourceFrame.ShowAll();

            if (SelectedWarpEvent != null)
                SelectedWarpEvent(this, null);
        }

        // Gets the index corresponding to a spin button value.
        WarpSourceData GetWarpSourceDataIndex(int i) {
            return sourceGroup.GetWarpSource(i);
        }

        void SetDestIndex(int group, int index) {
            destEditorContainer.Remove(destEditorContainer.Child);

            if (group != -1 && group < Project.GetNumGroups())
                destGroup = Project.GetIndexedDataType<WarpDestGroup>(group);

            if (group == -1 || group >= Project.GetNumGroups() ||
                    index == -1 || index >= destGroup.GetNumWarpDests())
            {
                destGroup = null;
                destData = null;
                warpDestFrame.Hide();
                return;
            }

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

        // Updates spinbutton range, also updates warnings
        void UpdateIndexSpinButtonRange() {
            infoBar.RemoveAll();

            IList<WarpSourceData> sourceDataList = sourceGroup.GetWarpSources();

            // Sanity check: make sure there's only one PointerWarp and it's at the end.
            for (int i=0; i<sourceDataList.Count-1; i++) {
                if (sourceDataList[i].WarpSourceType == WarpSourceType.Pointer) {
                    infoBar.Push(InfoLevel.Error, "Warp data formatting error: Warp #" + i + " is a PointerWarp, but there are other warps after it which won't be read. You may need to fix this manually.");
                    break;
                }
            }

            indexSpinButton.Adjustment.Lower = -1;

            int count = sourceDataList.Count;
            if (count != 0 && sourceDataList[count-1].WarpSourceType == WarpSourceType.Pointer) {
                count += sourceDataList[count-1].GetPointedChainLength();
                count--; // Don't count the "PointerWarp"
            }
            indexSpinButton.Adjustment.Upper = count-1;
        }

        protected void OnAddScreenWarpButtonClicked(object sender, EventArgs e)
        {
            WarpSourceData data = new WarpSourceData(Project,
                    command: WarpSourceData.WarpCommands[(int)WarpSourceType.Standard],
                    values: WarpSourceData.DefaultValues[(int)WarpSourceType.Standard],
                    parser: sourceGroup.FileParser,
                    spacing: new List<string>{"\t"});
            data.Map = map;

            sourceGroup.AddWarpSource(data);
            UpdateIndexSpinButtonRange();

            // Determine what index corresponds to this newly created warp (TODO: probably broken)
            var warpSourceList = sourceGroup.GetWarpSources();
            if (warpSourceList[warpSourceList.Count-1].WarpSourceType == WarpSourceType.Pointer)
                SetWarpIndex(sourceGroup.GetWarpSources().Count-2);
            else
                SetWarpIndex(sourceGroup.GetWarpSources().Count-1);
        }

        protected void OnAddPositionWarpButtonClicked(object sender, EventArgs e)
        {
            WarpSourceData data = new WarpSourceData(Project,
                    command: WarpSourceData.WarpCommands[(int)WarpSourceType.Pointed],
                    values: WarpSourceData.DefaultValues[(int)WarpSourceType.Pointed],
                    parser: sourceGroup.FileParser,
                    spacing: new List<string>{"\t"});

            sourceGroup.AddWarpSource(data);
            UpdateIndexSpinButtonRange();
            SetWarpIndex((int)indexSpinButton.Adjustment.Upper);
        }

        protected void OnRemoveWarpButtonClicked(object sender, EventArgs e)
        {
            if (indexSpinButton.ValueAsInt != -1) {
                sourceGroup.RemoveWarpSource(GetWarpSourceDataIndex(indexSpinButton.ValueAsInt));
                UpdateIndexSpinButtonRange();
                SetWarpIndex(indexSpinButton.ValueAsInt);
            }
        }

        protected void OnJumpToSourceClicked(object sender, EventArgs e) {
            mainWindow.SetRoom(roomSpinButton.ValueAsInt);
        }

        protected void OnJumpToDestClicked(object sender, EventArgs e) {
            if (destData != null) {
                syncCheckButton.Active = false;
                mainWindow.SetRoom((destData.DestGroup.Index<<8) | destData.Map);
            }
        }

        protected void OnSyncCheckButtonToggled(object sender, EventArgs e) {
            if (syncCheckButton.Active)
                SetMap(mainWindow.ActiveRoom.Index>>8, mainWindow.ActiveRoom.Index&0xff);
        }

        protected void OnOkButtonClicked(object sender, EventArgs e)
        {
            this.Dispose();
        }
    }
}
