using System.IO;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace LynnaLib
{
    /// <summary>
    /// This class loads a PNG file and converts it to a byte stream. Only works with greyscale PNGs
    /// which can be properly converted to 2bpp format.
    /// </summary>
    public class PngGfxStream : TrackedStream, IStream
    {
        public PngGfxStream(Project p, string baseFilename)
            : base(p, GetID(baseFilename))
        {
            this.pngFilePath = baseFilename + ".png";
            this.state = new()
            {
                PngFile = new(p.GetFileStream(pngFilePath)),
            };
            LoadFromPngFile();

            // PNG files, specifically, watch for changes on the filesystem.
            this.PngFile.ModifiedEvent += (_, _) =>
            {
                Project.TransactionManager.BeginTransaction("Reload PNG file", merge: false, disallowUndo: true);
                LoadFromPngFile();
                InvokeModifiedEvent(StreamModifiedEventArgs.All(this));
                Project.TransactionManager.EndTransaction();
            };
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private PngGfxStream(Project p, string id, TransactionState state)
            : base(p, id)
        {
            this.state = (State)state;
            this.pngFilePath = null;
        }

        public static string GetID(string baseFilename)
        {
            return "pngstream-" + baseFilename;
        }

        // ================================================================================
        // Variables
        // ================================================================================

        private static readonly log4net.ILog log = LogHelper.GetLogger();

        class State : TransactionState
        {
            [JsonRequired]
            public byte[] Data { get; set; }

            [JsonRequired]
            public PngProperties Properties { get; set; }

            [JsonRequired]
            public InstanceResolver<MemoryFileStream> PngFile { get; init; }
        }

        State state;

        // These are only valid on the server
        readonly string pngFilePath;

        // ================================================================================
        // Properties
        // ================================================================================

        byte[] Data { get { return state.Data; } }
        MemoryFileStream PngFile { get { return state.PngFile; } }
        PngProperties Properties { get { return state.Properties; } }

        // Stream Properties

        public override long Length
        {
            get
            {
                return Data.Length;
            }
        }

        public override long Position { get; set; }

        // TODO: Do something with this
        public bool Modified { get; private set; }

        // ================================================================================
        // Events
        // ================================================================================

        // Inherits ModifiedEvent

        // ================================================================================
        // Methods
        // ================================================================================

        // Stream methods

        public override long Seek(long dest, SeekOrigin origin)
        {
            switch (origin)
            {
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            int size = count;
            if (Position + count > Length)
                size = (int)(Length - Position);
            Array.Copy(Data, Position, buffer, offset, size);
            Position = Position + size;
            return size;
        }

        public override int ReadByte()
        {
            int ret = Data[Position];
            Position++;
            return ret;
        }

        public override ReadOnlySpan<byte> ReadAllBytes()
        {
            return Data;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void WriteAllBytes(ReadOnlySpan<byte> data)
        {
            Project.TransactionManager.CaptureInitialState<State>(this);
            state.Data = data.ToArray();
            Modified = true;
            InvokeModifiedEvent(StreamModifiedEventArgs.All(this));
        }

        public override void WriteByte(byte value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Use ImageSharp to write back modified PNG files.
        /// </summary>
        public void Save()
        {
            if (!Modified)
                return;

            if (pngFilePath == null)
            {
                log.Error("Can't save PNG file on remote instance.");
                return;
            }

            SaveTo(Path.Combine(Project.BaseDirectory, pngFilePath));

            Modified = false;
        }

        /// <summary>
        /// Save as PNG to another location. This is used for remote clients that want to edit PNG
        /// files manually.
        /// </summary>
        public void SaveTo(string outputFile)
        {
            if (Properties.nonDefault)
            {
                throw new Exception($"Can't save PNG file with a .properties file: {pngFilePath}.");
            }
            if (Data.Length % 16 != 0)
            {
                throw new Exception($"Can't save PNG file with data width not a multiple of 16: {pngFilePath}.");
            }

            int numTiles = Data.Length / 16;

            // Default to 16 tiles per row (the assumed default with no .properties file).
            int tilesPerRow = 16;

            int width = numTiles >= tilesPerRow ? tilesPerRow : numTiles;
            int height = (numTiles + tilesPerRow - 1) / tilesPerRow;

            using (Bitmap bitmap = new(width * 8, height * 8))
            {
                for (int tile = 0; tile < numTiles; tile++)
                {
                    int x = tile % tilesPerRow;
                    int y = tile / tilesPerRow;
                    GbGraphics.RenderRawTile(
                        bitmap,
                        x * 8,
                        y * 8,
                        new ReadOnlySpan<byte>(Data, tile * 16, 16),
                        GbGraphics.GrayPalette, 0);
                }

                bitmap.Save(outputFile);
            }
        }

        // ================================================================================
        // TrackedProjectData interface functions
        // ================================================================================

        public override TransactionState GetState()
        {
            return state;
        }

        public override void SetState(TransactionState s)
        {
            State newState = (State)s;
            if (newState.Data == null)
                throw new DeserializationException("Missing data in MemoryFileStream");
            this.state = newState;
            this.Modified = true;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
            State last = (State)prevState;

            var args = StreamModifiedEventArgs.FromChangedRange(last.Data, state.Data);
            if (args == null)
                return;
            InvokeModifiedEvent(args);
        }

        /// <summary>
        /// Replaces the graphical data with the contents of another PNG file. Throws
        /// InvalidImageException if there's a problem with the image, can throw other types of
        /// exeptions if the file couldn't be read.
        /// </summary>
        public void LoadFromPngFile(string filename)
        {
            byte[] data = File.ReadAllBytes(filename);

            Project.BeginTransaction("Load external PNG file", merge: false, disallowUndo: false);
            try
            {
                LoadFromPngData(data, false);
            }
            catch (Exception)
            {
                Project.EndTransaction();
                throw;
            }

            Project.EndTransaction();
            InvokeModifiedEvent(StreamModifiedEventArgs.All(this));
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        void LoadFromPngFile()
        {
            string propertiesFilename = Path.GetDirectoryName(pngFilePath) + "/" +
                Path.GetFileNameWithoutExtension(pngFilePath) + ".properties";

            state.Properties = LoadProperties(Project, propertiesFilename);

            LoadFromPngData(PngFile.ReadAllBytes().ToArray(), true);
        }

        /// <summary>
        /// Throws InvalidImageException if there was a problem with the image.
        /// </summary>
        void LoadFromPngData(byte[] pngData, bool fromProjectData)
        {
            Project.TransactionManager.CaptureInitialState<State>(this);
            using (var bitmap = new Bitmap(pngData))
            {
                int numTiles = bitmap.Width * bitmap.Height / 64;

                byte[] data = new byte[numTiles * 16];

                Func<int, int, int> lookupPixel = (x, y) =>
                {
                    var color = bitmap.GetPixel(x, y);

                    for (int i = 0; i < 4; i++)
                    {
                        if (color == GbGraphics.GrayPalette[i])
                        {
                            if (Properties.invert)
                                return 3 - i;
                            else
                                return i;
                        }
                    }

                    throw new InvalidImageException($"Invalid color in {pngFilePath} at {x},{y}: {color}.");
                };

                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int val = lookupPixel(x, y);

                        int x2 = x % 8;
                        int y2 = y % 8;

                        int tile;

                        if (Properties.interleave)
                        {
                            tile = (y / 16) * bitmap.Width / 8 * 2;
                            tile += x / 8 * 2;
                            if ((y / 8) % 2 == 1)
                                tile++;
                        }
                        else
                            tile = (y / 8) * bitmap.Width / 8 + (x / 8);

                        data[tile * 16 + y2 * 2 + 0] |= (byte)((val & 1) << (7 - x2));
                        data[tile * 16 + y2 * 2 + 1] |= (byte)((val >> 1) << (7 - x2));
                    }
                }

                state.Data = data;
            }

            // Don't write back to the PNG file after loading from it
            this.Modified = !fromProjectData;
        }

        // ================================================================================
        // Static methods
        // ================================================================================

        static PngProperties LoadProperties(Project p, string s)
        {
            PngProperties properties = new PngProperties();

            string baseFilename = Path.GetFileName(s);
            if (baseFilename.StartsWith("spr_"))
            {
                properties.invert = true;
                properties.interleave = true;
            }

            if (p.FileExists(s))
            {
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                MemoryFileStream propertyStream = p.GetFileStream(s);
                var dict = deserializer.Deserialize<Dictionary<string, string>>(propertyStream.ReadAllText());

                if (dict.ContainsKey("width"))
                    properties.width = int.Parse(dict["width"]);
                if (dict.ContainsKey("tile_padding"))
                    properties.tile_padding = int.Parse(dict["tile_padding"]);
                if (dict.ContainsKey("invert"))
                    properties.invert = bool.Parse(dict["invert"]);
                if (dict.ContainsKey("interleave"))
                    properties.interleave = bool.Parse(dict["interleave"]);

                properties.nonDefault = true;
            }

            return properties;
        }


        // Representation of ".properties" file. This is serialized with networking.
        class PngProperties
        {
            public int width;
            public int tile_padding;
            public bool invert;
            public bool interleave;
            public bool nonDefault;
        }
    }
}
