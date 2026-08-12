namespace LynnaLab;

using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// A polygon with float coordinates.
/// </summary>
public class Polygon
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public Polygon(IList<Vector2> coordinates)
    {
        this.Coordinates = coordinates;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    // ================================================================================
    // Properties
    // ================================================================================

    public IList<Vector2> Coordinates { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override string ToString()
    {
        return $"[{string.Join(", ", Coordinates)}]";
    }

    // ================================================================================
    // Operator overloads
    // ================================================================================

    public static Polygon operator *(Polygon p, Vector2 m)
    {
        var coordinates = new List<Vector2>(p.Coordinates.Count);
        foreach (var coord in p.Coordinates)
            coordinates.Add(coord * m);
        return new Polygon(coordinates);
    }

    public static Polygon operator *(Polygon p, float f)
    {
        var coordinates = new List<Vector2>(p.Coordinates.Count);
        foreach (var coord in p.Coordinates)
            coordinates.Add(coord * f);
        return new Polygon(coordinates);
    }

    public static Polygon operator +(Polygon p, Vector2 v)
    {
        var coordinates = new List<Vector2>(p.Coordinates.Count);
        foreach (var coord in p.Coordinates)
            coordinates.Add(coord + v);
        return new Polygon(coordinates);
    }

    public static Polygon operator -(Polygon p, Vector2 v)
    {
        var coordinates = new List<Vector2>(p.Coordinates.Count);
        foreach (var coord in p.Coordinates)
            coordinates.Add(coord - v);
        return new Polygon(coordinates);
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}
