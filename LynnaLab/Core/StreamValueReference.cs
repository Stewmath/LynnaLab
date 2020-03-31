using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
    // A ValueReference that references a Stream instead of a Data object.
    public class StreamValueReference : ValueReference
    {
        Stream stream;
        int valueIndex;

        // Standard constructor
        public StreamValueReference(string n, int offset, DataValueType t, bool editable=true)
            : base(n, t, editable)
        {
            valueIndex = offset;
        }

        // Constructor for DataValueType.ByteBits
        public StreamValueReference(string n, int offset, int startBit, int endBit, DataValueType t, bool editable=true)
            : base(n, startBit, endBit, t, editable)
        {
            valueIndex = offset;
        }

        public StreamValueReference(StreamValueReference r)
            : base(r)
        {
            this.stream = r.stream;
        }

        public void SetStream(Stream stream) {
            this.stream = stream;
        }

        public override string GetStringValue() {
            return Wla.ToHex(GetIntValue(), 2);
        }

        public override int GetIntValue()
        {
            stream.Seek(valueIndex, SeekOrigin.Begin);
            switch(ValueType) {
                case DataValueType.ByteBits:
                    {
                        int andValue = (1<<(endBit-startBit+1))-1;
                        return (stream.ReadByte()>>startBit)&andValue;
                    }
                case DataValueType.ByteBit:
                    return (stream.ReadByte()>>startBit)&1;
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

        public override void SetValue(string s) {
            // Use SetValue(int) for now
            throw new NotImplementedException();
        }

        public override void SetValue(int i) {
            stream.Seek(valueIndex, SeekOrigin.Begin);
            switch(ValueType) {
                case DataValueType.Byte:
                default:
                    stream.WriteByte((byte)i);
                    break;
                case DataValueType.Word:
                    stream.WriteByte((byte)(i&0xff));
                    stream.WriteByte((byte)(i>>8));
                    break;
                case DataValueType.ByteBits:
                    {
                        int andValue = ((1<<(endBit-startBit+1))-1);
                        int value = stream.ReadByte() & (~(andValue<<startBit));
                        value |= ((i&andValue)<<startBit);

                        stream.Seek(valueIndex, SeekOrigin.Begin);
                        stream.WriteByte((byte)value);
                    }
                    break;
                case DataValueType.ByteBit:
                    {
                        int value = stream.ReadByte() & ~(1<<startBit);
                        value |= ((i&1)<<startBit);

                        stream.Seek(valueIndex, SeekOrigin.Begin);
                        stream.WriteByte((byte)value);
                    }
                    break;
            }
        }

        public override void Initialize() {
            throw new NotImplementedException();
        }
    }
}

