using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using Util;

namespace LynnaLib
{
    public class Project : Undoable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        log4net.Appender.RollingFileAppender logAppender;

        // ================================================================================
        // Variables
        // ================================================================================

        public readonly ConstantsMapping UniqueGfxMapping;
        public readonly ConstantsMapping MainGfxMapping;
        public readonly ConstantsMapping PaletteHeaderMapping;
        public readonly ConstantsMapping MusicMapping;
        public readonly ConstantsMapping SourceTransitionMapping;
        public readonly ConstantsMapping DestTransitionMapping;
        public readonly ConstantsMapping InteractionMapping;
        public readonly ConstantsMapping EnemyMapping;
        public readonly ConstantsMapping PartMapping;
        public readonly ConstantsMapping ItemMapping;
        public readonly ConstantsMapping SeasonMapping;
        public readonly ConstantsMapping SpecialObjectMapping;
        public readonly ConstantsMapping ItemDropMapping;
        public readonly ConstantsMapping TreasureMapping;
        public readonly ConstantsMapping TreasureSpawnModeMapping;
        public readonly ConstantsMapping TreasureGrabModeMapping;
        public readonly ConstantsMapping TreasureObjectMapping;

        readonly string baseDirectory;


        /// <summary>
        /// Project state fields that are "undoable". Everything outside of here is not affected by undo/redo.
        /// That being said there is a lot more state that is kept track of in other classes
        /// (FileComponent, FileParser, etc).
        /// </summary>
        class State : TransactionState
        {
            // Maps label to file which contains it
            public Dictionary<string, FileParser> labelDictionary = new Dictionary<string, FileParser>();
            // Dictionary of .DEFINE's
            public Dictionary<string, string> definesDictionary = new Dictionary<string, string>();

            public TransactionState Copy()
            {
                State s = new State();
                s.labelDictionary = new Dictionary<string, FileParser>(labelDictionary);
                s.definesDictionary = new Dictionary<string, string>(definesDictionary);
                return s;
            }

            public bool Compare(TransactionState o)
            {
                if (!(o is State p))
                    return false;
                return labelDictionary.SequenceEqual(p.labelDictionary)
                    && definesDictionary.SequenceEqual(p.definesDictionary);
            }
        }

        State state = new State();

        UndoState undoState = new UndoState();

        // string -> FileParser
        Dictionary<string, FileParser> fileParserDictionary = new Dictionary<string, FileParser>();
        // string -> MemoryFileStream (binary file)
        Dictionary<string, MemoryFileStream> binaryFileDictionary = new Dictionary<string, MemoryFileStream>();
        // string -> GfxStream
        Dictionary<string, PngGfxStream> pngGfxStreamDictionary = new Dictionary<string, PngGfxStream>();

        Dictionary<Tuple<int, Season>, RealTileset> tilesetCache = new();
        Dictionary<int, bool> tilesetSeasonalCache = new Dictionary<int, bool>();
        Dictionary<int, Dungeon> dungeonCache = new Dictionary<int, Dungeon>();
        Dictionary<Tuple<int, Season>, WorldMap> worldMapCache = new();


        // Data structures which should be linked to a particular project
        Dictionary<string, ProjectDataType> dataStructDictionary = new Dictionary<string, ProjectDataType>();
        Dictionary<string, ObjectGroup> objectGroupDictionary = new Dictionary<string, ObjectGroup>();

        readonly int numTilesets;

        // See "GetStandardSpritePalettes"
        Color[][] _standardSpritePalettes;

        ProjectConfig config;

        // ================================================================================
        // Constructors
        // ================================================================================

