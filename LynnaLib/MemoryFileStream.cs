using System;
using System.IO;

namespace LynnaLib
{
    /// Like a FileStream but it's buffered in memory. Can be written to and saved.
    public class MemoryFileStream : Stream
    {
        // Arguments for modification callback
        public class ModifiedEventArgs
        {

            public readonly long modifiedRangeStart; // First changed address (inclusive)
            public readonly long modifiedRangeEnd;   // Last changed address (exclusive)

            public ModifiedEventArgs(long s, long e)
            {
                modifiedRangeStart = s;
                modifiedRangeEnd = e;
            }

            public bool ByteChanged(long position)
            {
                return position >= modifiedRangeStart && position < modifiedRangeEnd;
            }
        }

        public Project Project { get; private set; }

        public override bool CanRead
        {
            get { return true; }
        }
        public override bool CanSeek
        {
            get { return true; }
        }
        public override bool CanTimeout
        {
            get { return false; }
        }
        public override bool CanWrite
        {
            get { return true; }
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

        public string Name
        {
            get { return filename; }
        }

        long _length;
        long _position;
        byte[] data;
        bool modified = false;
        string filename;

        LockableEvent<ModifiedEventArgs> modifiedEvent = new LockableEvent<ModifiedEventArgs>();

        // TODO: replace above with this
        public event EventHandler<ModifiedEventArgs> ModifiedEvent;


        public MemoryFileStream(Project project, string filename)
        {
            this.Project = project;
            this.filename = filename;

            FileStream input = new FileStream(filename, FileMode.Open);
            _length = input.Length;

            data = new byte[Length];
            _position = 0;
            modified = false;
            input.Read(data, 0, (int)Length);
            input.Close();

            modifiedEvent += (sender, args) => { if (ModifiedEvent != null) ModifiedEvent(sender, args); };
        }

        public override void Flush()
        {
            if (modified)
            {
                FileStream output = new FileStream(filename, FileMode.Open);
                output.Write(data, 0, (int)Length);
                output.Close();
                modified = false;
            }
        }

        public override void SetLength(long len)
        {
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
            if (Position + count > Length)
                size = (int)(Length - Position);
            Array.Copy(data, Position, buffer, offset, size);
            Position = Position + size;
            return size;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            RecordChange();
            if (Position + count > Length)
                SetLength(Position + count);
            Array.Copy(buffer, offset, data, Position, count);
            Position = Position + count;
            if (Position > Length)
                Position = Length;
            modified = true;
            modifiedEvent.Invoke(this, new ModifiedEventArgs(offset, offset + count));
        }

        public override int ReadByte()
        {
            int ret = data[Position];
            Position++;
            return ret;
        }
        public override void WriteByte(byte value)
        {
            RecordChange();
            data[Position] = value;
            Position++;
            modified = true;
            modifiedEvent.Invoke(this, new ModifiedEventArgs(Position - 1, Position));
        }



        public int GetByte(int position)
        {
            return data[position];
        }


        public void AddModifiedEventHandler(EventHandler<ModifiedEventArgs> handler)
        {
            modifiedEvent += handler;
        }
        public void RemoveModifiedEventHandler(EventHandler<ModifiedEventArgs> handler)
        {
            modifiedEvent -= handler;
        }

        public void LockEvents()
        {
            modifiedEvent.Lock();
        }
        public void UnlockEvents()
        {
            modifiedEvent.Unlock();
        }

        public void RecordChange()
        {
            Project.UndoState.RecordChange(this, () => new Delta(this));
        }

        /// <summary>
        /// Keeps track of difference between an initial and subsequent state for undo handling
        /// </summary>
        public class Delta : TransactionDelta
        {
            public Delta(MemoryFileStream stream)
            {
                this.stream = stream;
                initialState = (byte[])stream.data.Clone();
            }

            MemoryFileStream stream;
            byte[] initialState, finalState;

            public void CaptureFinalState()
            {
                finalState = (byte[])stream.data.Clone();
            }

            public void Rewind()
            {
                Debug.Assert(finalState.SequenceEqual(stream.data),
                             $"Expected:\n{ObjectDumper.Dump(finalState)}\nActual:\n{ObjectDumper.Dump(stream.data)}");
                stream.data = (byte[])initialState.Clone();
                stream.modified = true;
            }

            public void InvokeModifiedEvents()
            {
                stream.modifiedEvent?.Invoke(stream, new ModifiedEventArgs(0, stream.Length));
            }
        }
    }
}
