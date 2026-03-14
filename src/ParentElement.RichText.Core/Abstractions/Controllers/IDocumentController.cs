using ParentElement.RichText.Core.Content;
using ParentElement.RichText.Core.Data;
using ParentElement.RichText.Core.Geometry;
using ParentElement.RichText.Core.Images;
using ParentElement.RichText.Core.Input;
using ParentElement.RichText.Core.Tables;
using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;
using SkiaSharp;

namespace ParentElement.RichText.Core.Abstractions.Controllers
{
    /// <summary>
    /// The primary abstraction for a rich-text editor. Provides document editing, formatting,
    /// navigation, clipboard, undo/redo, table management, image support, and rendering operations.
    /// </summary>
    public interface IDocumentController: ITextDocumentView
    {
        /// <summary>Gets or sets the background color of the document page.</summary>
        SKColor BackgroundColor { get; set; }

        /// <summary>Gets the total rendered height of the document content in pixels.</summary>
        float DocumentHeight { get; }

        /// <summary>Gets or sets whether the editor currently has keyboard focus.</summary>
        bool HasFocus { get; set; }

        /// <summary>Gets whether the document has unsaved changes since the last <see cref="MarkClean"/> call.</summary>
        bool IsModified { get; }

        /// <summary>Gets or sets the width of the document page in pixels.</summary>
        float PageWidth { get; set; }

        /// <summary>Gets or sets whether the document is read-only. When true, all editing operations are suppressed.</summary>
        bool ReadOnly { get; set; }

        /// <summary>Gets or sets a callback invoked when the pointer cursor shape should change.</summary>
        Action<EditorCursor>? RequestCursorUpdate { get; set; }

        /// <summary>Gets or sets a callback invoked when the canvas should be redrawn.</summary>
        Action? RequestRedraw { get; set; }

        /// <summary>Gets or sets the zoom scale applied when translating pointer coordinates to document coordinates.</summary>
        float ScrollScale { get; set; }

        /// <summary>Gets or sets the color used to highlight selected text.</summary>
        SKColor SelectionColor { get; set; }

        /// <summary>Gets the current document settings such as page size, margins, and default style.</summary>
        DocumentSettings Settings { get; }

        /// <summary>Gets the keyboard shortcut handler used to register and execute shortcut actions.</summary>
        ShortcutHandler Shortcuts { get; }

        /// <summary>Gets or sets the portion of the document currently visible in the viewport, in document coordinates.</summary>
        Rectangle VisibleBounds { get; set; }

        /// <summary>Raised when the total size of the document content changes.</summary>
        event Action<DocumentInfo>? OnContentSizeChanged;

        /// <summary>Raised when the caret moves or the selection changes, providing updated style and selection information.</summary>
        event Action<NavigationInfo>? OnNavigation;

        /// <summary>Adjusts the line height multiplier of the current paragraph by the given <paramref name="amount"/>.</summary>
        void AdjustLineSpacing(float amount);

        /// <summary>Adjusts the left indent of the current paragraph by the given <paramref name="amount"/> in pixels.</summary>
        void AdjustParagraphIndent(float amount);

        /// <summary>Applies the specified <paramref name="alignment"/> to the selected paragraphs.</summary>
        Task ApplyAlignment(TextAlignment alignment);

        /// <summary>Applies the specified background highlight <paramref name="color"/> to the selected text.</summary>
        Task ApplyBackgroundColor(SKColor color);

        /// <summary>Toggles bold formatting on the current selection.</summary>
        Task ApplyBold();

        /// <summary>Replaces the current document settings with <paramref name="settings"/> and re-renders the document.</summary>
        void ApplyDocumentSettings(DocumentSettings settings);

        /// <summary>Applies the specified foreground <paramref name="color"/> to the selected text.</summary>
        Task ApplyFontColor(SKColor color);

        /// <summary>Applies the specified <paramref name="fontFamily"/> to the selected text.</summary>
        Task ApplyFontFamily(string fontFamily);

        /// <summary>Applies the specified <paramref name="fontSize"/> (in points) to the selected text.</summary>
        Task ApplyFontSize(int fontSize);

        /// <summary>Toggles italic formatting on the current selection.</summary>
        Task ApplyItalic();

        /// <summary>Applies the given list <paramref name="type"/> and nesting <paramref name="level"/> to the selected paragraphs.</summary>
        void ApplyListFormat(ListType type, int level);

        /// <summary>Toggles strikethrough formatting on the current selection.</summary>
        Task ApplyStrikethrough();

        /// <summary>Applies all formatting attributes of the given <paramref name="style"/> to the current selection.</summary>
        Task ApplyStyle(IStyle style);

        /// <summary>Toggles subscript formatting on the current selection.</summary>
        Task ApplySubscript();

        /// <summary>Toggles superscript formatting on the current selection.</summary>
        Task ApplySuperscript();

        /// <summary>Toggles underline formatting on the current selection.</summary>
        Task ApplyUnderline();

        /// <summary>Increases or decreases the nesting level of the current list item by <paramref name="delta"/>.</summary>
        void ChangeListLevel(int delta);

        /// <summary>Removes all content from the document and resets it to an empty state.</summary>
        void ClearDocument();

