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


    // This is primarily used by the Data class to handle modifications based on string names.
    public interface ValueReferenceHandler {
        Project Project { get; }

        string GetValue(string name);
        int GetIntValue(string name);

        void SetValue(string name, string value);
        void SetValue(string name, int value);

        void AddValueModifiedHandler(EventHandler handler);
        void RemoveValueModifiedHandler(EventHandler handler);
    }


    // This class provides a way of accessing Data values of various different
    // formats.
    public abstract class ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        private Project project;
        private ValueReferenceHandler handler;


        protected string constantsMappingString;
        protected ConstantsMapping constantsMapping;
        protected Documentation _documentation;

        protected int startBit,endBit;


        protected Project Project {
            get {
                return handler?.Project;
            }
        }

        public ValueReferenceHandler Handler {
            get { return handler; }
        }

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
        public ValueReference(string n, DataValueType t, bool editable=true, string constantsMappingString=null) {
            ValueType = t;
            Name = n;
            Editable = editable;
            this.constantsMappingString = constantsMappingString;
        }
        // Constructor for DataValueType.ByteBits (TODO: make startBit and endBit optional
        // parameters in above constructor)
        public ValueReference(string n, int startBit, int endBit, DataValueType t, bool editable=true, string constantsMappingString=null) {
            ValueType = t;
            Name = n;
            this.startBit = startBit;
            this.endBit = endBit;
            Editable = editable;
            this.constantsMappingString = constantsMappingString;
        }

        public ValueReference(ValueReference r) {
            handler = r.handler;
            ValueType = r.ValueType;
            Name = r.Name;
            startBit = r.startBit;
            endBit = r.endBit;
            Editable = r.Editable;
            constantsMappingString = r.constantsMappingString;
            constantsMapping = r.constantsMapping;
            _documentation = r._documentation;
        }

        public void SetHandler(ValueReferenceHandler handler) {
            this.handler = handler;
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

        // Sets the value to its default.
        public abstract void Initialize();

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
