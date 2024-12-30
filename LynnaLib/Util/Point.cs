namespace Util;

public struct Point
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================

    public int X { get; set; }
    public int Y { get; set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    // ================================================================================
    // Implicit operators
    // ================================================================================
    public static Point operator+(Point p1, Point p2)
    {
        return new Point(p1.X + p2.X, p1.Y + p2.Y);
    }
}