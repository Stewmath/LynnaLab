using System.Numerics;
using ImGuiNET;
using LynnaLib;

namespace LynnaLab
{
    public class RoomEditor
    {
        // ================================================================================
        // Constructors
        // ================================================================================

        /// <summary>
        /// Assumes that the TopLevel has a valid Project loaded.
        /// </summary>
        public RoomEditor(TopLevel topLevel)
        {
            this.TopLevel = topLevel;

            roomLayoutEditor = new RoomLayoutEditor(topLevel);
            roomLayoutEditor.SetRoomLayout(Project.GetIndexedDataType<Room>(0x100).GetLayout(-1));

            tilesetViewer = new TilesetViewer(topLevel);
            tilesetViewer.SetTileset(roomLayoutEditor.Room.GetTileset(-1));

            minimap = new Minimap(topLevel);

            SetRoom(0);

            roomLayoutEditor.AddMouseAction(
                MouseButton.LeftClick,
                MouseModifier.Any | MouseModifier.Drag,
                GridAction.Callback,
                (_, args) =>
                {
                    int x = args.selectedIndex % roomLayoutEditor.Width;
                    int y = args.selectedIndex / roomLayoutEditor.Width;
                    roomLayoutEditor.RoomLayout.SetTile(x, y, tilesetViewer.SelectedIndex);
                });
            roomLayoutEditor.AddMouseAction(
                MouseButton.RightClick,
                MouseModifier.Any,
                GridAction.Callback,
                (_, args) =>
                {
                    int x = args.selectedIndex % roomLayoutEditor.Width;
                    int y = args.selectedIndex / roomLayoutEditor.Width;
                    int tile = roomLayoutEditor.RoomLayout.GetTile(x, y);
                    tilesetViewer.SelectedIndex = tile;
                });

            minimap.SelectedEvent += (selectedIndex) =>
            {
                SetRoomLayout(minimap.Map.GetRoomLayout(minimap.SelectedX, minimap.SelectedY, Season));
            };
        }

        // ================================================================================
        // Variables
        // ================================================================================
        RoomLayoutEditor roomLayoutEditor;
        TilesetViewer tilesetViewer;
        Minimap minimap;
        int suppressEvents = 0;

        // ================================================================================
        // Properties
        // ================================================================================

        public TopLevel TopLevel { get; private set; }
        public Project Project { get { return TopLevel.Project; } }

        public RoomLayout RoomLayout { get { return roomLayoutEditor.RoomLayout; } }
        public Room Room { get { return RoomLayout.Room; } }
        public int Season { get { return RoomLayout.Season; } }

        // ================================================================================
        // Public methods
        // ================================================================================

        public void Render()
        {
            const float OFFSET = 15.0f;

            ImGui.BeginChild("Left Panel", new Vector2(tilesetViewer.WidgetSize.X + OFFSET, 0.0f),
                             ImGuiChildFlags.Border);
            ImGui.SeparatorText("Tileset");
            tilesetViewer.Render();
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("Middle Panel", new Vector2(roomLayoutEditor.WidgetSize.X + OFFSET, 0.0f),
                             ImGuiChildFlags.Border);
            ImGui.SeparatorText("Room");
            roomLayoutEditor.Render();
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("Right Panel", Vector2.Zero, ImGuiChildFlags.Border);

            ImGui.SeparatorText("Overworld");

            {
                ImGui.PushItemWidth(150.0f);

                int worldIndex = Room.Group;
                if (Widget.InputHex("World", ref worldIndex, 1))
                {
                    if (worldIndex >= 0 && worldIndex < Project.NumGroups)
                    {
                        int s = -1;
                        if (worldIndex == 0)
                            s = 0;
                        SetMap(Project.GetWorldMap(worldIndex, s));
                    }
                }

                ImGui.SameLine();
                int roomIndex = Room.Index;
                if (Widget.InputHex("Room", ref roomIndex, 3))
                {
                    if (roomIndex >= 0 && roomIndex <= Project.NumRooms)
                        SetRoom(roomIndex);
                }

                ImGui.SameLine();
                int season = RoomLayout.Season;
                if (Widget.InputHex("Season", ref season, 1))
                {
                    if (Room.IsValidSeason(season))
                        SetRoomLayout(Room.GetLayout(season));
                }

                ImGui.PopItemWidth();
            }

            ImGui.SeparatorText("Minimap");
            minimap.Render();

            ImGui.EndChild();
        }

        public void SetRoom(int roomIndex)
        {
            if (roomIndex == Room.Index)
                return;
            SetRoom(Project.GetIndexedDataType<Room>(roomIndex));
        }

        /// <summary>
        /// Changes the loaded room, updates minimap & tileset viewer
        /// </summary>
        public void SetRoom(Room room)
        {
            if (Room == room)
                return;
            SetRoomLayout(room.GetLayout(Season));
        }

        /// <summary>
        /// Changes the loaded map, updates the loaded room accordingly
        /// </summary>
        public void SetMap(Map map)
        {
            RoomLayout roomLayout = map.GetRoomLayout(minimap.SelectedX, minimap.SelectedY, 0);

            suppressEvents++;
            minimap.SetMap(map);
            SetRoomLayout(roomLayout);
            suppressEvents--;
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Changes the RoomLayout, updates all the viewers.
        /// </summary>
        void SetRoomLayout(RoomLayout roomLayout)
        {
            suppressEvents++;

            roomLayoutEditor.SetRoomLayout(roomLayout);
            tilesetViewer.SetTileset(roomLayout.Tileset);
            minimap.SetMap(Project.GetWorldMap(roomLayout.Group, roomLayout.Season));
            minimap.SelectedIndex = roomLayout.Room.Index & 0xff;

            suppressEvents--;
        }
    }
}
