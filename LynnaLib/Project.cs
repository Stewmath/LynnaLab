using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LynnaLib
{
    /// <summary>
    /// Holds project state that must be tracked for undo/redo. (The fields in here used to be
    /// directly in the Project class, but now it contains an instance of this.)
    /// </summary>
    class ProjectStateHolder : TrackedProjectData
    {
        public ProjectStateHolder(Project p, Game game, ProjectConfig projectConfig)
            : base(p, "Project State") // ID is unique, there can only be one of this
        {
            this.State = new()
            {
                game = game,
                projectConfig = projectConfig,
                labelDictionary = new(),
                definesDictionary = new(),
            };
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private ProjectStateHolder(Project p, string id, TransactionState state)
            : base(p, id)
        {
            this.State = (ProjectState)state;

            if (State.labelDictionary == null || State.definesDictionary == null)
                throw new DeserializationException();

            if (!Enum.IsDefined(typeof(Game), State.game))
                throw new DeserializationException($"Invalid game specified: {State.game}");
        }

        public ProjectState State { get; private set; }

        /// <summary>
        /// Actual fields that are tracked are in here.
        /// </summary>
        public class ProjectState : TransactionState
        {
            // The game being edited
            public required Game game;

            // Project config from config.yaml
            public required ProjectConfig projectConfig;

            // Maps label to file which contains it
            public required Dictionary<string, InstanceResolver<FileParser>> labelDictionary;

            // Dictionary of .DEFINE's
            [JsonRequired]
            public required Dictionary<string, string> definesDictionary;

        }

        // ================================================================================
        // TrackedProjectData interface functions
        // ================================================================================

        public override TransactionState GetState()
        {
            return State;
        }

        public override void SetState(TransactionState s)
        {
            State = (ProjectState)s;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
            Project.MarkModified();
        }
    }

    /// <summary>
    /// The Project class is the gateway to accessing everything in an oracles-disasm project.
    /// </summary>
    public class Project
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // ================================================================================
        // Variables
        // ================================================================================

        ProjectStateHolder stateHolder;

        // --- Don't need synchronization for these ---
        Dictionary<Tuple<int, Season>, RealTileset> tilesetCache = new();
        Dictionary<int, bool> tilesetSeasonalCache = new Dictionary<int, bool>();

        int numTilesets;
        // See "GetStandardSpritePalettes"
        Color[][] _standardSpritePalettes;

        // --- Only valid server-side ---
        readonly string baseDirectory;
        log4net.Appender.RollingFileAppender logAppender;


        TransactionManager transactionManager;


        // All ProjectDataType instances will be tracked in this dictionary. This includes instances of:
        // - WorldMap
        // - Dungeon
        // - ObjectGroup
        // - FileComponent / Data
        // - And more.
        Dictionary<string, ProjectDataType> dataStructDictionary = new();

        // ================================================================================
        // Properties
        // ================================================================================

        public bool Modified { get; private set; }

        public Game Game
        {
            get { return stateHolder.State.game; }
        }

        // The string to use for navigating game-specific folders in the disassembly
        public string GameString
        {
            get { return Game == Game.Seasons ? "seasons" : "ages"; }
        }

        public ConstantsMapping UniqueGfxMapping
        {
            get { return GetConstantsMapping("UniqueGfxMapping"); }
        }
        public ConstantsMapping MainGfxMapping
        {
            get { return GetConstantsMapping("MainGfxMapping"); }
        }
        public ConstantsMapping PaletteHeaderMapping
        {
            get { return GetConstantsMapping("PaletteHeaderMapping"); }
        }
        public ConstantsMapping MusicMapping
        {
            get { return GetConstantsMapping("MusicMapping"); }
        }
        public ConstantsMapping SourceTransitionMapping
        {
            get { return GetConstantsMapping("SourceTransitionMapping"); }
        }
        public ConstantsMapping DestTransitionMapping
        {
            get { return GetConstantsMapping("DestTransitionMapping"); }
        }
        public ConstantsMapping InteractionMapping
        {
            get { return GetConstantsMapping("InteractionMapping"); }
        }
        public ConstantsMapping EnemyMapping
        {
            get { return GetConstantsMapping("EnemyMapping"); }
        }
        public ConstantsMapping PartMapping
        {
            get { return GetConstantsMapping("PartMapping"); }
        }
        public ConstantsMapping ItemMapping
        {
            get { return GetConstantsMapping("ItemMapping"); }
        }
        public ConstantsMapping SeasonMapping
        {
            get { return GetConstantsMapping("SeasonMapping"); }
        }
        public ConstantsMapping SpecialObjectMapping
        {
            get { return GetConstantsMapping("SpecialObjectMapping"); }
        }
        public ConstantsMapping ItemDropMapping
        {
            get { return GetConstantsMapping("ItemDropMapping"); }
        }
        public ConstantsMapping TreasureMapping
        {
            get { return GetConstantsMapping("TreasureMapping"); }
        }
        public ConstantsMapping TreasureSpawnModeMapping
        {
            get { return GetConstantsMapping("TreasureSpawnModeMapping"); }
        }
        public ConstantsMapping TreasureGrabModeMapping
        {
            get { return GetConstantsMapping("TreasureGrabModeMapping"); }
        }
        public ConstantsMapping TreasureObjectMapping
        {
            get { return GetConstantsMapping("TreasureObjectMapping"); }
        }


        // TODO: Make these private
        public Dictionary<string, InstanceResolver<FileParser>> LabelDictionary { get { return stateHolder.State.labelDictionary; } }
        public Dictionary<string, string> DefinesDictionary { get { return stateHolder.State.definesDictionary; } }

        public ProjectConfig Config { get { return stateHolder.State.projectConfig; } }

        public string BaseDirectory
        {
            get { return baseDirectory; }
        }

        public TransactionManager TransactionManager { get { return transactionManager; } }

        public int NumDungeons
        {
            get
            {
                if (GameString == "ages")
                    return 16;
                else
                    return 12;
            }
        }

        public int NumGroups
        {
            get
            {
                if (GameString == "ages")
                    return 8;
                else
                    return 8;
            }
        }

        /// Number of groups in the "rooms" directory (ie. "room0500" is in layout group 5)
        public int NumLayoutGroups
        {
            get
            {
                if (GameString == "ages")
                    return 6;
                else
                    return 7;
            }
        }

        public int NumRooms
        {
            get
            {
                if (GameString == "ages")
                    return 0x800;
                else
                    return 0x800;
            }
        }

        public int NumTilesets
        {
            get
            {
                return numTilesets;
            }
        }

        public int NumAnimations
        {
            get
            {
                return Eval("NUM_ANIMATION_GROUPS");
            }
        }

        public int NumTreasures
        {
            get
            {
                return Eval("NUM_TREASURES");
            }
        }

        public Bitmap LinkBitmap { get; private set; }

        /// <summary>
        /// Whether this project is created from a network download, rather than reading from the filesystem.
        /// </summary>
        public bool IsClient { get; }

        internal bool IsInConstructor { get; }

        // Private properties

        JsonSerializerOptions SerializerOptions { get; }


        // ================================================================================
        // Constructors
        // ================================================================================

        private Project(string baseDirectory)
        {
            IsInConstructor = true;

            if (baseDirectory == null)
                this.baseDirectory = null;
            else
            {
                this.baseDirectory = Path.GetFullPath(baseDirectory);
                if (!this.baseDirectory.EndsWith("/"))
                    this.baseDirectory += "/";
            }

            SerializerOptions = new()
            {
                IncludeFields = true,
                //IgnoreReadOnlyFields = true,
                //IgnoreReadOnlyProperties = true,
                //RespectRequiredConstructorParameters = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            };

            SerializerOptions.Converters.Add(new InstanceResolverConverter(this));
        }

        /// <summary>
        /// Main constructor. When considering networking, this only works on the server side, where
        /// the game data actually is.
        /// </summary>
        public Project(string d, Game game, ProjectConfig config)
            : this(d)
        {
            IsClient = false;

            transactionManager = new(this, NetworkID.Server);
            transactionManager.BeginTransaction("Initial project load", merge: false, disallowUndo: true);

            this.stateHolder = new(this, game, config);

            // Write logs to disassembly folder at LynnaLab/Logs/
            var configDirectory = baseDirectory + "LynnaLab/";
            var logDirectory = configDirectory + "Logs/";
            System.IO.Directory.CreateDirectory(logDirectory);

            logAppender = new log4net.Appender.RollingFileAppender();
            logAppender.AppendToFile = true;
            logAppender.Layout = new log4net.Layout.PatternLayout(
                "%date %-5level [%logger] - %message%newline%exception");
            logAppender.File = logDirectory + "Log.txt";
            logAppender.Threshold = log4net.Core.Level.Info;
            logAppender.MaxFileSize = 2 * 1024 * 1024;
            logAppender.MaxSizeRollBackups = 10;
            logAppender.RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Once;
            logAppender.ActivateOptions();
            LogHelper.AddAppenderToRootLogger(logAppender);

            log.Info("Opening project at \"" + baseDirectory + "\".");

            // At this step, all files that are required are loaded from the disk. (FileParsers
            // still need to be loaded, but they will be based on the binary data streams loaded
            // here.)
            foreach (string dir in new string[] {
                    "audio", "code", "constants", "data", "gfx", "gfx_compressible",
                    "include", "objects", "rooms", "tileset_layouts_expanded"})
            {
                LoadBinaryFilesRecursively(dir, new string[] { ".s", ".bin", ".png", ".properties" });
            }

            // Below are some defines that normally come from "version.s" but which LynnaLab fails
            // to parse due to it using some newer wla-dx syntax that isn't supported here yet.

            // Before parsing anything, create the "ROM_AGES" or "ROM_SEASONS" definition for ifdefs
            // to work
            DefinesDictionary.Add("ROM_" + GameString.ToUpper(), "");

            // And other things from "version.s" (which LynnaLib can't parse right now). Most of
            // these aren't really important, but I've been known to use the bugfix flags in data
            // files, so LynnaLab should know about them.
            DefinesDictionary.Add("REGION_US", "");
            DefinesDictionary.Add("ENABLE_US_BUGFIXES", "");

            // Hack-base only
            if (Config.ExpandedTilesets)
            {
                DefinesDictionary.Add("ENABLE_EU_BUGFIXES", "");
                DefinesDictionary.Add("ENABLE_BUGFIXES", "");
                DefinesDictionary.Add("AGES_ENGINE", "");
            }

            // Parse everything in constants/
            LoadFileParsersRecursively("constants/");

            // Must load this before constants mapping initialization
            LoadFileParser("data/" + GameString + "/paletteData.s");

            // Initialize constantsMappings
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser($"data/{GameString}/uniqueGfxHeaders.s"),
                    "UniqueGfxMapping",
                    "UNIQUE_GFXH_",
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser($"data/{GameString}/gfxHeaders.s"),
                    "MainGfxMapping",
                    "GFXH_",
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser($"data/{GameString}/paletteHeaders.s"),
                    "PaletteHeaderMapping",
                    "PALH_",
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/music.s"),
                    "MusicMapping",
                    "MUS_",
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/transitions.s"),
                    "SourceTransitionMapping",
                    "TRANSITION_SRC_"));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/transitions.s"),
                    "DestTransitionMapping",
                    "TRANSITION_DEST_"));
            AddConstantsMapping(
                new ConstantsMapping(
                    this,
                    "InteractionMapping",
                    new FileParser[] {
                        LoadFileParser("constants/common/interactions.s"),
                        LoadFileParser($"constants/{GameString}/interactions.s"),
                    },
                    "INTERAC_",
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    this,
                    "EnemyMapping",
                    new FileParser[] {
                        LoadFileParser("constants/common/enemies.s"),
                        LoadFileParser($"constants/{GameString}/enemies.s"),
                    },
                    "ENEMY_",
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    this,
                    "PartMapping",
                    new FileParser[] {
                        LoadFileParser("constants/common/parts.s"),
                        LoadFileParser($"constants/{GameString}/parts.s"),
                    },
                    "PART_",
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/items.s"),
                    "ItemMapping",
                    "ITEM_",
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/specialObjects.s"),
                    "SpecialObjectMapping",
                    "SPECIALOBJECT_"));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/itemDrops.s"),
                    "ItemDropMapping",
                    "ITEM_DROP_"));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/treasure.s"),
                    "TreasureMapping",
                    "TREASURE_",
                    maxValue: 256,
                    alphabetical: true));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/treasureSpawnModes.s"),
                    "TreasureSpawnModeMapping",
                    "TREASURE_SPAWN_MODE_"));
            AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/common/treasureSpawnModes.s"),
                    "TreasureGrabModeMapping",
                    "TREASURE_GRAB_MODE_"));

            if (Game == Game.Seasons)
            {
                AddConstantsMapping(
                new ConstantsMapping(
                    LoadFileParser("constants/seasons/seasons.s"),
                    "SeasonMapping",
                    "SEASON_"));
            }

            // Parse everything in data/
            // A few files need to be loaded before others through
            if (!Config.ExpandedTilesets)
            {
                LoadFileParser("data/" + GameString + "/tilesetMappings.s");
                LoadFileParser("data/" + GameString + "/tilesetCollisions.s");
                LoadFileParser("data/" + GameString + "/tilesetHeaders.s");
            }
            LoadFileParsersRecursively("data/");

            // Parse wram.s
            LoadFileParser("include/wram.s");

            // Parse everything in objects/
            LoadFileParsersRecursively("objects/" + GameString + "/");

            // Load all Treasure Objects. This is necessary because they contain definitions which
            // are used elsewhere. Can't rely on "lazy loading".
            // Ideally this would happen automatically in the FileParser, but this is simpler for
            // now.
            AddConstantsMapping(new ConstantsMapping(
                                    this,
                                    "TreasureObjectMapping",
                                    new string[] { "TREASURE_OBJECT_" }));

            for (int t = 0; t < NumTreasures; t++)
            {
                TreasureGroup g = GetIndexedDataType<TreasureGroup>(t);
                for (int s = 0; s < g.NumTreasureObjectSubids; s++)
                    g.GetTreasureObject(s);
            }

            numTilesets = determineNumTilesets();

            LinkBitmap = LoadLinkBitmap();

            // Preload some data. Generally we want most "TrackedProjectData" instances to be loaded
            // in the Project constructor, because otherwise it may appear to the undo system as if
            // we are "creating" new objects (IE. new rooms) after initialization.

            for (int g=0; g<NumGroups; g++)
            {
                ForEachSeason(g, (s) => GetWorldMap(g, s));
            }
            for (int dungeon=0; dungeon<NumDungeons; dungeon++)
            {
                GetDungeon(dungeon);
            }
            for (int r=0; r<NumRooms; r++)
            {
                Room room = GetIndexedDataType<Room>(r);
                room.GetObjectGroup();
                room.GetWarpGroup();
            }

            transactionManager.EndTransaction();
            log.Info("Finished loading project.");
            IsInConstructor = false;
        }

        /// <summary>
        /// Creates a blank project which should be hooked up to a server to receive data.
        /// </summary>
        public Project()
            : this(null)
        {
            IsClient = true;

            transactionManager = new(this, NetworkID.Unassigned); // ID will be assigned later

            IsInConstructor = false;

            // After this, wait for data from the network.
        }

        /// <summary>
        /// Called after all data is finished loading. For client instances, this may be a fair bit
        /// after the constructor is finished.
        /// TODO: delete this
        /// </summary>
        public void FinalizeLoad()
        {
            numTilesets = determineNumTilesets();

            LinkBitmap = LoadLinkBitmap();
        }

        /// <summary>
        /// Getting the number of tilesets should not be a complicated calculation. However, I
        /// neglected to add extra tilesets to the Seasons hack-base branch at first, so this must
        /// detect whether they've been added.
        /// </summary>
        int determineNumTilesets()
        {
            if (!Config.ExpandedTilesets)
            {
                if (Game == Game.Ages)
                    return 0x67;
                else
                    return 0x63;
            }

            if (Game == Game.Ages)
                return 0x80;

            // Detect number of Seasons tileset
            Data data = GetData("tilesetData");
            Data end = GetData("tileset00Seasons");
            for (int t = 0; t < 0x81; t++)
            {
                if (data == end)
                    return t;
                data = data.GetDataAtOffset(8);
            }

            // Something went wrong
            throw new ProjectErrorException("Couldn't calculate # of tilesets");
        }

        void VisitDirectoriesRecursively(string directory, Action<string> action)
        {
            if (!directory.EndsWith("/"))
                directory += "/";
            foreach (string f in Helper.GetSortedFiles(baseDirectory + directory))
            {
                action(Path.GetRelativePath(baseDirectory, f));
            }

            // Ignore folders that belong to the other game
            string ignoreDirectory = (GameString == "ages" ? "seasons" : "ages");
            foreach (string d in Helper.GetSortedDirectories(baseDirectory + directory))
            {
                if (d == ignoreDirectory)
                    continue;
                VisitDirectoriesRecursively(directory + d, action);
            }
        }

        /// <summary>
        /// Load file parsers recursively. This DOES check the directories on the disk, although the
        /// data itself should already be loaded in MemoryFileStreamss.
        /// </summary>
        void LoadFileParsersRecursively(string directory)
        {
            // LynnaLib can't parse these yet, and generally shouldn't need to
            var blacklist = new string[]
            {
                "macros.s",
                "version.s",
            };

            VisitDirectoriesRecursively(directory, (filename) =>
            {
                if (filename.Substring(filename.LastIndexOf('.')) == ".s")
                {
                    if (blacklist.Contains(Path.GetFileName(filename)))
                        return;

                    LoadFileParser(filename);
                }
            });
        }

        /// <summary>
        /// Load files recursively as MemoryFileStreams.
        /// </summary>
        void LoadBinaryFilesRecursively(string directory, IEnumerable<string> extensions)
        {
            VisitDirectoriesRecursively(directory, (filename) =>
            {
                string extension = Path.GetExtension(filename);
                if (extensions.Contains(extension))
                {
                    if (extension == ".png")
                        LoadFileStream(filename, true);
                    else
                        LoadFileStream(filename, false);
                }
            });
        }

        /// <summary>
        /// Simply creating a ConstantsMapping will register it in the data struct dictionary, so
        /// there's nothing to do here.
        /// </summary>
        void AddConstantsMapping(ConstantsMapping mapping)
        {
        }

        // ================================================================================
        // Public/internal methods
        // ================================================================================

        public IEnumerable<TrackedProjectData> GetAllTrackedData()
        {
            return dataStructDictionary.Values.Where((a) => a is TrackedProjectData).Select((a) => (TrackedProjectData)a);
        }

        // TODO: Randomize loading order to catch bugs
        public TrackedProjectData AddExternalData(string identifier, Type instanceType, Type stateType, string stateStr)
        {
            log.Debug($"Adding external data: Type={instanceType}, ID={identifier}, StateType={stateType}");

            if (identifier == "Project State")
                log.Debug("Data state: " + stateStr);

            TransactionState state = (TransactionState)Deserialize(stateType, stateStr);

            Helper.Assert(instanceType.IsSubclassOf(typeof(TrackedProjectData)),
                          $"AddExternalData: Invalid class: " + instanceType.Name);

            try
            {
                var obj = (TrackedProjectData)Activator.CreateInstance(
                    instanceType,
                    BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
                    null,
                    new object[] { this, identifier, state },
                    null
                );
                Helper.Assert(identifier == obj.Identifier,
                              $"AddExternalData: Identifier mismatch ({identifier}, {obj.Identifier})");

                if (obj is ProjectStateHolder h)
                {
                    if (stateHolder != null)
                        throw new Exception("Tried to replace project's stateHolder?");
                    stateHolder = h;
                }
                return obj;
            }
            catch (System.Reflection.TargetInvocationException)
            {
                throw;
            }
        }

        /// <summary>
        /// Should only call this from the project constructor, so that everything's loaded at the
        /// beginning.
        /// </summary>
        private FileParser LoadFileParser(string filename)
        {
            if (CheckHasDataType<FileParser>(filename))
                return GetDataType<FileParser>(filename, createIfMissing: false);
            FileParser parser = new FileParser(this, filename);
            return parser;
        }

        /// <summary>
        /// Load a file from the disk into a MemoryFileStream. Should only call this from the
        /// project constructor, so that everything's loaded at the beginning.
        /// </summary>
        private void LoadFileStream(string filename, bool watchForFilesystemChanges)
        {
            if (CheckHasDataType<MemoryFileStream>(filename))
                return;

            string fullPath = Path.Combine(BaseDirectory, filename);
            if (!Helper.IsSubPathOf(fullPath, BaseDirectory))
                throw new Exception($"Bad file path (outside of project base directory): {filename}");
            if (!File.Exists(fullPath))
                throw new Exception($"File does not exist on disk: {fullPath}");

            // Instantiating this adds it to the data struct dictionary
            var stream = new MemoryFileStream(this, filename, watchForFilesystemChanges);
        }

        /// <summary>
        /// Get a FileParser that's already been initialized.
        /// </summary>
        private FileParser GetFileParser(string filename)
        {
            if (!CheckHasDataType(typeof(FileParser), filename))
            {
                throw new Exception($"FileParser did not exist already: {filename}");
            }
            return GetDataType<FileParser>(filename, createIfMissing: false);
        }

        /// <summary>
        /// Get a file stream that's already been loaded from the disk.
        /// </summary>
        public MemoryFileStream GetFileStream(string filename)
        {
            if (!CheckHasDataType<MemoryFileStream>(filename))
            {
                throw new Exception($"File stream did not exist: {filename}");
            }
            return GetDataType<MemoryFileStream>(filename, createIfMissing: false);
        }

        /// <summary>
        /// Unlike with "GetFileStream", the filename passed in has no path. This searches for a gfx
        /// file in all gfx directories (for the current game).
        ///
        /// Despite the directory traversal this works fine over the network, since it's checking
        /// cached data.
        ///
        /// The underlying type that provides the data will either be a MemoryFileStream (.bin) or a
        /// PngGfxStream (.png). The PNG is converted to binary in the gameboy's 2bpp format when
        /// accessed through the IStream.
        /// </summary>
        public IStream GetGfxStream(string filename)
        {
            var directories = new List<string>();

            // Check if it exists in any of these directories
            directories.Add("gfx/common/");
            directories.Add("gfx_compressible/common/");
            directories.Add("gfx/" + GameString + "/");
            directories.Add("gfx_compressible/" + GameString + "/");

            foreach (string directory in directories)
            {
                string baseFilename = directory + filename;
                if (FileExists(baseFilename + ".bin"))
                {
                    return GetFileStream(baseFilename + ".bin");
                }
                else if (FileExists(baseFilename + ".png"))
                {
                    string pngID = PngGfxStream.GetID(baseFilename);

                    if (CheckHasDataType<TrackedStream>(pngID))
                    {
                        return GetDataType<TrackedStream>(pngID, createIfMissing: false);
                    }
                    else
                    {
                        // Creating the PngGfxStream will register as a transaction, though it's not
                        // one that we want to allow the user to undo.
                        TransactionManager.BeginTransaction($"Load PNG: {filename}", merge: false, disallowUndo: true);

                        // Will add itself to data struct dictionary
                        var retval = new PngGfxStream(this, baseFilename);

                        TransactionManager.EndTransaction();

                        return retval;
                    }
                }
            }

            log.Warn($"GFX file not found: {filename}");
            return null;
        }

        public void Save()
        {
            if (IsClient) // Nothing to do on clients - don't have filesystem access
                return;

            // Note the separate foreach loops. Ordering can be important.
            foreach (ProjectDataType data in dataStructDictionary.Values)
            {
                if (data is FileParser p)
                    p.Save();
            }
            foreach (ProjectDataType data in dataStructDictionary.Values)
            {
                if (data is MemoryFileStream m)
                    m.Save();
                if (data is PngGfxStream p)
                    p.Save();
            }

            Modified = false;
        }

        public void Close()
        {
            log.Info("Closing project");
            foreach (var t in tilesetCache.Values)
            {
                t.Dispose();
            }
            foreach (ProjectDataType data in dataStructDictionary.Values)
            {
                if (data is FileParser parser)
                    parser.Close();
            }

            if (logAppender != null) // May be null on clients
            {
                LogHelper.RemoveAppenderFromRootLogger(logAppender);
                logAppender.Close();
            }
        }

        public string GenUniqueID(Type type)
        {
            int counter = 0;
            while (true)
            {
                string id = $"UniqueID-{counter}";
                if (!CheckHasDataType(type, id))
                    return id;
                counter++;
            }
        }

        public bool CheckHasDataType(Type t, string identifier)
        {
            string s = GetFullIdentifier(t, identifier);
            return dataStructDictionary.ContainsKey(s);
        }

        public bool CheckHasDataType<T>(string identifier) where T : ProjectDataType
        {
            return CheckHasDataType(typeof(T), identifier);
        }

        /// <summary>
        /// Should call this any time an object of type ProjectDataType is created. Then it can be
        /// retrieved through "GetDataType()" without being recreated again. (If this is not done,
        /// multiple instances of the supposedly same data may be created which could cause issues,
        /// and networking will probably due to its inability to find the data in question.)
        /// </summary>
        internal void AddDataType(Type type, ProjectDataType data)
        {
            if (!(type == data.GetType() || type.IsAssignableFrom(data.GetType())) || type == typeof(ProjectDataType))
                throw new Exception($"AddDataType: Invalid type: {type.FullName} (identifier: {data.Identifier})");

            string s = GetFullIdentifier(type, data.Identifier);

            if (dataStructDictionary.ContainsKey(s))
                throw new Exception("Data with identifier \"" + s +
                        "\" was attempted to be added to the project multiple times.");

            log.Debug($"Adding ProjectDataType: {s}");

            dataStructDictionary[s] = data;

            // Register the data as being created in the next transaction - assuming it's not being
            // created from a transaction being applied (undo/redo/network traffic).
            if (!TransactionManager.IsUndoing)
            {
                if (data is TrackedProjectData tracked)
                    transactionManager.RegisterNewData(type, tracked);
            }
        }

        /// <summary>
        /// Mainly called when an undo is performed. Undo system can't really handle data being
        /// removed within a transaction, but if data was added and then undone, this gets called.
        /// </summary>
        internal void RemoveDataType(Type type, string identifier, bool fromUndo)
        {
            string s = GetFullIdentifier(type, identifier);
            if (!dataStructDictionary.ContainsKey(s))
                throw new Exception($"Tried to remove data \"{s}\" which didn't exist!");
            dataStructDictionary.Remove(s);
            if (!fromUndo)
            {
                Helper.Assert(!TransactionManager.IsUndoing, "Removing ProjectDataType in the middle of an undo?");
                transactionManager.UnregisterData(type, identifier);
            }
        }

        internal void RemoveDataType<T>(string identifier, bool fromUndo=false) where T : ProjectDataType
        {
            RemoveDataType(typeof(T), identifier, fromUndo);
        }

        /// <summary>
        /// Get a datatype for which only one instance exists with a given identifier.
        ///
        /// If it doesn't exist already, this method will create an instance of the object, so long
        /// as the type implements "ProjectDataInstantiator" or "IndexedProjectDataInstantiator".
        /// Otherwise it will throw an exception.
        ///
        /// When considering network synchronization, there are two broad cases to consider:
        ///
        /// - Classes which extend TrackedProjectData: These will be transferred over the network
        /// and created on the other side. Therefore it's not critical that they implement
        /// instantiators - they don't need to be instantiated on the client side.
        ///
        /// - Everything else should implement instantiators. This is because this function MUST be
        /// able to instantiate these objects on demand, because they will not exist on the client
        /// side at first. Any ProjectDataType can be wrapped in an InstanceResolver, meaning that
        /// there could be a reference to it in any state-holding class. If this method can't
        /// instantiate the ProjectDataType, the InstanceResolver can's resolve the instance, so it
        /// must throw an exception!
        ///
        /// The only real reason for this distinction is that I don't want to have to implement
        /// state tracking for absolutely everything. Classes that don't need state tracking (those
        /// that don't extend TrackedProjectData) are simply recreated on the other end of the
        /// network connection.
        /// </summary>
        internal ProjectDataType GetDataType(Type type, string identifier, bool createIfMissing)
        {
            ProjectDataType instance;

            // Sanity check: Any class that doesn't extend TrackedProjectData should implement an
            // instantiator.
            // TODO: Move this to tests
            Debug.Assert(typeof(TrackedProjectData).IsAssignableFrom(type)
                         || typeof(ProjectDataInstantiator).IsAssignableFrom(type)
                         || typeof(IndexedProjectDataInstantiator).IsAssignableFrom(type),
                         $"Type {type.FullName} failed instantiator sanity check."
            );

            // This will verify the type is ok to use
            if (TryLookupDataType(type, identifier, out instance))
                return instance;

            if (!createIfMissing)
                throw new Exception($"Couldn't find project data: Type: {type.Name}, ID: {identifier}");

            log.Debug("Instantiating: " + GetFullIdentifier(type, identifier));

            if (typeof(IndexedProjectDataInstantiator).IsAssignableFrom(type))
            {
                int integerID;
                try
                {
                    integerID = int.Parse(identifier);
                }
                catch (FormatException)
                {
                    throw;
                }
                catch (OverflowException)
                {
                    throw;
                }

                return GetIndexedDataType(type, integerID);
            }

            if (typeof(ProjectDataInstantiator).IsAssignableFrom(type))
            {
                MethodInfo method = type.GetMethod(
                    "LynnaLib.ProjectDataInstantiator.Instantiate",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                if (method != null)
                {
                    instance = (ProjectDataType)method.Invoke(null, new object[] { this, identifier });
                    return instance;
                }
                else
                    throw new Exception($"Could not find Instantiate method for type {type.Name}.");
            }
            else
                throw new Exception($"Type {type.Name} is missing an instantiator.");
        }

        internal T GetDataType<T>(string identifier, bool createIfMissing)
            where T : ProjectDataType
        {
            return (T)GetDataType(typeof(T), identifier, createIfMissing);
        }

        // TODO: Make this internal
        // TODO: useDefaultConstructor argument (or createifmissing)
        public ProjectDataType GetIndexedDataType(Type type, int index)
        {
            ProjectDataType instance;
            string identifier = index.ToString();

            // This call verifies the type is ok to use
            if (TryLookupDataType(type, identifier, out instance))
                return instance;

            // Doesn't exist, must create it.
            if (typeof(IndexedProjectDataInstantiator).IsAssignableFrom(type))
            {
                MethodInfo method = type.GetMethod(
                    "LynnaLib.IndexedProjectDataInstantiator.Instantiate",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                if (method != null)
                {
                    instance = (ProjectDataType)method.Invoke(null, new object[] { this, index });
                    return instance;
                }
                else
                    throw new Exception($"Could not find Instantiate method for type {type.Name}.");
            }
            else
                throw new Exception();
        }

        public T GetIndexedDataType<T>(int index)
            where T : ProjectDataType
        {
            return (T)GetIndexedDataType(typeof(T), index);
        }


        bool TryLookupDataType<T>(string identifier, out T data)
            where T : ProjectDataType
        {
            ProjectDataType o;
            bool retval = TryLookupDataType(typeof(T), identifier, out o);
            data = (T)o;
            return retval;
        }
        bool TryLookupDataType(Type type, string identifier, out ProjectDataType data)
        {
            if (identifier == null)
                throw new Exception("GetDataType: identifier can't be null.");

            // This call verifies the type is ok to use
            string s = GetFullIdentifier(type, identifier);

            ProjectDataType o;
            if (dataStructDictionary.TryGetValue(s, out o))
            {
                data = o;
                return true;
            }
            data = null;
            return false;
        }



        /// <summary>
        /// Always load Tileset objects through this function. We do not want duplicate Tileset
        /// objects referring to the same data.
        /// </summary>
        public RealTileset GetTileset(int index, Season season, bool autoCorrect = false)
        {
            Debug.Assert(index >= 0 && index < NumTilesets);

            season = ValidateTilesetSeason(index, season, autoCorrect);

            RealTileset retval;
            tilesetCache.TryGetValue(new Tuple<int, Season>(index, season), out retval);
            if (retval == null)
            {
                retval = new RealTileset(this, index, season);
                tilesetCache[new Tuple<int, Season>(index, season)] = retval;
            }
            return retval;
        }

        /// <summary>
        /// This function checks if a tileset is seasonal without instantiating a Tileset object. We
        /// do not want to call the constructor for Tileset objects with an invalid season
        /// parameter.
        /// </summary>
        public bool TilesetIsSeasonal(int index)
        {
            Debug.Assert(index >= 0 && index < NumTilesets);

            // Though this seems trivial, we need to cache this for performance. Traversing
            // FileParsers is expensive.
            bool value;
            if (tilesetSeasonalCache.TryGetValue(index, out value))
            {
                return value;
            }

            Data data = GetData("tilesetData", index * 8);
            value = data.CommandLowerCase == "m_seasonaltileset";
            tilesetSeasonalCache[index] = value;
            return value;
        }

        /// <summary>
        /// Check if the season value is valid for this tileset.
        /// </summary>
        public bool TilesetSeasonIsValid(int index, Season season)
        {
            if (TilesetIsSeasonal(index))
                return (int)season >= 0 && (int)season <= 3;
            else
                return season == Season.None;
        }

        /// <summary>
        /// Checks if the season value for the tileset is valid, then either throws an exception or
        /// returns an acceptable value.
        /// </summary>
        public Season ValidateTilesetSeason(int index, Season season, bool autoCorrect = false)
        {
            if (!TilesetSeasonIsValid(index, season))
            {
                if (autoCorrect)
                    return TilesetIsSeasonal(index) ? Season.Spring : Season.None;
                else
                    throw new ProjectErrorException($"Invalid season {season} for tileset {index:X2}");
            }
            return season;
        }

        /// <summary>
        /// Check if the season value is valid for this group. (World maps and the rooms inside of
        /// them are considered "seasonal" by their index number, NOT based on the tilesets they
        /// use. Non-seasonal tilesets can be used on seasonal maps.)
        /// </summary>
        public bool GroupSeasonIsValid(int group, Season season)
        {
            if (group < 0 || group >= NumGroups)
                return false;
            else if (Game == Game.Seasons && group == 0)
                return (int)season >= 0 && (int)season <= 3;
            else
                return season == Season.None;
        }

        /// <summary>
        /// Perform an action for every valid season in a given group.
        /// </summary>
        public void ForEachSeason(int group, Action<Season> action)
        {
            if (group == 0 && Game == Game.Seasons)
            {
                for (int s=0; s<4; s++)
                    action((Season)s);
            }
            else
                action(Season.None);
        }

        public IList<Tileset> GetAllTilesets()
        {
            var list = new List<Tileset>();

            for (int i=0; i<NumTilesets; i++)
            {
                if (!TilesetIsSeasonal(i))
                    list.Add(GetTileset(i, Season.None));
                else
                {
                    for (int s=0; s<4; s++)
                        list.Add(GetTileset(i, (Season)s));
                }
            }

            return list;
        }

        public Room GetRoom(int index)
        {
            return GetIndexedDataType<Room>(index);
        }

        public RoomLayout GetRoomLayout(int index, Season season)
        {
            return GetIndexedDataType<Room>(index).GetLayout(season);
        }

        public Dungeon GetDungeon(int index)
        {
            return GetDataType<Dungeon>(index.ToString(), createIfMissing: true);
        }

        public WorldMap GetWorldMap(int index, Season season)
        {
            if (!GroupSeasonIsValid(index, season))
                throw new ProjectErrorException($"Invalid season {season} for group {index}.");
            return GetDataType<WorldMap>($"{index}_{season}", createIfMissing: true);
        }

        /// <summary>
        ///  Get an object gfx header. It would make sense for this to be a "ProjectIndexedDataType",
        ///  but that's not possible due to lack of multiple inheritance...
        /// </summary>
        public ObjectGfxHeaderData GetObjectGfxHeaderData(int index)
        {
            return GetData("objectGfxHeaderTable", 3 * index) as ObjectGfxHeaderData;
        }

        // This should only be used for getting "top-level" object groups (groupXMapYObjectData).
        // For "sublevels", use ObjectGroup's functions.
        internal ObjectGroup GetObjectGroup(string identifier, ObjectGroupType type)
        {
            ObjectGroup group;
            if (CheckHasDataType(typeof(ObjectGroup), identifier))
                group = GetDataType<ObjectGroup>(identifier, createIfMissing: false);
            else
            {
                group = new ObjectGroup(this, identifier, type);
            }

            if (type != group.GetGroupType())
            {
                throw new AssemblyErrorException(
                String.Format("Object group '{0}' used as both type '{1}' and '{2}'!",
                    identifier,
                    type,
                    group.GetGroupType()));
            }
            return group;

        }

        // Adds a definition to definesDictionary. Don't confuse with the "SetDefinition" method.
        public void AddDefinition(string name, string value, bool replace = false)
        {
            if (DefinesDictionary.ContainsKey(name))
            {
                if (!replace)
                    log.Warn("\"" + name + "\" defined multiple times");
            }
            DefinesDictionary[name] = value;
        }
        public void AddLabel(string label, FileParser source)
        {
            if (LabelDictionary.ContainsKey(label))
                throw new DuplicateLabelException("Label \"" + label + "\" defined for a second time.");
            LabelDictionary.Add(label, new(source));
        }
        public void RemoveLabel(string label)
        {
            InstanceResolver<FileParser> f;
            if (!LabelDictionary.TryGetValue(label, out f))
                return;
            LabelDictionary.Remove(label);
            f.Instance.RemoveLabel(label);
        }
        public FileParser GetFileWithLabel(string label)
        {
            return GetFileWithLabelAsReference(label).Instance;
        }
        /// <summary>
        /// Get a reference to a FileParser without resolving it, potentially useful in state-based
        /// constructors where one is not supposed to try to resolve any ProjectDataTypes.
        /// </summary>
        public InstanceResolver<FileParser> GetFileWithLabelAsReference(string label)
        {
            if (!HasLabel(label))
                throw new InvalidLookupException("Label \"" + label + "\" was needed but could not be located!");
            return LabelDictionary[label];
        }
        public Label GetLabel(string label)
        {
            if (!HasLabel(label))
                throw new InvalidLookupException("Label \"" + label + "\" was needed but could not be located!");
            return LabelDictionary[label].Instance.GetLabel(label);
        }
        public bool HasLabel(string label)
        {
            return LabelDictionary.ContainsKey(label);
        }

        // Returns "name" if the label is already unique, otherwise this calls
        // "GetUniqueLabelNameWithDigits".
        public string GetUniqueLabelName(string name)
        {
            if (!HasLabel(name))
                return name;
            return GetUniqueLabelNameWithDigits(name);
        }

        // Returns a unique label name starting with the string "name" and with 2 digits after that.
        public string GetUniqueLabelNameWithDigits(string name)
        {
            int nameIndex = 0;
            string attempt;
            do
            {
                attempt = name + "_" + nameIndex.ToString("d2");
                nameIndex++;
            }
            while (HasLabel(attempt));

            return attempt;
        }

        /// Throws a NotFoundException when the data doesn't exist.
        public Data GetData(string label, int offset = 0)
        {
            return GetFileWithLabel(label).GetData(label, offset);
        }

        public string GetDefinition(string val)
        {
            string mapping;
            if (DefinesDictionary.TryGetValue(val, out mapping))
                return mapping;
            return null;
        }

        // If a define exists for the given string, returns the result, otherwise returns the
        // original value
        private string TryDictLookup(string val)
        {
            val = val.Trim();

            string mapping;
            if (DefinesDictionary.TryGetValue(val, out mapping))
                return mapping;
            return val;
        }

        /// <summary>
        /// Attempts to get the integer representation of the value. Throws FormatException on error.
        /// </summary>
        public int Eval(string val)
        {
            int result;
            if (!TryEval(val, out result))
                throw new FormatException("Couldn't parse '" + val + "'.");
            return result;
        }

        /// <summary>
        /// Attempts to get the integer representation of the value, returns true on success.
        /// </summary>
        public bool TryEval(string val, out int result)
        {
            result = 0;
            val = TryDictLookup(val).Trim();

            // Find brackets
            for (int i = 0; i < val.Length; i++)
            {
                if (val[i] == '(')
                {
                    int x = 1;
                    int j;
                    for (j = i + 1; j < val.Length; j++)
                    {
                        if (val[j] == '(')
                            x++;
                        else if (val[j] == ')')
                        {
                            x--;
                            if (x == 0)
                                break;
                        }
                    }
                    if (j == val.Length)
                        return false;

                    string left = val.Substring(0, i);
                    int middle;
                    string right = val.Substring(j + 1, val.Length);

                    if (!TryEval(val.Substring(i + 1, j), out middle))
                        return false;

                    val = left + middle + right;
                }
            }
            // Split up string while keeping delimiters
            string[] delimiters = { "+", "-", "|", "*", "/", ">>", "<<" };
            string source = val;
            foreach (string delimiter in delimiters)
                source = source.Replace(delimiter, ";" + delimiter + ";");
            string[] parts = source.Split(';');

            if (parts.Length > 1)
            {
                if (parts.Length < 3)
                    return false;
                int left, right;
                if (!TryEval(parts[0], out left))
                    return false;
                if (!TryEval(parts[2], out right))
                    return false;
                int ret;
                if (parts[1] == "+")
                    ret = left + right;
                else if (parts[1] == "-")
                    ret = left - right;
                else if (parts[1] == "|")
                    ret = left | right;
                else if (parts[1] == "*")
                    ret = left * right;
                else if (parts[1] == "/")
                    ret = left / right;
                else if (parts[1] == ">>")
                    ret = left >> right;
                else if (parts[1] == "<<")
                    ret = left << right;
                else
                    return false;
                string newVal = "" + ret;
                for (int j = 3; j < parts.Length; j++)
                {
                    newVal += parts[j];
                }
                return TryEval(newVal, out result);
            }
            // else parts.Length == 1

            if (val[0] == '>')
            {
                if (!TryEval(val.Substring(1), out result))
                    return false;
                result = (result >> 8) & 0xff;
                return true;
            }
            else if (val[0] == '<')
            {
                if (!TryEval(val.Substring(1), out result))
                    return false;
                result = result & 0xff;
                return true;
            }
            else if (val[0] == '$')
            {
                return int.TryParse(val.Substring(1),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out result);
            }
            else if (val[val.Length - 1] == 'h')
            {
                return int.TryParse(val.Substring(0, val.Length - 1),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out result);
            }
            else if (val[0] == '%')
            {
                return int.TryParse(val.Substring(1),
                                    System.Globalization.NumberStyles.BinaryNumber,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out result);
            }
            else
                return int.TryParse(val, out result);
        }

        // Same as above but verifies the value is a byte.
        public byte EvalToByte(string val)
        {
            int byteVal = Eval(val);
            if (byteVal < 0 || byteVal >= 256)
                throw new FormatException("Value '" + val + "' resolves to '" + byteVal + "', which isn't a byte.");
            return (byte)byteVal;
        }

        // Check if a room is used in a dungeon. Used by HighlightingMinimap.
        // Efficiency is O(D), where D = number of dungeons.
        public bool RoomUsedInDungeon(int roomIndex)
        {
            for (int i = 0; i < NumDungeons; i++)
            {
                Dungeon d = GetDungeon(i);
                if (d.RoomUsed(roomIndex))
                    return true;
            }

            return false;
        }

        /// <summary>
        ///  Returns the standard sprite palettes (first 6 palettes used by most sprites).
        /// </summary>
        public Color[][] GetStandardSpritePalettes()
        {
            if (_standardSpritePalettes != null)
                return _standardSpritePalettes;

            _standardSpritePalettes = new Color[6][];

            RgbData data = GetData("standardSpritePaletteData") as RgbData;

            for (int i = 0; i < 6; i++)
            {
                _standardSpritePalettes[i] = new Color[4];
                for (int j = 0; j < 4; j++)
                {
                    _standardSpritePalettes[i][j] = data.Color;
                    data = data.NextData as RgbData;
                }
            }

            return _standardSpritePalettes;
        }

        // Gets the dungeon a room is in. Also returns the coordinates within the dungeon in x/y
        // parameters.
        public Dungeon GetRoomDungeon(Room room, out int x, out int y, out int floor)
        {
            x = -1;
            y = -1;
            floor = -1;

            for (int d = 0; d < NumDungeons; d++)
            {
                Dungeon dungeon = GetDungeon(d);
                if (dungeon.GetRoomPosition(room, out x, out y, out floor))
                    return dungeon;
            }

            return null;
        }

        public FileParser GetDefaultEnemyObjectFile()
        {
            string filename = "objects/" + GameString + "/enemyData.s";
            return GetFileParser(filename);
        }

        // There are "groups" (high digit of room number) and "layout groups". The layout group is
        // a tileset value which determines where the room layout will be loaded from (room0XYY.bin,
        // where "X" is the layout group, and YY is the low byte of the room). This is a dumb and
        // confusing distinction, but it's the way things are.
        // This function gets the expected "layout group" for a given "group". On the hack-base
        // branch, which changes how things work, this actually determines which layout group will
        // be used. On the master branch, a warning will be shown to a user if there's a mismatch.
        public int GetCanonicalLayoutGroup(int group, Season season)
        {
            if (GameString == "ages")
            {
                if (group == 1) // Present underwater & past overworld are swapped because reasons?
                    return 2;
                else if (group == 2)
                    return 1;
                else if (group >= 0 && group < 6)
                    return group;
                else if (group == 6 || group == 7) // "Duplicate" groups used for dungeon sidescrollers
                    return group - 2;
                else
                    throw new ArgumentException();
            }
            else if (GameString == "seasons")
            {
                if (group == 0)
                    return (int)season;
                else if (group == 1 || group == 2 || group == 3)
                    return 4;
                else if (group == 4 || group == 5)
                    return group + 1;
                else if (group == 6 || group == 7)
                    return group - 1;
                else
                    throw new ArgumentException();
            }
            else
                throw new Exception();
        }

        // This method only works on EXISTING constants in a specific file defined with ".define".
        // Unlike the "AddDefinition" method, this modifies a file with a new value.
        // TODO: Update the ConstantsMappings somehow. (Should overhaul those and use the
        // "Project.AddDefinition" method for updating constants, or something.)
        public void SetDefinition(string filename, string constant, string value)
        {
            FileParser parser = GetFileParser(filename);
            parser.SetDefinition(constant, value);
        }

        public Dictionary<string, string> GetDefinesDictionary()
        {
            return DefinesDictionary;
        }

        /// <summary>
        /// Mark the current state of the project as the starting point for an undo-able operation.
        /// If a "transaction" is already active when this called, the first one gets priority.
        ///
        /// Each BeginTransaction call MUST be paired with an EndTransaction call.
        ///
        /// If merge=true, merge the upcoming transaction with the previous transaction if their
        /// descriptions match. In this case, the description has semantic meaning. If the character
        /// "#" is included in the description string, the remainder of the string will not be
        /// displayed, but will still be used for comparisons to determine whether to merge.
        /// </summary>
        public void BeginTransaction(string description, bool merge = false, bool disallowUndo = false)
        {
            TransactionManager.BeginTransaction(description, merge, disallowUndo);
        }

        /// <summary>
        /// Mark the current state of the project as the ending point for an undo-able operation.
        /// </summary>
        public void EndTransaction()
        {
            TransactionManager.EndTransaction();
        }

        /// <summary>
        /// This checks the list of files loaded into memory and checks if the given filename
        /// (relative from the project base directory) exists. This does not include ALL files, only
        /// the ones already loaded through "LoadFileStream()" (which should include anything we
        /// care about).
        /// </summary>
        public bool FileExists(string filename)
        {
            return CheckHasDataType<TrackedStream>(filename);
        }

        public void MarkModified()
        {
            Modified = true;
        }

        /// <summary>
        /// TODO: Refactor things so this function doesn't need to exist because it's confusing.
        /// This only needs to be called when something externally modifies ProjectStateHolder.
        /// </summary>
        internal void CaptureSelfInitialState()
        {
            TransactionManager.CaptureInitialState<ProjectStateHolder.ProjectState>(stateHolder);
        }

        // ================================================================================
        // Serialization
        // ================================================================================

        public string Serialize<T>(T state)
        {
            return Serialize(typeof(T), state);
        }

        public string Serialize(Type type, object state)
        {
            string s = JsonSerializer.Serialize(state, type, SerializerOptions);
            if (s == null)
                throw new Exception("Serialization error");
            return s;
        }

        public T Deserialize<T>(string stateStr)
        {
            return (T)Deserialize(typeof(T), stateStr);
        }

        /// <summary>
        /// Do not call this unless you're certain that it's safe to deserialize the type that's
        /// being passed in. (The GetStateType and GetInstType functions do this validation.)
        /// </summary>
        public object Deserialize(Type type, string stateStr)
        {
            object s;
            try
            {
                s = JsonSerializer.Deserialize(stateStr, type, SerializerOptions);
            }
            catch (DeserializationException)
            {
                throw;
            }
            if (s == null)
                throw new DeserializationException("Null output from JsonSerializer");
            return s;
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        ConstantsMapping GetConstantsMapping(string identifier)
        {
            return GetDataType<ConstantsMapping>(identifier, createIfMissing: false);
        }

        Bitmap LoadLinkBitmap()
        {
            var stream = GetGfxStream("spr_link");
            stream.Seek(32 * 16, SeekOrigin.Begin);
            byte[] leftHalf = new byte[32];
            byte[] rightHalf = new byte[32];
            stream.Read(leftHalf, 0, 32);
            stream.Read(rightHalf, 0, 32);
            Bitmap leftBitmap = GbGraphics.RawTileToBitmap(leftHalf, GetStandardSpritePalettes()[0]);
            Bitmap rightBitmap = GbGraphics.RawTileToBitmap(rightHalf, GetStandardSpritePalettes()[0]);
            Bitmap fullBitmap = new Bitmap(16, 16);
            leftBitmap.DrawOn(fullBitmap, 0, 0);
            rightBitmap.DrawOn(fullBitmap, 8, 0);
            leftBitmap.Dispose();
            rightBitmap.Dispose();
            return fullBitmap;
        }

        // ================================================================================
        // Static methods
        // ================================================================================

        public static Game GameFromString(string game)
        {
            if (String.Equals(game, "seasons", StringComparison.OrdinalIgnoreCase))
                return Game.Seasons;
            else if (String.Equals(game, "ages", StringComparison.OrdinalIgnoreCase))
                return Game.Ages;
            else
                throw new Exception("Invalid game string: '" + game + "'");
        }

        public static string GetFullIdentifier(Type type, string id)
        {
            if (!typeof(ProjectDataType).IsAssignableFrom(type))
                throw new Exception($"GetFullIdentifier: Invalid type {type.FullName}");
            type = FixTypeForTracking(type);
            return type.FullName + "_" + id;
        }

        public static (Type, string) SplitFullIdentifier(string fullID)
        {
            string[] s = new string[2];
            int splitPos = fullID.IndexOf('_');
            s[0] = fullID.Substring(0, splitPos);
            s[1] = fullID.Substring(splitPos+1);
            if (s.Length != 2)
                throw new Exception($"Bad identifier: {fullID}");
            Type t = GetInstType(s[0]);
            return (t, s[1]);
        }

        public static Type GetStateType(string type)
        {
            Type t = Type.GetType(type);
            if (t == null)
                throw new Exception("Could not resolve type: " + type);
            if (!(typeof(TransactionState).IsAssignableFrom(t)))
                throw new Exception("GetStateType: Invalid type: " + t);
            return t;
        }

        public static Type GetInstType(string type)
        {
            Type t = Type.GetType(type);
            if (t == null)
                throw new Exception("Could not resolve type: " + type);
            if (!(typeof(TrackedProjectData).IsAssignableFrom(t)))
                throw new Exception("GetInstType: Invalid type: " + t);
            return t;
        }

        // NOTE: EXTREMELY HACKY WORKAROUND HERE!!!
        // FileComponent is one of the ProjectDataType's that's polymorphic. We may call
        // GetDataType<T> with type "FileComponent" or a derived class. The identifier must be the
        // same no matter what class it's called with.
        // This would be the case for any class that's referred to with an InstanceResolver that's
        // of a base class, rather than the subclass.
        static Type FixTypeForTracking(Type type)
        {
            if (type.IsSubclassOf(typeof(FileComponent)))
                return typeof(FileComponent);
            if (type.IsSubclassOf(typeof(TrackedStream)))
                return typeof(TrackedStream);
            return type;
        }
    }

    public enum Game
    {
        Seasons = 0,
        Ages
    }
}
