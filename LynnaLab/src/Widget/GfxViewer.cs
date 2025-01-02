namespace LynnaLab;

public class GfxViewer : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public GfxViewer(ProjectWorkspace workspace, string name) : base(name)
    {
        Workspace = workspace;

        base.TileWidth = 8;
        base.TileHeight = 8;
        base.Width = 0;
        base.Height = 0;
        base.Scale = 2;
        base.Selectable = true;
    }


    // ================================================================================
    // Variables
    // ================================================================================

    Image image;

    GraphicsState graphicsState;
    int offsetStart, offsetEnd;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }

    public override Image Image
    {
        get { return image; }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void SetGraphicsState(GraphicsState state, int offsetStart, int offsetEnd, int width = -1, int scale = 2)
    {
        var tileModifiedHandler = (int bank, int tile) =>
        {
            if (bank == -1 && tile == -1) // Full invalidation
                RedrawAll();
            else
                Draw(tile + bank * 0x180);
        };

        if (graphicsState != null)
            graphicsState.RemoveTileModifiedHandler(tileModifiedHandler);
        if (state != null)
            state.AddTileModifiedHandler(tileModifiedHandler);

        graphicsState = state;

        int size = (offsetEnd - offsetStart) / 16;
        if (width == -1)
            width = (int)Math.Sqrt(size);
        int height = size / width;

        this.offsetStart = offsetStart;
        this.offsetEnd = offsetEnd;

        Width = width;
        Height = height;
        TileWidth = 8;
        TileHeight = 8;
        Scale = scale;

        image = TopLevel.Backend.CreateImage(Width * TileWidth, Height * TileHeight);

        RedrawAll();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void RedrawAll()
    {
        for (int i = offsetStart / 16; i < offsetEnd / 16; i++)
            Draw(i);
    }

    /// <summary>
    /// Draw a tile. This uses CPU-based drawing and is not very efficient.
    /// </summary>
    void Draw(int tile)
    {
        int offset = tile * 16;

        if (!(offset >= offsetStart && offset < offsetEnd))
            return;

        int x = ((offset - offsetStart) / 16) % Width;
        int y = ((offset - offsetStart) / 16) / Width;

        int bank = 0;
        if (offset >= 0x1800)
        {
            offset -= 0x1800;
            bank = 1;
        }
        byte[] data = new byte[16];
        Array.Copy(graphicsState.VramBuffer[bank], offset, data, 0, 16);

        using (Bitmap _subImage = GbGraphics.RawTileToBitmap(data))
        {
            Image subImage = TopLevel.Backend.ImageFromBitmap(_subImage);
            subImage.DrawOn(image,
                            new Point(0, 0),
                            new Point(x * 8, y * 8),
                            new Point(8, 8));
            subImage.Dispose();
        }
    }
}