        /// <summary>Moves the caret or adjusts the selection to the document position nearest to the given <paramref name="point"/>.</summary>
        void Click(Point point);

        /// <summary>Copies the current selection to the clipboard. Returns <c>true</c> if content was copied.</summary>
        Task<bool> Copy();

        /// <summary>Cuts the current selection to the clipboard.</summary>
        Task Cut();

        /// <summary>Extends the current selection to the document position nearest to the given <paramref name="point"/>.</summary>
        void DragTo(Point point);

        /// <summary>Renders the visible portion of the document onto the given <paramref name="canvas"/>.</summary>
        void Draw(SKCanvas canvas);

        /// <summary>Returns a <see cref="DocumentReader"/> that provides structured access to the document content.</summary>
        DocumentReader GetContentReader();

        /// <summary>Returns the total number of pages in the document based on the current page height setting.</summary>
        int GetPageCount();

        /// <summary>Updates the pointer cursor hint for the document position nearest to the given <paramref name="point"/> without changing the selection.</summary>
        void HoverAt(Point point);

        /// <summary>Inserts the given <paramref name="text"/> at the current caret position, replacing any active selection.</summary>
        void Insert(string text);

        /// <summary>Inserts an inline image at the current caret position with the given display dimensions.</summary>
        void InsertInlineImage(SKImage image, float displayWidth, float displayHeight);

        /// <summary>Inserts an inline table at the current caret position using the given <paramref name="options"/> and returns the new table.</summary>
        InlineTable InsertTable(TableOptions options);

        /// <summary>Gets whether the caret is currently positioned inside a table cell.</summary>
        bool IsInTableCell { get; }

        /// <summary>Gets whether the caret is currently positioned inside a table header row.</summary>
        bool IsInTableHeaderRow { get; }

        /// <summary>Deletes the table row that contains the current caret position.</summary>
        void DeleteTableRow();

        /// <summary>Deletes the table column that contains the current caret position.</summary>
        void DeleteTableColumn();

        /// <summary>Inserts a new row above the row that contains the current caret position.</summary>
        void InsertTableRowBefore();

        /// <summary>Inserts a new row below the row that contains the current caret position.</summary>
        void InsertTableRowAfter();

        /// <summary>Inserts a new column to the left of the column that contains the current caret position.</summary>
        void InsertTableColumnBefore();

        /// <summary>Inserts a new column to the right of the column that contains the current caret position.</summary>
        void InsertTableColumnAfter();

        /// <summary>Gets whether the user is currently performing a multi-cell selection within a table.</summary>
        bool IsMultiCellSelectingTable { get; }

        /// <summary>Returns <c>true</c> if the current multi-cell selection can be merged into a single cell.</summary>
        bool CanMergeSelectedCells();

        /// <summary>Merges all cells in the current multi-cell selection into a single spanned cell.</summary>
        void MergeSelectedCells();

        /// <summary>Clears the modified flag, marking the current document state as the clean baseline.</summary>
        void MarkClean();

        /// <summary>Processes a keyboard event, executing any mapped shortcut or inserting/deleting text as appropriate. Returns <c>true</c> if the event was handled.</summary>
        Task<bool> OnKeyEvent(KeyInfo key);

        /// <summary>Pastes the current clipboard content at the caret position, replacing any active selection.</summary>
        Task Paste();

        /// <summary>Signals that the pointer button has been released at the given <paramref name="point"/>.</summary>
        void PointerReleased(Point point);

        /// <summary>Renders the entire document to a PDF and writes it to the given <paramref name="stream"/>.</summary>
        void PrintToPdf(Stream stream);

        /// <summary>Re-applies the most recently undone operation.</summary>
        Task Redo();

        /// <summary>Renders the specified <paramref name="pageNumber"/> (1-based) to a bitmap at the given <paramref name="scale"/>.</summary>
        SKBitmap RenderPageToBitmap(int pageNumber, float scale);

        /// <summary>Replaces the formatting attributes of text that matches the given <paramref name="style"/> with those of <paramref name="style"/>.</summary>
        void ReplaceStyle(IStyle style);

        /// <summary>Scrolls the viewport by the given offset in pixels.</summary>
        void ScrollBy(float x, float y);

        /// <summary>Scrolls the viewport to the given absolute position in pixels.</summary>
        void ScrollTo(float x, float y);

        /// <summary>Selects all content in the document.</summary>
        Task SelectAll();

        /// <summary>Collapses the current selection to a caret without moving the caret position.</summary>
        void SelectNone();

        /// <summary>Sets the absolute left block indent of the current paragraph to <paramref name="value"/> pixels.</summary>
        void SetBlockIndent(float value);

        /// <summary>Toggles a bullet (unordered) list on the selected paragraphs.</summary>
        void ToggleBulletList();

        /// <summary>Toggles a first-line indent on the current paragraph.</summary>
        void ToggleFirstLineIndent();

        /// <summary>Toggles a numbered (ordered) list on the selected paragraphs.</summary>
        void ToggleNumberedList();

        /// <summary>Reverses the most recently applied editing operation.</summary>
        Task Undo();
    }
}
