using System;
using System.Collections.Generic;

namespace LynnaLab {

/// <summary>
///  This provides an interface that the ValueReferenceEditor can use to create infoboxes.
/// </summary>
public class Documentation {
    List<Tuple<string,string>> _valueList;


    public string Description { get; set; }
    public string Name { get; set; }
    public IList<Tuple<string,string>> ValueList {
        get {
            return _valueList;
        }
    }

    public Documentation(string name, string desc, IList<Tuple<string,string>> _values) {
        Name = name;
        Description = desc;
        if (_values != null)
            _valueList = new List<Tuple<string,string>>(_values);
    }
}

}
