using System.IO;

namespace LynnaLib;

/// <summary>
/// A ValueReference that references a Stream instead of a Data object.
/// </summary>
public class StreamValueReference : ValueReference
{
    // ================================================================================
    // Constructors
    // ================================================================================

    // Standard constructor
    public StreamValueReference(Project project, MemoryFileStream stream, int offset, DataValueType type, int startBit = 0, int endBit = 0, int maxValue = -1,
            string constantsMappingString = null)
        : base(project, DataValueReference.GetValueType(type), constantsMappingString)
    {
        this.stream = stream;
        this.dataType = type;
        this.offset = offset;
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
        streamEventWrapper.Bind<MemoryFileStream.ModifiedEventArgs>("ModifiedEvent", OnStreamModified);
        streamEventWrapper.ReplaceEventSource(stream);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    DataValueType dataType; // Enum borrowed from DataValueReference
    MemoryFileStream stream;
    int offset;
    int startBit, endBit;

    EventWrapper<MemoryFileStream> streamEventWrapper = new EventWrapper<MemoryFileStream>();


    // ================================================================================
    // Public methods
    // ================================================================================


    public override string GetStringValue()
    {
        return Wla.ToHex(GetIntValue(), 2);
    }

    public override int GetIntValue()
    {
        stream.Seek(offset, SeekOrigin.Begin);
        switch (dataType)
        {
            case DataValueType.ByteBits:
                {
                    int andValue = (1 << (endBit - startBit + 1)) - 1;
                    return (stream.ReadByte() >> startBit) & andValue;
                }
            case DataValueType.ByteBit:
                return (stream.ReadByte() >> startBit) & 1;
            case DataValueType.Word:
                {
                    int w = stream.ReadByte();
                    w |= stream.ReadByte() << 8;
                    return w;
                }
            default:
                return stream.ReadByte();
        }
    }

    public override void SetValue(string s)
    {
        // Use SetValue(int) for now
        throw new NotImplementedException();
    }

    public override void SetValue(int i)
    {
        if (GetIntValue() == i)
            return;

        stream.Seek(offset, SeekOrigin.Begin);

        switch (dataType)
        {
            case DataValueType.Byte:
                stream.WriteByte((byte)i);
                break;
            case DataValueType.Word:
                stream.WriteByte((byte)(i & 0xff));
                stream.WriteByte((byte)(i >> 8));
                break;
            case DataValueType.ByteBits:
                {
                    int andValue = ((1 << (endBit - startBit + 1)) - 1);
                    int value = stream.ReadByte() & (~(andValue << startBit));
                    value |= ((i & andValue) << startBit);

                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.WriteByte((byte)value);
                    break;
                }
            case DataValueType.ByteBit:
                {
                    int value = stream.ReadByte() & ~(1 << startBit);
                    value |= ((i & 1) << startBit);

                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.WriteByte((byte)value);
                    break;
                }
            default:
                throw new NotImplementedException();
        }

        RaiseModifiedEvent(null);
    }

    public override void Initialize()
    {
        throw new NotImplementedException();
    }


    // ================================================================================
    // Private methods
    // ================================================================================


    void OnStreamModified(object sender, MemoryFileStream.ModifiedEventArgs args)
    {
        if (sender != stream)
            throw new Exception("StreamValueReference.OnStreamModified: Wrong stream object?");
        else if (args.ByteChanged(offset))
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
        Project project,
        MemoryFileStream stream,
        string name,
        int offset,
        DataValueType type,
        int startBit = 0,
        int endBit = 0,
        int maxValue = -1,
        bool editable = true,
        string constantsMappingString = null,
        string tooltip = null)
    {
        var vr = new StreamValueReference(project, stream, offset, type, startBit, endBit, maxValue, constantsMappingString);
        var descriptor = new ValueReferenceDescriptor(vr, name, editable, tooltip);
        return descriptor;
    }
}
