using System;
using System.Collections.Generic;
using Util;

namespace LynnaLib
{
    // This class contains a list of ValueReferences and allows you to look them up by name to get
    // or set them.
    public class ValueReferenceGroup
    {
        IList<ValueReference> valueReferences;
        LockableEvent<ValueModifiedEventArgs> lockableModifiedEvent = new LockableEvent<ValueModifiedEventArgs>();


        /// Constructor to let subclasses set valueReferences manually
        protected ValueReferenceGroup()
        {
            lockableModifiedEvent += (sender, args) => ModifiedEvent?.Invoke(sender, args);
        }

        public ValueReferenceGroup(IList<ValueReference> refs) : this()
        {
            SetValueReferences(refs);
        }


        public event EventHandler<ValueModifiedEventArgs> ModifiedEvent;



        // Properties

        public Project Project
        {
            get
            {
                if (valueReferences.Count == 0)
                    return null;
                return valueReferences[0].Project;
            }
        }

        public int Count
        {
            get { return valueReferences.Count; }
        }


        // Indexers

        public ValueReference this[int i]
        {
            get { return valueReferences[i]; }
        }
        public ValueReference this[string name]
        {
            get
            {
                foreach (var r in valueReferences)
                {
                    if (r.Name == name)
                        return r;
                }
                throw new ArgumentException("ValueReference \"" + name + "\" isn't in this group.");
            }
        }


        // Public methods

        public IList<ValueReference> GetValueReferences()
        {
            return valueReferences;
        }
        public ValueReference GetValueReference(string name)
        {
            foreach (ValueReference r in valueReferences)
            {
                if (r.Name == name)
                {
                    return r;
                }
            }
            return null;
        }

        public int GetNumValueReferences()
        { // TODO: replace with "Count" property
            return valueReferences.Count;
        }

        public int GetIndexOf(ValueReference r)
        {
            int i = 0;
            foreach (ValueReference s in valueReferences)
            {
                if (s.Name == r.Name)
                    return i;
                i++;
            }
            return -1;
        }

        public bool HasValue(string name)
        {
            foreach (var r in valueReferences)
                if (r.Name == name)
                    return true;
            return false;
        }


        public string GetValue(string name)
        {
            foreach (var r in valueReferences)
            {
                if (r.Name == name)
                    return r.GetStringValue();
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }
        public int GetIntValue(string name)
        {
            foreach (var r in valueReferences)
            {
                if (r.Name == name)
                    return r.GetIntValue();
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }

        public void SetValue(string name, string value)
        {
            foreach (var r in valueReferences)
            {
                if (r.Name == name)
                {
                    r.SetValue(value);
                    return;
                }
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }
        public void SetValue(string name, int value)
        {
            foreach (var r in valueReferences)
            {
                if (r.Name == name)
                {
                    r.SetValue(value);
                    return;
                }
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }

        // TODO: remove these, use the public event instead
        public void AddValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler)
        {
            ModifiedEvent += handler;
        }
        public void RemoveValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler)
        {
            ModifiedEvent -= handler;
        }

        /// Call this to prevent events from firing until EndAtomicOperation is called.
        public void BeginAtomicOperation()
        {
            lockableModifiedEvent.Lock();
            // TODO: Would be ideal if this also locked events for the ValueReferences themselves
        }

        public void EndAtomicOperation()
        {
            lockableModifiedEvent.Unlock();
        }

        public void CopyFrom(ValueReferenceGroup vrg)
        {
            BeginAtomicOperation();

            foreach (var vr in valueReferences)
            {
                vr.SetValue(vrg.GetValue(vr.Name));
            }

            EndAtomicOperation();
        }


        // Protected

        protected void SetValueReferences(IList<ValueReference> refs)
        {
            if (valueReferences != null)
                throw new Exception();

            valueReferences = new List<ValueReference>();
            foreach (var vref in refs)
            {
                ValueReference copy = vref.Clone();
                valueReferences.Add(copy);

                copy.AddValueModifiedHandler((sender, args) => lockableModifiedEvent?.Invoke(sender, args));
            }
        }
    }
}
