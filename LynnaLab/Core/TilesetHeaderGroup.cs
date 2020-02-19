using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab {

    // This class is only used when "ExpandedTilesets" is false in config.yaml.
    public class TilesetHeaderGroup : ProjectIndexedDataType {

        Stream mappingsDataFile, collisionsDataFile;

        internal TilesetHeaderGroup(Project p, int i) : base(p, i)
        {
            FileParser tableFile = Project.GetFileWithLabel("tilesetHeaderGroupTable");
            Data pointerData = tableFile.GetData("tilesetHeaderGroupTable", Index*2);
            string labelName = pointerData.GetValue(0);

            FileParser headerFile = Project.GetFileWithLabel(labelName);
            TilesetHeaderData headerData = headerFile.GetData(labelName) as TilesetHeaderData;
            bool next = true;

            while (next) {
                if (headerData == null)
                    throw new Exception("Expected tileset header group " + Index.ToString("X") + " to reference tileset header data (m_TilesetHeader)");

                Stream dataFile = headerData.ReferencedData;
                dataFile.Position = 0;
                if (headerData.DestAddress == Project.EvalToInt("w3TileMappingIndices")) {
                    // Mappings
                    mappingsDataFile = dataFile;
                }
                else if (headerData.DestAddress == Project.EvalToInt("w3TileCollisions")) {
                    // Collisions
                    collisionsDataFile = dataFile;
                }

                if (headerData.ShouldHaveNext()) {
                    headerData = headerData.NextData as TilesetHeaderData;
                    if (headerData != null)
                        next = true;
                }
                else
                    next = false;
            }
        }

        public byte GetMappingsData(int i) {
            if (mappingsDataFile == null)
                throw new Exception("Tileset header group 0x" + Index.ToString("X") + " does not reference mapping data.");
            mappingsDataFile.Seek(i, SeekOrigin.Begin);
            return (byte)mappingsDataFile.ReadByte();
        }
        public byte GetCollisionsData(int i) {
            if (collisionsDataFile == null)
                throw new Exception("Tileset header group 0x" + Index.ToString("X") + " does not reference collision data.");
            collisionsDataFile.Seek(i, SeekOrigin.Begin);
            return (byte)collisionsDataFile.ReadByte();
        }

        public void SetMappingsData(int i, byte value) {
            if (mappingsDataFile == null)
                throw new Exception("Tileset header group 0x" + Index.ToString("X") + " does not reference mapping data.");
            mappingsDataFile.Seek(i, SeekOrigin.Begin);
            mappingsDataFile.WriteByte(value);
        }
        public void SetCollisionsData(int i, byte value) {
            if (collisionsDataFile == null)
                throw new Exception("Tileset header group 0x" + Index.ToString("X") + " does not reference collision data.");
            collisionsDataFile.Seek(i, SeekOrigin.Begin);
            collisionsDataFile.WriteByte(value);
        }

        // No need for a save function, dataFiles are tracked elsewhere
    }
}
