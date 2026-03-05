using ParentElement.RichText.Core.Data;
using SkiaSharp;

namespace ParentElement.RichText.Core.Geometry;

public struct Point
{
    /// <summary>Initializes a new <see cref="Point"/> with the given coordinates.</summary>
    public Point(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The horizontal coordinate in pixels.</summary>
    public float X { get; set; }

    /// <summary>The vertical coordinate in pixels.</summary>
    public float Y { get; set; }

    /// <summary>Converts this point to a SkiaSharp <see cref="SKPoint"/>.</summary>
    public SKPoint ToSkia()
    {
        return new SKPoint(X, Y);
    }

    /// <summary>
    /// Transforms this point from view (screen) space into document space by applying
    /// the inverse of the given <paramref name="view"/> scale and offset.
    /// </summary>
    public Point FromView(ViewModifier view)
    {
        var x = (X / view.Scale.X + view.ScaledOffset.X);
        var y = (Y / view.Scale.X + view.ScaledOffset.Y);

        return new Point(x, y);
    }
}
