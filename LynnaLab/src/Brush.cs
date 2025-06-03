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

/// <summary>
/// Provides a non-generic interface for getting some data from the Brush class.
/// I made this so that the TileGrid class could access Brush fields without being a generic class itself.
/// </summary>
public class BrushInterfacer
{
    // ================================================================================
    // Constructors
    // ================================================================================

    /// <summary>
    /// Create a BrushInterfacer. "tileDrawer(index)" is a function which draws the given tile
    /// index, assuming that the ImGui cursor is already at the correct position before calling it.
    /// </summary>
    public static BrushInterfacer Create<T>(Brush<T> brush, Action<T, float> tileDrawer)
    {
        BrushInterfacer interfacer = Initialize(brush);

        var prepAndDraw = (int x, int y, T i, float s, Func<int, int, bool> prepTile) =>
        {
            if (prepTile(x, y))
                tileDrawer(i, s);
        };

        // This is a pretty complicated lambda. The gist of it is to create a lambda which "cancels
        // out" the template parameter T so that "drawAll" can be a class field. (This class is not
        // generic, so we can't store "brush" or the "tileDrawer" lambda in class fields.)
        interfacer.drawAll = (prepTile, x1, y1, w, h, s) => brush.Draw((x, y, i) => prepAndDraw(x, y, i, s, prepTile), x1, y1, w, h);

        return interfacer;
    }

    /// <summary>
    /// Create a BrushInterfacer, using a texture to draw the preview instead of a per-tile drawer function.
    /// </summary>
    public static BrushInterfacer Create<T>(Brush<T> brush)
    {
        BrushInterfacer interfacer = Initialize(brush);
        return interfacer;
    }

    static BrushInterfacer Initialize<T>(Brush<T> brush)
    {
        BrushInterfacer interfacer = new BrushInterfacer
        {
            getWidth = () => brush.BrushWidth,
            getHeight = () => brush.BrushHeight,
        };
        return interfacer;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Func<int> getWidth, getHeight;

    // Only one of these should be non-null. This determines how the image will be drawn.
    Action<Func<int, int, bool>, int, int, int, int, float> drawAll;
    TextureBase previewTexture;
    int tileSize; // used with previewTexture

    // ================================================================================
    // Properties
    // ================================================================================

    public int BrushWidth { get { return getWidth(); } }
    public int BrushHeight { get { return getHeight(); } }
    public Point BrushSize { get { return new Point(BrushWidth, BrushHeight); } }

    // ================================================================================
    // Methods
    // ================================================================================

    /// <summary>
    /// prepTile(x, y): Function called immediately before drawing each tile; should set ImGui's
    /// render position to the position at which it should be drawn, or return false if the (x, y)
    /// position is invalid.
    /// </summary>
    public void Draw(Func<int, int, bool> prepTile, int x1, int y1, int width, int height, float scale)
    {
        if (previewTexture != null)
        {
            for (int x=x1; x<x1+width; x++)
            {
                for (int y=y1; y<y1+height; y++)
                {
                    if (!prepTile(x, y))
                        continue;
                    Vector2 offset = new Vector2((x - x1) % BrushWidth, (y - y1) % BrushHeight) * tileSize;
                    ImGuiX.DrawImage(previewTexture, scale, topLeft: offset, bottomRight: offset + new Vector2(tileSize, tileSize));
                }
            }
        }
        else if (drawAll != null)
            drawAll(prepTile, x1, y1, width, height, scale);
    }

    /// <summary>
    /// Sets the preview texture, disposing of the previous one if it exists.
    /// </summary>
    public void SetPreviewTexture(TextureBase texture, int tileSize)
    {
        previewTexture?.Dispose();
        previewTexture = texture;
        this.tileSize = tileSize;
    }
}
