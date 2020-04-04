using System;
using System.Collections.Generic;

namespace LynnaLab
{
    // In order to use this properly, the Handler class must implement (or override if inheriting
    // from "Data") the "GetIntValue" and "SetValue(string, int)" functions.
    public class AbstractIntValueReference : ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Standard constructor for most DataValueTypes
        public AbstractIntValueReference(ValueReferenceHandler handler, string n, DataValueType t, bool editable=true, string constantsMappingString=null)
        : base(n, t, editable, constantsMappingString) {
            SetHandler(handler);
        }

        public AbstractIntValueReference(AbstractIntValueReference r)
        : base(r) {
        }

        public AbstractIntValueReference(ValueReference r, ValueReferenceHandler handler)
        : base(r) {
            SetHandler(handler);
        }

        public override string GetStringValue() {
            return Wla.ToByte((byte)GetIntValue());
        }
        public override int GetIntValue() {
            return Handler.GetIntValue(Name);
        }
        public override void SetValue(string s) {
            SetValue(Project.EvalToInt(s));
        }
        public override void SetValue(int i) {
            Handler.SetValue(Name, i);
        }

        public override void Initialize() {
            throw new NotImplementedException();
        }
    }
}
