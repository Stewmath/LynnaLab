using System.IO;

namespace LynnaLib
{
    /// Represents a possible tileset/layout combination of a room. Currently, the only reason this
    /// is distinct from the "Room" class is to handle different seasons.
    public class RoomLayout
    {
        private static readonly log4net.ILog log = LogHelper.GetLogger();

        // TODO: Class is not entirely undo-proofed, but should work on hack-base.
        // Variables "width", "height", and "tileDataFile" should never change on the hack-base
        // branch due to the expanded tilesets patch, so it is not critical to undo-proof these.
        // Fully accounting for "layout groups" in order to support undos on the master branch would
        // be a fair bit more complicated.

        // Actual width and height of room (note that width can differ from Stride, see properties)
        int width, height;
        MemoryFileStream tileDataFile;

        // Tied to the ValueReference through an event handler - undo-proof
        Tileset loadedTileset;

        // List of positions where each tile index is used in the room
        List<(int,int)>[] tilePositions = new List<(int,int)>[256];


        // Event invoked when tileset used for this room is changed
        public event EventHandler<RoomTilesetChangedEventArgs> TilesetChangedEvent;
        // Event invoked when room layout is changed
        public event EventHandler<RoomLayoutChangedEventArgs> LayoutChangedEvent;


        internal RoomLayout(Room room, int season)
        {
            Room = room;
            Season = season;

            UpdateTileset();
        }


        public Room Room { get; private set; }
        public int Season { get; private set; }

        // Properties from "Room" class
        public Project Project { get { return Room.Project; } }
        public int Group { get { return Room.Group; } }

        public int Height
        {
            get { return height; }
        }
        public int Width
        {
            get { return width; }
        }
        public RealTileset Tileset
        {
            get
            {
                return Room.GetTileset(Season);
            }
        }
        public bool IsSmall
        {
            get { return width == 10; }
        }


        /// # of bytes per row (including unused bytes; this means it has a value of 16 for large
        /// rooms, which is 1 higher than the width, which is 15).
        int Stride
        {
            get { return width == 10 ? 10 : 16; }
        }

        public byte[] GetLayout()
        {
            tileDataFile.Position = 0;
            byte[] output = new byte[Stride * Height];
            tileDataFile.Read(output, 0, Stride * Height);
            return output;
        }

        public int GetTile(int x, int y)
        {
            tileDataFile.Position = y * Stride + x;
            return tileDataFile.ReadByte();
        }
        public void SetTile(int x, int y, int value)
        {
            Project.BeginTransaction($"Modify room layout#r{Room.Index:X3}s{Season}", true);

            int oldTile = GetTile(x, y);
            if (oldTile != value)
            {
                tilePositions[oldTile].Remove((x, y));
                tilePositions[value].Add((x, y));
                tileDataFile.Position = y * Stride + x;
                tileDataFile.WriteByte((byte)value);
                // Modifying the data will trigger the callback to the TileDataModified function
            }

            Project.UndoState.OnRewind("Modify room layout", () =>
            { // On undo
                CalculateTilePositions();
            }, (isRedo) =>
            { // On redo or right now
                if (isRedo)
                    CalculateTilePositions();
            });

            Project.EndTransaction();
        }

        /// <summary>
        /// Returns a list of positions where a given tile is located.
        /// </summary>
        public IList<(int, int)> GetTilePositions(int tileIndex)
        {
            return tilePositions[tileIndex].AsReadOnly();
        }


        /// <summary>
        /// Tied to the Room's ValueReference for the tileset assignment. This means that this
        /// should be invoked in all cases the tileset changes, including undo/redo.
        /// </summary>
        internal void UpdateTileset()
        {
            if (loadedTileset != Tileset)
            {
                var oldTileset = loadedTileset;

                if (loadedTileset != null)
                {
                    loadedTileset.LayoutGroupModifiedEvent -= ModifiedLayoutGroupCallback;
                }
                Tileset.LayoutGroupModifiedEvent += ModifiedLayoutGroupCallback;

                loadedTileset = Tileset;

                UpdateRoomData();

                TilesetChangedEvent?.Invoke(
                    this,
                    new RoomTilesetChangedEventArgs { oldTileset = oldTileset, newTileset = Tileset });
            }
        }


        // Private methods

        void CalculateTilePositions()
        {
            for (int t = 0; t < 256; t++)
            {
                tilePositions[t] = new List<(int, int)>();
            }
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int tile = GetTile(x, y);
                    tilePositions[tile].Add((x, y));
                }
            }
        }

        void UpdateRoomData()
        {
            if (tileDataFile != null)
            {
                tileDataFile.RemoveModifiedEventHandler(TileDataModified);
                tileDataFile = null;
            }


            int layoutGroup;
            if (Project.Config.ExpandedTilesets)
                layoutGroup = Project.GetCanonicalLayoutGroup(Group, Season);
            else
                layoutGroup = Tileset.LayoutGroup;

            // Get the tileDataFile
            string label = "room" + ((layoutGroup << 8) + (Room.Index & 0xff)).ToString("X4").ToLower();
            FileParser parserFile = Project.GetFileWithLabel(label);
            Data data = parserFile.GetData(label);
            if (data.CommandLowerCase != "m_roomlayoutdata")
            {
                throw new AssemblyErrorException("Expected label \"" + label + "\" to be followed by the m_RoomLayoutData macro.");
            }
            string roomString = data.GetValue(0) + ".bin";
            try
            {
                tileDataFile = Project.GetBinaryFile(
                        "rooms/" + Project.GameString + "/small/" + roomString);
            }
            catch (FileNotFoundException)
            {
                try
                {
                    tileDataFile = Project.GetBinaryFile(
                            "rooms/" + Project.GameString + "/large/" + roomString);
                }
                catch (FileNotFoundException)
                {
                    throw new AssemblyErrorException("Couldn't find \"" + roomString + "\" in \"rooms/small\" or \"rooms/large\".");
                }
            }

            tileDataFile.AddModifiedEventHandler(TileDataModified);

            if (tileDataFile.Length == 80)
            { // Small map
                width = 10;
                height = 8;
            }
            else if (tileDataFile.Length == 176)
            { // Large map
                width = 0xf;
                height = 0xb;
            }
            else
                throw new AssemblyErrorException("Size of file \"" + tileDataFile.Name + "\" was invalid!");

            CalculateTilePositions();
        }

        // Room layout modified
        void TileDataModified(object sender, MemoryFileStream.ModifiedEventArgs args)
        {
            LayoutChangedEvent?.Invoke(this, new RoomLayoutChangedEventArgs {});
        }

        void ModifiedLayoutGroupCallback()
        {
            UpdateRoomData();
        }
    }

    public struct RoomTilesetChangedEventArgs
    {
        public Tileset newTileset, oldTileset;
    }

    public struct RoomLayoutChangedEventArgs // stub in case we need this later
    {
    }
}
