namespace LynnaLab;

/// <summary>
/// Caches textures for room layouts. Actually just reads off the cached overworld map image with
/// the desired room in it.
/// </summary>
public class RoomTextureCacher : IDisposeNotifier
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public RoomTextureCacher(ProjectWorkspace workspace, RoomLayout layout)
    {
        Workspace = workspace;
        Layout = layout;

        GenerateTexture();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    /*
    Dictionary<RoomLayout, EventWrapper<Tileset>> tilesetEventWrappers
        = new Dictionary<RoomLayout, EventWrapper<Tileset>>();
        */

    TextureBase roomTexture;
    RgbaTexture mapTexture; // Retrieved from MapTextureCacher

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public RoomLayout Layout { get; private set; }

    // ================================================================================
    // Events
    // ================================================================================

    public event EventHandler DisposedEvent;

    // ================================================================================
    // Public methods
    // ================================================================================

    public TextureBase GetTexture()
    {
        return roomTexture;
    }

    // Should never be called except when closing a project.
    public void Dispose()
    {
        roomTexture.Dispose();
        roomTexture = null;
        // Don't dispose mapTexture as we're not the owner
        DisposedEvent?.Invoke(this, null);
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    void GenerateTexture()
    {
        // Get the overworld map image with the room we want
        int x = Layout.Room.Index % 16;
        int y = (Layout.Room.Index % 256) / 16;
        var roomSize = Layout.Size * 16;
        this.mapTexture = Workspace.GetCachedMapTexture((Workspace.Project.GetWorldMap(Layout.Room.Group, Layout.Season), 0));
        roomTexture = TopLevel.Backend.CreateTextureWindow(mapTexture, new Point(x, y) * roomSize, roomSize);
    }
}
