using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using Util;

namespace LynnaLab
{
    // A ValueReference that references a Stream instead of a Data object.
    public class StreamValueReference : ValueReference
    {
        DataValueType dataType; // Enum borrowed from DataValueReference
        MemoryFileStream stream;
        int offset;
        int startBit, endBit;

        WeakEventWrapper<MemoryFileStream, MemoryFileStream.ModifiedEventArgs> streamEventWrapper
            = new WeakEventWrapper<MemoryFileStream, MemoryFileStream.ModifiedEventArgs>();
        LockableEvent<ValueModifiedEventArgs> eventHandler = new LockableEvent<ValueModifiedEventArgs>();

        // Standard constructor
        public StreamValueReference(MemoryFileStream stream, string name, int offset, DataValueType type, int startBit=0, int endBit=0, bool editable=true)
            : base(name, DataValueReference.GetValueType(type), editable, null)
        {
            this.stream = stream;
            this.dataType = type;
            this.offset = offset;
            this.startBit = startBit;
            this.endBit = endBit;

            MaxValue = DataValueReference.GetMaxValueForType(type, startBit, endBit);

            streamEventWrapper.Event += OnStreamModified;
            BindEventHandler();
        }

        public StreamValueReference(StreamValueReference r)
            : base(r)
        {
            this.stream = r.stream;
            this.dataType = r.dataType;
            this.offset = r.offset;
            this.startBit = r.startBit;
            this.endBit = r.endBit;
            this.eventHandler = r.eventHandler;

            this.eventHandler = r.eventHandler;
            streamEventWrapper.Event += OnStreamModified;
            BindEventHandler();
        }

        void BindEventHandler() {
            streamEventWrapper.UnbindAll();
            streamEventWrapper.Bind(stream, "ModifiedEvent");
        }


        public override string GetStringValue() {
            return Wla.ToHex(GetIntValue(), 2);
        }

        public override int GetIntValue()
        {
            stream.Seek(offset, SeekOrigin.Begin);
            switch (dataType) {
            case DataValueType.ByteBits: {
                int andValue = (1<<(endBit-startBit+1))-1;
                return (stream.ReadByte()>>startBit)&andValue;
            }
            case DataValueType.ByteBit:
                return (stream.ReadByte()>>startBit)&1;
            case DataValueType.Word: {
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
            stream.Seek(offset, SeekOrigin.Begin);
            switch (dataType) {
                case DataValueType.Byte:
                    stream.WriteByte((byte)i);
                    break;
                case DataValueType.Word:
                    stream.WriteByte((byte)(i&0xff));
                    stream.WriteByte((byte)(i>>8));
                    break;
                case DataValueType.ByteBits: {
                    int andValue = ((1<<(endBit-startBit+1))-1);
                    int value = stream.ReadByte() & (~(andValue<<startBit));
                    value |= ((i&andValue)<<startBit);

                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.WriteByte((byte)value);
                    break;
                }
                case DataValueType.ByteBit: {
                    int value = stream.ReadByte() & ~(1<<startBit);
                    value |= ((i&1)<<startBit);

                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.WriteByte((byte)value);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            eventHandler.Invoke(this, null);
        }

        public override void Initialize() {
            throw new NotImplementedException();
        }

        public override void AddValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler) {
            eventHandler += handler;
        }
        public override void RemoveValueModifiedHandler(EventHandler<ValueModifiedEventArgs> handler) {
            eventHandler += handler;
        }

        public override ValueReference Clone() {
            return new StreamValueReference(this);
        }


        void OnStreamModified(object sender, MemoryFileStream.ModifiedEventArgs args) {
            if (sender != stream)
                throw new Exception("StreamValueReference.OnStreamModified: Wrong stream object?");
            else if (args.ByteChanged(offset)) {
                eventHandler.Invoke(this, null);
            }
        }
    }
}
