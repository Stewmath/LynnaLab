using System;
using System.Collections.Generic;

namespace LynnaLab
{
    // In order to use this properly, the Handler class must implement (or override if inheriting
    // from "Data") the "GetIntValue" and "SetValue(string, int)" functions.
    public class AbstractIntValueReference : ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Private variables

        Func<int> getter;
        Action<int> setter;
        LockableEvent<ValueModifiedEventArgs> eventHandler = new LockableEvent<ValueModifiedEventArgs>();


        // Constructors

        public AbstractIntValueReference(Project project, string name, ValueReferenceType type, Func<int> getter, Action<int> setter, int maxValue, bool editable=true, string constantsMappingString=null)
        : base(name, type, editable, constantsMappingString) {
            base.Project = project;
            this.getter = getter;
            this.setter = setter;
            base.MaxValue = maxValue;
        }

        public AbstractIntValueReference(AbstractIntValueReference r)
        : base(r) {
            this.getter = r.getter;
            this.setter = r.setter;
        }

        public AbstractIntValueReference(ValueReference r, Func<int> getter = null, Action<int> setter = null)
        : base(r) {
            this.getter = getter;
            this.setter = setter;

            if (this.getter == null)
                this.getter = r.GetIntValue;
            if (this.setter == null)
                this.setter = r.SetValue;
        }


        // Methods

        public override string GetStringValue() {
            return Wla.ToHex(GetIntValue(), 2);
        }
        public override int GetIntValue() {
            return getter();
        }
        public override void SetValue(string s) {
            SetValue(Project.EvalToInt(s));
            eventHandler.Invoke(this, null);
        }
        public override void SetValue(int i) {
            setter(i);
        }

        public override void AddValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler) {
            eventHandler += handler;
        }
        public override void RemoveValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler) {
            eventHandler -= handler;
        }

        public override void Initialize() {
            throw new NotImplementedException();
        }

        public override ValueReference Clone() {
            return new AbstractIntValueReference(this);
        }
    }
}
