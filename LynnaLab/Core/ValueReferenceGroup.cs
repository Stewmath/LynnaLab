using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    // This class contains a list of ValueReferences and allows you to look them up by name to get
    // or set them.
    public class ValueReferenceGroup {
        IList<ValueReference> valueReferences;


        public Project Project {
            get {
                if (valueReferences.Count == 0)
                    return null;
                return valueReferences[0].Project;
            }
        }


        public ValueReferenceGroup(IList<ValueReference> refs) {
            valueReferences = new List<ValueReference>();
            foreach (var vref in refs) {
                ValueReference copy = vref.Clone();
                valueReferences.Add(copy);
            }
        }

        // Let subclasses set valueReferences manually
        protected ValueReferenceGroup() {}


        // Public

        public IList<ValueReference> GetValueReferences() {
            return valueReferences;
        }
        public ValueReference GetValueReference(string name) {
            foreach (ValueReference r in valueReferences) {
                if (r.Name == name) {
                    return r;
                }
            }
            return null;
        }

        public int GetNumValueReferences() {
            return valueReferences.Count;
        }

        public int GetIndexOf(ValueReference r) {
            int i=0;
            foreach (ValueReference s in valueReferences) {
                if (s.Name == r.Name)
                    return i;
                i++;
            }
            return -1;
        }

        public bool HasValue(string name) {
            foreach (var r in valueReferences)
                if (r.Name == name)
                    return true;
            return false;
        }


        public string GetValue(string name) {
            foreach (var r in valueReferences) {
                if (r.Name == name)
                    return r.GetStringValue();
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }
        public int GetIntValue(string name) {
            foreach (var r in valueReferences) {
                if (r.Name == name)
                    return r.GetIntValue();
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }

        public void SetValue(string name, string value) {
            foreach (var r in valueReferences) {
                if (r.Name == name) {
                    r.SetValue(value);
                    return;
                }
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }
        public void SetValue(string name, int value) {
            foreach (var r in valueReferences) {
                if (r.Name == name) {
                    r.SetValue(value);
                    return;
                }
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }

        public void AddValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler) {
            foreach (var vref in valueReferences)
                vref.AddValueModifiedHandler(handler);
        }
        public void RemoveValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler) {
            foreach (var vref in valueReferences)
                vref.RemoveValueModifiedHandler(handler);
        }


        // Protected

        protected void SetValueReferences(IList<ValueReference> refs) {
            valueReferences = refs;
        }
    }
}
