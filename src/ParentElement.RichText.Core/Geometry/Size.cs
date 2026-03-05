namespace ParentElement.RichText.Core.Geometry;

public struct Size
{
    /// <summary>The width component in pixels.</summary>
    public float Width { get; }

    /// <summary>The height component in pixels.</summary>
    public float Height { get; }

    /// <summary>Initializes a new <see cref="Size"/> with the given width and height.</summary>
    public Size(float width, float height)
    {
        Width = width;
        Height = height;
    }
}
