using System;
using System.Collections.Generic;

namespace LynnaLab
{
    public enum ValueReferenceType {
        String = 0,
        Int,
        Bool
    }

    // This is a stub for now
    public class ValueModifiedEventArgs {
    }


    // This class provides a way of accessing Data values of various different
    // formats.
    public abstract class ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Private variables

        private ConstantsMapping constantsMapping;
        Project _project;


        // To be set by the subclasses

        public Project Project {
            get { return _project; }
            protected set {
                _project = value;
                if (_project != null && ConstantsMappingString != null) {
                    constantsMapping = (ConstantsMapping)typeof(Project).GetField(ConstantsMappingString)
                        .GetValue(Project);
                    Documentation = constantsMapping.OverallDocumentation;
                    Documentation.Name = "Field: " + Name;
                }
            }
        }
        public int MaxValue { get; protected set; }
        public int MinValue { get; protected set; }
        public ValueReferenceType ValueType { get; protected set; }


        // Other properties

        public string Name { get; protected set; }
        public bool Editable { get; set; }
        public string Tooltip { get; protected set; }

        public string ConstantsMappingString { get; private set; }
        public ConstantsMapping ConstantsMapping {
            get { return constantsMapping; }
        }

        // This documentation tends to change based on what the current value is...
        public Documentation Documentation { get; set; }


        // Constructors

        // Standard constructor for most ValueReferenceTypes
        public ValueReference(string name, ValueReferenceType type, bool editable, string constantsMappingString) {
            ValueType = type;
            Name = name;
            Editable = editable;
            this.ConstantsMappingString = constantsMappingString;
        }

        public ValueReference(ValueReference r) {
            _project = r._project;
            MaxValue = r.MaxValue;
            MinValue = r.MinValue;
            ValueType = r.ValueType;
            Name = r.Name;
            Editable = r.Editable;
            ConstantsMappingString = r.ConstantsMappingString;
            constantsMapping = r.constantsMapping;
            Documentation = r.Documentation;
            Tooltip = r.Tooltip;
        }


        // Methods

        public abstract string GetStringValue();
        public abstract int GetIntValue();
        public abstract void SetValue(string s);
        public abstract void SetValue(int i);

        // TODO: Replace these with a normal event object
        public abstract void AddValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler);
        public abstract void RemoveValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler);

        // Sets the value to its default.
        public abstract void Initialize();

        public abstract ValueReference Clone();

        /// <summary>
        ///  Returns a field from documentation (ie. "@desc{An interaction}").
        /// </summary>
        public string GetDocumentationField(string name) {
            if (Documentation == null)
                return null;
            return Documentation.GetField(name);
        }
    }
}
