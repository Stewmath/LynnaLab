namespace Util;

public interface IDisposeNotifier : IDisposable
{
    public event EventHandler DisposedEvent;
}

/// <summary>
/// Simple class that lets you look up something based on a key and returns the cached value, or
/// creates the value for you if it doesn't exist already.
///
/// This listens to the DisposedEvent on the ValueClass and automatically removes any values that
/// are disposed.
/// </summary>
public class Cacher<KeyClass, ValueClass> : IDisposable where ValueClass : IDisposeNotifier
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public Cacher(Func<KeyClass, ValueClass> generator)
    {
        this.generator = generator;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Dictionary<KeyClass, ValueClass> cache = new();
    Dictionary<ValueClass, KeyClass> cacheByValue = new();
    Func<KeyClass, ValueClass> generator;

    // ================================================================================
    // Properties
    // ================================================================================

    public IEnumerable<ValueClass> Values { get { return cache.Values; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public bool HasKey(KeyClass key)
    {
        return cache.ContainsKey(key);
    }

    public ValueClass GetOrCreate(KeyClass key)
    {
        ValueClass tx;
        if (cache.TryGetValue(key, out tx))
            return tx;

        tx = generator(key);
        tx.DisposedEvent += OnChildDisposed;
        cache[key] = tx;
        cacheByValue[tx] = key;
        return tx;
    }

    public bool TryGetValue(KeyClass key, out ValueClass value)
    {
        return cache.TryGetValue(key, out value);
    }

    public void DisposeKey(KeyClass key)
    {
        var tx = cache[key];
        tx.Dispose(); // Should invoke OnChildDisposed
        Debug.Assert(!cache.ContainsKey(key));
    }

    public virtual void Dispose()
    {
        foreach (KeyClass key in cache.Keys)
        {
            DisposeKey(key);
        }
        cache = null;
        cacheByValue = null;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void OnChildDisposed(object sender, object args)
    {
        ValueClass value = (ValueClass)sender;
        KeyClass key = cacheByValue[value];

        cache.Remove(key);
        cacheByValue.Remove(value);

        value.DisposedEvent -= OnChildDisposed;
    }
}
