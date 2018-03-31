using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    // This class contains a list of ValueReferences and allows you to look them up by name to get
    // or set them.
    public class ValueReferenceGroup {
        IList<ValueReference> valueReferences;

        public ValueReferenceGroup(IList<ValueReference> refs) {
            valueReferences = new List<ValueReference>();
            foreach (var vref in refs) {
                ValueReference copy =
                    (ValueReference)Activator.CreateInstance(vref.GetType(), new object[] { vref });
                valueReferences.Add(copy);
            }
        }

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

        // This function might not work because it's making copies of the
        // ValueReferences passed to it in the constructor?
        public int GetIndexOf(ValueReference r) {
            return valueReferences.IndexOf(r);
        }

        public string GetStringValue(string name) {
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

        public void SetValue(string name, string val) {
            foreach (var r in valueReferences) {
                if (r.Name == name) {
                    r.SetValue(val);
                    return;
                }
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }
        public void SetValue(string name, int val) {
            foreach (var r in valueReferences) {
                if (r.Name == name) {
                    r.SetValue(val);
                    return;
                }
            }
            throw new InvalidLookupException("Couldn't find ValueReference corresponding to \"" + name + "\".");
        }
    }
}
