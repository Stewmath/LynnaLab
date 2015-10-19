using System;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class WarpEditor : Gtk.Bin
    {
        // Properties

        Project Project {get; set;}

        // Variables

        WarpSourceGroup sourceGroup;
        WarpDestGroup destGroup;

        ValueReferenceEditor sourceEditor;
        ValueReferenceEditor destEditor;

        int map;

        // Constructor

        public WarpEditor(Project p) {
            Project = p;
            this.Build();

            addWarpButton.Image = new Gtk.Image(Stock.Add, Gtk.IconSize.Button);
            addSpecificWarpButton.Image = new Gtk.Image(Stock.Add, Gtk.IconSize.Button);

            roomSpinButton.Adjustment.Lower = 0;
            roomSpinButton.Adjustment.Upper = Project.GetNumRooms()-1;
            roomSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                SetMap(roomSpinButton.ValueAsInt>>8, roomSpinButton.ValueAsInt&0xff);
            };
            indexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                SetWarpIndex(indexSpinButton.ValueAsInt);
            };
        }

        public void SetMap(int group, int map) {
            roomSpinButton.Value = (group<<8) | map;
            sourceGroup = Project.GetIndexedDataType<WarpSourceGroup>(group);

            this.map = map;

            frameLabel.Text = "Room " + ((group<<8)+map).ToString("X3") + " Warp Sources";
            SetWarpIndex(0);
        }

        // Load the i'th warp in the current map.
        void SetWarpIndex(int i) {
            List<WarpSourceData> sourceDataList = sourceGroup.GetMapWarpSourceData(map);

            indexSpinButton.Adjustment.Lower = -1;
            indexSpinButton.Adjustment.Upper = sourceDataList.Count-1;

            if (i > indexSpinButton.Adjustment.Upper)
                i = (int)indexSpinButton.Adjustment.Upper;

            indexSpinButton.Value = i;

            valueEditorContainer.Remove(valueEditorContainer.Child);

            if (i == -1) {
                SetDestIndex(-1,-1);
                return;
            }

            Gtk.HBox hbox = new Gtk.HBox();

            WarpSourceData warpSourceData = sourceDataList[i];

            if (warpSourceData.WarpSourceType == WarpSourceType.StandardWarp)
                SetDestIndex(warpSourceData.DestGroup, warpSourceData.DestIndex);

            sourceEditor = new ValueReferenceEditor(Project,
                    warpSourceData);

            Alignment a = new Alignment(0,0,0,0);
            a.Add(sourceEditor);
            hbox.Add(a);

            if (warpSourceData.WarpSourceType == WarpSourceType.PointerWarp) {
                Table table = new Table(1, 1, false);
                table.ColumnSpacing = 6;
                table.RowSpacing = 6;

                SpinButton pointerSpinButton = new SpinButton(0,10,1);

                EventHandler valueChangedHandler = delegate(object sender, EventArgs e) {
                    WarpSourceData pointedData = warpSourceData.GetPointedWarp();

                    pointerSpinButton.Adjustment.Lower = 0;
                    pointerSpinButton.Adjustment.Upper = warpSourceData.GetPointedChainLength()-1;

                    if (pointerSpinButton.ValueAsInt > pointerSpinButton.Adjustment.Upper) {
                        pointerSpinButton.Value = pointerSpinButton.Adjustment.Upper;
                    }
                    int index = pointerSpinButton.ValueAsInt;

                    while (index > 0) {
                        pointedData = pointedData.GetNextWarp();
                        index--;
                    }

                    table.Remove(destEditor);

                    destEditor = new ValueReferenceEditor(Project, pointedData);

                    destEditor.AddDataModifiedHandler(delegate(object sender2, EventArgs e2) {
                            SetDestIndex(pointedData.DestGroup, pointedData.DestIndex);
                        });

                    table.Attach(destEditor, 0, 2, 1, 2);

                    SetDestIndex(pointedData.DestGroup, pointedData.DestIndex);
                };

                pointerSpinButton.ValueChanged += valueChangedHandler;

                // Button which, when clicked, adds a new PointedData to the
                // "chain".
                Gtk.Button addPointedWarpButton =
                    new Gtk.Button(new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button));

                addPointedWarpButton.Clicked += delegate(object sender, EventArgs e) {
                    WarpSourceData pointedData = warpSourceData.GetPointedWarp();

                    while (pointedData.GetNextWarp() != null) {
                        pointedData = pointedData.GetNextWarp();
                    }

                    WarpSourceData nextData = new WarpSourceData(Project,
                            WarpSourceData.WarpCommands[(int)WarpSourceType.PointedWarp],
                            WarpSourceData.DefaultValues[(int)WarpSourceType.PointedWarp],
                            pointedData.FileParser,
                            new List<int>{-1});
                    pointedData.SetNextWarp(nextData);

                    pointerSpinButton.Adjustment.Upper++;
                    pointerSpinButton.Value = warpSourceData.GetPointedChainLength()-1;
                    valueChangedHandler(null, null);
                };

                // Button which removes a PointedData from the "chain", unless
                // there is only one remaining.
                Gtk.Button removePointedWarpButton =
                    new Gtk.Button(new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button));

                removePointedWarpButton.Clicked += delegate(object sender, EventArgs e) {
                    int index = pointerSpinButton.ValueAsInt;
                    WarpSourceData pointedData = warpSourceData.GetPointedWarp();

                    if (pointedData.GetPointedChainLength() <= 1) // Don't delete the last one
                        return;

                    while (index > 0) {
                        pointedData = pointedData.GetNextWarp();
                        index--;
                    }

                    pointedData.FileParser.RemoveFileComponent(pointedData);

                    valueChangedHandler(null, null);
                };

                table.Attach(new Gtk.Label("Pointer index"), 0, 1, 0, 1);
                table.Attach(pointerSpinButton, 1, 2, 0, 1);
                table.Attach(addPointedWarpButton, 2, 3, 0, 1);
                table.Attach(removePointedWarpButton, 3, 4, 0, 1);

                // Invoke handler
                valueChangedHandler(pointerSpinButton, null);

                Frame frame = new Frame(warpSourceData.PointerString);
                frame.Add(table);

                hbox.Add(frame);
            }
            else { // Not pointerWarp
                sourceEditor.AddDataModifiedHandler(delegate(object sender, EventArgs e) {
                        SetDestIndex(warpSourceData.DestGroup, warpSourceData.DestIndex);
                        });
            }

            valueEditorContainer.Add(hbox);
            valueEditorContainer.ShowAll();
        }

        void SetDestIndex(int group, int index) {
            destGroupSpinButton.Value = group;
            destIndexSpinButton.Value = index;

            destEditorContainer.Remove(destEditorContainer.Child);

            if (group != -1 && group < Project.GetNumGroups())
                destGroup = Project.GetIndexedDataType<WarpDestGroup>(group);

            if (group == -1 || group >= Project.GetNumGroups() ||
                    index == -1 || index >= destGroup.GetNumWarpDests())
            {
                frame2.Hide();
                return;
            }

            WarpDestData destData = destGroup.GetWarpDest(index);
            ValueReferenceEditor editor = new ValueReferenceEditor(Project,destData);

            int numReferences = destData.GetNumReferences();
            if (numReferences == 2)
                destInfoLabel.Text = "Used by " + (numReferences-1) + " other source";
            else
                destInfoLabel.Text = "Used by " + (numReferences-1) + " other sources";

            destEditorContainer.Add(editor);
            destEditorContainer.ShowAll();
            frame2.ShowAll();
        }

        protected void OnAddWarpButtonClicked(object sender, EventArgs e)
        {
            WarpSourceData data = new WarpSourceData(Project,
                    WarpSourceData.WarpCommands[(int)WarpSourceType.StandardWarp],
                    WarpSourceData.DefaultValues[(int)WarpSourceType.StandardWarp],
                    sourceGroup.FileParser,
                    new List<int>{-1});
            data.Map = map;
            data.Transition = 4;

            sourceGroup.AddWarpSourceData(data);

            SetWarpIndex((int)indexSpinButton.Adjustment.Upper+1);
        }

        protected void OnAddSpecificWarpButtonClicked(object sender, EventArgs e)
        {
            // Check if a pointer warp already exists
            List<WarpSourceData> warps = sourceGroup.GetMapWarpSourceData(map);

            foreach (WarpSourceData sourceData in warps) {
                if (sourceData.WarpSourceType == WarpSourceType.PointerWarp) {
                    // Warning dialog
                    Gtk.MessageDialog d = new Gtk.MessageDialog(null,
                            DialogFlags.DestroyWithParent,
                            MessageType.Warning,
                            ButtonsType.YesNo,
                            "This map already has specific-position warp data; adding another would be redundant. Continue anyway?");
                    Gtk.ResponseType response = (ResponseType)d.Run();
                    d.Destroy();

                    if (response == ResponseType.Yes)
                        break;
                    else
                        return;
                }
            }

            WarpSourceData data = new WarpSourceData(Project,
                    WarpSourceData.WarpCommands[(int)WarpSourceType.PointerWarp],
                    WarpSourceData.DefaultValues[(int)WarpSourceType.PointerWarp],
                    sourceGroup.FileParser,
                    new List<int>{-1,2});
            data.Map = map;
            data.Transition = 4;

            sourceGroup.AddWarpSourceData(data);

            SetWarpIndex((int)indexSpinButton.Adjustment.Upper+1);
        }

        protected void OnRemoveWarpButtonClicked(object sender, EventArgs e)
        {
            List<WarpSourceData> dataList = sourceGroup.GetMapWarpSourceData(map);
            sourceGroup.RemoveWarpSourceData(dataList[indexSpinButton.ValueAsInt]);
            SetWarpIndex(indexSpinButton.ValueAsInt);
        }

        protected void OnOkButtonClicked(object sender, EventArgs e)
        {
            this.Destroy();
        }
    }
}
