using System;
using System.Collections.Generic;
using Util;

namespace LynnaLab
{
    // A ValueReference which isn't directly tied to any data; instead it takes getter and setter
    // functions for data modifications.
    // A caveat about using this: if this is used as a layer on top of actual Data values, then if
    // those Data values are changed, the event handlers installed by "AddValueModifiedHandler"
    // won't trigger. They will only trigger if modifications are made through this class.
    public class AbstractIntValueReference : ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Private variables

        Func<int> getter;
        Action<int> setter;
        LockableEvent<ValueModifiedEventArgs> eventHandler = new LockableEvent<ValueModifiedEventArgs>();


        // Constructors

        public AbstractIntValueReference(Project project, string name, Func<int> getter, Action<int> setter, int maxValue, int minValue=0, ValueReferenceType type=ValueReferenceType.Int, bool editable=true, string constantsMappingString=null, string tooltip=null)
        : base(name, type, editable, constantsMappingString) {
            base.Project = project;
            this.getter = getter;
            this.setter = setter;

            base.MaxValue = maxValue;
            base.MinValue = minValue;

            base.Tooltip = tooltip;
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
        }
        public override void SetValue(int i) {
            if (i > MaxValue) {
                log.Warn(string.Format("Tried to set \"{0}\" to {1} (max value is {2})", Name, i, MaxValue));
                i = MaxValue;
            }
            if (i == GetIntValue())
                return;
            setter(i);
            eventHandler.Invoke(this, null);
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
