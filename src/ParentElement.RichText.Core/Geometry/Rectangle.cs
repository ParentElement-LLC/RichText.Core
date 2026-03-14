using SkiaSharp;

namespace ParentElement.RichText.Core.Geometry;

/// <summary>An axis-aligned rectangle in pixel coordinates, defined by its position and size.</summary>
public struct Rectangle
{
    /// <summary>The x-coordinate of the left edge of the rectangle.</summary>
    public float X { get; }

    /// <summary>The y-coordinate of the top edge of the rectangle.</summary>
    public float Y { get; }

    /// <summary>The width of the rectangle in pixels.</summary>
    public float Width { get; }

    /// <summary>The height of the rectangle in pixels.</summary>
    public float Height { get; }

    /// <summary>The y-coordinate of the top edge. Equivalent to <see cref="Y"/>.</summary>
    public float Top => Y;

    /// <summary>The y-coordinate of the bottom edge. Equivalent to <c>Y + Height</c>.</summary>
    public float Bottom => Y + Height;

    /// <summary>The x-coordinate of the left edge. Equivalent to <see cref="X"/>.</summary>
    public float Left => X;

    /// <summary>The x-coordinate of the right edge. Equivalent to <c>X + Width</c>.</summary>
    public float Right => X + Width;

    /// <summary>The top-left corner of the rectangle.</summary>
    public Point TopLeft => new Point(X, Y);

    /// <summary>The top-right corner of the rectangle.</summary>
    public Point TopRight => new Point(X + Width, Y);

    /// <summary>The bottom-left corner of the rectangle.</summary>
    public Point BottomLeft => new Point(X, Y + Height);

    /// <summary>The bottom-right corner of the rectangle.</summary>
    public Point BottomRight => new Point(X + Width, Y + Height);

    /// <summary>The center point of the rectangle.</summary>
    public Point Center => new Point(X + (Width / 2), Y + (Height / 2));

    /// <summary>Initializes a new <see cref="Rectangle"/> with explicit position and size.</summary>
    public Rectangle(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Initializes a new <see cref="Rectangle"/> by copying the position and size of <paramref name="other"/>.</summary>
    public Rectangle(Rectangle other)
    {
        X = other.X;
        Y = other.Y;
        Width = other.Width;
        Height = other.Height;
    }

    /// <summary>Initializes a new <see cref="Rectangle"/> positioned at the origin with the given size.</summary>
    public Rectangle(float width, float height)
    {
        X = 0;
        Y = 0;
        Width = width;
        Height = height;
    }

    /// <summary>Returns a new rectangle whose width and height are each increased by the given amounts. Position is unchanged.</summary>
    public Rectangle Inflate(float widthAmount, float heightAmount) => new Rectangle(X, Y, Width + widthAmount, Height + heightAmount);

    /// <summary>Returns a new rectangle whose width and height are each increased by <paramref name="amount"/>. Position is unchanged.</summary>
    public Rectangle Inflate(float amount) => Inflate(amount, amount);

    /// <summary>Returns a new rectangle whose width and height are each decreased by the given amounts. Position is unchanged.</summary>
    public Rectangle Deflate(float widthAmount, float heightAmount) => Inflate(-widthAmount, -heightAmount);

    /// <summary>Returns a new rectangle whose width and height are each decreased by <paramref name="amount"/>. Position is unchanged.</summary>
    public Rectangle Deflate(float amount) => Deflate(amount, amount);

    /// <summary>Converts this rectangle to a SkiaSharp <see cref="SKRect"/>.</summary>
    public SKRect ToSkia()
    {
        return new SKRect(X, Y, X + Width, Y + Height);
    }
}
