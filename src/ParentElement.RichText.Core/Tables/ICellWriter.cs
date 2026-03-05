using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;
using SkiaSharp;

namespace ParentElement.RichText.Core.Tables;

/// <summary>
/// Write-only abstraction for populating a table cell's content programmatically
/// (e.g. during document import).  Mirrors the subset of <see cref="ParentElement.RichText.Core.Tables.CellController"/>
/// that is relevant to content population.
/// </summary>
public interface ICellWriter
{
    /// <summary>Inserts raw text at the current caret position.</summary>
    void Insert(string text);

    /// <summary>Sets the current character style used for subsequent <see cref="Insert"/> calls.</summary>
    void ApplyStyle(IStyle style);

    /// <summary>Sets the paragraph alignment of the current paragraph.</summary>
    void ApplyAlignment(TextAlignment alignment);

    /// <summary>Sets the list type and level of the current paragraph directly (no toggle logic).</summary>
    void ApplyListFormat(ListType type, int level);

    /// <summary>Sets the left block indent of the current paragraph in pixels.</summary>
    void SetBlockIndent(float value);

    /// <summary>Sets the line-height multiplier for the current paragraph.</summary>
    void AdjustLineSpacing(float amount);

    /// <summary>Inserts an inline image at the current caret position.</summary>
    void InsertInlineImage(SKImage image, float w, float h);
}
