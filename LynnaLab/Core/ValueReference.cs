using System;
using System.Collections.Generic;

namespace LynnaLab
{
    // Enum of types of values that can be had.
    public enum DataValueType {
        String=0,
        HalfByte,
        Byte,
        Word,
        ByteBit,
        ByteBits,
        WordBits,
        ObjectPointer,
        WarpDestIndex,
    }

    // This class provides a way of accessing Data values of various different
    // formats.
    public abstract class ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected string constantsMappingString;
        protected ConstantsMapping constantsMapping;
        protected Documentation _documentation;

        protected int startBit,endBit;


        protected Project Project { get; set; }

        public virtual Data Data { get { return null; } } // Only defined in some subclasses

        public DataValueType ValueType {get; set;}
        public string Name {get; set;}
        public bool Editable {get; set;}

        public int MaxValue { // For integer-based ones
            get {
                switch(ValueType) {
                    case DataValueType.Byte:
                        return 0xff;
                    case DataValueType.Word:
                        return 0xffff;
                    case DataValueType.ByteBit:
                        return 1;
                    case DataValueType.ByteBits:
                    case DataValueType.WordBits:
                        return (1<<(endBit-startBit+1))-1;
                    default:
                        return 0;
                }
            }
        }
        public ConstantsMapping ConstantsMapping {
            get { return constantsMapping; }
        }

        // This documentation tends to change based on what the current value is...
        public Documentation Documentation {
            get {
                return _documentation;
            }
            set {
                _documentation = value;
            }
        }

        // Standard constructor for most DataValueTypes
        public ValueReference(string n, DataValueType t, bool editable=true) {
            ValueType = t;
            Name = n;
            Editable = editable;
            if (t == DataValueType.ByteBits || t == DataValueType.ByteBit || t == DataValueType.WordBits)
                throw new Exception("Wrong constructor");
        }
        // Constructor for DataValueType.ByteBits
        public ValueReference(string n, int startBit, int endBit, DataValueType t, bool editable=true) {
            ValueType = t;
            Name = n;
            this.startBit = startBit;
            this.endBit = endBit;
            Editable = editable;
        }
        // Constructor which takes a ConstantsMapping, affecting the interface
        // that will be used
        public ValueReference(string n, int startBit, int endBit, DataValueType t, bool editable, string constantsMappingString) {
            ValueType = t;
            Name = n;
            this.startBit = startBit;
            this.endBit = endBit;
            Editable = editable;
            this.constantsMappingString = constantsMappingString;
        }
        // Same as above but without start/endBit
        public ValueReference(string n, DataValueType t, bool editable, string constantsMappingString) {
            ValueType = t;
            Name = n;
            Editable = editable;
            this.constantsMappingString = constantsMappingString;
            Console.WriteLine("Mapping string " + this.constantsMappingString);
            if (t == DataValueType.ByteBits || t == DataValueType.ByteBit || t == DataValueType.WordBits)
                throw new Exception("Wrong constructor");
        }

        public ValueReference(ValueReference r) {
            ValueType = r.ValueType;
            Name = r.Name;
            startBit = r.startBit;
            endBit = r.endBit;
            Editable = r.Editable;
            constantsMappingString = r.constantsMappingString;
            constantsMapping = r.constantsMapping;
            _documentation = r._documentation;
        }


        // NOTE: For the "Bit" DataTypes, this gets the value of the whole string, not just the bits
        // in question!
        public abstract string GetStringValue();

        // This correctly handles the "Bit" DataType.
        public virtual int GetIntValue() {
            int intValue = Project.EvalToInt(GetStringValue());
            switch(ValueType) {
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
        public abstract void SetValue(string s);

        public virtual void SetValue(int i) {
            switch(ValueType) {
                case DataValueType.HalfByte:
                    SetValue(Wla.ToHalfByte((byte)i));
                    break;
                case DataValueType.Byte:
                case DataValueType.WarpDestIndex:
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
                        if (ValueType == DataValueType.ByteBits)
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
        }

        /// <summary>
        ///  Returns a field from documentation (ie. "@desc{An interaction}").
        /// </summary>
        public string GetDocumentationField(string name) {
            if (_documentation == null)
                return null;
            return _documentation.GetField(name);
        }
    }
}
