namespace ParentElement.RichText.Core.Images;

/// <summary>
/// Identifies the resize handle being dragged on a selected image.
/// Corner handles (TopLeft, TopRight, BottomRight, BottomLeft) maintain aspect ratio.
/// Edge handles (TopCenter, MiddleRight, BottomCenter, MiddleLeft) resize one axis freely.
/// </summary>
public enum ResizeHandleType
{
    None,
    TopLeft,
    TopCenter,
    TopRight,
    MiddleRight,
    BottomRight,
    BottomCenter,
    BottomLeft,
    MiddleLeft,
}
