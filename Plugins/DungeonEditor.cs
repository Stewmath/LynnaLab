using System;
using System.Collections.Generic;
using System.IO;
using Gtk;
using LynnaLab;

namespace Plugins
{
    public class DungeonEditor : Plugin
    {
        PluginManager manager;

        Project Project {
            get {
                return manager.Project;
            }
        }

        public override String Name {
            get {
                return "Dungeon Editor";
            }
        }
        public override String Tooltip {
            get {
                return "Edit dungeon layout and minimap";
            }
        }
        public override bool IsDockable {
            get {
                return false;
            }
        }

        public override void Init(PluginManager manager) {
            this.manager = manager;
        }
        public override void Exit() {
        }

        public override void Clicked() {
            Box tmpBox, tmpBox2;
            Alignment tmpAlign;
            Box vbox = new Gtk.VBox();
            vbox.Spacing = 3;
            Box hbox = new Gtk.HBox();
            hbox.Spacing = 3;

            Box dungeonVreContainer = new Gtk.VBox();
            Box roomVreContainer = new Gtk.VBox();
            ValueReferenceEditor dungeonVre = null;
            ValueReferenceEditor roomVre = null;

            Alignment frame = new Alignment(0,0,0,0);
            var dungeonSpinButton = new SpinButton(0,15,1);
            var floorSpinButton = new SpinButton(0,15,1);
            var roomSpinButton = new SpinButtonHexadecimal(0,255,1);
            roomSpinButton.Digits = 2;
            Minimap minimap = null;

            System.Action RoomChanged = () => {
                Dungeon dungeon = minimap.Map as Dungeon;
                Room room = minimap.GetRoom();

                roomSpinButton.Value = room.Index&0xff;

                var vrs = new List<ValueReference>();
                vrs.Add(new StreamValueReference("Up", room.Index&0xff, 0,0, DataValueType.ByteBit));
                vrs.Add(new StreamValueReference("Right", room.Index&0xff, 1,1, DataValueType.ByteBit));
                vrs.Add(new StreamValueReference("Down", room.Index&0xff, 2,2, DataValueType.ByteBit));
                vrs.Add(new StreamValueReference("Left", room.Index&0xff, 3,3, DataValueType.ByteBit));
                vrs.Add(new StreamValueReference("Key", room.Index&0xff, 4,4, DataValueType.ByteBit));
                vrs.Add(new StreamValueReference("Chest", room.Index&0xff, 5,5, DataValueType.ByteBit));
                vrs.Add(new StreamValueReference("Boss", room.Index&0xff, 6,6, DataValueType.ByteBit));
                vrs.Add(new StreamValueReference("Dark", room.Index&0xff, 7,7, DataValueType.ByteBit));

                Stream stream = Project.GetBinaryFile("rooms/" + Project.GameString + "/group" + dungeon.Group + "DungeonProperties.bin");
                foreach (StreamValueReference r in vrs)
                    r.SetStream(stream);

                if (roomVre != null)
                    roomVreContainer.Remove(roomVre);

                var vrg = new ValueReferenceGroup(vrs);
                roomVre = new ValueReferenceEditor(Project, vrg, 4, "Minimap Data");

                roomVreContainer.Add(roomVre);
            };

            System.Action DungeonChanged = () => {
                Dungeon dungeon = Project.GetIndexedDataType<Dungeon>(dungeonSpinButton.ValueAsInt);

                floorSpinButton.Adjustment.Upper = dungeon.NumFloors-1;
                if (floorSpinButton.ValueAsInt >= dungeon.NumFloors)
                    floorSpinButton.Value = dungeon.NumFloors-1;

                var vrs = new List<ValueReference>();
                vrs.Add(new ValueReference("Group", 0, DataValueType.String, false));
                vrs.Add(new ValueReference("Wallmaster dest room", 0, DataValueType.Byte));
                vrs.Add(new ValueReference("Bottom floor layout", 0, DataValueType.Byte, false));
                vrs.Add(new ValueReference("# of floors", 0, DataValueType.Byte, false));
                vrs.Add(new ValueReference("Base floor name", 0, DataValueType.Byte));
                vrs.Add(new ValueReference("Floors unlocked with compass", 0, DataValueType.Byte));

                Data data = dungeon.DataStart;
                foreach (ValueReference r in vrs) {
                    r.SetData(data);
                    data = data.NextData;
                }

                // Remove last ValueReferenceEditor
                if (dungeonVre != null)
                    dungeonVreContainer.Remove(dungeonVre);

                var vrg = new ValueReferenceGroup(vrs);
                dungeonVre = new ValueReferenceEditor(Project, vrg, "Base Data");

                dungeonVre.AddDataModifiedHandler(() => {
                            floorSpinButton.Adjustment.Upper = dungeon.NumFloors;
                            minimap.GenerateImage();
                            RoomChanged();
                        });

                // Replace the "group" option with a custom widget for finer
                // control.
                SpinButton groupSpinButton = new SpinButton(4,5,1);
                groupSpinButton.Value = dungeon.Group;
                groupSpinButton.ValueChanged += (c,d) => {
                    vrg.SetValue("Group", ">wGroup" + groupSpinButton.ValueAsInt + "Flags");
                };
                dungeonVre.ReplaceWidget(0, groupSpinButton);
                dungeonVre.ShowAll();

                // Tooltips
                dungeonVre.SetTooltip(0, "Also known as the high byte of the room index.");
                dungeonVre.SetTooltip(1, "The low byte of the room index wallmasters will send you to.");
                dungeonVre.SetTooltip(2, "The index of the layout for the bottom floor. Subsequent floors will use subsequent indices.");
                dungeonVre.SetTooltip(4, "Determines what the game will call the bottom floor. For a value of:\n$00: The bottom floor is 'B3'.\n$01: The bottom floor is 'B2'.\n$02: The bottom floor is 'B1'.\n$03: The bottom floor is 'F1'.");
                dungeonVre.SetTooltip(5, "A bitset of floors that will appear on the map when the compass is obtained.\n\nEg. If this is $05, then floors 0 and 2 will be unlocked (bits 0 and 2 are set).");

                dungeonVreContainer.Add(dungeonVre);
                minimap.SetMap(dungeon);
                minimap.Floor = floorSpinButton.ValueAsInt;

                RoomChanged();
            };

            dungeonSpinButton.ValueChanged += (a,b) => { DungeonChanged(); };
            floorSpinButton.ValueChanged += (a,b) => { DungeonChanged(); };

            frame.Add(vbox);

            tmpBox = new Gtk.HBox();
            tmpBox.Add(new Gtk.Label("Dungeon "));
            tmpBox.Add(dungeonSpinButton);
            tmpBox.Add(new Gtk.Label("Floor "));
            tmpBox.Add(floorSpinButton);
            tmpAlign = new Alignment(0,0,0,0);
            tmpAlign.Add(tmpBox);

            vbox.Add(tmpAlign);
            vbox.Add(hbox);

            // Leftmost column

            tmpBox = new VBox();
            tmpBox.Add(dungeonVreContainer);

            var addFloorButton = new Button("Add Floor");
            addFloorButton.Image = new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button);
            addFloorButton.Clicked += (a,b) => {
                Dungeon dungeon = minimap.Map as Dungeon;

                int newFloorIndex = dungeon.FirstLayoutIndex + dungeon.NumFloors;

                // Shift all subsequent layouts 64 bytes down in the data file
                Stream layoutFile = Project.GetBinaryFile("rooms/" + Project.GameString + "/dungeonLayouts.bin");
                layoutFile.SetLength(layoutFile.Length+64);
                for (int i=(int)layoutFile.Length/64-1; i>newFloorIndex; i--) {
                    var buf = new byte[64];
                    layoutFile.Position = (i-1)*64;
                    layoutFile.Read(buf, 0, 64);
                    layoutFile.Write(buf, 0, 64);
                }

                // Clear the new floor
                layoutFile.Position = newFloorIndex*64;
                for (int j=0;j<64;j++)
                    layoutFile.WriteByte(0);

                // Shift each dungeon's "FirstLayoutIndex" to match the shifted layouts.
                for (int i=0; i<Project.GetNumDungeons(); i++) {
                    Dungeon d2 = Project.GetIndexedDataType<Dungeon>(i);
                    if (d2.FirstLayoutIndex >= newFloorIndex)
                        d2.FirstLayoutIndex++;
                }

                dungeon.NumFloors = dungeon.NumFloors+1;
                floorSpinButton.Value = dungeon.NumFloors-1;
                DungeonChanged();
            };
            tmpAlign = new Gtk.Alignment(0.5f,0,0,0);
            tmpAlign.Add(addFloorButton);
            tmpBox.Add(tmpAlign);

