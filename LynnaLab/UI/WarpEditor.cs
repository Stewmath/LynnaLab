using System;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class WarpEditor : Gtk.Bin
    {
        // Properties

        Project Project {get; set;}

        // Variables

        WarpSourceGroup warpGroup;
        WarpDestGroup destGroup;

        ValueReferenceEditor sourceEditor;
        ValueReferenceEditor destEditor;

        int map;

        // Constructor

        public WarpEditor(Project p) {
            Project = p;
            this.Build();

            indexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                SetWarpIndex(indexSpinButton.ValueAsInt);
            };
            destIndexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                SetDestIndex(destGroupSpinButton.ValueAsInt, destIndexSpinButton.ValueAsInt);
            };
            destGroupSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                SetDestIndex(destGroupSpinButton.ValueAsInt, destIndexSpinButton.ValueAsInt);
            };

            destGroupSpinButton.Adjustment.Upper = Project.GetNumGroups()-1;
        }

        public void SetMap(int group, int map) {
            warpGroup = Project.
                GetIndexedDataType<WarpSourceGroup>(group);

            this.map = map;

            indexSpinButton.Adjustment.Lower = -1;
            indexSpinButton.Adjustment.Upper = warpGroup.GetMapWarpSourceData(map).Count-1;

            frameLabel.Text = "Room " + ((group<<8)+map).ToString("X3") + " Warp Sources";
            SetWarpIndex(0);
        }

        // Load the i'th warp in the current map.
        void SetWarpIndex(int i) {
            if (i > indexSpinButton.Adjustment.Upper)
                i = (int)indexSpinButton.Adjustment.Upper;

            indexSpinButton.Value = i;

            valueEditorContainer.Remove(valueEditorContainer.Child);

            if (i == -1)
                return;

            Gtk.HBox hbox = new Gtk.HBox();

            WarpSourceData warpSourceData = warpGroup.GetMapWarpSourceData(map)[i];

            if (warpSourceData.WarpSourceType == WarpSourceType.StandardWarp)
                SetDestIndex(warpSourceData.Group, warpSourceData.DestIndex);

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
                pointerSpinButton.Adjustment.Lower = 0;
                pointerSpinButton.Adjustment.Upper = warpSourceData.GetPointedChainLength()-1;

                table.Attach(new Gtk.Label("Pointer index"), 0, 1, 0, 1);
                table.Attach(pointerSpinButton, 1, 2, 0, 1);

                EventHandler valueChangedHandler = delegate(object sender, EventArgs e) {
                    int index = pointerSpinButton.ValueAsInt;
                    WarpSourceData pointedData = warpSourceData.GetPointedWarp();

                    while (index > 0) {
                        pointedData = pointedData.GetNextWarp();
                        index--;
                    }

                    table.Remove(destEditor);

                    destEditor =
                        new ValueReferenceEditor(Project, pointedData);

                    destEditor.AddDataModifiedHandler(delegate(object sender2, EventArgs e2) {
                            SetDestIndex(pointedData.Group, pointedData.DestIndex);
                            });

                    table.Attach(destEditor, 0, 2, 1, 2);

                    SetDestIndex(pointedData.Group, pointedData.DestIndex);
                };

                pointerSpinButton.ValueChanged += valueChangedHandler;

                // Invoke handler
                valueChangedHandler(pointerSpinButton, null);

                Frame frame = new Frame(warpSourceData.PointerString);
                frame.Add(table);

                hbox.Add(frame);
            }
            else { // Not pointerWarp
                sourceEditor.AddDataModifiedHandler(delegate(object sender, EventArgs e) {
                        SetDestIndex(warpSourceData.Group, warpSourceData.DestIndex);
                        });
            }

            valueEditorContainer.Add(hbox);
            valueEditorContainer.ShowAll();
        }

        void SetDestIndex(int group, int index) {
            destGroupSpinButton.Value = group;
            destIndexSpinButton.Value = index;

            destGroup = Project.
                GetIndexedDataType<WarpDestGroup>(group);

            destIndexSpinButton.Adjustment.Lower = -1;
            destIndexSpinButton.Adjustment.Upper = destGroup.GetNumWarpDests()-1;

            destEditorContainer.Remove(destEditorContainer.Child);

            if (index == -1)
                return;

            Data destData = destGroup.GetWarpDest(index);
            ValueReferenceEditor editor = new ValueReferenceEditor(Project,destData);

            editor.AddDataModifiedHandler(delegate(object sender, EventArgs e) {
//                     destIndexLabel.Text = "Group " + 
                    });

            destEditorContainer.Add(editor);
            destEditorContainer.ShowAll();
        }
    }
}
