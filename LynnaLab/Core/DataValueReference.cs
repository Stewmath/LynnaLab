using System;
using System.Collections.Generic;
using Util;
using System.Windows;

namespace LynnaLab
{
    // Enum of types of values that can be had.
    public enum DataValueType {
        String = 0,

        // Use this if the _entire_ value being edited is a half-byte. If you want to edit part of
        // a full byte, use "bytebits" instead.
        HalfByte,

        Byte,
        Word,
        ByteBit,
        ByteBits,
        WordBits,
    }


    // This class provides a way of accessing Data values of various different
    // formats.
    public class DataValueReference : ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Static default value stuff

        public static string[] defaultDataValues = {
            ".",
            "$0",
            "$00",
            "$0000",
            "0",
            "$00",
            "$0000",
        };

        public static int GetMaxValueForType(DataValueType type, int startBit, int endBit) {
            switch(type) {
            case DataValueType.Byte:
                return 0xff;
            case DataValueType.HalfByte:
                return 0xf;
            case DataValueType.Word:
                return 0xffff;
            case DataValueType.ByteBit:
                return 1;
            case DataValueType.ByteBits:
            case DataValueType.WordBits:
                return (1<<(endBit-startBit+1))-1;
            default:
                return 0; // String types
            }
            throw new Exception();
        }


        // Private variables
        int valueIndex;
        int startBit,endBit;
        DataValueType dataType;
        Data _data;
        bool useConstantAlias; // If true, save value as ConstantsMapping string instead of as int

        NewEventWrapper<Data> dataEventWrapper = new NewEventWrapper<Data>();


        // Properties
        public Data Data {
            get { return _data; }
        }


        public DataValueReference(Data data, string name, int index, DataValueType type, int startBit=0, int endBit=0, bool editable=true, string constantsMappingString=null, bool useConstantAlias=false, string tooltip=null)
        : base(name, GetValueType(type), editable, constantsMappingString) {
            this._data = data;
            this.dataType = type;
            this.valueIndex = index;
            this.startBit = startBit;
            this.endBit = endBit;
            this.useConstantAlias = useConstantAlias;

            base.Tooltip = tooltip;
            base.Project = _data.Project;

            MaxValue = GetMaxValueForType(type, startBit, endBit);

            BindEventHandler();
        }

        public DataValueReference(DataValueReference vref) : base(vref) {
            this.dataType = vref.dataType;
            this.valueIndex = vref.valueIndex;
            this.startBit = vref.startBit;
            this.endBit = vref.endBit;
            this._data = vref._data;
            this.useConstantAlias = vref.useConstantAlias;

            BindEventHandler();
        }

        void BindEventHandler() {
            dataEventWrapper.Bind<DataModifiedEventArgs>("DataModifiedEvent", OnDataModified);
            dataEventWrapper.ReplaceEventSource(_data);
        }


        // This is borrowed by StreamValueReference also
        public static ValueReferenceType GetValueType(DataValueType dataType) {
            if (dataType == DataValueType.String)
                return ValueReferenceType.String;
            else if (dataType == DataValueType.HalfByte || dataType == DataValueType.Byte
                    || dataType == DataValueType.Word || dataType == DataValueType.ByteBits
                    || dataType == DataValueType.WordBits)
                return ValueReferenceType.Int;
            else if (dataType == DataValueType.ByteBit)
                return ValueReferenceType.Bool;
            else
                throw new NotImplementedException();
        }


        // Methods

        // NOTE: For the "Bit" DataTypes, this gets the value of the whole string, not just the bits
        // in question! (TODO: can this be fixed?)
        public override string GetStringValue() {
            return Data.GetValue(valueIndex);
        }
        public override int GetIntValue() {
            int intValue = Project.EvalToInt(GetStringValue());
            switch (dataType) {
                case DataValueType.ByteBits:
                case DataValueType.WordBits:
                    {
                        int andValue = (1<<(endBit-startBit+1))-1;
                        return (intValue>>startBit)&andValue;
                    }
                case DataValueType.ByteBit:
                    return (intValue>>startBit)&1;
                default:
                    return intValue;
            }
        }

        // This has the same caveat as "GetStringValue".
        public override void SetValue(string s) {
            Data.SetValue(valueIndex,s);
        }
        public override void SetValue(int i) {
            if (i > MaxValue) {
                log.Warn(string.Format("Tried to set \"{0}\" to {1} (max value is {2})", Name, i, MaxValue));
                i = MaxValue;
            }

            if (useConstantAlias) {
                if (dataType == DataValueType.ByteBits || dataType == DataValueType.WordBits 
                        || dataType == DataValueType.ByteBit) {
                    throw new Exception("Can't use constant alias on this data type");
                }

                SetValue(ConstantsMapping.ByteToString(i));
                return;
            }
            switch(dataType) {
                case DataValueType.HalfByte:
                    SetValue(Wla.ToHalfByte((byte)i));
                    break;
                case DataValueType.Byte:
                default:
                    SetValue(Wla.ToByte((byte)i));
                    break;
                case DataValueType.Word:
                    SetValue(Wla.ToWord(i));
                    break;
                case DataValueType.ByteBits:
                case DataValueType.WordBits:
                    {
                        int andValue = ((1<<(endBit-startBit+1))-1);
                        int value = Project.EvalToInt(GetStringValue()) & (~(andValue<<startBit));
                        value |= ((i&andValue)<<startBit);
                        if (dataType == DataValueType.ByteBits)
                            SetValue(Wla.ToByte((byte)value));
                        else
                            SetValue(Wla.ToWord(value));
                    }
                    break;
                case DataValueType.ByteBit:
                    {
                        int value = Project.EvalToInt(GetStringValue()) & ~(1<<startBit);
                        value |= ((i&1)<<startBit);
                        SetValue(Wla.ToByte((byte)value));
                    }
                    break;
            }
            // Shouldn't need to invoke modifiedEvent here because a handler is installed on the
            // underlying data.
        }

        public override void Initialize() {
            if (valueIndex >= Data.GetNumValues())
                Data.SetNumValues(valueIndex+1, "$00");
            Data.SetValue(valueIndex, defaultDataValues[(int)dataType]);
            RaiseModifiedEvent(null);
        }

        public override ValueReference Clone() {
            return new DataValueReference(this);
        }


        void OnDataModified(object sender, DataModifiedEventArgs args) {
            if (sender != _data)
                throw new Exception("DataValueReference.OnDataModified: Wrong data object?");
            else if (args.ValueIndex == valueIndex) { // NOTE: Doesn't check for "size change" events (value -1).
                RaiseModifiedEvent(null);
            }
        }
    }
}
