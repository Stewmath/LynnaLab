using System.Collections;

namespace Util;

/// <summary>
/// Behaves like a stack with a set maximum capacity. If the maximum is exceeded, the oldest
/// elements are dropped
/// </summary>
public class CircularStack<T> : IEnumerable<T>
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public CircularStack(int capacity)
    {
        this.Capacity = capacity;
        elements = new T[capacity];
    }

    // ================================================================================
    // Variables
    // ================================================================================

    T[] elements;
    int nextPos = 0;
    int version = 0;

    // ================================================================================
    // Properties
    // ================================================================================

    public int Count { get; private set; }
    public int Capacity { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Push(T e)
    {
        elements[nextPos] = e;
        nextPos = NextIndex();
        Count++;
        if (Count > Capacity)
            Count = Capacity;
        version++;
    }

    public T Pop()
    {
        if (Count == 0)
            throw new InvalidOperationException("Tried to pop from an empty stack");

        nextPos = PrevIndex();
        T retval = elements[nextPos];
        elements[nextPos] = default;
        Count--;
        version++;
        return retval;
    }

    public T Peek()
    {
        if (Count == 0)
            throw new InvalidOperationException("Tried to peek from an empty stack");

        return elements[PrevIndex()];
    }

    public void Clear()
    {
        elements = new T[Capacity];
        nextPos = 0;
        Count = 0;
        version++;
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        int pos = (this.nextPos - Count + Capacity) % Capacity;
        int v = version;
        for (int i=0; i<Count; i++)
        {
            if (v != version)
                throw new InvalidOperationException("Modified CircularStack while iterating over it");
            yield return this.elements[pos++];
            if (pos == Capacity)
                pos = 0;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return (this as IEnumerable<T>).GetEnumerator();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    int NextIndex()
    {
        return (nextPos + 1) % Capacity;
    }

    int PrevIndex()
    {
        if (nextPos == 0)
            return Capacity - 1;
        return nextPos - 1;
    }
}
