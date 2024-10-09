namespace LynnaLab;

/// <summary>
/// Represents a grid of selected tiles (or just a single tile) which can be drawn somewhere else.
///
/// Primarily used for room tile selection, but could potentially be used for other things.
/// </summary>
public class Brush
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public Brush()
    {

    }

    // ================================================================================
    // Variables
    // ================================================================================

    int[,] tiles;

    // ================================================================================
    // Events
    // ================================================================================

    public event EventHandler BrushChanged;

    // ================================================================================
    // Properties
    // ================================================================================

    public int BrushWidth { get { return tiles.GetLength(0); } }
    public int BrushHeight { get { return tiles.GetLength(1); } }

    public bool IsSingleTile { get { return BrushWidth == 1 && BrushHeight == 1; } }

    /// <summary>
    /// The source from which the current brush pattern was retrieved.
    /// IE. Right-clicking on the tileset viewer sents the Source to that tileset viewer.
    /// </summary>
    public TileGrid Source { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Get the tile at position (x, y) in the brush.
    /// </summary>
    public int GetTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= BrushWidth || y >= BrushHeight)
        {
            throw new Exception($"Position {x},{y} outside of brush range");
        }

        return tiles[x,y];
    }

    /// <summary>
    /// Turn this into a 1x1 brush using the specified tile.
    /// </summary>
    public void SetTile(TileGrid source, int tile)
    {
        Source = source;
        tiles = new int[,] { { tile } };
        BrushChanged?.Invoke(this, null);
    }

    /// <summary>
    /// Set the array of tiles used by this brush.
    /// </summary>
    public void SetTiles(TileGrid source, int[,] newTiles)
    {
        Source = source;
        tiles = newTiles;
        BrushChanged?.Invoke(this, null);
    }

    /// <summary>
    /// Calls the given function for each tile in the brush, using (xOffset, yOffset) as an offset
    /// to pass to that function.
    /// </summary>
    public void Draw(Action<int, int, int> drawer, int xOffset, int yOffset)
    {
        for (int x = 0; x < BrushWidth; x++)
        {
            for (int y = 0; y < BrushHeight; y++)
            {
                int destX = x + xOffset;
                int destY = y + yOffset;
                drawer(destX, destY, tiles[x,y]);
            }
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}
