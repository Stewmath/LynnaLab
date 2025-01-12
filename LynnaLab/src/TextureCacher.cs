namespace LynnaLab;

/// <summary>
/// Interface for caching textures based on a LynnaLib class. It is assumed that, whatever class
/// is used as the KeyClass, there is only one unique instance of that class representing the
/// given data (ie. only one Tileset class ever exists per actual tileset). Most LynnaLib
/// classes are designed with this in mind already.
/// </summary>
public abstract class TextureCacher<KeyClass> : IDisposable
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public TextureCacher(ProjectWorkspace workspace)
    {
        this.Workspace = workspace;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Dictionary<KeyClass, Texture> textureCache = new Dictionary<KeyClass, Texture>();

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public Texture GetTexture(KeyClass key)
    {
        Texture tx;
        if (textureCache.TryGetValue(key, out tx))
            return tx;

        tx = GenerateTexture(key);
        textureCache[key] = tx;
        return tx;
    }

    public void DisposeTexture(KeyClass key)
    {
        var tx = textureCache[key];
        tx.Dispose();
        textureCache.Remove(key);
    }

    public void Dispose()
    {
        foreach (KeyClass key in textureCache.Keys)
        {
            DisposeTexture(key);
        }
        textureCache = null;
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected Texture CacheLookup(KeyClass key)
    {
        return textureCache[key];
    }

    protected abstract Texture GenerateTexture(KeyClass key);


    // ================================================================================
    // Private methods
    // ================================================================================
}
