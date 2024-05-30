using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using Util;

namespace LynnaLib
{
    // Invoked when something changes the layout of rooms in a dungeon.
    public class DungeonRoomChangedEventArgs {
        public bool all; // True if all rooms must be updated

        public int x, y, floor; // Otherwise, this is the position in grid of changed room
    }

    /// Represents a dungeon, which is really just an organized layout of rooms. Some "dungeons" are
    /// just collections of miscellaneous large rooms (like Ambi's Palace).
    ///
    /// This class assumes that each dungeon uniquely controls its own dungeon layout data. This is
    /// technically not true in vanilla Ages, as dungeons 0 and 9 reference the same layout data
    /// (maku path). This has been changed in the hack-base branch (fresh duplicate data created).
    ///
    /// The above assumption could also break down if there is overlap between dungeons in the
    /// "dungeonLayouts.s" file, but that should only occur if there is a misconfiguration.
    ///
    /// Utmost flexibility to overcome the above limitations would involve installing handlers on
    /// the underlying data itself, rather than doing things when the "SetRoom" function, etc. is
    /// called. That's annoying to do when floors can be added and deleted though.
    public class Dungeon : Map
    {
        // The start of the data, at the "dungeonDataXX" label
        Data dataStart;

        // roomsUsed[i] = # of times room "i" is used in this dungeon
        int[] roomsUsed;


        internal Dungeon(Project p, int i) : base(p) {
            this.Index = i;

            FileParser dungeonDataFile = Project.GetFileWithLabel("dungeonDataTable");
            Data pointerData = dungeonDataFile.GetData("dungeonDataTable", Index*2);
            string label = pointerData.GetValue(0);
            dataStart = dungeonDataFile.GetData(label);

            DetermineRoomsUsed();
            GenerateValueReferenceGroup();
        }


        // Event invoked when a room number changes (not including things that trigger FloorChangedEvent)
        public event EventHandler<DungeonRoomChangedEventArgs> RoomChangedEvent;

        // Event invoked when a floor is added or removed
        public event EventHandler<EventArgs> FloorsChangedEvent;


        public ValueReferenceGroup ValueReferenceGroup { get; private set; }

        public int NumFloors {
            get {
                return GetDataIndex(3);
            }
            private set {
                SetDataIndex(3, value);
            }
        }

        // Map properties

        public override int MainGroup {
            get {
                return GetDataIndex(0)-Project.EvalToInt(">wGroup4Flags")+4;
            }
        }

        // I don't know why, but sidescrolling rooms use groups 6/7 instead of 4/5
        public int SidescrollGroup {
            get {
                return MainGroup + 2;
            }
        }

        public override int MapWidth {
            get {
                return 8;
            }
        }
        public override int MapHeight {
            get {
                return 8;
            }
        }
        public override int RoomWidth {
            get {
                return GetRoom(0,0).GetLayout(-1).Width;
            }
        }
        public override int RoomHeight {
            get {
                return GetRoom(0,0).GetLayout(-1).Height;
            }
        }
        public override int Season {
            get { return -1; }
        }

        // Other properties

        public int Index { get; private set; }


        public void SetRoom(int x, int y, int floor, int room) {
            int pos = y * 8 + x;
            if (pos >= 0x40)
                throw new ArgumentException(string.Format("Arguments {0:X},{1:X} to 'SetRoom' too high.", x, y));
            if (floor >= NumFloors)
                throw new ArgumentException(string.Format("Floor {0} too high.", floor));

            Data d = GetFloorLayoutData(floor).GetDataAtOffset(y * 8 + x);

            roomsUsed[d.GetIntValue(0)]--;
            d.SetByteValue(0, (byte)room);
            roomsUsed[(byte)room]++;

            RoomChangedEvent?.Invoke(this, new DungeonRoomChangedEventArgs {x = x, y = y, floor = floor});
        }

        public bool RoomUsed(int roomIndex) {
            int group = roomIndex >> 8;
            if (group != MainGroup && group != SidescrollGroup)
                return false;
            return roomsUsed[roomIndex & 0xff] > 0;
        }

        // Map methods

        public override Room GetRoom(int x, int y, int floor=0) {
            int pos = y * 8 + x;
            if (pos >= 0x40)
                throw new ArgumentException(string.Format("Arguments {0:X},{1:X} to 'GetRoom' too high.", x, y));
            if (floor >= NumFloors)
                throw new ArgumentException(string.Format("Floor {0} too high.", floor));

            int roomIndex = GetFloorLayoutData(floor).GetDataAtOffset(y * 8 + x).GetIntValue(0);
            Room room = Project.GetIndexedDataType<Room>(roomIndex + MainGroup*0x100);

            // Change the group if the room is sidescrolling. As a result, the group number of
            // sidescrolling rooms in a dungeon will be different from other rooms, which will look
            // weird, but that's the way it works apparently...
            if (room.GetTileset(-1).SidescrollFlag)
                room = Project.GetIndexedDataType<Room>(roomIndex + SidescrollGroup*0x100);

            return room;
        }

