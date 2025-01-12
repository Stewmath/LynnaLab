namespace LynnaLab;

/// <summary>
/// Caches textures for room layouts.
///
/// Actually, most of the code here is making a lot of effort to not draw the texture immediately,
/// instead waiting for the tileset to update its tile textures. And also listening on various
/// events that could change the resultant texture.
/// </summary>
public class RoomTextureCacher : TextureCacher<RoomLayout>
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public RoomTextureCacher(ProjectWorkspace workspace)
        : base(workspace)
    {

    }

    // ================================================================================
    // Variables
    // ================================================================================

    Dictionary<RoomLayout, EventWrapper<Tileset>> tilesetEventWrappers
        = new Dictionary<RoomLayout, EventWrapper<Tileset>>();

    // ================================================================================
    // Properties
    // ================================================================================

    // ================================================================================
    // Public methods
    // ================================================================================

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected override Texture GenerateTexture(RoomLayout layout)
    {
        // Create the blank texture
        Texture texture = TopLevel.Backend.CreateTexture(layout.Width * 16, layout.Height * 16);

        // Watch for changes to the tileset's tiles.
        // This will be invoked as the tiles get rendered lazily, or when tileset edits occur.
        EventHandler<int> OnTileModified = (sender, tileIndex) =>
        {
            Tileset tileset = sender as Tileset;
            Debug.Assert(tileset == layout.Tileset);

            if (tileIndex == -1) // Must redraw everything
            {
                LazyRedraw(texture, layout);
                return;
            }

            var tilePositions = layout.GetTilePositions(tileIndex);

            TopLevel.LazyInvoke(() =>
            {
                texture.BeginAtomicOperation();
                foreach ((int x, int y) in tilePositions)
                {
                    DrawTile(texture, layout, x, y);
                }
                texture.EndAtomicOperation();
            });
        };

        // Register tile modified handler
        var tilesetEventWrapper = new EventWrapper<Tileset>(layout.Tileset);
        tilesetEventWrappers[layout] = tilesetEventWrapper;
        tilesetEventWrapper.Bind<int>("TileModifiedEvent", OnTileModified);

        // Watch for changes to the room's tileset index
        layout.TilesetChangedEvent += TilesetChanged;

        // Watch for changes to the room layout
        EventHandler<RoomLayoutChangedEventArgs> onLayoutModified = (sender, args) =>
        {
            // We want immediate feedback from editing the room, so no lazy drawing
            Redraw(texture, layout);
        };
        layout.LayoutChangedEvent += onLayoutModified;

        // Draw all tile images that are already rendered
        LazyRedraw(texture, layout, cachedOnly: true);

        // Request that all undrawn tiles from the tileset be drawn
        layout.Tileset.RequestRedraw();

        return texture;
    }


    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Invoked when the RoomLayout's tileset index has changed.
    /// </summary>
    void TilesetChanged(object sender, RoomTilesetChangedEventArgs args)
    {
        var layout = sender as RoomLayout;

        if (!tilesetEventWrappers.TryGetValue(layout, out var tilesetEventWrapper))
            throw new Exception("Internal error: tilesetEventWrapper missing");

        tilesetEventWrapper.ReplaceEventSource(args.newTileset);
        LazyRedraw(base.GetTexture(layout), layout);
    }

    void Redraw(Texture texture, RoomLayout layout, bool cachedOnly = false)
    {
        Tileset tileset = layout.Tileset;
        texture.BeginAtomicOperation();
        for (int x = 0; x < layout.Width; x++)
        {
            for (int y = 0; y < layout.Height; y++)
            {
                int tile = layout.GetTile(x, y);
                if (!cachedOnly || tileset.TileIsRendered(tile))
                    DrawTile(texture, layout, x, y);
            }
        }
        texture.EndAtomicOperation();
    }

    void LazyRedraw(Texture texture, RoomLayout layout, bool cachedOnly = false)
    {
        TopLevel.LazyInvoke(() => Redraw(texture, layout, cachedOnly));
    }

    /// <summary>
    /// Draw a tile onto the room.
    /// Inexpensive unless the tile texture has not been loaded onto the GPU yet.
    /// </summary>
    void DrawTile(Texture texture, RoomLayout layout, int x, int y)
    {
        int tileIndex = layout.GetTile(x, y);
        var tileBitmap = layout.Tileset.GetTileBitmap(tileIndex);
        var tileTexture = TopLevel.TextureFromBitmapTracked(tileBitmap);

        tileTexture.DrawOn(texture,
                         new Point(0, 0),
                         new Point(x * 16, y * 16),
                         new Point(16, 16));
    }
}
