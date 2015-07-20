using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
	public class GfxHeaderParser
	{
		List<string> gfxDirectories = new List<string>() {
			"gfx/",
			"gfx_compressible/"
		};

		struct GfxHeader {
			public int DestAddr, DestBank, Size;
			public bool Next;
			public FileStream GfxFile;
		}

		Project project;

		List<GfxHeader> gfxHeaderList = new List<GfxHeader>();

		byte[][] vramBuffer = new byte[2][] { new byte[0x2000], new byte[0x2000] };
		byte[][] wramBuffer = new byte[8][] { new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000], 
			new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000] };

		public GfxHeaderParser(Project project, string name)
		{
			this.project = project;
			AsmParser gfxPointerFile = project.GetFileWithLabel("gfxHeaderPointers");
			Data headerPointerData = gfxPointerFile.GetData("gfxHeaderPointers", project.EvalToInt(name));
			AsmParser gfxHeaderFile = project.GetFileWithLabel(headerPointerData.Values[0]);
			Data headerData = gfxHeaderFile.GetData(headerPointerData.Values[0]);

			bool next = true;
			while (next) {
				GfxHeader header = new GfxHeader();

				string filename = headerData.Values[0] + ".bin";
				header.DestAddr = project.EvalToInt(headerData.Values[1]);
				header.DestBank = header.DestAddr & 0x000f;
				header.DestAddr &= 0xfff0;
				header.Size = project.EvalToInt(headerData.Values[2]);
				header.Next = (header.Size & 0x80) == 0x80;
				header.Size &= 0x7f;
			
				header.GfxFile = null;
				foreach (string directory in gfxDirectories) {
					if (File.Exists(project.BaseDirectory + directory + filename)) {
						header.GfxFile = project.GetBinaryFile(directory + filename);
						break;
					}
				}
				if (header.GfxFile == null) {
					throw new Exception("Could not find graphics file " + filename);
				}

				header.GfxFile.Position = 0;

				if ((header.DestAddr & 0xe000) == 0x8000) {
					int bank = header.DestBank & 1;
					int dest = header.DestAddr & 0x1fff;
					header.GfxFile.Read(vramBuffer[bank], dest, 0x2000-dest);
				}
				else if ((header.DestAddr & 0xf000) == 0xd000) {
					int bank = header.DestBank & 7;
					int dest = header.DestAddr & 0x0fff;
					header.GfxFile.Read(wramBuffer[bank], dest, 0x1000-dest);
				}

				gfxHeaderList.Add(header);
				if (header.Next && headerData.Next != null) {
					headerData = headerData.Next;
					next = true;
				}
				else
					next = false;

				Console.WriteLine("Label: " + filename);
				Console.WriteLine("Addr: {0:X}", header.DestAddr);
				Console.WriteLine("Bank: {0:X}", header.DestBank);
				Console.WriteLine("Repeat: {0:X}", header.Size);
			}



		}

		public byte[] getTileData(int bank, int offset=0, int size=0x1800)
		{
			byte[] retArray = new byte[size];
			Array.Copy(vramBuffer[bank], offset, retArray, 0, size);
			return retArray;
		}

		public byte[] getMapData(int bank, int offset=0, int size=0x800)
		{
			byte[] retArray = new byte[size];
			Array.Copy(vramBuffer[bank], 0x1800+offset, retArray, 0, size);
			return retArray;
		}

		public byte[] readRam(int bank, int offset, int size) {
			byte[] retArray = new byte[size];
			Array.Copy(wramBuffer, offset, retArray, 0, size);
			return retArray;
		}
	}
}

