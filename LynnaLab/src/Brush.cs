namespace LynnaLab;

/// <summary>
/// Represents a grid of selected tiles (or just a single tile) which can be drawn somewhere else.
///
/// The "tiles" are represented as the template class "T". This is usually "int", as typical tiles
/// can be represented as an int, but they can be something more complex if called for. IE. Subtiles
/// require an integer for the tile in addition to FlipX/Y, palette, and so on.
///
/// Primarily used for room tile selection, but could potentially be used for other things.
/// </summary>
public class Brush<T>
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public Brush(T defaultTile)
    {
        tiles = new T[,] { { defaultTile } };
    }

    // ================================================================================
    // Variables
    // ================================================================================

    T[,] tiles;

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
    public T GetTile(int x, int y)
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
    public void SetTile(TileGrid source, T tile)
    {
        Source = source;
        tiles = new T[,] { { tile } };
        BrushChanged?.Invoke(this, null);
    }

    /// <summary>
    /// Set the array of tiles used by this brush.
    /// </summary>
    public void SetTiles(TileGrid source, T[,] newTiles)
    {
        Source = source;
        tiles = newTiles;
        BrushChanged?.Invoke(this, null);
    }

    /// <summary>
    /// Calls the given function for each tile in the brush, using (xOffset, yOffset) as an offset
    /// to pass to that function.
    /// Mainly used for drawing, but can be used for anything involving looping all tiles affected
    /// by the brush.
    /// </summary>
    public void Draw(Action<int, int, T> drawer, int xOffset, int yOffset, int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int destX = x + xOffset;
                int destY = y + yOffset;
                drawer(destX, destY, tiles[x % BrushWidth, y % BrushHeight]);
            }
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}
