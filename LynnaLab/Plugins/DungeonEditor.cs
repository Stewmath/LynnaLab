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

        Minimap minimap = null;
        SpinButton dungeonSpinButton, floorSpinButton;
        SpinButtonHexadecimal roomSpinButton;

        Box dungeonVreContainer, roomVreContainer;
        ValueReferenceEditor dungeonVre, roomVre;

        Project Project {
            get {
                return manager.Project;
            }
        }

        public override String Name { get { return "Dungeon Editor"; } }
        public override String Tooltip { get { return "Edit dungeon layout and minimap"; } }
        public override bool IsDockable { get { return false; } }
        public override string Category { get { return "Window"; } }

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

            dungeonVreContainer = new Gtk.VBox();
            roomVreContainer = new Gtk.VBox();
            dungeonVre = null;
            roomVre = null;

            Alignment frame = new Alignment(0,0,0,0);
            dungeonSpinButton = new SpinButton(0,15,1);
            floorSpinButton = new SpinButton(0,15,1);
            roomSpinButton = new SpinButtonHexadecimal(0,255,1);
            roomSpinButton.Digits = 2;

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

            var addFloorAboveButton = new Button("Add Floor Above");
            addFloorAboveButton.Image = new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button);
            addFloorAboveButton.Clicked += (a,b) => {
                int floorIndex = floorSpinButton.ValueAsInt + 1;
                (minimap.Map as Dungeon).InsertFloor(floorIndex);
                DungeonChanged();
                floorSpinButton.Value = floorIndex;
            };
            tmpAlign = new Gtk.Alignment(0.5f,0,0,0);
            tmpAlign.Add(addFloorAboveButton);
            tmpBox.Add(tmpAlign);

            var addFloorBelowButton = new Button("Add Floor Below");
            addFloorBelowButton.Image = new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button);
            addFloorBelowButton.Clicked += (a,b) => {
                int floorIndex = floorSpinButton.ValueAsInt;
                (minimap.Map as Dungeon).InsertFloor(floorIndex);
                DungeonChanged();
            };
            tmpAlign = new Gtk.Alignment(0.5f,0,0,0);
            tmpAlign.Add(addFloorBelowButton);
            tmpBox.Add(tmpAlign);

            var removeFloorButton = new Button("Remove Floor");
            removeFloorButton.Image = new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button);
            removeFloorButton.Clicked += (a,b) => {
                Dungeon dungeon = minimap.Map as Dungeon;

                if (dungeon.NumFloors <= 1)
                    return;

                Gtk.MessageDialog d = new MessageDialog(null,
                        DialogFlags.DestroyWithParent,
                        MessageType.Warning,
                        ButtonsType.YesNo,
                        "Really delete this floor?");
                var response = (ResponseType)d.Run();
                d.Dispose();

                if (response == Gtk.ResponseType.Yes) {
                    dungeon.RemoveFloor(floorSpinButton.ValueAsInt);
                    DungeonChanged();
                }
            };
            tmpAlign = new Gtk.Alignment(0.5f,0,0,0);
            tmpAlign.Add(removeFloorButton);
            tmpBox.Add(tmpAlign);

            hbox.Add(tmpBox);

            // Middle column (minimap)

            minimap = new Minimap();
            minimap.AddTileSelectedHandler((sender, index) => {
                RoomChanged();
            });

            hbox.Add(minimap);

            // Rightmost column

            tmpAlign = new Alignment(0,0,0,0);
            tmpAlign.Add(roomVreContainer);

            tmpBox2 = new HBox();
            tmpBox2.Add(new Gtk.Label("Room "));
            roomSpinButton.ValueChanged += (a,b) => {
                (minimap.Map as Dungeon).SetRoom(minimap.SelectedX, minimap.SelectedY,
                        minimap.Floor, roomSpinButton.ValueAsInt);
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

        void DungeonChanged() {
            Dungeon dungeon = Project.GetIndexedDataType<Dungeon>(dungeonSpinButton.ValueAsInt);

            floorSpinButton.Adjustment.Upper = dungeon.NumFloors-1;
            if (floorSpinButton.ValueAsInt >= dungeon.NumFloors)
                floorSpinButton.Value = dungeon.NumFloors-1;

            var vrs = new List<ValueReference>();
            Data data = dungeon.DataStart;
            vrs.Add(new DataValueReference(data, "Group", 0, DataValueType.String, editable:false));
            vrs.Add(new DataValueReference(data, "Wallmaster dest room", 1, DataValueType.Byte));
            vrs.Add(new DataValueReference(data, "# of floors", 3, DataValueType.Byte, editable:false));
            vrs.Add(new DataValueReference(data, "Base floor name", 4, DataValueType.Byte));
            vrs.Add(new DataValueReference(data, "Floors unlocked with compass", 5, DataValueType.Byte));

            var vrg = new ValueReferenceGroup(vrs);

            // Replace the "group" option with a custom widget for finer
            // control.
            SpinButton groupSpinButton = new SpinButton(4,5,1);
            groupSpinButton.Value = dungeon.MainGroup;
            groupSpinButton.ValueChanged += (c,d) => {
                vrg.SetValue("Group", ">wGroup" + groupSpinButton.ValueAsInt + "Flags");
            };


            if (dungeonVre != null)
                dungeonVre.ReplaceValueReferenceGroup(vrg);
            else {
                dungeonVre = new ValueReferenceEditor(Project, vrg, "Base Data");
                dungeonVre.ReplaceWidget("Group", groupSpinButton); // TODO
                dungeonVre.ShowAll();

                // Tooltips
                dungeonVre.SetTooltip(0, "Also known as the high byte of the room index.");
                dungeonVre.SetTooltip(1, "The low byte of the room index wallmasters will send you to.");
                dungeonVre.SetTooltip(3, "Determines what the game will call the bottom floor. For a value of:\n$00: The bottom floor is 'B3'.\n$01: The bottom floor is 'B2'.\n$02: The bottom floor is 'B1'.\n$03: The bottom floor is 'F1'.");
                dungeonVre.SetTooltip(4, "A bitset of floors that will appear on the map when the compass is obtained.\n\nEg. If this is $05, then floors 0 and 2 will be unlocked (bits 0 and 2 are set).");

                dungeonVreContainer.Add(dungeonVre);
            }

            minimap.SetMap(dungeon);
            minimap.Floor = floorSpinButton.ValueAsInt;

            RoomChanged();
        }

        void RoomChanged() {
            Dungeon dungeon = minimap.Map as Dungeon;
            Room room = minimap.GetRoom();

            roomSpinButton.Value = room.Index&0xff;

            // This could go in the constructor I guess
            var vrs = new ValueReference[] {
                new AbstractBoolValueReference(Project,
                        name: "Up",
                        getter: () => minimap.GetRoom().DungeonFlagUp,
                        setter: (v) => minimap.GetRoom().DungeonFlagUp = v),
                new AbstractBoolValueReference(Project,
                        name: "Right",
                        getter: () => minimap.GetRoom().DungeonFlagRight,
                        setter: (v) => minimap.GetRoom().DungeonFlagRight = v),
                new AbstractBoolValueReference(Project,
                        name: "Down",
                        getter: () => minimap.GetRoom().DungeonFlagDown,
                        setter: (v) => minimap.GetRoom().DungeonFlagDown = v),
                new AbstractBoolValueReference(Project,
                        name: "Left",
                        getter: () => minimap.GetRoom().DungeonFlagLeft,
                        setter: (v) => minimap.GetRoom().DungeonFlagLeft = v),
                new AbstractBoolValueReference(Project,
                        name: "Key",
                        getter: () => minimap.GetRoom().DungeonFlagKey,
                        setter: (v) => minimap.GetRoom().DungeonFlagKey = v),
                new AbstractBoolValueReference(Project,
                        name: "Chest",
                        getter: () => minimap.GetRoom().DungeonFlagChest,
                        setter: (v) => minimap.GetRoom().DungeonFlagChest = v),
                new AbstractBoolValueReference(Project,
                        name: "Boss",
                        getter: () => minimap.GetRoom().DungeonFlagBoss,
                        setter: (v) => minimap.GetRoom().DungeonFlagBoss = v),
                new AbstractBoolValueReference(Project,
                        name: "Dark",
                        getter: () => minimap.GetRoom().DungeonFlagDark,
                        setter: (v) => minimap.GetRoom().DungeonFlagDark = v),
            };

            var vrg = new ValueReferenceGroup(vrs);

            if (roomVre != null)
                roomVre.ReplaceValueReferenceGroup(vrg);
            else {
                roomVre = new ValueReferenceEditor(Project, vrg, 4, "Minimap Data");
                roomVreContainer.Add(roomVre);
            }
        }
    }
}