        public Project(string d, string gameToLoad, ProjectConfig config)
        {
            GameString = gameToLoad;
            baseDirectory = Path.GetFullPath(d);
            if (!baseDirectory.EndsWith("/"))
                baseDirectory += "/";
            this.config = config;

            // Write logs to disassembly folder at LynnaLab/Logs/
            var configDirectory = baseDirectory + "LynnaLab/";
            var logDirectory = configDirectory + "Logs/";
            System.IO.Directory.CreateDirectory(logDirectory);

            logAppender = new log4net.Appender.RollingFileAppender();
            logAppender.AppendToFile = true;
            logAppender.Layout = new log4net.Layout.PatternLayout(
                "%date{ABSOLUTE} [%logger] %level - %message%newline%exception");
            logAppender.File = logDirectory + "Log.txt";
            logAppender.Threshold = log4net.Core.Level.All;
            logAppender.MaxFileSize = 2 * 1024 * 1024;
            logAppender.MaxSizeRollBackups = 3;
            logAppender.RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Composite;
            logAppender.ActivateOptions();
            LogHelper.AddAppenderToRootLogger(logAppender);

            log.Info("Opening project at \"" + baseDirectory + "\".");

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

            // version.s contains some important defines that should be visible everywhere
            GetFileParser("constants/" + GameString + "/version.s");

            // Parse everything in constants/
            LoadFilesRecursively("constants/");

            // Must load this before constants mapping initialization
            GetFileParser("data/" + GameString + "/paletteData.s");

            // Initialize constantsMappings
            UniqueGfxMapping = new ConstantsMapping(
                    GetFileParser($"data/{GameString}/uniqueGfxHeaders.s"),
                    "UNIQUE_GFXH_",
                    alphabetical: true);
            MainGfxMapping = new ConstantsMapping(
                    GetFileParser($"data/{GameString}/gfxHeaders.s"),
                    "GFXH_",
                    alphabetical: true);
            PaletteHeaderMapping = new ConstantsMapping(
                    GetFileParser($"data/{GameString}/paletteHeaders.s"),
                    "PALH_",
                    alphabetical: true);
            MusicMapping = new ConstantsMapping(
                    GetFileParser("constants/common/music.s"),
                    "MUS_",
                    alphabetical: true);
            SourceTransitionMapping = new ConstantsMapping(
                    GetFileParser("constants/common/transitions.s"),
                    "TRANSITION_SRC_");
            DestTransitionMapping = new ConstantsMapping(
                    GetFileParser("constants/common/transitions.s"),
                    "TRANSITION_DEST_");
            InteractionMapping = new ConstantsMapping(
                    this,
                    new FileParser[] {
                        GetFileParser("constants/common/interactions.s"),
                        GetFileParser($"constants/{GameString}/interactions.s"),
                    },
                    "INTERAC_",
                    alphabetical: true);
            EnemyMapping = new ConstantsMapping(
                    this,
                    new FileParser[] {
                        GetFileParser("constants/common/enemies.s"),
                        GetFileParser($"constants/{GameString}/enemies.s"),
                    },
                    "ENEMY_",
                    alphabetical: true);
            PartMapping = new ConstantsMapping(
                    this,
                    new FileParser[] {
                        GetFileParser("constants/common/parts.s"),
                        GetFileParser($"constants/{GameString}/parts.s"),
                    },
                    "PART_",
                    alphabetical: true);
            ItemMapping = new ConstantsMapping(
                    GetFileParser("constants/common/items.s"),
                    "ITEM_",
                    alphabetical: true);
            SeasonMapping = new ConstantsMapping(
                    GetFileParser("constants/seasons/seasons.s"),
                    "SEASON_");
            SpecialObjectMapping = new ConstantsMapping(
                    GetFileParser("constants/common/specialObjects.s"),
                    "SPECIALOBJECT_");
            ItemDropMapping = new ConstantsMapping(
                    GetFileParser("constants/common/itemDrops.s"),
                    "ITEM_DROP_");
            TreasureMapping = new ConstantsMapping(
                    GetFileParser("constants/common/treasure.s"),
                    "TREASURE_",
                    maxValue: 256,
                    alphabetical: true);
            TreasureSpawnModeMapping = new ConstantsMapping(
                    GetFileParser("constants/common/treasureSpawnModes.s"),
                    "TREASURE_SPAWN_MODE_");
            TreasureGrabModeMapping = new ConstantsMapping(
                    GetFileParser("constants/common/treasureSpawnModes.s"),
                    "TREASURE_GRAB_MODE_");

            // Parse everything in data/
            // A few files need to be loaded before others through
            if (!Config.ExpandedTilesets)
            {
                GetFileParser("data/" + GameString + "/tilesetMappings.s");
                GetFileParser("data/" + GameString + "/tilesetCollisions.s");
                GetFileParser("data/" + GameString + "/tilesetHeaders.s");
            }
            LoadFilesRecursively("data/");

            // Parse wram.s
            GetFileParser("include/wram.s");

            // Parse everything in objects/
            LoadFilesRecursively("objects/" + GameString + "/");

            // Load all Treasure Objects. This is necessary because they contain definitions which
            // are used elsewhere. Can't rely on "lazy loading".
            // Ideally this would happen automatically in the FileParser, but this is simpler for
            // now.
            TreasureObjectMapping = new ConstantsMapping(this, new string[] { "TREASURE_OBJECT_" });
            for (int t = 0; t < NumTreasures; t++)
            {
                TreasureGroup g = GetIndexedDataType<TreasureGroup>(t);
                for (int s = 0; s < g.NumTreasureObjectSubids; s++)
                    g.GetTreasureObject(s);
            }

            numTilesets = determineNumTilesets();

            LinkBitmap = LoadLinkBitmap();

            // Don't allow undos that go before our initialization!
            // At the time of writing, only ConstantsMappings actually register undoable events in
            // this constructor.
            UndoState.ClearHistory();
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

        void LoadFilesRecursively(string directory)
        {
            // LynnaLib can't parse these yet, and generally shouldn't need to
            var blacklist = new string[]
            {
                "macros.s",
                "version.s",
            };

            if (!directory.EndsWith("/"))
                directory += "/";
            foreach (string f in Helper.GetSortedFiles(baseDirectory + directory))
            {
                if (f.Substring(f.LastIndexOf('.')) == ".s")
                {
                    string basename = f.Substring(f.LastIndexOf('/') + 1);
                    if (blacklist.Contains(basename))
                        continue;

                    string filename = directory + basename;
                    GetFileParser(filename);
                }
            }

            // Ignore folders that belong to the other game
            string ignoreDirectory = (GameString == "ages" ? "seasons" : "ages");
            foreach (string d in Helper.GetSortedDirectories(baseDirectory + directory))
            {
                if (d == ignoreDirectory)
                    continue;
                LoadFilesRecursively(directory + d);
            }
        }

        // ================================================================================
        // Properties
        // ================================================================================

        public Dictionary<string, FileParser> LabelDictionary { get { return state.labelDictionary; } }
        public Dictionary<string, string> DefinesDictionary { get { return state.definesDictionary; } }

        public bool Modified { get; private set; }

        public ProjectConfig Config
        {
            get
            {
                return config;
            }
        }

        public string BaseDirectory
        {
            get { return baseDirectory; }
        }

        // The string to use for navigating game-specific folders in the disassembly
        public string GameString
        {
            get; private set;
        }

        public Game Game
        {
            get { return GameString == "ages" ? Game.Ages : Game.Seasons; }
        }

        public UndoState UndoState { get { return undoState; } }

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

        public Action<Func<bool>> LazyInvoke { get; set; }


        // ================================================================================
        // Public/internal methods
        // ================================================================================

        internal FileParser GetFileParser(string filename)
        {
            if (!FileExists(filename))
                return null;
            FileParser p;
            if (!fileParserDictionary.TryGetValue(filename, out p))
            {
                p = new FileParser(this, filename);
                fileParserDictionary[filename] = p;
            }
            return p;
        }

        public MemoryFileStream GetBinaryFile(string filename)
        {
            filename = baseDirectory + filename;
            MemoryFileStream stream = null;
            if (!binaryFileDictionary.TryGetValue(filename, out stream))
            {
                stream = new MemoryFileStream(this, filename);
                binaryFileDictionary[filename] = stream;
            }
            return stream;
        }

        /// <summary>
        ///  Searches for a gfx file in all gfx directories (for the current game).
        /// </summary>
        public Stream LoadGfx(string filename)
        {
            PngGfxStream pngGfxStream;
            if (pngGfxStreamDictionary.TryGetValue(filename, out pngGfxStream))
                return pngGfxStream;

            var directories = new List<string>();

            directories.Add("gfx/common/");
            directories.Add("gfx_compressible/common/");
            directories.Add("gfx/" + GameString + "/");
            directories.Add("gfx_compressible/" + GameString + "/");

            foreach (string directory in directories)
            {
                string baseFilename = directory + filename;
                if (FileExists(baseFilename + ".bin"))
                {
                    return GetBinaryFile(baseFilename + ".bin");
                }
                else if (FileExists(baseFilename + ".png"))
                {
                    pngGfxStream = new PngGfxStream(BaseDirectory + baseFilename + ".png");
                    pngGfxStreamDictionary.Add(filename, pngGfxStream);
                    return pngGfxStream;
                }
            }

            return null;
        }

        public void Save()
        {
            foreach (ProjectDataType data in dataStructDictionary.Values)
            {
                data.Save();
            }
            foreach (FileParser parser in fileParserDictionary.Values)
            {
                parser.Save();
            }
            foreach (MemoryFileStream file in binaryFileDictionary.Values)
            {
                file.Flush();
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
            foreach (var d in dungeonCache.Values)
            {
            }
            foreach (var w in worldMapCache.Values)
            {
            }
            foreach (var group in objectGroupDictionary.Values)
            {
            }
            foreach (ProjectDataType data in dataStructDictionary.Values)
            {
            }
            foreach (FileParser parser in fileParserDictionary.Values)
            {
                parser.Close();
            }
            foreach (PngGfxStream stream in pngGfxStreamDictionary.Values)
            {
                stream.Close();
            }
            foreach (MemoryFileStream file in binaryFileDictionary.Values)
            {
                file.Close();
            }
            LogHelper.RemoveAppenderFromRootLogger(logAppender);
            logAppender.Close();
        }

        public T GetIndexedDataType<T>(int identifier) where T : ProjectIndexedDataType
        {
            string s = typeof(T).Name + "_" + identifier;
            ProjectDataType o;
            if (dataStructDictionary.TryGetValue(s, out o))
                return o as T;

            try
            {
                o = (ProjectIndexedDataType)Activator.CreateInstance(
                        typeof(T),
                        BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
                        null,
                        new object[] { this, identifier },
                        null
                        );
            }
            // If an exception occurs during reflection, it always throws
            // a TargetInvocationException. So we unpack that and throw the "real" exception.
            // NOTE: Undid this for now by using "throw;" to preserve the stack
            // trace. Unfortunately this means we lose the actual exception
            // type. Might affect "catch"es (need to test this).
            catch (System.Reflection.TargetInvocationException)
            {
                throw;
                //throw ex.InnerException;
            }

            AddDataType(o);

            return o as T;
        }

        /// <summary>
        ///   Get a datatype for which only one instance exists with a given identifier. This will
        ///   return that instance if it exists, or create it of it doesn't exist.
        /// </summary>
        public T GetDataType<T>(string identifier) where T : ProjectDataType
        {
            string s = typeof(T).Name + "_" + identifier;
            ProjectDataType o;
            if (dataStructDictionary.TryGetValue(s, out o))
                return o as T;

            try
            {
                o = (ProjectDataType)Activator.CreateInstance(
                        typeof(T),
                        BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
                        null,
                        new object[] { this, identifier },
                        null
                        );
            }
            // If an exception occurs during reflection, it always throws
            // a TargetInvocationException. So we unpack that and throw the "real" exception.
            catch (System.Reflection.TargetInvocationException ex)
            {
                throw ex.InnerException;
            }

            AddDataType(o);

            return o as T;
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
            if (Game == Game.Seasons && group == 0)
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


        public Dungeon GetDungeon(int index)
        {
            Dungeon retval;
            dungeonCache.TryGetValue(index, out retval);
            if (retval == null)
            {
                retval = new Dungeon(this, index);
                dungeonCache[index] = retval;
            }
            return retval;
        }

        public WorldMap GetWorldMap(int index, Season season)
        {
            if (!GroupSeasonIsValid(index, season))
                throw new ProjectErrorException($"Invalid season {season} for group {index}.");
            WorldMap retval;
            worldMapCache.TryGetValue(new Tuple<int, Season>(index, season), out retval);
            if (retval == null)
            {
                retval = new WorldMap(this, index, season);
                worldMapCache[new Tuple<int, Season>(index, season)] = retval;
            }
            return retval;
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
            if (objectGroupDictionary.TryGetValue(identifier, out group))
            {
                if (type != group.GetGroupType())
                    throw new AssemblyErrorException(
                        String.Format("Object group '{0}' used as both type '{1}' and '{2}'!",
                            identifier,
                            type,
                            group.GetGroupType()));
                return group;
            }
            group = new ObjectGroup(this, identifier, type);
            objectGroupDictionary[identifier] = group;
            return group;

        }

        void AddDataType(ProjectDataType data)
        {
            string s = data.GetIdentifier();
            if (dataStructDictionary.ContainsKey(s))
                throw new Exception("Data with identifier \"" + data.GetIdentifier() +
                        "\" was attempted to be added to the project multiple times.");
            dataStructDictionary[s] = data;
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
            LabelDictionary.Add(label, source);
        }
        public void RemoveLabel(string label)
        {
            FileParser f;
            if (!LabelDictionary.TryGetValue(label, out f))
                return;
            LabelDictionary.Remove(label);
            f.RemoveLabel(label);
        }
        public FileParser GetFileWithLabel(string label)
        {
            try
            {
                return LabelDictionary[label];
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidLookupException("Label \"" + label + "\" was needed but could not be located!");
            }
        }
        public Label GetLabel(string label)
        {
            try
            {
                return LabelDictionary[label].GetLabel(label);
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidLookupException("Label \"" + label + "\" was needed but could not be located!");
            }
        }
        public bool HasLabel(string label)
        {
            try
            {
                FileParser p = LabelDictionary[label];
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
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
            FileParser parser = fileParserDictionary[filename];
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
        public void BeginTransaction(string description, bool merge = false)
        {
            UndoState.BeginTransaction(description, merge);
        }

        /// <summary>
        /// Mark the current state of the project as the ending point for an undo-able operation.
        /// </summary>
        public void EndTransaction()
        {
            UndoState.EndTransaction();
        }

        public bool FileExists(string filename)
        {
            return File.Exists(BaseDirectory + filename);
        }

        public void MarkModified()
        {
            Modified = true;
        }

        // ================================================================================
        // Undoable interface functions
        // ================================================================================

        public TransactionState GetState()
        {
            return state;
        }

        public void SetState(TransactionState s)
        {
            state = (State)s.Copy();
        }

        public void InvokeModifiedEvent(TransactionState prevState)
        {
            MarkModified();
        }

        // ================================================================================
        // Private methods
        // ================================================================================

        Bitmap LoadLinkBitmap()
        {
            var stream = LoadGfx("spr_link");
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
    }

    public enum Game
    {
        Seasons = 0,
        Ages
    }
}
