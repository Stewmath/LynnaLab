using System;
using System.IO;

namespace Util {
    public class MemoryFileStream : Stream {
        // Arguments for modification callback
        public class ModifiedEventArgs {
            public ModifiedEventArgs(long s, long e) {
                modifiedRangeStart = s;
                modifiedRangeEnd = e;
            }

            public readonly long modifiedRangeStart; // First changed address (inclusive)
            public readonly long modifiedRangeEnd;   // Last changed address (exclusive)
        }

        public override bool CanRead {
            get { return true; }
        }
        public override bool CanSeek {
            get { return true; }
        }
        public override bool CanTimeout {
            get { return false; }
        }
        public override bool CanWrite {
            get { return true; }
        }
        public override long Length {
            get { return _length; }
        }
        public override long Position {
            get { return _position; }
            set { _position = value; }
        }

        public string Name {
            get { return filename; }
        }

        long _length;
        long _position;
        byte[] data;
        bool modified = false;
        string filename;

        LockableEvent<ModifiedEventArgs> modifiedEvent = new LockableEvent<ModifiedEventArgs>();


        public MemoryFileStream(string filename) {
            this.filename = filename;
            
            FileStream input = new FileStream(filename, FileMode.Open);
            _length = input.Length;

            data = new byte[Length];
            _position = 0;
            modified = false;
            input.Read(data, 0, (int)Length);
            input.Close();
        }

        public override void Flush() {
            if (modified) {
                FileStream output = new FileStream(filename, FileMode.Open);
                output.Write(data, 0, (int)Length);
                output.Close();
                modified = false;
            }
        }

        public override void SetLength(long len) {
            // This code should work, but it's not currently needed. Not sure how to handle the modified
            // event handler for this. See todo below.
            throw new NotImplementedException();

            /*
            if (Length != len) {
                byte[] newData = new byte[len];
                Array.Copy(data, newData, Math.Min(len, _length));
                data = newData;

                _length = len;

                modified = true;
                modifiedEvent.Invoke(this, null); // TODO: should send an actual argument I guess
            }
            */
        }

        public override long Seek(long dest, SeekOrigin origin) {
            switch (origin) {
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

        public override int Read(byte[] buffer, int offset, int count) {
            int size = count;
            if (Position + count > Length)
                size = (int)(Length-Position);
            Array.Copy(data, Position, buffer, offset, size);
            Position = Position + size;
            return size;
        }
        public override void Write(byte[] buffer, int offset, int count) {
            if (Position + count > Length)
                SetLength(Position + count);
            Array.Copy(buffer, offset, data, Position, count);
            Position = Position + count;
            if (Position > Length)
                Position = Length;
            modified = true;
            modifiedEvent.Invoke(this, new ModifiedEventArgs(offset, offset + count));
        }

        public override int ReadByte() {
            int ret = data[Position];
            Position++;
            return ret;
        }
        public override void WriteByte(byte value) {
            data[Position] = value;
            Position++;
            modified = true;
            modifiedEvent.Invoke(this, new ModifiedEventArgs(Position-1, Position));
        }


        public void AddModifiedEventHandler(EventHandler<ModifiedEventArgs> handler) {
            modifiedEvent += handler;
        }
        public void RemoveModifiedEventHandler(EventHandler<ModifiedEventArgs> handler) {
            modifiedEvent -= handler;
        }
    }
}
