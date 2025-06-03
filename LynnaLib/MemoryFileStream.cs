using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Util;

namespace LynnaLib
{
    /// Like a FileStream but it's buffered in memory. Can be written to and saved.
    public class MemoryFileStream : TrackedStream
    {
        public class State : TransactionState
        {
            [JsonRequired]
            public byte[] data;
        }

        // ================================================================================
        // Properties
        // ================================================================================

        public override long Length
        {
            get { return state.data.Length; }
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

        // ================================================================================
        // Variables
        // ================================================================================

        private static readonly log4net.ILog log = LogHelper.GetLogger();

        FileSystemWatcher watcher;
        bool watcherReloadScheduled;
        object watcherLock = new();

        State state = new State();

        // NOTE: Not tracked with state. Every time the stream is accessed the position should be
        // set to whatever we're reading.
        long _position;

        readonly string filepath;
        bool _modified;

        // ================================================================================
        // Events
        // ================================================================================

        // Inherits ModifiedEvent

        // ================================================================================
        // Constructors
        // ================================================================================

        public MemoryFileStream(Project project, string filename, bool watchForFilesystemChanges)
            : base(project, filename)
        {
            // Sanity check for security - don't access any new files after project is initialized
            if (!project.IsInConstructor)
                throw new Exception("Initializing MemoryFileStream outside of Project constructor");

            this.filepath = Path.Combine(project.BaseDirectory, filename);

            LoadFromFile();

            if (watchForFilesystemChanges)
                InitializeFileWatcher();
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private MemoryFileStream(Project p, string id, TransactionState state)
            : base(p, id)
        {
            this.filepath = null; // NEVER allow remote clients to set this! Don't give them filesystem access!
            this.state = (State)state;
        }

        void InitializeFileWatcher()
        {
            // FileSystemWatcher doesn't work well on Linux. Creating hundreds or thousands of these
            // uses up system resources in a way that causes the OS to complain.
            // Automatic file reloading is disabled on Linux until a good workaround is found.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return;

            watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(filepath);
            watcher.Filter = Path.GetFileName(filepath);
            watcher.NotifyFilter = NotifyFilters.LastWrite;

            watcher.Changed += (o, a) =>
            {
                lock (watcherLock)
                {
                    if (watcherReloadScheduled)
                        return;
                    watcherReloadScheduled = true;
                }

                log.Info($"File {filepath} changed, triggering reload event");

                // Wait one second before rereading the file. FileSystemWatcher seems a bit glitchy,
                // we need to give the file time to "stabilize".
                System.Threading.Thread.Sleep(1000);

                watcherReloadScheduled = false;

                // Use MainThreadInvoke to avoid any threading headaches
                Helper.MainThreadInvoke(() =>
                {
                    Project.BeginTransaction("File Reload", disallowUndo: true);
                    Project.TransactionManager.CaptureInitialState<State>(this);
                    LoadFromFile();
                    InvokeModifiedEvent(new StreamModifiedEventArgs(0, Length));
                    Project.EndTransaction();
                });
            };

            watcher.EnableRaisingEvents = true;
        }

        void LoadFromFile()
        {
            FileStream input = new FileStream(filepath, FileMode.Open);

            if (input.Length == 0)
            {
                // It seems like when a filewatcher is installed, this can get triggered when it's
                // seen as "empty"? Perhaps because it can't open the file properly. Obviously this
                // is bad. Just ignore it in that case.
                return;
            }

            state.data = new byte[input.Length];
            _position = 0;
            if (input.Read(state.data, 0, (int)input.Length) != input.Length)
                throw new Exception("MemoryFileStream: Didn't read enough bytes");
            input.Close();
        }

        // ================================================================================
        // Public methods
        // ================================================================================

        public void Save()
        {
            // TODO: How to handle this when on a remote client (probably just do nothing)
            if (Modified)
            {
                FileStream output = new FileStream(filepath, FileMode.Open);
                output.Write(Data, 0, (int)Length);
                output.Close();
                Modified = false;
            }
        }

        public void SetLength(long len)
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
                ModifiedEvent?.Invoke(this, null); // TODO: should send an actual argument I guess
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

        public override int ReadByte()
        {
            int ret = Data[Position];
            Position++;
            return ret;
        }

        public override ReadOnlySpan<byte> ReadAllBytes()
        {
            return Data;
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
            InvokeModifiedEvent(new StreamModifiedEventArgs(offset, offset + count));
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
            InvokeModifiedEvent(new StreamModifiedEventArgs(Position - 1, Position));
        }

        public override void WriteAllBytes(ReadOnlySpan<byte> data)
        {
            Project.TransactionManager.CaptureInitialState<State>(this);
            state.data = data.ToArray();
            InvokeModifiedEvent(StreamModifiedEventArgs.All(this));
        }

        public int GetByte(int position)
        {
            return Data[position];
        }

        public string ReadAllText()
        {
            return System.Text.Encoding.UTF8.GetString(Data);
        }
        public string[] ReadAllLines()
        {
            return ReadAllText().Split('\n');
        }


        public void AddModifiedEventHandler(EventHandler<StreamModifiedEventArgs> handler)
        {
            ModifiedEvent += handler;
        }
        public void RemoveModifiedEventHandler(EventHandler<StreamModifiedEventArgs> handler)
        {
            ModifiedEvent -= handler;
        }

        public void RecordChange()
        {
            Project.TransactionManager.CaptureInitialState<State>(this);
        }

        // ================================================================================
        // TrackedProjectData interface functions
        // ================================================================================

        public override TransactionState GetState()
        {
            return state;
        }

        public override void SetState(TransactionState s)
        {
            State newState = (State)s;
            if (newState.data == null)
                throw new DeserializationException("Missing data in MemoryFileStream");
            this.state = newState;
            this.Modified = true;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
            State last = (State)prevState;

            var args = StreamModifiedEventArgs.FromChangedRange(last.data, state.data);
            if (args == null)
                return;
            InvokeModifiedEvent(args);
        }
    }
}
