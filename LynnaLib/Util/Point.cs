using System.Numerics;

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

    public int X { get; }
    public int Y { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public Vector2 AsVector2()
    {
        return new Vector2(X, Y);
    }

    public override string ToString()
    {
        return $"<X: {X}, Y: {Y}";
    }

    public override int GetHashCode()
    {
        return X * 38913 + Y;
    }

    public override bool Equals(Object? o)
    {
        if (!(o is Point p))
            return false;
        return this == p;
    }

    // ================================================================================
    // Implicit operators
    // ================================================================================
    public static Point operator+(Point p1, Point p2)
    {
        return new Point(p1.X + p2.X, p1.Y + p2.Y);
    }

    public static Point operator-(Point p1, Point p2)
    {
        return new Point(p1.X - p2.X, p1.Y - p2.Y);
    }

    public static Point operator*(Point p1, int mult)
    {
        return new Point(p1.X * mult, p1.Y * mult);
    }

    public static Point operator*(Point p1, Point p2)
    {
        return new Point(p1.X * p2.X, p1.Y * p2.Y);
    }

    public static bool operator==(Point p1, Point p2)
    {
        return p1.X == p2.X && p1.Y == p2.Y;
    }

    public static bool operator!=(Point p1, Point p2)
    {
        return !(p1 == p2);
    }
}
