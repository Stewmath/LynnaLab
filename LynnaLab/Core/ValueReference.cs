using System;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{

    // Enum of types of values that can be had.
    public enum DataValueType {
        String=0,
        Byte,
        Word,
        InteractionPointer,
    }

    // This class provides a way of accessing Data values of various different
    // formats.
    public class ValueReference {

        // Static default value stuff

        public static string[] defaultDataValues = {
            ".",
            "$00",
            "$0000",
            "interactionData4000",
        };

        public static string[] GetDefaultValues(DataValueType[] valueList) {
            string[] ret = new string[valueList.Length];
            for (int i=0;i<valueList.Length;i++) {
                ret[i] = defaultDataValues[(int)valueList[i]];
            }
            return ret;
        }
        public static string[] GetDefaultValues(IList<ValueReference> valueList) {
            string[] ret = new string[valueList.Count];
            for (int i=0;i<valueList.Count;i++) {
                ret[i] = defaultDataValues[(int)valueList[i].ValueType];
            }
            return ret;
        }

        //////////////// 

        Data data;
        int valueIndex;

        public DataValueType ValueType {get; set;}
        public string Name {get; set;}

        public ValueReference(string n, int index, DataValueType t) {
            valueIndex = index;
            ValueType = t;
            Name = n;
        }

        public ValueReference(ValueReference r) {
            data = r.data;
            valueIndex = r.valueIndex;
            ValueType = r.ValueType;
            Name = r.Name;
        }

        public void SetData(Data d) {
            data = d;
        }

        public virtual string GetStringValue() {
            return data.GetValue(valueIndex);
        }
        public virtual int GetIntValue() {
            return data.GetIntValue(valueIndex);
        }
        public virtual void SetValue(string s) {
            switch(ValueType) {
                case DataValueType.String:
                default:
                    data.SetValue(valueIndex,s);
                    break;
            }
        }
        public virtual void SetValue(int i) {
            switch(ValueType) {
                case DataValueType.Byte:
                    data.SetByteValue(valueIndex,(byte)i);
                    break;
                case DataValueType.Word:
                    data.SetWordValue(valueIndex,i);
                    break;
                default:
                    throw new Exception("Tried to use an integer to set a string-based value");
            }
        }
    }
}
