using System;
using System.IO;

namespace LynnaLib
{
    /// Like a FileStream but it's buffered in memory. Can be written to and saved.
    public class MemoryFileStream : Stream, Undoable
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

        class State : TransactionState
        {
            public byte[] data;

            public TransactionState Copy()
            {
                State s = new State();
                s.data = (byte[])data.Clone();
                return s;
            }

            public bool Compare(TransactionState o)
            {
                if (!(o is State state))
                    return false;
                return data.SequenceEqual(state.data);
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

        public string FilePath
        {
            get { return filepath; }
        }

        public string RelativeFilePath
        {
            get { return System.IO.Path.GetRelativePath(Project.BaseDirectory, filepath); }
        }

        public bool Modified
        {
            get { return _modified; }
            set
            {
                _modified = value;
                if (value)
                    Project.MarkModified();
            }
        }

        private byte[] Data { get { return state.data; } }

        State state = new State();
        long _length;
        long _position;
        bool _modified;
        string filepath;

        LockableEvent<ModifiedEventArgs> modifiedEvent = new LockableEvent<ModifiedEventArgs>();

        // TODO: replace above with this
        public event EventHandler<ModifiedEventArgs> ModifiedEvent;


        public MemoryFileStream(Project project, string filename)
        {
            this.Project = project;
            this.filepath = filename;

            FileStream input = new FileStream(filename, FileMode.Open);
            _length = input.Length;

            state.data = new byte[Length];
            _position = 0;
            input.Read(Data, 0, (int)Length);
            input.Close();

            modifiedEvent += (sender, args) => { if (ModifiedEvent != null) ModifiedEvent(sender, args); };
        }

        public override void Flush()
        {
            if (Modified)
            {
                FileStream output = new FileStream(filepath, FileMode.Open);
                output.Write(Data, 0, (int)Length);
                output.Close();
                Modified = false;
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
            Array.Copy(Data, Position, buffer, offset, size);
            Position = Position + size;
            return size;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            RecordChange();
            if (Position + count > Length)
                SetLength(Position + count);
            Array.Copy(buffer, offset, Data, Position, count);
            Position = Position + count;
            if (Position > Length)
                Position = Length;
            Modified = true;
            modifiedEvent.Invoke(this, new ModifiedEventArgs(offset, offset + count));
        }

        public override int ReadByte()
        {
            int ret = Data[Position];
            Position++;
            return ret;
        }
        public override void WriteByte(byte value)
        {
            if (Data[Position] == value)
            {
                Position++;
                return;
            }
            RecordChange();
            Data[Position] = value;
            Position++;
            Modified = true;
            modifiedEvent.Invoke(this, new ModifiedEventArgs(Position - 1, Position));
        }



        public int GetByte(int position)
        {
            return Data[position];
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
            Project.UndoState.CaptureInitialState(this);
        }

        // ================================================================================
        // Undoable interface functions
        // ================================================================================

        public TransactionState GetState()
        {
            return state;
        }

        public void SetState(TransactionState state)
        {
            this.state = (State)state.Copy();
            Modified = true;
        }

        public void InvokeModifiedEvent(TransactionState prevState)
        {
            State last = prevState as State;

            if (last.data.Length == state.data.Length)
            {
                // Compare the new and old data to try to optimize which parts we mark as modified.
                int start = 0, end = state.data.Length - 1;

                while (start < state.data.Length && last.data[start] == state.data[start])
                    start++;
                if (start == state.data.Length)
                    return;
                while (last.data[end] == state.data[end])
                    end--;
                end++;
                if (start >= end)
                    return;
                modifiedEvent?.Invoke(this, new ModifiedEventArgs(start, end));
            }
            else
            {
                // Just mark everything as modified
                modifiedEvent?.Invoke(this, new ModifiedEventArgs(0, Length));
            }
        }
    }
}
