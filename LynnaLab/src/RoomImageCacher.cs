using System.Diagnostics;

namespace LynnaLab;

/// <summary>
/// Caches images for room layouts.
///
/// Actually, most of the code here is making a lot of effort to not draw the image immediately,
/// instead waiting for the tileset to update its tile images. And also listening on various
/// events that could change the resultant image.
/// </summary>
public class RoomImageCacher : ImageCacher<RoomLayout>
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public RoomImageCacher(ProjectWorkspace workspace)
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

    protected override Image GenerateImage(RoomLayout layout)
    {
        // Create the blank image
        Image image = TopLevel.Backend.CreateImage(layout.Width * 16, layout.Height * 16);

        // Watch for changes to the tileset's tiles.
        // This will be invoked as the tiles get rendered lazily, or when tileset edits occur.
        EventHandler<int> OnTileModified = (sender, tileIndex) =>
        {
            Tileset tileset = sender as Tileset;
            Debug.Assert(tileset == layout.Tileset);

            if (tileIndex == -1) // Must redraw everything
            {
                LazyRedraw(image, layout);
                return;
            }

            var tilePositions = layout.GetTilePositions(tileIndex);

            TopLevel.LazyInvoke(() =>
            {
                image.BeginAtomicOperation();
                foreach ((int x, int y) in tilePositions)
                {
                    DrawTile(image, layout, x, y);
                }
                image.EndAtomicOperation();
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
            Redraw(image, layout);
        };
        layout.LayoutChangedEvent += onLayoutModified;

        // Draw all tile images that are already rendered
        LazyRedraw(image, layout, cachedOnly: true);

        // Request that all undrawn tiles from the tileset be drawn
        layout.Tileset.RequestRedraw();

        return image;
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
        LazyRedraw(base.GetImage(layout), layout);
    }

    void Redraw(Image image, RoomLayout layout, bool cachedOnly = false)
    {
        Tileset tileset = layout.Tileset;
        image.BeginAtomicOperation();
        for (int x = 0; x < layout.Width; x++)
        {
            for (int y = 0; y < layout.Height; y++)
            {
                int tile = layout.GetTile(x, y);
                if (!cachedOnly || tileset.TileIsRendered(tile))
                    DrawTile(image, layout, x, y);
            }
        }
        image.EndAtomicOperation();
    }

    void LazyRedraw(Image image, RoomLayout layout, bool cachedOnly = false)
    {
        TopLevel.LazyInvoke(() => Redraw(image, layout, cachedOnly));
    }

    /// <summary>
    /// Draw a tile onto the room.
    /// Inexpensive unless the tile image has not been loaded onto the GPU yet.
    /// </summary>
    void DrawTile(Image image, RoomLayout layout, int x, int y)
    {
        int tileIndex = layout.GetTile(x, y);
        var tileBitmap = layout.Tileset.GetTileBitmap(tileIndex);
        var tileImage = TopLevel.ImageFromBitmapTracked(tileBitmap);

        tileImage.DrawOn(image,
                         new Point(0, 0),
                         new Point(x * 16, y * 16),
                         new Point(16, 16));
    }
}
