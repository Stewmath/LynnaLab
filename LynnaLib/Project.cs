using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using Util;

namespace LynnaLib
{
    public class Project
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        log4net.Appender.RollingFileAppender logAppender;

        string baseDirectory, configDirectory, logDirectory;

        Dictionary<string, FileParser> fileParserDictionary = new Dictionary<string, FileParser>();

        // Maps label to file which contains it
        Dictionary<string, FileParser> labelDictionary = new Dictionary<string, FileParser>();
        // Dict of opened binary files
        Dictionary<string, MemoryFileStream> binaryFileDictionary = new Dictionary<string, MemoryFileStream>();
        // Dict of opened .PNG files
        Dictionary<string, PngGfxStream> pngGfxStreamDictionary = new Dictionary<string, PngGfxStream>();
        // Dictionary of .DEFINE's
        Dictionary<string, string> definesDictionary = new Dictionary<string, string>();

        // Data structures which should be linked to a particular project
        Dictionary<string, ProjectDataType> dataStructDictionary = new Dictionary<string, ProjectDataType>();
        Dictionary<string, ObjectGroup> objectGroupDictionary = new Dictionary<string, ObjectGroup>();


        // See "GetStandardSpritePalettes"
        Color[][] _standardSpritePalettes;

        ProjectConfig config;


        public Project(string d, string gameToLoad = "")
        {
            baseDirectory = d + '/';
            configDirectory = baseDirectory + "LynnaLab/";
            logDirectory = configDirectory + "Logs/";

            System.IO.Directory.CreateDirectory(configDirectory);
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

            string configFile = configDirectory + "config.yaml";
            try
            {
                config = ProjectConfig.Load(File.ReadAllText(configFile));
            }
            catch (FileNotFoundException)
            {
                log.Warn("Couldn't open config file '" + configFile + "'.");
                config = new ProjectConfig();
            }

            if (gameToLoad != "")
                config.EditingGame = gameToLoad;


            // Before parsing anything, create the "ROM_AGES" or "ROM_SEASONS" definition for ifdefs
            // to work
            definesDictionary.Add("ROM_" + GameString.ToUpper(), "");

            // Parse everything in constants/
            LoadFilesRecursively("constants/");

            // Must load this before constants mapping initialization
            GetFileParser("data/" + GameString + "/paletteData.s");

            // Initialize constantsMappings
            UniqueGfxMapping = new ConstantsMapping(
                    GetFileParser($"data/{GameString}/uniqueGfxHeaders.s"),
                    "UNIQUE_GFXH_");
            MainGfxMapping = new ConstantsMapping(
                    GetFileParser($"data/{GameString}/gfxHeaders.s"),
                    "GFXH_");
            PaletteHeaderMapping = new ConstantsMapping(
                    GetFileParser($"data/{GameString}/paletteHeaders.s"),
                    "PALH_");
            MusicMapping = new ConstantsMapping(
                    GetFileParser("constants/music.s"),
                    new string[] { "MUS_", "SND_" });
            SourceTransitionMapping = new ConstantsMapping(
                    GetFileParser("constants/transitions.s"),
                    "TRANSITION_SRC_");
            DestTransitionMapping = new ConstantsMapping(
                    GetFileParser("constants/transitions.s"),
                    "TRANSITION_DEST_");
            InteractionMapping = new ConstantsMapping(
                    GetFileParser("constants/interactionTypes.s"),
                    "INTERACID_",
                    alphabetical: true);
            EnemyMapping = new ConstantsMapping(
                    GetFileParser("constants/enemyTypes.s"),
                    "ENEMYID_",
                    alphabetical: true);
            PartMapping = new ConstantsMapping(
                    GetFileParser("constants/partTypes.s"),
                    "PARTID_",
                    alphabetical: true);
            ItemMapping = new ConstantsMapping(
                    GetFileParser("constants/itemTypes.s"),
                    "ITEMID_",
                    alphabetical: true);
            SeasonMapping = new ConstantsMapping(
                    GetFileParser("constants/seasons.s"),
                    "SEASON_");
            SpecialObjectMapping = new ConstantsMapping(
                    GetFileParser("constants/specialObjectTypes.s"),
                    "SPECIALOBJECTID_");
            ItemDropMapping = new ConstantsMapping(
                    GetFileParser("constants/itemDrops.s"),
                    "ITEM_DROP_");
            TreasureMapping = new ConstantsMapping(
                    GetFileParser("constants/treasure.s"),
                    "TREASURE_",
                    maxValue: 256);
            TreasureSpawnModeMapping = new ConstantsMapping(
                    GetFileParser("constants/treasureSpawnModes.s"),
                    "TREASURE_SPAWN_MODE_");
            TreasureGrabModeMapping = new ConstantsMapping(
                    GetFileParser("constants/treasureSpawnModes.s"),
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
        }

        void LoadFilesRecursively(string directory)
        {
            if (!directory.EndsWith("/"))
                directory += "/";
            foreach (string f in Helper.GetSortedFiles(baseDirectory + directory))
            {
                if (f.Substring(f.LastIndexOf('.')) == ".s")
                {
                    string basename = f.Substring(f.LastIndexOf('/') + 1);
                    if (basename == "macros.s") continue; // LynnaLib doesn't understand macros

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


        // Properties

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
            get { return Config.EditingGame; }
        }

        public Game Game
        {
            get { return GameString == "ages" ? Game.Ages : Game.Seasons; }
        }

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
                if (Config.ExpandedTilesets)
                    return 0x80;
                else if (GameString == "ages")
                    return 0x67;
                else
                    return 0x63;
            }
        }

        public int NumAnimations
        {
            get
            {
                return EvalToInt("NUM_ANIMATION_GROUPS");
            }
        }

        public int NumTreasures
        {
            get
            {
                return EvalToInt("NUM_TREASURES");
            }
        }


        // Methods

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
                stream = new MemoryFileStream(filename);
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
        }

