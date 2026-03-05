using ParentElement.Topten.RichTextKit;
using SkiaSharp;

namespace ParentElement.RichText.Core.Tables;

public class TableCell
{
    internal CellController Controller { get; }

    /// <summary>Whether this cell is a header cell (typically rendered with a distinct background).</summary>
    public bool IsHeader { get; set; }

    /// <summary>The number of rows this cell spans. Managed by the table; defaults to 1.</summary>
    public int RowSpan { get; internal set; } = 1;

    /// <summary>The number of columns this cell spans. Managed by the table; defaults to 1.</summary>
    public int ColSpan { get; internal set; } = 1;

    /// <summary>Background color override used when creating this cell, or null for the default.</summary>
    public SKColor? BackgroundColor { get; }

    /// <summary>
    /// Initializes a new <see cref="TableCell"/> with the given content width, default style, and callbacks.
    /// </summary>
    public TableCell(float cellContentWidth, IStyle defaultStyle, bool isHeader,
                     Action invalidate, Action contentChanged, SKColor? backgroundOverride = null)
    {
        IsHeader = isHeader;
        BackgroundColor = backgroundOverride;
        Controller = new CellController(cellContentWidth, defaultStyle, invalidate, contentChanged, backgroundOverride);
    }
}
