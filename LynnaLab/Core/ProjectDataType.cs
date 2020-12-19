using System;
using System.Runtime.CompilerServices;

namespace LynnaLib
{
    public abstract class ProjectDataType {
        Project project;
        string identifier;

        public Project Project
        {
            get { return project; }
        }
        public string Identifier
        {
            get { return identifier; }
        }
        public bool Modified { get; set; }

        internal ProjectDataType(Project p, string identifier) {
            project = p;
            this.identifier = identifier;
            // TODO: Somehow assert that this is always instantiated through the Project class (to
            // ensure that duplicate instances for the same data are not created)
        }
        internal ProjectDataType(Project p, int i)
            : this(p, i.ToString()) {
        }

        public string GetIdentifier() {
            return this.GetType().Name + "_" + identifier;
        }

        public virtual void Save() {}
    }

    public abstract class ProjectIndexedDataType : ProjectDataType {
        readonly int _index;

        public int Index
        {
            get { return _index; }
        }

        internal ProjectIndexedDataType(Project p, int index)
            : base(p, index.ToString()) {
            _index = index;
        }
    }
}
