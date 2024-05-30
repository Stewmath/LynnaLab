using System;
using System.IO;
using System.Collections.Generic;
using Bitmap = System.Drawing.Bitmap;
using YamlDotNet.Serialization;

namespace LynnaLib
{
    public class PngGfxStream : Stream {
        private byte[] data;
        int _length;

        PngProperties properties;


        public PngGfxStream(string filename) {
            Bitmap bitmap = new Bitmap(filename);

            string propertiesFilename = Path.GetDirectoryName(filename) + "/" +
                Path.GetFileNameWithoutExtension(filename) + ".properties";

            properties = LoadProperties(propertiesFilename);

            _length = bitmap.Width * bitmap.Height * 2 / 8;

            data = new byte[Length * 8];

            Func<int,int,int> lookupPixel = (x, y) => {
                System.Drawing.Color color = bitmap.GetPixel(x, y);
                if (color.R != color.G || color.R != color.B || color.G != color.B)
                    throw new InvalidImageException(filename + " isn't a greyscale image.");

                int[] colors = {
                    0x00,
                    0x55,
                    0xaa,
                    0xff
                };

                for (int i=0; i<4; i++) {
                    if (color.R == colors[i]) {
                        if (properties.invert)
                            return i;
                        else
                            return 3-i;
                    }
                }

                throw new InvalidImageException("Invalid color in " + filename + ".");
            };

            for (int y=0; y<bitmap.Height; y++) {
                for (int x=0; x<bitmap.Width; x++) {
                    int val = lookupPixel(x, y);

                    int x2 = x % 8;
                    int y2 = y % 8;

                    int tile;

                    if (properties.interleave) {
                        tile = (y / 16) * bitmap.Width / 8 * 2;
                        tile += x / 8 * 2;
                        if ((y / 8) % 2 == 1)
                            tile++;
                    }
                    else
                        tile = (y / 8) * bitmap.Width / 8 + (x / 8);

                    data[tile * 16 + y2 * 2 + 0] |= (byte)((val&1) << (7-x2));
                    data[tile * 16 + y2 * 2 + 1] |= (byte)((val>>1) << (7-x2));
                }
            }
        }


        // Properties

        public override bool CanRead {
            get { return true; }
        }
        public override bool CanSeek {
            get { return true; }
        }
        public override bool CanTimeout {
            get { return false; }
        }
        public override bool CanWrite {
            get { return false; }
        }
        public override long Length {
            get {
                return _length;
            }
        }
        public override long Position { get; set; }


        // Methods

        public override void Flush() {
            throw new NotImplementedException();
        }

        public override void SetLength(long len) {
            throw new NotImplementedException();
        }

        public override long Seek(long dest, SeekOrigin origin) {
            switch (origin) {
            case SeekOrigin.End:
                Position = Length - dest;
                break;
            case SeekOrigin.Begin:
                Position = dest;
                break;
            case SeekOrigin.Current:
                Position += dest;
                break;
            }
            return Position;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            int size = count;
            if (Position + count > Length)
                size = (int)(Length-Position);
            Array.Copy(data, Position, buffer, offset, size);
            Position = Position + size;
            return size;
        }
        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotImplementedException();
        }

        public override int ReadByte() {
            int ret = data[Position];
            Position++;
            return ret;
        }
        public override void WriteByte(byte value) {
            throw new NotImplementedException();
        }


        // Static methods

        static PngProperties LoadProperties(string s) {
            PngProperties properties = new PngProperties();

            string baseFilename = Path.GetFileName(s);
            if (baseFilename.StartsWith("spr_")) {
                properties.invert = true;
                properties.interleave = true;
            }

            if (File.Exists(s)) {
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

                var dict = deserializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(s));

                if (dict.ContainsKey("width"))
                    properties.width = int.Parse(dict["width"]);
                if (dict.ContainsKey("tile_padding"))
                    properties.tile_padding = int.Parse(dict["tile_padding"]);
                if (dict.ContainsKey("invert"))
                    properties.invert = bool.Parse(dict["invert"]);
                if (dict.ContainsKey("interleave"))
                    properties.interleave = bool.Parse(dict["interleave"]);
            }

            return properties;
        }


        // Representation of ".properties" file
        class PngProperties {
            public int width;
            public int tile_padding;
            public bool invert;
            public bool interleave;
        }
    }
}
