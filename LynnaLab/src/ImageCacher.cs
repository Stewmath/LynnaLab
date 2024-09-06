namespace LynnaLab;

/// <summary>
/// Interface for caching images based on a LynnaLib class. It is assumed that, whatever class
/// is used as the KeyClass, there is only one unique instance of that class representing the
/// given data (ie. only one Tileset class ever exists per actual tileset). Most LynnaLib
/// classes are designed with this in mind already.
/// </summary>
public abstract class ImageCacher<KeyClass>
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public ImageCacher(ProjectWorkspace workspace)
    {
        this.Workspace = workspace;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Dictionary<KeyClass, Image> imageCache = new Dictionary<KeyClass, Image>();

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public TopLevel TopLevel { get { return Workspace.TopLevel; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public Image GetImage(KeyClass key)
    {
        Image image;
        if (imageCache.TryGetValue(key, out image))
            return image;

        image = GenerateImage(key);
        imageCache[key] = image;
        return image;
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected abstract Image GenerateImage(KeyClass key);


    // ================================================================================
    // Private methods
    // ================================================================================
}
