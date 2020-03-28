using System;
using System.Collections.Generic;

namespace LynnaLab
{
    // A class can implement this to recieve callbacks with an AbstractValueReference.
    // (Currently only for integer values.)
    public interface ValueReferenceHandler {
        int ReferenceHandlerGetValue(string name);
        void ReferenceHandlerSetValue(string name, int value);
    }

    public class AbstractValueReference : ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private ValueReferenceHandler callback;

        // Standard constructor for most DataValueTypes
        public AbstractValueReference(Project p, ValueReferenceHandler callback, string n, DataValueType t, bool editable=true)
        : base(n, t, editable) {
            base.Project = p;
            this.callback = callback;
        }

        public override string GetStringValue() {
            return Wla.ToHex(GetIntValue(), 2);
        }
        public override void SetValue(string s) {
            SetValue(Project.EvalToInt(s));
        }

        public override int GetIntValue() {
            return callback.ReferenceHandlerGetValue(base.Name);
        }

        public override void SetValue(int i) {
            callback.ReferenceHandlerSetValue(base.Name, i);
        }
    }
}
