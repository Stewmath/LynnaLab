using System;
using System.Collections.Generic;
using Util;

namespace LynnaLib
{
    // Similar to AbstractIntValueReference
    public class AbstractBoolValueReference : AbstractIntValueReference
    {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // Constructors

        public AbstractBoolValueReference(Project project, string name, Func<bool> getter, Action<bool> setter, ValueReferenceType type = ValueReferenceType.Bool, bool editable = true, string tooltip = null)
        : base(
                project,
                name,
                getter: () => getter() ? 1 : 0,
                setter: (v) => setter(v != 0 ? true : false),
                maxValue: 1,
                type: type,
                editable: editable,
                constantsMappingString: null,
                tooltip: tooltip)
        { }

        public AbstractBoolValueReference(AbstractBoolValueReference r)
        : base(r) { }

        public AbstractBoolValueReference(ValueReference r, Func<bool> getter = null, Action<bool> setter = null)
        : base(r, () => getter() ? 1 : 0, (v) => setter(v != 0 ? true : false)) { }


        // Methods

        public override ValueReference Clone()
        {
            return new AbstractBoolValueReference(this);
        }
    }
}
