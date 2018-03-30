using System;
using System.Collections.Generic;

namespace LynnaLab
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
    class Entry {
        public string str;
        public byte b;
        public Documentation documentation; // Documentation, if available (null otherwise)

        public Entry(string _str, byte _b, Documentation _doc) {
            str = _str;
            b = _b;
            documentation = _doc;
        }
    }

    // This list is only necessary to preserve ordering
    List<string> stringList = new List<string>();

    // Mappings in both directions
    Dictionary<string,Entry> stringToByte = new Dictionary<string,Entry>();
    Dictionary<byte,Entry> byteToString = new Dictionary<byte,Entry>();

    string[] prefixes;

    FileParser parser;


    public Project Project {
        get { return parser.Project; }
    }

    public string[] Prefixes {
        get { return prefixes; }
    }

    public Documentation DefaultDocumentation {
        get {
            return new Documentation("", "", GetAllValuesWithDescriptions());
        }
    }

    public ConstantsMapping(FileParser parser, string prefix)
        : this(parser, new string[] { prefix }) {}

    public ConstantsMapping(FileParser _parser, string[] _prefixes)
    {
        this.parser = _parser;
        this.prefixes = _prefixes;

        Dictionary<string,Tuple<string,DocumentationFileComponent>> definesDictionary = parser.DefinesDictionary;
        foreach (string key in definesDictionary.Keys) {
            bool acceptable = false;
            foreach (string prefix in prefixes)
                if (key.Substring(0, prefix.Length) == prefix) {
                    acceptable = true;
                    break;
                }

            if (acceptable) {
                Entry tmp;
                if (!stringToByte.TryGetValue(key, out tmp)) {
                    try {
                        var tup = definesDictionary[key];
                        string val = tup.Item1;
                        var docComponent = tup.Item2;

                        byte b = (byte)Project.EvalToInt(val);
                        stringList.Add(key);

                        Documentation doc = (docComponent == null ? null : new Documentation("", docComponent, "subid"));
                        Entry ent = new Entry(key, b, doc); // TODO: remove subid from here

                        stringToByte[key] = ent;
                        byteToString[b] = ent;
                    }
                    catch (FormatException) {}
                }
            }
        }
    }

    // May throw KeyNotFoundException
    public byte StringToByte(string key) {
        return stringToByte[key].b;
    }
    public string ByteToString(byte key) {
        return byteToString[key].str;
    }

    public int IndexOf(string key) {
        var list = GetAllStrings();
        return list.IndexOf(key); // TODO: optimize
    }
    public int IndexOf(byte val) {
        try {
            return IndexOf(ByteToString((byte)val));
        }
        catch(KeyNotFoundException) {
            return -1;
        }
    }


    public byte GetIndexByte(int i) {
        return StringToByte(stringList[i]);
    }
    public string GetIndexString(int i) {
        return stringList[i];
    }


    public IList<string> GetAllStrings() {
        return stringList;
    }


    public string GetDocumentationField(byte key, string name) {
        var doc = GetDocumentation(key);
        if (doc == null)
            return null;
        return doc.FileComponent.GetDocumentationField(name);
    }
    public string GetDocumentationField(string key, string name) {
        var doc = GetDocumentation(key);
        if (doc == null)
            return null;
        return doc.FileComponent.GetDocumentationField(name);
    }


    /// <summary>
    ///  Returns a list of all possible values (human-readable; shows both the byte and the
    ///  corresponding string), along with their description if they have one.
    /// </summary>
    public IList<Tuple<string,string>> GetAllValuesWithDescriptions() {
        var list = new List<Tuple<string,string>>();
        foreach (byte key in byteToString.Keys) {
            string name =  Wla.ToByte(key) + ": " + RemovePrefix(byteToString[key].str);
            var tup = new Tuple<string,string>(name, GetDocumentationField(key,"desc"));
            list.Add(tup);
        }
        return list;
    }


    /// <summary>
    ///  Takes a string, and removes any prefix corresponding to one of this ConstantsMapping's
    ///  prefixes.
    /// </summary>
    public string RemovePrefix(string s) {
        foreach (string prefix in Prefixes) {
            if (s.Length >= prefix.Length && s.Substring(0,prefix.Length) == prefix) {
                s = s.Substring(prefix.Length);
                break;
            }
        }

        return s;
    }

    public Documentation GetDocumentation(byte b) {
        try {
            return byteToString[b].documentation;
        }
        catch(KeyNotFoundException) {
            return null;
        }
    }
    public Documentation GetDocumentation(string s) {
        try {
            return stringToByte[s].documentation;
        }
        catch(KeyNotFoundException) {
            return null;
        }
    }
}
}
