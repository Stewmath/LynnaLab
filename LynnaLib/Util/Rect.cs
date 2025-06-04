using System.Numerics;

namespace Util
{
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

        public override bool Equals(object o)
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

        public static bool operator==(FRect rect1, FRect rect2)
        {
            if (rect1 is null || rect2 is null)
                return (rect1 is null && rect2 is null);

            return rect1.X == rect2.X
            && rect1.Y == rect2.Y
            && rect1.Width == rect2.Width
            && rect1.Height == rect2.Height;
        }

        public static bool operator!=(FRect rect1, FRect rect2)
        {
            return !(rect1 == rect2);
        }

        public static FRect operator*(FRect rect1, float scale)
        {
            return new FRect(rect1.X * scale, rect1.Y * scale,
                             rect1.Width * scale, rect1.Height * scale);
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
}
