namespace LynnaLab;

/// <summary>
/// Provides a space where one can draw tiles and select them for reuse
/// </summary>
public class ScratchPad : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ScratchPad(string name, TileGrid referenceGrid, Brush brush)
        : base(name)
    {
        this.brush = brush;
        base.WindowFlags = ImGuiWindowFlags.HorizontalScrollbar;

        Grid = new ScratchPadGrid("Grid", brush, referenceGrid, 32, 32);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Brush brush;

    // ================================================================================
    // Properties
    // ================================================================================

    public TileGrid Grid { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        Grid.Render();
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}

/// <summary>
/// The grid used within a ScratchPad frame
/// </summary>
public class ScratchPadGrid : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ScratchPadGrid(string name, Brush brush, TileGrid referenceGrid, int width, int height)
        : base(name)
    {
        base.TileWidth = referenceGrid.TileWidth;
        base.TileHeight = referenceGrid.TileHeight;
        base.Width = width;
        base.Height = height;

        this.ReferenceGrid = referenceGrid;

        tileGrid = new int[Width, Height];

        // Register mouse buttons for tile selection & placement
        base.RegisterTilePlacementInputs(
            brush,
            (x, y) => GetTile(x, y),
            (x, y, t) => SetTile(x, y, t),
            () => Width,
            () => Height);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    int[,] tileGrid;

    // ================================================================================
    // Properties
    // ================================================================================

    /// <summary>
    /// A TileGrid where each tile index provides the image to use for that value.
    /// It must override the Image property rather than the TileDrawer function for this to work.
    /// </summary>
    TileGrid ReferenceGrid { get; set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void SetTile(int x, int y, int value)
    {
        tileGrid[x, y] = value;
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected override void TileDrawer(int index)
    {
        int value = GetTile(index);
        ReferenceGrid.DrawTileImage(value, Scale);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Returns the tile index at the given position in the scratchpad.
    /// </summary>
    int GetTile(int index)
    {
        var (x, y) = TileToXY(index);
        return tileGrid[x, y];
    }

    int GetTile(int x, int y)
    {
        return tileGrid[x, y];
    }
}
