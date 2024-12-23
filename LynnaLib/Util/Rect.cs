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

        // ================================================================================
        // Operator overloads
        // ================================================================================

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
