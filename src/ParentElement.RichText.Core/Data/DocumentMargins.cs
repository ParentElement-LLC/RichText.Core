namespace ParentElement.RichText.Core.Data;

public struct DocumentMargins
{
    /// <summary>The left margin in pixels.</summary>
    public float Left { get; init; }

    /// <summary>The right margin in pixels.</summary>
    public float Right { get; init; }

    /// <summary>The top margin in pixels.</summary>
    public float Top { get; init; }

    /// <summary>The bottom margin in pixels.</summary>
    public float Bottom { get; init; }

    /// <summary>Initializes a new <see cref="DocumentMargins"/> with the given margin values in pixels.</summary>
    public DocumentMargins(float left, float right, float top, float bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }
}