        public override bool GetRoomPosition(Room room, out int x, out int y, out int floor) {
            x = -1;
            y = -1;
            floor = -1;

            if (room.Group != MainGroup && room.Group != SidescrollGroup)
                return false;
            if (roomsUsed[room.Index & 0xff] == 0)
                return false;

            for (int f=0;f<NumFloors;f++) {
                for (int j=0;j<MapHeight;j++) {
                    for (int i=0;i<MapWidth;i++) {
                        if (GetRoom(i,j,f) == room) {
                            x = i;
                            y = j;
                            floor = f;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// Insert a floor below "floorIndex". If "floorIndex == NumFloors" then the floor is
        /// inserted at the top.
        public void InsertFloor(int floorIndex) {
            if (floorIndex < 0 || floorIndex > NumFloors)
                throw new ArgumentException("Can't insert floor " + floorIndex + ".");

            FileComponent component;
            string dungeonLayoutLabel = dataStart.GetValue(2);

            if (floorIndex == 0)
                component = Project.GetLabel(dungeonLayoutLabel);
            else
                component = Project.GetData(dungeonLayoutLabel, floorIndex * 64 - 1);

            // Insert the blank floor data
            List<string> textToInsert = new List<string>();
            for (int y=0; y<8; y++)
                textToInsert.Add("\t.db $00 $00 $00 $00 $00 $00 $00 $00");

            // Location to add extra newline differs if this is the first floor
            if (floorIndex == 0) {
                component.FileParser.InsertParseableTextAfter(component, new string[] {""});
                component.FileParser.InsertParseableTextAfter(component, textToInsert.ToArray());
            }
            else {
                component.FileParser.InsertParseableTextAfter(component, textToInsert.ToArray());
                component.FileParser.InsertParseableTextAfter(component, new string[] {""});
            }

            NumFloors++;
            DetermineRoomsUsed();

            FloorsChangedEvent?.Invoke(this, null);
        }

        public void RemoveFloor(int floorIndex) {
            if (floorIndex < 0 || floorIndex >= NumFloors)
                throw new ArgumentException("Can't remove floor " + floorIndex + ": doesn't exist.");
            if (NumFloors <= 1)
                throw new ArgumentException("Can't remove last remaining floor in a dungeon. (Or at least it's not a good idea.)");

            FileComponent component = GetFloorLayoutData(floorIndex);
            FileParser parser = component.FileParser;
            int count = 0;
            while (count < 64) {
                if (component is Data)
                    count++;
                FileComponent next = component.Next;
                parser.RemoveFileComponent(component);
                component = next;
            }
            // Remove ending newline, but don't want to accidentally remove data
            if (component is StringFileComponent)
                parser.RemoveFileComponent(component);

            NumFloors--;
            DetermineRoomsUsed();

            FloorsChangedEvent?.Invoke(this, null);
        }


        void GenerateValueReferenceGroup() {
            var groupValueReference = new AbstractIntValueReference(Project,
                    name: "Group",
                    getter: () => MainGroup,
                    setter: (v) => SetGroup(v),
                    minValue: 4,
                    maxValue: 5,
                    tooltip: "Also known as the high byte of the room index."
                    );
            // TODO: tie groupValueReference to the underlying data's modified event handler somehow

            var list = new ValueReference[] {
                groupValueReference,
                new DataValueReference(
                        data: dataStart,
                        name: "Wallmaster dest room",
                        index: 1,
                        type: DataValueType.Byte,
                        tooltip: "The low byte of the room index wallmasters will send you to."),
                new DataValueReference(
                        data: dataStart,
                        name: "Base floor name",
                        index: 4,
                        type: DataValueType.Byte,
                        tooltip: "Determines what the game will call the bottom floor. For a value of:\n$00: The bottom floor is 'B3'.\n$01: The bottom floor is 'B2'.\n$02: The bottom floor is 'B1'.\n$03: The bottom floor is 'F1'."),
                new DataValueReference(
                        data: dataStart,
                        name: "Floors unlocked with compass",
                        index: 5,
                        type: DataValueType.Byte,
                        tooltip: "A bitset of floors that will appear on the map when the compass is obtained.\n\nEg. If this is $05, then floors 0 and 2 will be unlocked (bits 0 and 2 are set).")
            };

            ValueReferenceGroup = new ValueReferenceGroup(list);
        }

        void DetermineRoomsUsed() {
            roomsUsed = new int[256];
            for (int f=0; f<NumFloors; f++) {
                for (int x=0; x<MapWidth; x++) {
                    for (int y=0; y<MapHeight; y++) {
                        roomsUsed[GetRoom(x, y, f).Index & 0xff]++;
                    }
                }
            }
        }

        int GetDataIndex(int i) {
            return dataStart.GetIntValue(i);
        }

        Data GetFloorLayoutData(int floor) {
            return Project.GetData(dataStart.GetValue(2), floor * 64);
        }

        void SetDataIndex(int i, int val) {
            dataStart.SetByteValue(i, (byte)val);
        }

        void SetGroup(int g) {
            if (!(g == 4 || g == 5))
                throw new ArgumentException("Invalid group '" + g + "' for dungeon.");
            dataStart.SetValue(0, ">wGroup" + g.ToString() + "Flags");

            RoomChangedEvent?.Invoke(this, new DungeonRoomChangedEventArgs {all = true});
        }
    }
}
