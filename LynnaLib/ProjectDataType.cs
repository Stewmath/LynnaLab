using System;
using System.Runtime.CompilerServices;

namespace LynnaLib
{
    public abstract class ProjectDataType
    {
        Project project;
        string identifier;

        public Project Project
        {
            get { return project; }
        }
        /// <summary>
        /// Unique identifier (not including the type name)
        /// </summary>
        public string Identifier
        {
            get { return identifier; }
        }

        /// <summary>
        /// Full identifier including type
        /// </summary>
        public string FullIdentifier
        {
            get { return Project.GetFullIdentifier(GetType(), Identifier); }
        }

        protected ProjectDataType(Project p, string identifier)
        {
            project = p;
            this.identifier = identifier;

            Project.AddDataType(GetType(), this);
        }
    }

    public abstract class ProjectIndexedDataType : ProjectDataType
    {
        readonly int _index;

        public int Index
        {
            get { return _index; }
        }

        internal ProjectIndexedDataType(Project p, int index)
            : base(p, index.ToString())
        {
            _index = index;
        }
    }
}
