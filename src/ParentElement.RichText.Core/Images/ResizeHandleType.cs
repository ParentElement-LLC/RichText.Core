namespace ParentElement.RichText.Core.Images;

/// <summary>
/// Identifies the resize handle being dragged on a selected image.
/// Corner handles (TopLeft, TopRight, BottomRight, BottomLeft) maintain aspect ratio.
/// Edge handles (TopCenter, MiddleRight, BottomCenter, MiddleLeft) resize one axis freely.
/// </summary>
public enum ResizeHandleType
{
    /// <summary>No handle; the image is not being resized.</summary>
    None,
    /// <summary>Top-left corner handle; drag maintains aspect ratio.</summary>
    TopLeft,
    /// <summary>Top-center edge handle; drag resizes height only.</summary>
    TopCenter,
    /// <summary>Top-right corner handle; drag maintains aspect ratio.</summary>
    TopRight,
    /// <summary>Middle-right edge handle; drag resizes width only.</summary>
    MiddleRight,
    /// <summary>Bottom-right corner handle; drag maintains aspect ratio.</summary>
    BottomRight,
    /// <summary>Bottom-center edge handle; drag resizes height only.</summary>
    BottomCenter,
    /// <summary>Bottom-left corner handle; drag maintains aspect ratio.</summary>
    BottomLeft,
    /// <summary>Middle-left edge handle; drag resizes width only.</summary>
    MiddleLeft,
}
