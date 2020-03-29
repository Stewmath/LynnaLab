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

        // TODO: remove this
        public static void InitializeDataValues(Data data, IList<ValueReference> refs) {
            foreach (DataValueReference r in refs) {
                r.SetHandler(data);
                r.Initialize();
            }
        }


        // TODO: Get rid of this?
        public Data Data { get { return Handler as Data; } } // Only defined in some subclasses


        protected int valueIndex;

        // Standard constructor for most DataValueTypes
        public DataValueReference(string n, int index, DataValueType t, bool editable=true, string constantsMappingString=null)
        : base(n, t, editable, constantsMappingString) {
            valueIndex = index;
        }
        // Constructor for DataValueType.ByteBits
        public DataValueReference(string n, int index, int startBit, int endBit, DataValueType t, bool editable=true, string constantsMappingString=null)
        : base(n, startBit, endBit, t, editable, constantsMappingString) {
            valueIndex = index;
        }

        public DataValueReference(DataValueReference r)
        : base(r) {
            valueIndex = r.valueIndex;
        }

        public override string GetStringValue() {
            return Data.GetValue(valueIndex);
        }
        public override void SetValue(string s) {
            switch(ValueType) {
                case DataValueType.String:
                default:
                    Data.SetValue(valueIndex,s);
                    break;
            }
        }

        public override void Initialize() {
            if (valueIndex <= Data.GetNumValues())
                Data.SetNumValues(valueIndex+1);
            Data.SetValue(valueIndex, defaultDataValues[(int)ValueType]);
        }
    }
}
