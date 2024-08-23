using System.Numerics;
using ImGuiNET;
using LynnaLib;

namespace LynnaLab
{
    public class RoomEditor : Widget
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
            minimap.SetMap(Project.GetWorldMap(0, 0));

            roomLayoutEditor.AddMouseAction(MouseButton.LeftClick,
                                            MouseModifier.Any | MouseModifier.Drag,
                                            GridAction.Callback,
            (_, args) =>
            {
                int x = args.selectedIndex % roomLayoutEditor.Width;
                int y = args.selectedIndex / roomLayoutEditor.Width;
                roomLayoutEditor.RoomLayout.SetTile(x, y, tilesetViewer.SelectedIndex);
            });
            roomLayoutEditor.AddMouseAction(MouseButton.RightClick,
                                            MouseModifier.Any,
                                            GridAction.Callback,
            (_, args) =>
            {
                int x = args.selectedIndex % roomLayoutEditor.Width;
                int y = args.selectedIndex / roomLayoutEditor.Width;
                int tile = roomLayoutEditor.RoomLayout.GetTile(x, y);
                tilesetViewer.SelectedIndex = tile;
            });
        }

        // ================================================================================
        // Variables
        // ================================================================================
        RoomLayoutEditor roomLayoutEditor;
        TilesetViewer tilesetViewer;
        Minimap minimap;

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

        public override void Render()
        {
            ImGui.BeginChild("Tileset", new Vector2(tilesetViewer.CanvasWidth, 0.0f));
            ImGui.SeparatorText("Tileset");
            tilesetViewer.Render();
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("Room", new Vector2(roomLayoutEditor.CanvasWidth, 0.0f));
            ImGui.SeparatorText("Room");
            roomLayoutEditor.Render();
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("Minimap");
            ImGui.SeparatorText("Minimap");
            ImGui.BeginChild("MinimapChild", Vector2.Zero, 0, ImGuiWindowFlags.HorizontalScrollbar);
            minimap.Render();
            ImGui.EndChild();
            ImGui.EndChild();
        }

        /// <summary>
        /// Changes the loaded room, updates minimap & tileset viewer
        /// </summary>
        public void SetRoom(Room room)
        {
            if (Room == room)
                return;

            roomLayoutEditor.SetRoomLayout(room.GetLayout(Season));
            RoomLayoutChanged();
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Called when roomLayoutEditor has changed to reference a new RoomLayout
        /// </summary>
        void RoomLayoutChanged()
        {
            minimap.SelectedIndex = Room.Index & 0xff;
            tilesetViewer.SetTileset(RoomLayout.Tileset);
        }
    }
}
