using System;
using System.Collections.Generic;

namespace LynnaLab
{
    // This class provides a way of accessing Data values of various different
    // formats.
    public class DataValueReference : ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Static default value stuff

        public static string[] defaultDataValues = {
            ".",
            "$0",
            "$00",
            "$0000",
            "0",
            "$00",
            "objectData4000",
            "$00",
        };

        public static void InitializeDataValues(Data data, IList<DataValueReference> refs) {
            int numValues = 0;
            foreach (DataValueReference r in refs) {
                if (r.valueIndex+1 > numValues)
                    numValues = r.valueIndex+1;
            }

            data.SetNumValues(numValues);

            foreach (DataValueReference r in refs) {
                data.SetValue(r.valueIndex, defaultDataValues[(int)r.ValueType]);
            }
        }


        Data data;
        protected int valueIndex;

        public override Data Data { get { return data; } }

        // Standard constructor for most DataValueTypes
        public DataValueReference(string n, int index, DataValueType t, bool editable=true)
        : base(n, t, editable) {
            valueIndex = index;
        }
        // Constructor for DataValueType.ByteBits
        public DataValueReference(string n, int index, int startBit, int endBit, DataValueType t, bool editable=true)
        : base(n, startBit, endBit, t, editable) {
            valueIndex = index;
        }
        // Constructor which takes a ConstantsMapping, affecting the interface
        // that will be used
        public DataValueReference(string n, int index, int startBit, int endBit, DataValueType t, bool editable, string constantsMappingString)
        : base(n, startBit, endBit, t, editable, constantsMappingString) {
            valueIndex = index;
        }
        // Same as above but without start/endBit
        public DataValueReference(string n, int index, DataValueType t, bool editable, string constantsMappingString)
        : base(n, t, editable, constantsMappingString) {
            valueIndex = index;
        }

        public DataValueReference(DataValueReference r)
        : base(r) {
            data = r.data;
            valueIndex = r.valueIndex;
        }

        public void SetData(Data d) {
            data = d;
            Project = d?.Project;
            if (data != null && constantsMappingString != null) {
                constantsMapping = (ConstantsMapping)typeof(Project).GetField(constantsMappingString).GetValue(data.Project);
                if (_documentation == null) {
                    _documentation = constantsMapping.OverallDocumentation;
                    _documentation.Name = "Field: " + Name;
                }
            }
        }

        public override string GetStringValue() {
            return data.GetValue(valueIndex);
        }
        public override void SetValue(string s) {
            switch(ValueType) {
                case DataValueType.String:
                default:
                    data.SetValue(valueIndex,s);
                    break;
            }
        }
    }
}
