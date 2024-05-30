using System;
using System.IO;

namespace Util
{
    public class SubStream : Stream
    {
        public override bool CanRead
        {
            get { return stream.CanRead; }
        }
        public override bool CanSeek
        {
            get { return stream.CanSeek; }
        }
        public override bool CanTimeout
        {
            get { return stream.CanTimeout; }
        }
        public override bool CanWrite
        {
            get { return stream.CanWrite; }
        }
        public override long Length
        {
            get { return _length; }
        }
        public override long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        long _length;
        long _position;

        Stream stream;
        int streamOffset;

        public SubStream(Stream stream, int offset, int size)
        {
            this.stream = stream;
            this.streamOffset = offset;

            _length = size;
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override void SetLength(long len)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long dest, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.End:
                    Position = Length - dest;
                    break;
                case SeekOrigin.Begin:
                    Position = dest;
                    break;
                case SeekOrigin.Current:
                    Position += dest;
                    break;
            }
            return Position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int size = count;
            if (Position + size > Length)
                size = (int)(Length - Position);
            stream.Position = Position + streamOffset;
            stream.Read(buffer, offset, size);
            Position = Position + size;
            return size;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position + count > Length)
                throw new Exception("Write operation passes end of stream");
            stream.Position = Position + streamOffset;
            stream.Write(buffer, offset, count);
            Position = Position + count;
            if (Position > Length)
                Position = Length;
        }

        public override int ReadByte()
        {
            stream.Position = Position + streamOffset;
            int ret = stream.ReadByte();
            Position++;
            return ret;
        }
        public override void WriteByte(byte value)
        {
            stream.Position = Position + streamOffset;
            stream.WriteByte(value);
            Position++;
        }
    }
}
