using System.Numerics;

namespace Util;

/// <summary>
/// Floating-point rect
/// </summary>
public class FRect
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public FRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }

    public Vector2 Size { get { return new Vector2(Width, Height); } }

    public Vector2 TopLeft { get { return new Vector2(X, Y); } }
    public Vector2 BottomRight { get { return TopLeft + Size; } }
    public Vector2 Center { get { return (TopLeft + BottomRight) / 2; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public bool Contains(Vector2 point)
    {
        return point.X >= X && point.X <= X + Width
            && point.Y >= Y && point.Y <= Y + Height;
    }

    public override bool Equals(object? o)
    {
        return (o is FRect r) && this == r;
    }

    public override int GetHashCode()
    {
        return X.GetHashCode() * 23 + Y.GetHashCode() * 17 + Width.GetHashCode() * 11 + Height.GetHashCode();
    }

    // ================================================================================
    // Operator overloads
    // ================================================================================

    public static bool operator ==(FRect rect1, FRect rect2)
    {
        if (rect1 is null || rect2 is null)
            return (rect1 is null && rect2 is null);

        return rect1.X == rect2.X
        && rect1.Y == rect2.Y
        && rect1.Width == rect2.Width
        && rect1.Height == rect2.Height;
    }

    public static bool operator !=(FRect rect1, FRect rect2)
    {
        return !(rect1 == rect2);
    }

    public static FRect operator *(FRect rect1, float scale)
    {
        return new FRect(rect1.X * scale, rect1.Y * scale,
                         rect1.Width * scale, rect1.Height * scale);
    }

    public static FRect operator +(FRect rect, Vector2 point)
    {
        return new FRect(rect.X + point.X, rect.Y + point.Y, rect.Width, rect.Height);
    }

    public static FRect operator -(FRect rect, Vector2 point)
    {
        return new FRect(rect.X - point.X, rect.Y - point.Y, rect.Width, rect.Height);
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    /// <summary>
    /// Return a rectangle which contains the two points.
    /// </summary>
    public static FRect FromVectors(Vector2 p1, Vector2 p2)
    {
        Vector2 tl = new Vector2(
            Math.Min(p1.X, p2.X),
            Math.Min(p1.Y, p2.Y)
        );
        Vector2 br = new Vector2(
            Math.Max(p1.X, p2.X),
            Math.Max(p1.Y, p2.Y)
        );

        return new FRect(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
    }
}


/// <summary>
/// Integer rect
/// </summary>
public class PRect
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public PRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Point Size { get { return new Point(Width, Height); } }

    public Point TopLeft { get { return new Point(X, Y); } }
    public Point BottomRight { get { return TopLeft + Size - new Point(1, 1); } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public bool Contains(Point point)
    {
        return point.X >= X && point.X < X + Width
            && point.Y >= Y && point.Y < Y + Height;
    }

    public override bool Equals(object? o)
    {
        return (o is PRect r) && this == r;
    }

    public override int GetHashCode()
    {
        return X.GetHashCode() * 23 + Y.GetHashCode() * 17 + Width.GetHashCode() * 11 + Height.GetHashCode();
    }

    public void ForEachPoint(System.Action<Point> action)
    {
        for (int x = TopLeft.X; x <= BottomRight.X; x++)
        {
            for (int y = TopLeft.Y; y <= BottomRight.Y; y++)
            {
                action(new Point(x, y));
            }
        }
    }

    // ================================================================================
    // Operator overloads
    // ================================================================================

    public static bool operator ==(PRect rect1, PRect rect2)
    {
        if (rect1 is null || rect2 is null)
            return (rect1 is null && rect2 is null);

        return rect1.X == rect2.X
        && rect1.Y == rect2.Y
        && rect1.Width == rect2.Width
        && rect1.Height == rect2.Height;
    }

    public static bool operator !=(PRect rect1, PRect rect2)
    {
        return !(rect1 == rect2);
    }

    public static PRect operator *(PRect rect1, int scale)
    {
        return new PRect(rect1.X * scale, rect1.Y * scale,
                         rect1.Width * scale, rect1.Height * scale);
    }

    public static PRect operator +(PRect rect, Point point)
    {
        return new PRect(rect.X + point.X, rect.Y + point.Y, rect.Width, rect.Height);
    }

    public static PRect operator -(PRect rect, Point point)
    {
        return new PRect(rect.X - point.X, rect.Y - point.Y, rect.Width, rect.Height);
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    /// <summary>
    /// Return a rectangle which contains the two points.
    /// </summary>
    public static PRect FromPoints(Point p1, Point p2)
    {
        Point tl = new Point(
            Math.Min(p1.X, p2.X),
            Math.Min(p1.Y, p2.Y)
        );
        Point br = new Point(
            Math.Max(p1.X, p2.X),
            Math.Max(p1.Y, p2.Y)
        );

        return new PRect(tl.X, tl.Y, br.X - tl.X + 1, br.Y - tl.Y + 1);
    }
}
