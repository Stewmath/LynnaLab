using System.Text.Json.Serialization;

namespace LynnaLib
{
    /// <summary>
    ///  Takes a file from the constants folder and creates a 1:1 mapping between definitions and
    ///  values.
    /// </summary>
    public class ConstantsMapping : TrackedProjectData
    {
        /// <summary>
        ///  A string/byte pair.
        /// </summary>
        class Entry
        {
            public string str;
            public int val;
            public Documentation documentation; // Documentation, if available (null otherwise)

            public Entry(string _str, int _val, Documentation _doc)
            {
                str = _str;
                val = _val;
                documentation = _doc;
            }

            // Empty constructor just for deserialization
            public Entry()
            {

            }
        }


        static log4net.ILog log = LogHelper.GetLogger();

        // ================================================================================
        // Variables
        // ================================================================================

        // TODO: DESERIALIZATION UNFINISHED - must implement CaptureInitialState logic
        // Anything undoable goes in here
        class State : TransactionState
        {
            // This list is only necessary to preserve ordering
            public List<string> stringList = new();

            // Mappings in both directions
            public Dictionary<string, Entry> stringToByte = new();
            public Dictionary<int, Entry> byteToString = new();

            public IList<string> prefixes;
            public int maxValue;

            public Documentation _documentation;
        }

        State state = new State();

        // This list is only necessary to preserve ordering
        List<string> StringList { get { return state.stringList; } }

        // ================================================================================
        // Properties
        // ================================================================================

        // Mappings in both directions
        Dictionary<string, Entry> StringToByteDict { get { return state.stringToByte; } }
        Dictionary<int, Entry> ByteToStringDict { get { return state.byteToString; } }

        int MaxValue
        {
            get { return state.maxValue; }
            set { state.maxValue = value; }
        }

        public IList<string> Prefixes
        {
            get { return state.prefixes; }
            private set { state.prefixes = value; }
        }

        /// <summary>
        ///  Returns a Documentation object for this entire set of values.
        /// </summary>
        public Documentation OverallDocumentation
        {
            get
            {
                if (state._documentation == null)
                {
                    state._documentation = new Documentation("", "", GetAllValuesWithDescriptions());
                    state._documentation.KeyName = "ID";
                }
                return state._documentation;
            }
        }

        // ================================================================================
        // Constructors
        // ================================================================================

        /// If the optional "maxValue" parameter is passed, any constants with this value or above is
        /// ignored when generating the mapping.
        internal ConstantsMapping(FileParser parser, string id, string prefix, int maxValue = -1, bool alphabetical = false)
            : this(parser, id, new string[] { prefix }, maxValue, alphabetical) { }

        internal ConstantsMapping(FileParser _parser, string id, string[] _prefixes, int maxValue = -1, bool alphabetical = false)
            : this(
                _parser.Project,
                id,
                _parser.DefinesDictionary.ToDictionary(kp => kp.Key, kp => kp.Value.Item1),
                _prefixes,
                maxValue,
                alphabetical,
                _parser.DefinesDictionary.ToDictionary(kp => kp.Key, kp => kp.Value.Item2?.Instance))
        { }

        internal ConstantsMapping(
            Project p,
            string id,
            FileParser[] parsers,
            string prefix,
            int maxValue = -1,
            bool alphabetical = false)
            : this(p, id, new string[] {prefix}, maxValue)
        {
            var defines = new Dictionary<string, string>();
            var documentation = new Dictionary<string, DocumentationFileComponent>();

            foreach (var parser in parsers)
            {
                foreach (var kp in parser.DefinesDictionary)
                {
                    defines.Add(kp.Key, kp.Value.Item1);
                    documentation.Add(kp.Key, kp.Value.Item2?.Instance);
                }
            }

            InitializeDefines(defines, alphabetical, documentation);
        }


        internal ConstantsMapping(
            Project p,
            string id,
            Dictionary<string, string> definesDictionary,
            string[] _prefixes,
            int maxValue = -1,
            bool alphabetical = false,
            Dictionary<string, DocumentationFileComponent> documentationDictionary = null)
            : this(p, id, _prefixes, maxValue)
        {
            InitializeDefines(definesDictionary, alphabetical, documentationDictionary);
        }

