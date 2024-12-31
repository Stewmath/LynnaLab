namespace LynnaLib;

// Enum of types of values that can be had.
public enum DataValueType
{
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
public class DataValueReference : ValueReference
{
    // ================================================================================
    // Static things
    // ================================================================================

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

    public static int GetMaxValueForType(DataValueType type, int startBit, int endBit)
    {
        switch (type)
        {
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
                return (1 << (endBit - startBit + 1)) - 1;
            default:
                return 0; // String types
        }
        throw new Exception();
    }

    // This is borrowed by StreamValueReference also
    public static ValueReferenceType GetValueType(DataValueType dataType)
    {
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



    // ================================================================================
    // Constuctors
    // ================================================================================

    /// <param="constantsMappingString">
    /// If specified, will use ContantsMapping aliases when updating the value of the data.
    /// </param>
    public DataValueReference(Data data, int index, DataValueType type,
        int startBit = 0,
        int endBit = 0,
        int maxValue = -1,
        string constantsMappingString = null)
    : base(data.Project, GetValueType(type), constantsMappingString)
    {
        this._data = data;
        this.dataType = type;
        this.valueIndex = index;
        this.startBit = startBit;
        this.endBit = endBit;

        if (maxValue == -1)
            MaxValue = DataValueReference.GetMaxValueForType(type, startBit, endBit);
        else
            MaxValue = maxValue;

        BindEventHandler();
    }

    void BindEventHandler()
    {
        dataEventWrapper.Bind<DataModifiedEventArgs>("ModifiedEvent", OnDataModified);
        dataEventWrapper.ReplaceEventSource(_data);
    }



    // ================================================================================
    // Variables
    // ================================================================================

    int valueIndex;
    int startBit, endBit;
    DataValueType dataType;
    Data _data;

    EventWrapper<Data> dataEventWrapper = new EventWrapper<Data>();


    // ================================================================================
    // Properties
    // ================================================================================

    public Data Data
    {
        get { return _data; }
    }


    // ================================================================================
    // Public methods
    // ================================================================================


    // NOTE: For the "Bit" DataTypes, this gets the value of the whole string, not just the bits
    // in question! (TODO: can this be fixed?)
    public override string GetStringValue()
    {
        return Data.GetValue(valueIndex);
    }
    public override int GetIntValue()
    {
        int intValue = Project.EvalToInt(GetStringValue());
        switch (dataType)
        {
            case DataValueType.ByteBits:
            case DataValueType.WordBits:
                {
                    int andValue = (1 << (endBit - startBit + 1)) - 1;
                    return (intValue >> startBit) & andValue;
                }
            case DataValueType.ByteBit:
                return (intValue >> startBit) & 1;
            default:
                return intValue;
        }
    }

    // This has the same caveat as "GetStringValue".
    public override void SetValue(string s)
    {
        base.BeginTransaction();
        Data.SetValue(valueIndex, s);
        base.EndTransaction();
    }
    public override void SetValue(int i)
    {
        base.BeginTransaction();
        if (i > MaxValue)
        {
            log.Warn(string.Format("Tried to set value  to {0} (max value is {1})", i, MaxValue));
            i = MaxValue;
        }

        if (ConstantsMapping != null)
        {
            // Use a constants mapping alias for the value if supported by the data type.
            if (!(dataType == DataValueType.ByteBits || dataType == DataValueType.WordBits || dataType == DataValueType.ByteBit))
            {
                SetValue(ConstantsMapping.ByteToString(i));
                base.EndTransaction();
                return;
            }
        }

        switch (dataType)
        {
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
                    int andValue = ((1 << (endBit - startBit + 1)) - 1);
                    int value = Project.EvalToInt(GetStringValue()) & (~(andValue << startBit));
                    value |= ((i & andValue) << startBit);
                    if (dataType == DataValueType.ByteBits)
                        SetValue(Wla.ToByte((byte)value));
                    else
                        SetValue(Wla.ToWord(value));
                }
                break;
            case DataValueType.ByteBit:
                {
                    int value = Project.EvalToInt(GetStringValue()) & ~(1 << startBit);
                    value |= ((i & 1) << startBit);
                    SetValue(Wla.ToByte((byte)value));
                }
                break;
        }
        base.EndTransaction();

        // Shouldn't need to invoke modifiedEvent here because a handler is installed on the
        // underlying data.
    }

    public override void Initialize()
    {
        base.BeginTransaction();
        if (valueIndex >= Data.GetNumValues())
            Data.SetNumValues(valueIndex + 1, "$00");
        Data.SetValue(valueIndex, defaultDataValues[(int)dataType]);
        RaiseModifiedEvent(null);
        base.EndTransaction();
    }

    // ================================================================================
    // Private methods
    // ================================================================================


    void OnDataModified(object sender, DataModifiedEventArgs args)
    {
        if (sender != _data)
            throw new Exception("DataValueReference.OnDataModified: Wrong data object?");
        else if (args.ValueIndex == valueIndex || args.ValueIndex == -1)
        {
            RaiseModifiedEvent(null);
        }
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    /// <summary>
    /// Helper function to create a DataValueReference wrapped around a ValueReferenceDescriptor in
    /// a single function call.
    /// </summary>
    public static ValueReferenceDescriptor Descriptor(
        Data data,
        string name,
        int index,
        DataValueType type,
        int startBit = 0,
        int endBit = 0,
        int maxValue = -1,
        string constantsMappingString = null,
        bool editable = true,
        string tooltip = null)
    {
        var vr = new DataValueReference(data, index, type,
                                        startBit, endBit, maxValue, constantsMappingString);
        var descriptor = new ValueReferenceDescriptor(vr, name, editable, tooltip);
        return descriptor;
    }
}
