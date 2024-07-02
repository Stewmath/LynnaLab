using System;
using System.Collections.Generic;
using System.Linq;

using Util;

namespace LynnaLib
{
    /// <summary>
    ///  Takes a file from the constants folder and creates a 1:1 mapping between definitions and
    ///  values.
    /// </summary>
    public class ConstantsMapping
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
        }


        static log4net.ILog log = LogHelper.GetLogger();


        // This list is only necessary to preserve ordering
        List<string> stringList = new List<string>();

        // Mappings in both directions
        Dictionary<string, Entry> stringToByte = new Dictionary<string, Entry>();
        Dictionary<int, Entry> byteToString = new Dictionary<int, Entry>();

        IList<string> prefixes;

        private Documentation _documentation;

        int maxValue;


        public Project Project { get; private set; }

        public IList<string> Prefixes
        {
            get { return prefixes; }
        }

        /// <summary>
        ///  Returns a Documentation object for this entire set of values.
        /// </summary>
        public Documentation OverallDocumentation
        {
            get
            {
                if (_documentation == null)
                    _documentation = new Documentation("", "", GetAllValuesWithDescriptions());
                return _documentation;
            }
        }

        /// If the optional "maxValue" parameter is passed, any constants with this value or above is
        /// ignored when generating the mapping.
        public ConstantsMapping(FileParser parser, string prefix, int maxValue = -1, bool alphabetical = false)
            : this(parser, new string[] { prefix }, maxValue, alphabetical) { }

        public ConstantsMapping(FileParser _parser, string[] _prefixes, int maxValue = -1, bool alphabetical = false)
            : this(_parser.Project,
                    _parser.DefinesDictionary.ToDictionary(kp => kp.Key, kp => kp.Value.Item1),
                    _prefixes,
                    maxValue,
                    alphabetical,
                    _parser.DefinesDictionary.ToDictionary(kp => kp.Key, kp => kp.Value.Item2)
                    )
        { }

        public ConstantsMapping(Project p,
                Dictionary<string, string> definesDictionary,
                string[] _prefixes,
                int maxValue = -1,
                bool alphabetical = false,
                Dictionary<string, DocumentationFileComponent> documentationDictionary = null)
            : this(p, _prefixes, maxValue)
        {
            foreach (string key in definesDictionary.Keys)
            {
                bool acceptable = false;
                foreach (string prefix in prefixes)
                {
                    if (key.Length > prefix.Length && key.Substring(0, prefix.Length) == prefix)
                    {
                        acceptable = true;
                        break;
                    }
                }

                if (acceptable)
                {
                    if (!stringToByte.ContainsKey(key))
                    {
                        try
                        {
                            string valStr = definesDictionary[key];
                            DocumentationFileComponent docComponent = null;
                            documentationDictionary?.TryGetValue(key, out docComponent);

                            int val = Project.EvalToInt(valStr);

                            AddKeyValuePair(key, val, docComponent);
                        }
                        catch (FormatException) { }
                    }
                }

                if (alphabetical)
                    stringList.Sort();
            }
        }

        public ConstantsMapping(Project p, IList<string> prefixes, int maxValue = -1)
        {
            this.Project = p;
            this.prefixes = prefixes;

            if (maxValue == -1)
                maxValue = Int32.MaxValue;
            this.maxValue = maxValue;
        }

        public void AddKeyValuePair(string key, int value, DocumentationFileComponent docComponent = null)
        {
            if (value >= maxValue)
                return;
            if (byteToString.ContainsKey(value))
            {
                log.Warn(string.Format($"Key {key} already existed in ConstantsMapping as {byteToString[value].str}."));
                return;
            }
            if (stringToByte.ContainsKey(key))
            {
                log.Warn(string.Format("Overwriting key {0} in ConstantsMapping", key));
            }

            stringList.Add(key);

            Documentation doc = null;
            if (docComponent != null)
                doc = new Documentation(docComponent, key);
            Entry ent = new Entry(key, value, doc); // TODO: remove doc from here

            stringToByte[key] = ent;
            byteToString[value] = ent;
        }

        // May throw KeyNotFoundException
        public int StringToByte(string key)
        {
            return stringToByte[key].val;
        }
        // Will always return something (either the string, or the number in hex)
        // TODO: Rename to "ValueToString" or something
        public string ByteToString(int key)
        {
            try
            {
                return byteToString[key].str;
            }
            catch (KeyNotFoundException)
            { // Fallback
                return Wla.ToHex(key, 2);
            }
        }

        // These functions return -1 if values aren't found
        public int IndexOf(string key)
        {
            var list = GetAllStrings();
            return list.IndexOf(key); // TODO: optimize
        }
        public int IndexOf(int val)
        {
            try
            {
                return IndexOf(ByteToString(val));
            }
            catch (KeyNotFoundException)
            {
                return -1;
            }
        }


        public int GetIndexByte(int i)
        {
            return StringToByte(stringList[i]);
        }
        public string GetIndexString(int i)
        {
            return stringList[i];
        }


        public bool HasValue(int val)
        {
            return byteToString.ContainsKey(val);
        }
        public bool HasString(string s)
        {
            return stringToByte.ContainsKey(s);
        }


        public IList<string> GetAllStrings()
        {
            return stringList;
        }


        /// <summary>
        ///  Returns a list of all possible values (human-readable; shows both the byte and the
        ///  corresponding string), along with their description if they have one.
        /// </summary>
        public IList<Tuple<string, string>> GetAllValuesWithDescriptions()
        {
            var list = new List<Tuple<string, string>>();
            foreach (int key in byteToString.Keys)
            {
                string name = Wla.ToHex(key, 2) + ": " + RemovePrefix(byteToString[key].str);
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
            try
            {
                Documentation d = byteToString[b].documentation;
                if (d == null)
                    return null;
                return new Documentation(d);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
        public Documentation GetDocumentationForValue(string s)
        {
            try
            {
                Documentation d = stringToByte[s].documentation;
                if (d == null)
                    return null;
                return new Documentation(d);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