        internal ConstantsMapping(Project p, string id, IList<string> prefixes, int maxValue = -1)
            : base(p, id)
        {
            this.Prefixes = prefixes;

            if (maxValue == -1)
                maxValue = Int32.MaxValue;
            this.MaxValue = maxValue;

            // TODO: Why aren't we calling InitializeDefines here?
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private ConstantsMapping(Project p, string id, TransactionState state)
            : base(p, id)
        {
            this.state = (State)state;

            if (this.state.stringList == null || this.state.stringToByte == null || this.state.byteToString == null)
                throw new DeserializationException();
        }

        void InitializeDefines(
                Dictionary<string, string> definesDictionary,
                bool alphabetical = false,
                Dictionary<string, DocumentationFileComponent> documentationDictionary = null)
        {
            foreach (string key in definesDictionary.Keys)
            {
                bool acceptable = false;
                foreach (string prefix in Prefixes)
                {
                    if (key.Length > prefix.Length && key.Substring(0, prefix.Length) == prefix)
                    {
                        acceptable = true;
                        break;
                    }
                }

                if (acceptable)
                {
                    if (!StringToByteDict.ContainsKey(key))
                    {
                        string valStr = definesDictionary[key];
                        DocumentationFileComponent docComponent = null;
                        documentationDictionary?.TryGetValue(key, out docComponent);

                        int val;

                        if (Project.TryEval(valStr, out val))
                            AddKeyValuePair(key, val, docComponent, fromConstructor: true);
                        else
                            Console.WriteLine("ConstantsMapping: " + valStr);
                    }
                }

                if (alphabetical)
                    StringList.Sort();
            }
        }


        public void AddKeyValuePair(string key, int value, DocumentationFileComponent docComponent = null, bool fromConstructor = false)
        {
            if (value >= MaxValue)
                return;
            if (ByteToStringDict.ContainsKey(value))
            {
                log.Warn(string.Format($"Key {key} already existed in ConstantsMapping as {ByteToStringDict[value].str}."));
                return;
            }
            if (StringToByteDict.ContainsKey(key))
            {
                log.Warn(string.Format("Overwriting key {0} in ConstantsMapping", key));
            }

            if (!fromConstructor)
                Project.TransactionManager.CaptureInitialState<State>(this);

            StringList.Add(key);

            Documentation doc = null;
            if (docComponent != null)
                doc = new Documentation(docComponent, key);
            Entry ent = new Entry(key, value, doc); // TODO: remove doc from here

            StringToByteDict[key] = ent;
            ByteToStringDict[value] = ent;
        }

        // May throw KeyNotFoundException
        public int StringToByte(string key)
        {
            return StringToByteDict[key].val;
        }
        // Will always return something (either the string, or the number in hex)
        // TODO: Rename to "ValueToString" or something
        public string ByteToString(int key)
        {
            if (ByteToStringDict.ContainsKey(key))
                return ByteToStringDict[key].str;

            // Fallback
            return Wla.ToHex(key, 2);
        }

        // Returns -1 if values aren't found
        public int IndexOf(string key)
        {
            var list = GetAllStrings();
            return list.IndexOf(key); // TODO: optimize
        }


        public int GetIndexByte(int i)
        {
            return StringToByte(StringList[i]);
        }
        public string GetIndexString(int i)
        {
            return StringList[i];
        }


        public bool HasValue(int val)
        {
            return ByteToStringDict.ContainsKey(val);
        }
        public bool HasString(string s)
        {
            return StringToByteDict.ContainsKey(s);
        }


        public IList<string> GetAllStrings()
        {
            return StringList;
        }


        /// <summary>
        ///  Returns a list of all possible values (human-readable; shows both the byte and the
        ///  corresponding string), along with their description if they have one.
        /// </summary>
        public IList<Tuple<string, string>> GetAllValuesWithDescriptions()
        {
            var list = new List<Tuple<string, string>>();
            foreach (int key in ByteToStringDict.Keys)
            {
                string name = Wla.ToHex(key, 2) + ": " + RemovePrefix(ByteToStringDict[key].str);
                string desc = GetDocumentationForValue(key)?.Description ?? "";

                var tup = new Tuple<string, string>(name, desc);
                list.Add(tup);
            }
            return list;
        }


        /// <summary>
        ///  Takes a string, and removes any prefix corresponding to one of this ConstantsMapping's
        ///  prefixes.
        /// </summary>
        public string RemovePrefix(string s)
        {
            foreach (string prefix in Prefixes)
            {
                if (s.Length >= prefix.Length && s.Substring(0, prefix.Length) == prefix)
                {
                    s = s.Substring(prefix.Length);
                    break;
                }
            }

            return s;
        }

        /// <summary>
        ///  Returns a "default" documentation object for a particular value of this ConstantsMapping.
        ///  </summary>
        public Documentation GetDocumentationForValue(int b)
        {
            if (!ByteToStringDict.ContainsKey(b))
                return null;
            Documentation d = ByteToStringDict[b].documentation;
            if (d == null)
                return null;
            return new Documentation(d);
        }
        public Documentation GetDocumentationForValue(string s)
        {
            if (!StringToByteDict.ContainsKey(s))
                return null;
            Documentation d = StringToByteDict[s].documentation;
            if (d == null)
                return null;
            return new Documentation(d);
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
            this.state = (State)s;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
        }
    }
}