            var removeFloorButton = new Button("Remove Top Floor");
            removeFloorButton.Image = new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button);
            removeFloorButton.Clicked += (a,b) => {
                Dungeon dungeon = minimap.Map as Dungeon;

                if (dungeon.NumFloors <= 1)
                    return;

                Gtk.MessageDialog d = new MessageDialog(null,
                        DialogFlags.DestroyWithParent,
                        MessageType.Warning,
                        ButtonsType.YesNo,
                        "Are you quite certain that you wish to delete the top floor of this dungeon?");
                var response = (ResponseType)d.Run();
                d.Destroy();

                if (response == Gtk.ResponseType.Yes) {
                    int deletedFloorIndex = dungeon.FirstLayoutIndex + dungeon.NumFloors-1;

                    // Shift all subsequent layouts 64 bytes up in the data file
                    Stream layoutFile = Project.GetBinaryFile("rooms/" + Project.GameString + "/dungeonLayouts.bin");
                    for (int i=deletedFloorIndex; i<layoutFile.Length/64-1; i++) {
                        var buf = new byte[64];
                        layoutFile.Position = (i+1)*64;
                        layoutFile.Read(buf, 0, 64);
                        layoutFile.Position = i*64;
                        layoutFile.Write(buf, 0, 64);
                    }

                    layoutFile.SetLength(layoutFile.Length-64);

                    // Shift each dungeon's "FirstLayoutIndex" to match the shifted layouts.
                    for (int i=0; i<Project.GetNumDungeons(); i++) {
                        Dungeon d2 = Project.GetIndexedDataType<Dungeon>(i);
                        if (d2.FirstLayoutIndex > deletedFloorIndex)
                            d2.FirstLayoutIndex--;
                    }

                    dungeon.NumFloors = dungeon.NumFloors-1;

                    DungeonChanged();
                }
            };
            tmpAlign = new Gtk.Alignment(0.5f,0,0,0);
            tmpAlign.Add(removeFloorButton);
            tmpBox.Add(tmpAlign);

            hbox.Add(tmpBox);

            // Middle column (minimap)

            minimap = new Minimap();
            minimap.TileSelectedEvent += (sender) => {
                RoomChanged();
            };

            hbox.Add(minimap);

            // Rightmost column

            tmpAlign = new Alignment(0,0,0,0);
            tmpAlign.Add(roomVreContainer);

            tmpBox2 = new HBox();
            tmpBox2.Add(new Gtk.Label("Room "));
            roomSpinButton.ValueChanged += (a,b) => {
                (minimap.Map as Dungeon).SetRoom(minimap.SelectedX, minimap.SelectedY,
                        minimap.Floor, roomSpinButton.ValueAsInt);
                minimap.GenerateImage();
                RoomChanged();
            };
            tmpBox2.Add(roomSpinButton);

            tmpBox = new VBox();
            tmpBox.Add(tmpBox2);
            tmpBox.Add(tmpAlign);

            hbox.Add(tmpBox);



            Window w = new Window(null);
            w.Add(frame);
            w.ShowAll();

            Map map = manager.GetActiveMap();
            if (map is Dungeon)
                dungeonSpinButton.Value = map.Index;

            DungeonChanged();
        }
    }
}
