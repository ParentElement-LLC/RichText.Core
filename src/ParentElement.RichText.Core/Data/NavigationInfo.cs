using ParentElement.Topten.RichTextKit;

namespace ParentElement.RichText.Core.Data;

/// <summary>Caret/selection state raised via <see cref="Abstractions.Controllers.IDocumentController.OnNavigation"/> whenever the selection changes.</summary>
public struct NavigationInfo
{
    /// <summary>The resolved character style at the current caret position.</summary>
    public IStyle StyleAtCaret { get; }

    /// <summary>Detailed information about the current selection, including word and paragraph boundaries.</summary>
    public SelectionInfo SelectionInfo { get; }

    /// <summary>The current text selection range within the document.</summary>
    public TextRange Selection { get; }

    /// <summary>Initializes a new <see cref="NavigationInfo"/> with the given selection range, caret style, and selection details.</summary>
    public NavigationInfo(TextRange selection, IStyle styleAtCaret, SelectionInfo selectionInfo)
    {
        Selection = selection;
        StyleAtCaret = styleAtCaret;
        SelectionInfo = selectionInfo;
    }
}
