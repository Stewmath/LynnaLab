using System;
using System.Runtime.CompilerServices;

namespace LynnaLab
{
    public abstract class ProjectDataType {
        public Project Project
        {
            get { return project; }
        }
        public string Identifier
        {
            get { return identifier; }
        }
        public bool Modified { get; set; }

        Project project;
        string identifier;

        public ProjectDataType(Project p, string identifier) {
            project = p;
            this.identifier = identifier;
            project.AddDataType(this);
        }
        public ProjectDataType(Project p, int i)
            : this(p, i.ToString()) {
        }

        public string GetIdentifier() {
            return this.GetType().Name + "_" + identifier;
        }

        public virtual void Save() {}
    }

    public abstract class ProjectIndexedDataType : ProjectDataType {

        public int Index
        {
            get { return _index; }
        }

        int _index;

        public ProjectIndexedDataType(Project p, int index)
            : base(p, index.ToString()) {
            _index = index;
        }
    }
}
