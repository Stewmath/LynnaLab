using System;
using System.Runtime.CompilerServices;

namespace LynnaLab
{
    public abstract class ProjectDataType {
        public Project Project
        {
            get { return project; }
        }
        public int Identifier
        {
            get { return identifier; }
        }
        public bool Modified { get; set; }

        Project project;
        int identifier;

        public ProjectDataType(Project p, int identifier) {
            project = p;
            this.identifier = identifier;
            project.AddDataType(this);
        }

        public string GetIdentifier() {
            return this.GetType().Name + "_" + identifier;
        }

        public virtual void Save() {}
    }

    public abstract class ProjectIndexedDataType : ProjectDataType {

        public int Index
        {
            get { return Identifier; }
        }

        public ProjectIndexedDataType(Project p, int index) : base(p, index) {
        }
    }
}
