using System;
using System.Collections.Generic;

namespace LynnaLib
{

    /// <summary>
    ///  This provides an interface that the ValueReferenceEditor can use to create infoboxes.
    ///
    ///  Can be constructed either with manual data, or with a DocumentationFileComponent.
    ///
    ///  Though this roughly corresponds to DocumentationFileComponent, the user should generally filter
    ///  out exactly what they want before passing this to DocumentationDialog.
    ///
    ///  This contains a list of fields, which are interpreted as a list of possible values for whatever
    ///  this is documenting (referred to by the "Title").
    /// </summary>
    public class Documentation
    {
        Dictionary<string, string> _fieldDict;
        ISet<string> _fieldKeys; // Maintained separately from documentationParams to preserve original case

        public string Name { get; set; }
        public string KeyName { get; set; } = "Key";
        public string Description { get; set; }

        public ICollection<string> Keys
        {
            get
            {
                return _fieldKeys;
            }
        }


        /// <summary>
        ///  Build documentation with manual data
        /// </summary>
        public Documentation(string name, string desc, ICollection<Tuple<string, string>> _values)
        {
            Name = name;
            _fieldDict = new Dictionary<string, string>();
            _fieldKeys = new SortedSet<string>();

            if (_values != null)
            {
                foreach (Tuple<string, string> tup in _values)
                {
                    _fieldDict[tup.Item1.ToLower()] = tup.Item2;
                    _fieldKeys.Add(tup.Item1);
                }
            }

            Description = desc;
        }

        public Documentation(DocumentationFileComponent fileComponent, string name)
        {
            Name = name;
            _fieldDict = new Dictionary<string, string>();

            foreach (string key in fileComponent.Keys)
            {
                if (key == "desc")
                    Description = fileComponent.GetField(key);
                else
                    _fieldDict[key.ToLower()] = fileComponent.GetField(key);
            }

            _fieldKeys = new SortedSet<string>(fileComponent.Keys);
            _fieldKeys.Remove("desc");
        }

        public Documentation(Documentation d)
        {
            _fieldDict = new Dictionary<string, string>(d._fieldDict);
            _fieldKeys = new SortedSet<string>(d._fieldKeys);
            Description = d.Description;
            Name = d.Name;
            KeyName = d.KeyName;
        }


        public string GetField(string field)
        {
            field = field.ToLower();
            try
            {
                return _fieldDict[field];
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        public void SetField(string field, string value)
        {
            _fieldKeys.Add(field);
            _fieldDict[field.ToLower()] = value;
        }

        public void RemoveField(string field)
        {
            _fieldKeys.Remove(field);
            _fieldDict.Remove(field.ToLower());
        }


        public Documentation GetSubDocumentation(string field)
        {
            string value = GetField(field);
            if (value == null)
                return null;

            Documentation newDoc = new Documentation(this);

            List<string> newKeys = new List<string>();
            Dictionary<string, string> newFields = DocumentationFileComponent.ParseDoc(value, newKeys);

            foreach (string key in newKeys)
            {
                newDoc._fieldKeys.Add(key);
                newDoc._fieldDict[key.ToLower()] = newFields[key];
            }

            newDoc.Name = Name + " (" + field + ")";

            return newDoc;
        }
    }

}
