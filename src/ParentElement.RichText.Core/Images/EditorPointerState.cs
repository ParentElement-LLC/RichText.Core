using ParentElement.RichText.Core.Controllers;

namespace ParentElement.RichText.Core.Images;

/// <summary>
/// Platform-agnostic cursor hint that DocumentController sends to the UI layer
/// via the <see cref="DocumentController.RequestCursorUpdate"/> callback.
/// </summary>
public enum EditorCursor
{
    /// <summary>The default text-editing cursor.</summary>
    Default,
    /// <summary>North-south resize cursor, used for top and bottom edge handles.</summary>
    ResizeNS,
    /// <summary>East-west resize cursor, used for left and right edge handles and column dividers.</summary>
    ResizeEW,
    /// <summary>Northwest-southeast diagonal resize cursor, used for the TopLeft and BottomRight corner handles.</summary>
    ResizeDiagNWSE,
    /// <summary>Northeast-southwest diagonal resize cursor, used for the TopRight and BottomLeft corner handles.</summary>
    ResizeDiagNESW,
    /// <summary>Move cursor, shown when the pointer is over an image body to indicate it can be dragged.</summary>
    Move,
}
