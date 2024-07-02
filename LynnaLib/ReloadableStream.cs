using System;
using System.IO;
using Util;

/// Base class for Stream objects which can watch for changes to their respective files.
public abstract class ReloadableStream : Stream
{
    private static readonly log4net.ILog log = LogHelper.GetLogger();

    FileSystemWatcher watcher;

    public ReloadableStream(string filename)
    {
        watcher = new FileSystemWatcher();
        watcher.Path = Path.GetDirectoryName(filename);
        watcher.Filter = Path.GetFileName(filename);
        watcher.NotifyFilter = NotifyFilters.LastWrite;

        watcher.Changed += (o, a) =>
        {
            log.Info($"File {filename} changed, triggering reload event");

            // Use MainThreadInvoke to avoid any threading headaches
            Helper.MainThreadInvoke(() =>
            {
                Reload();
                if (ExternallyModifiedEvent != null)
                    ExternallyModifiedEvent(this, null);
            });
        };

        watcher.EnableRaisingEvents = true;
    }

    // Event which triggers when the stream is modified externally
    public event EventHandler<EventArgs> ExternallyModifiedEvent;

    // Function which handles reloading the data
    protected abstract void Reload();


    public override void Close()
    {
        watcher.Dispose();
        base.Close();
    }
}
