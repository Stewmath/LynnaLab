using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    public class Project
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public readonly ConstantsMapping UniqueGfxMapping;
        public readonly ConstantsMapping MainGfxMapping;
        public readonly ConstantsMapping PaletteHeaderMapping;
        public readonly ConstantsMapping MusicMapping;
        public readonly ConstantsMapping SourceTransitionMapping;
        public readonly ConstantsMapping DestTransitionMapping;

        log4net.Appender.RollingFileAppender logAppender;

        string baseDirectory;
        string configDirectory;

        Dictionary<string,FileParser> fileParserDictionary = new Dictionary<string,FileParser>();

        // Maps label to file which contains it
        Dictionary<string,FileParser> labelDictionary = new Dictionary<string,FileParser>();
        // List of opened binary files
        Dictionary<string,MemoryFileStream> binaryFileDictionary = new Dictionary<string,MemoryFileStream>();
        // Dictionary of .DEFINE's
        Dictionary<string,string> definesDictionary = new Dictionary<string,string>();

        // Data structures which should be linked to a particular project
        Dictionary<string,ProjectDataType> dataStructDictionary = new Dictionary<string,ProjectDataType>();

        public string BaseDirectory {
            get { return baseDirectory; }
        }

        public Project(string d)
        {
            baseDirectory = d + '/';
            configDirectory = baseDirectory + "LynnaLab/";
            System.IO.Directory.CreateDirectory(configDirectory);

            logAppender = new log4net.Appender.RollingFileAppender();
            logAppender.AppendToFile = true;
            logAppender.Layout = new log4net.Layout.PatternLayout("%date{ABSOLUTE} [%logger] %level - %message%newline%exception");
            logAppender.File = configDirectory + "Log.txt";
            logAppender.Threshold = log4net.Core.Level.All;
            logAppender.MaxFileSize = 2 * 1024 * 1024;
            logAppender.MaxSizeRollBackups = 3;
            logAppender.RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Composite;
            logAppender.ActivateOptions();
            LogHelper.AddAppenderToRootLogger(logAppender);

            log.Info("Opened project at \"" + baseDirectory + "\".");

            // Parse everything in constants/
            foreach (string f in Directory.EnumerateFiles(baseDirectory + "constants/")) {
                if (f.Substring(f.LastIndexOf('.')) == ".s") {
                    string filename = "constants/" + f.Substring(f.LastIndexOf('/') + 1);
                    GetFileParser(filename);
                }
            }

            // Initialize constantsMappings
            UniqueGfxMapping = new ConstantsMapping(
                    GetFileParser("constants/uniqueGfxHeaders.s") as FileParser,
                    "UNIQGFXH_");
            MainGfxMapping = new ConstantsMapping(
                    GetFileParser("constants/gfxHeaders.s") as FileParser,
                    "GFXH_");
            PaletteHeaderMapping = new ConstantsMapping(
                    GetFileParser("constants/paletteHeaders.s") as FileParser,
                    "PALH_");
            MusicMapping = new ConstantsMapping(
                    GetFileParser("constants/music.s") as FileParser,
                    new string[] {"MUS_", "SND_"} );
            SourceTransitionMapping = new ConstantsMapping(
                    GetFileParser("constants/transitions.s") as FileParser,
                    "TRANSITION_SRC_");
            DestTransitionMapping = new ConstantsMapping(
                    GetFileParser("constants/transitions.s") as FileParser,
                    "TRANSITION_DEST_");

            // Parse everything in data/
            // A few files need to be loaded before others through
            GetFileParser("data/tilesetMappings.s");
            GetFileParser("data/tilesetCollisions.s");
            GetFileParser("data/tilesetHeaders.s");
            foreach (string f in Directory.EnumerateFiles(baseDirectory + "data/")) {
                if (f.Substring(f.LastIndexOf('.')) == ".s") {
                    string filename = "data/" + f.Substring(f.LastIndexOf('/') + 1);
                    GetFileParser(filename);
                }
            }
            // Parse wram.s
            GetFileParser("include/wram.s");
            // Parse everything in interactions/
            foreach (string f in Directory.EnumerateFiles(baseDirectory + "interactions/")) {

                string basename = f.Substring(f.LastIndexOf('/') + 1);
                if (basename == "macros.s") continue; // LynnaLab doesn't understand macros

                if (f.Substring(f.LastIndexOf('.')) == ".s") {
                    string filename = "interactions/" + basename;
                    GetFileParser(filename);
                }
            }
        }

        public FileParser GetFileParser(string filename) {
            FileParser p;
            if (!fileParserDictionary.TryGetValue(filename, out p)) {
                p = new FileParser(this, filename);
                fileParserDictionary[filename] = p;
            }
            return p;
        }

        public MemoryFileStream GetBinaryFile(string filename) {
            filename = baseDirectory + filename;
            MemoryFileStream stream = null;
            if (!binaryFileDictionary.TryGetValue(filename, out stream)) {
                stream = new MemoryFileStream(filename);
                binaryFileDictionary[filename] = stream;
            }
            return stream;
        }

        public void Save() {
            foreach (ProjectDataType data in dataStructDictionary.Values) {
                data.Save();
            }
            foreach (FileParser parser in fileParserDictionary.Values) {
                parser.Save();
            }
            foreach (MemoryFileStream file in binaryFileDictionary.Values) {
                file.Flush();
            }
        }

        public void Close() {
            foreach (MemoryFileStream file in binaryFileDictionary.Values) {
                file.Close();
            }
            logAppender.Close();
            LogHelper.RemoveAppenderFromRootLogger(logAppender);
        }

        public T GetIndexedDataType<T>(int identifier) where T:ProjectIndexedDataType {
            string s = typeof(T).Name + "_" + identifier;
            ProjectDataType o;
            if (dataStructDictionary.TryGetValue(s, out o))
                return o as T;
            o = (ProjectIndexedDataType)Activator.CreateInstance(
                    typeof(T),
                    BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
                    null,
                    new object[] { this, identifier },
                    null
                    );
            dataStructDictionary[s] = o;

            return o as T;
        }
        public T GetDataType<T>(string identifier) where T:ProjectDataType {
            string s = typeof(T).Name + "_" + identifier;
            ProjectDataType o;
            if (dataStructDictionary.TryGetValue(s, out o))
                return o as T;
            o = (ProjectDataType)Activator.CreateInstance(
                    typeof(T),
                    BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
                    null,
                    new object[] { this, identifier },
                    null
                    );
            dataStructDictionary[s] = o;

            return o as T;
        }

        public void AddDataType(ProjectDataType data) {
            string s = data.GetIdentifier();
            if (dataStructDictionary.ContainsKey(s))
                throw new Exception("Data with identifier \"" + data.GetIdentifier() +
                        "\" was attempted to be added to the project multiple times.");
            dataStructDictionary[s] = data;
        }

        public void AddDefinition(string name, string value) {
            if (definesDictionary.ContainsKey(name)) {
                log.Warn("WARNING: \"" + name + "\" defined multiple times");
            }
            definesDictionary[name] = value;
        }
        public void AddLabel(string label, FileParser source) {
            if (labelDictionary.ContainsKey(label))
                throw new DuplicateLabelException("Label \"" + label + "\" defined for a second time.");
            labelDictionary.Add(label, source);
        }
        public void RemoveLabel(string label) {
            FileParser f;
            if (!labelDictionary.TryGetValue(label, out f))
                return;
            labelDictionary.Remove(label);
            f.RemoveLabel(label);
        }
        public FileParser GetFileWithLabel(string label) {
            try {
                return labelDictionary[label];
            } catch(KeyNotFoundException) {
                throw new LabelNotFoundException("Label \"" + label + "\" was needed but could not be located!");
            }
        }
        public bool HasLabel(string label) {
            try {
                FileParser p = labelDictionary[label];
                return true;
            }
            catch(KeyNotFoundException) {
                return false;
            }
        }
        public Data GetData(string label, int offset=0) {
            return GetFileWithLabel(label).GetData(label, offset);
        }

        // Handles only simple substitution
        public string Eval(string val)
        {
            val = val.Trim();

            string mapping;
            if (definesDictionary.TryGetValue(val, out mapping))
                return mapping;
            return val;
        }

        // TODO: finish arithmetic parsing
        public int EvalToInt(string val) {
            val = Eval(val).Trim();
            // Find brackets
            for (int i = 0; i < val.Length; i++) {
                if (val[i] == '(') {
                    int x = 1;
                    int j = 0;
                    for (j = i + 1; j < val.Length; j++) {
                        if (val[j] == '(')
                            x++;
                        else if (val[j] == ')') {
                            x--;
                            if (x == 0)
                                break;
                        }
                    }
                    if (j == val.Length)
                        return Convert.ToInt32(val); // Will throw NumberFormatException
                    string newVal = val.Substring(0, i);
                    newVal += EvalToInt(val.Substring(i + 1, j));
                    newVal += val.Substring(j + 1, val.Length);
                    val = newVal;
                }
            }
            // Split up string while keeping delimiters
            string[] delimiters = { "+", "-", "|" };
            string source = val;
            foreach (string delimiter in delimiters)
                source = source.Replace(delimiter, ";" + delimiter + ";");
            string[] parts = source.Split(';');

            if (parts.Length > 1) {
                if (parts.Length < 3)
                    throw new FormatException();
                int ret = 0;
                if (parts[1] == "+")
                    ret = EvalToInt(parts[0]) + EvalToInt(parts[2]);
                else if (parts[1] == "-")
                    ret = EvalToInt(parts[0]) - EvalToInt(parts[2]);
                else if (parts[1] == "|")
                    ret = EvalToInt(parts[0]) | EvalToInt(parts[2]);
                else
                    throw new FormatException();
                string newVal = "" + ret;
                for (int j = 3; j < parts.Length; j++) {
                    newVal += parts[j];
                }
                return EvalToInt(newVal);
            }
            // else parts.Length == 1

            if (val[0] == '>')
                return (EvalToInt(val.Substring(1))>>8)&0xff;
            else if (val[0] == '<')
                return EvalToInt(val.Substring(1))&0xff;
            else if (val[0] == '$')
                return Convert.ToInt32(val.Substring(1), 16);
            else if (val[val.Length - 1] == 'h')
                return Convert.ToInt32(val.Substring(0, val.Length - 1), 16);
            else if (val[0] == '%')
                return Convert.ToInt32(val.Substring(1), 2);
            else
                return Convert.ToInt32(val);
        }

        // Get a set of all rooms used in the dungeons. Used by
        // HighlightingMinimap.
        public HashSet<int> GetRoomsUsedInDungeons() {
            var rooms = new HashSet<int>();

            for (int i=0; i<GetNumDungeons(); i++) {
                Dungeon d = GetIndexedDataType<Dungeon>(i);
                for (int f=0; f<d.NumFloors; f++) {
                    for (int x=0; x<d.MapWidth; x++) {
                        for (int y=0; y<d.MapHeight; y++) {
                            rooms.Add(d.GetRoom(x, y, f).Index);
                        }
                    }
                }
            }

            return rooms;
        }

        public int GetNumDungeons() {
            return 16;
        }

        public int GetNumGroups() {
            return 8;
        }

        public int GetNumRooms() {
            return 0x600;
        }

        public int GetNumAreas() {
            return 0x67;
        }

        public int GetFirstDungeonGroup() {
            return 4;
        }
    }
}
