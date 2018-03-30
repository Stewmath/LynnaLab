using System;
using System.Collections.Generic;

namespace LynnaLab {

/// <summary>
///  This provides an interface that the ValueReferenceEditor can use to create infoboxes.
///
///  Can be constructed either with manual data, or with a DocumentationFileComponent.
/// </summary>
public class Documentation {
    List<Tuple<string,string>> _valueList;
    DocumentationFileComponent _fileComponent;


    public string Description { get; set; }
    public string Name { get; set; }
    public IList<Tuple<string,string>> ValueList {
        get {
            return _valueList;
        }
    }

    // This may be null (depending what constructor was used).
    public DocumentationFileComponent FileComponent {
        get { return _fileComponent; }
    }

    public bool UsedFileComponentConstructor {
        get { return _fileComponent != null; }
    }

    /// <summary>
    ///  Build documentation with manual data
    /// </summary>
    public Documentation(string name, string desc, IList<Tuple<string,string>> _values) {
        Name = name;
        Description = desc;
        if (_values != null)
            _valueList = new List<Tuple<string,string>>(_values);
    }

    /// <summary>
    ///  Build documentation with a raw documentation segment
    /// </summary>
    public Documentation(string name, DocumentationFileComponent fileComponent, string field) {
        Name = name;
        Description = fileComponent.GetDocumentationField("desc");

        _fileComponent = fileComponent;
        var values = fileComponent.GetDocumentationFieldSubdivisions(field);
        if (values != null)
            _valueList = new List<Tuple<string,string>>(values);
    }


    /// <summary>
    ///  This is only valid if the "DescriptionFileComponent" contructor was used.
    /// </summary>
    public string GetDocumentationField(string field) {
        return _fileComponent.GetDocumentationField(field);
    }

    /// <summary>
    ///  This is only valid if the "DescriptionFileComponent" contructor was used.
    /// </summary>
    public IList<Tuple<string,string>> GetDocumentationFieldSubdivisions(string field) {
        return _fileComponent.GetDocumentationFieldSubdivisions(field);
    }
}

}
