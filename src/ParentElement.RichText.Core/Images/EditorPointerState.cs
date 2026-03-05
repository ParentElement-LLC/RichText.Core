using ParentElement.RichText.Core.Controllers;

namespace ParentElement.RichText.Core.Images;

/// <summary>
/// Platform-agnostic cursor hint that DocumentController sends to the UI layer
/// via the <see cref="DocumentController.RequestCursorUpdate"/> callback.
/// </summary>
public enum EditorCursor
{
    Default,
    ResizeNS,
    ResizeEW,
    ResizeDiagNWSE,  // NW ↔ SE diagonal (TopLeft / BottomRight handles)
    ResizeDiagNESW,  // NE ↔ SW diagonal (TopRight / BottomLeft handles)
    Move,            // Image body – indicates drag-to-move
}