        public void Close()
        {
            foreach (MemoryFileStream file in binaryFileDictionary.Values)
            {
                file.Close();
            }
            logAppender.Close();
            LogHelper.RemoveAppenderFromRootLogger(logAppender);
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


        Dictionary<Tuple<int, int>, Tileset> tilesetCache = new Dictionary<Tuple<int, int>, Tileset>();
        Dictionary<int, bool> tilesetSeasonalCache = new Dictionary<int, bool>();
        Dictionary<int, Dungeon> dungeonCache = new Dictionary<int, Dungeon>();
        Dictionary<Tuple<int, int>, WorldMap> worldMapCache = new Dictionary<Tuple<int, int>, WorldMap>();

        /// Always load Tileset objects through this function. We do not want duplicate Tileset
        /// objects referring to the same data.
        public Tileset GetTileset(int index, int season)
        {
            Debug.Assert(index >= 0 && index < NumTilesets);
            Debug.Assert(season >= -1 && season <= 3);

            if (TilesetIsSeasonal(index))
            {
                if (season == -1)
                    season = 0;
            }
            else
            {
                season = -1;
            }

            Tileset retval;
            tilesetCache.TryGetValue(new Tuple<int, int>(index, season), out retval);
            if (retval == null)
            {
                retval = new Tileset(this, index, season);
                tilesetCache[new Tuple<int, int>(index, season)] = retval;
            }
            return retval;
        }

        /// This function checks if a tileset is seasonal without instantiating a Tileset object. We
        /// do not want to call the constructor for Tileset objects with an invalid season
        /// parameter.
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

        public WorldMap GetWorldMap(int index, int season)
        {
            if (Game == Game.Ages || index != 0)
                season = -1;
            WorldMap retval;
            worldMapCache.TryGetValue(new Tuple<int, int>(index, season), out retval);
            if (retval == null)
            {
                retval = new WorldMap(this, index, season);
                worldMapCache[new Tuple<int, int>(index, season)] = retval;
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
            if (definesDictionary.ContainsKey(name))
            {
                if (!replace)
                    log.Warn("\"" + name + "\" defined multiple times");
            }
            definesDictionary[name] = value;
        }
        public void AddLabel(string label, FileParser source)
        {
            if (labelDictionary.ContainsKey(label))
                throw new DuplicateLabelException("Label \"" + label + "\" defined for a second time.");
            labelDictionary.Add(label, source);
        }
        public void RemoveLabel(string label)
        {
            FileParser f;
            if (!labelDictionary.TryGetValue(label, out f))
                return;
            labelDictionary.Remove(label);
            f.RemoveLabel(label);
        }
        public FileParser GetFileWithLabel(string label)
        {
            try
            {
                return labelDictionary[label];
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
                return labelDictionary[label].GetLabel(label);
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
                FileParser p = labelDictionary[label];
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
            if (definesDictionary.TryGetValue(val, out mapping))
                return mapping;
            return null;
        }

        // Handles only simple substitution
        private string Eval(string val)
        {
            val = val.Trim();

            string mapping;
            if (definesDictionary.TryGetValue(val, out mapping))
                return mapping;
            return val;
        }

        // TODO: finish arithmetic parsing
        public int EvalToInt(string val)
        {
            val = Eval(val).Trim();

            try
            {
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
                            return Convert.ToInt32(val); // Will throw FormatException
                        string newVal = val.Substring(0, i);
                        newVal += EvalToInt(val.Substring(i + 1, j));
                        newVal += val.Substring(j + 1, val.Length);
                        val = newVal;
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
                        throw new FormatException();
                    int ret;
                    if (parts[1] == "+")
                        ret = EvalToInt(parts[0]) + EvalToInt(parts[2]);
                    else if (parts[1] == "-")
                        ret = EvalToInt(parts[0]) - EvalToInt(parts[2]);
                    else if (parts[1] == "|")
                        ret = EvalToInt(parts[0]) | EvalToInt(parts[2]);
                    else if (parts[1] == "*")
                        ret = EvalToInt(parts[0]) * EvalToInt(parts[2]);
                    else if (parts[1] == "/")
                        ret = EvalToInt(parts[0]) / EvalToInt(parts[2]);
                    else if (parts[1] == ">>")
                        ret = EvalToInt(parts[0]) >> EvalToInt(parts[2]);
                    else if (parts[1] == "<<")
                        ret = EvalToInt(parts[0]) << EvalToInt(parts[2]);
                    else
                        throw new FormatException();
                    string newVal = "" + ret;
                    for (int j = 3; j < parts.Length; j++)
                    {
                        newVal += parts[j];
                    }
                    return EvalToInt(newVal);
                }
                // else parts.Length == 1

                if (val[0] == '>')
                    return (EvalToInt(val.Substring(1)) >> 8) & 0xff;
                else if (val[0] == '<')
                    return EvalToInt(val.Substring(1)) & 0xff;
                else if (val[0] == '$')
                    return Convert.ToInt32(val.Substring(1), 16);
                else if (val[val.Length - 1] == 'h')
                    return Convert.ToInt32(val.Substring(0, val.Length - 1), 16);
                else if (val[0] == '%')
                    return Convert.ToInt32(val.Substring(1), 2);
                else
                    return Convert.ToInt32(val);
            }
            catch (FormatException)
            {
                throw new FormatException("Couldn't parse '" + val + "'.");
            }
        }

        // Same as above but verifies the value is a byte.
        public byte EvalToByte(string val)
        {
            int byteVal = EvalToInt(val);
            if (byteVal < 0 || byteVal >= 256)
                throw new FormatException("Value '" + val + "' resolves to '" + byteVal + "', which isn't a byte.");
            return (byte)byteVal;
        }

        // Check if a room is used in a dungeon. Used by HighlightingMinimap.
        // Efficiency is O(D), where D = number of dungeons.
        public bool RoomUsedInDungeon(int roomIndex)
        {
            var rooms = new HashSet<int>();

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
        public int GetCanonicalLayoutGroup(int group, int season)
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
                    return season;
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


        // Private methods

        bool FileExists(string filename)
        {
            return File.Exists(BaseDirectory + filename);
        }
    }

    public enum Game
    {
        Seasons = 0,
        Ages
    }
}
