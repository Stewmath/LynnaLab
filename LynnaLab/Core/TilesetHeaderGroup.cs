using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLab {

	public class TilesetHeaderGroup {

        Project project;

        byte[] mappingsData = new byte[512];
        byte[] collisionsData = new byte[256];

        int index;

		public TilesetHeaderGroup(Project p, int i) {
            project = p;
            index = i;

            FileParser tableFile = project.GetFileWithLabel("tilesetHeaderGroupTable");
            Data pointerData = tableFile.GetData("tilesetHeaderGroupTable", index*2);
            string labelName = pointerData.Values[0];

            FileParser headerFile = project.GetFileWithLabel(labelName);
            TilesetHeaderData headerData = headerFile.GetData(labelName) as TilesetHeaderData;
            bool next = true;

            while (next) {
                if (headerData == null)
                    throw new Exception("Expected tileset header group " + index.ToString("X") + " to reference tileset header data (m_TilesetHeader)");

                FileStream file = headerData.ReferencedData;
                file.Position = 0;
                if (headerData.DestAddress == 0xdc00) {
                    // Mappings
                    file.Read(mappingsData, 0, 0x200);
                }
                else if (headerData.DestAddress == 0xdb00) {
                    // Collisions
                    file.Read(collisionsData, 0, 0x100);
                }

                if (headerData.ShouldHaveNext()) {
                    headerData = headerData.Next as TilesetHeaderData;
                    if (headerData != null)
                        next = true;
                }
                else
                    next = false;
            }
		}

        public byte[] GetMappingsData() {
            return mappingsData;
        }
        public byte[] GetCollisionsData() {
            return collisionsData;
        }
	}

}
