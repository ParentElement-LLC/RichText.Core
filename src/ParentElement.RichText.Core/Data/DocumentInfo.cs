using System.Numerics;

namespace ParentElement.RichText.Core.Data;

public struct DocumentInfo
{
    /// <summary>The total rendered height of the document content in pixels.</summary>
    public readonly float Height;

    /// <summary>The width of the document page in pixels.</summary>
    public readonly float Width;

    /// <summary>The current scroll offset of the viewport in pixels.</summary>
    public readonly Vector2 ScrollOffset;

    /// <summary>Initializes a new <see cref="DocumentInfo"/> with the given dimensions and scroll position.</summary>
    public DocumentInfo(float width, float height, Vector2 scrollOffset)
    {
        Width = width;
        Height = height;
        ScrollOffset = scrollOffset;
    }
}
