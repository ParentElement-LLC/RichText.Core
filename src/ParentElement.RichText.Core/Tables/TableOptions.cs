using SkiaSharp;

namespace ParentElement.RichText.Core.Tables;

public class TableOptions
{
    /// <summary>The number of rows in the table.</summary>
    public int Rows { get; set; }

    /// <summary>The number of columns in the table.</summary>
    public int Columns { get; set; }

    /// <summary>The style applied to table borders (none, solid, dashed, or dotted).</summary>
    public TableBorderStyle BorderStyle { get; set; }

    /// <summary>The thickness of table borders in pixels. Defaults to 1.</summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>The color applied to table borders. Defaults to #C0C0C0.</summary>
    public SKColor BorderColor { get; set; } = new SKColor(0xC0, 0xC0, 0xC0);

    /// <summary>The background color applied to header row cells. Defaults to white.</summary>
    public SKColor HeaderBackgroundColor { get; set; } = SKColors.White;

    /// <summary>Whether the first row of the table should be treated as a header row.</summary>
    public bool IncludeHeader { get; set; }
}
