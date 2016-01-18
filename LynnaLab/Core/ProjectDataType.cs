using System;
using System.Runtime.CompilerServices;

namespace LynnaLab
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
            project.AddDataType(this);
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
