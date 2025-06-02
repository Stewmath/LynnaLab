namespace Util;

/// <summary>
/// Simple interface that resembles the Stream class (though also has extra stuff like
/// "ModifiedEvent").
/// </summary>
public interface IStream
{
    public long Length { get; }
    public long Position { get; set; }

    public event EventHandler<StreamModifiedEventArgs> ModifiedEvent;

    public long Seek(long dest, System.IO.SeekOrigin origin = System.IO.SeekOrigin.Begin);

    public int Read(byte[] buffer, int offset, int count);
    public int ReadByte();
    public ReadOnlySpan<byte> ReadAllBytes();

    public void Write(byte[] buffer, int offset, int count);
    public void WriteAllBytes(ReadOnlySpan<byte> data);
    public void WriteByte(byte value);
}

// Arguments for modification callback
public class StreamModifiedEventArgs
{
    public readonly long modifiedRangeStart; // First changed address (inclusive)
    public readonly long modifiedRangeEnd;   // Last changed address (exclusive)

    public StreamModifiedEventArgs(long s, long e)
    {
        modifiedRangeStart = s;
        modifiedRangeEnd = e;
    }

    public bool ByteChanged(long position)
    {
        return position >= modifiedRangeStart && position < modifiedRangeEnd;
    }

    public static StreamModifiedEventArgs All(IStream stream)
    {
        return new StreamModifiedEventArgs(0, stream.Length);
    }

    public static StreamModifiedEventArgs FromChangedRange(byte[] first, byte[] second)
    {
        if (first.Length == second.Length)
        {
            // Compare the new and old data to try to optimize which parts we mark as modified.
            int start = 0, end = second.Length - 1;

            while (start < second.Length && first[start] == second[start])
                start++;
            if (start == second.Length)
                return null;
            while (first[end] == second[end])
                end--;
            end++;
            if (start >= end)
                return null;
            return new StreamModifiedEventArgs(start, end);
        }
        else
        {
            // Just mark everything as modified
            return new StreamModifiedEventArgs(0, second.Length);
        }
    }
}
