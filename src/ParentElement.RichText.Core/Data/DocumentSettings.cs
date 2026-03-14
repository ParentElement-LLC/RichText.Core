using ParentElement.Topten.RichTextKit;
using SkiaSharp;

namespace ParentElement.RichText.Core.Data;

/// <summary>Immutable-by-convention settings passed to <see cref="Controllers.DocumentController"/> to configure page layout, default style, and visual appearance.</summary>
public struct DocumentSettings
{
    /// <summary>
    /// Initializes a new DocumentSettings object.
    /// </summary>
    public DocumentSettings()
    {
        DocumentMargins = default;
    }

    /// <summary>The background color of the document page. Defaults to white.</summary>
    public SKColor DocumentBackgroundColor { get; set; } = SKColors.White;

    /// <summary>The default character style applied to new text. Defaults to <see cref="DefaultStyle"/>.</summary>
    public IStyle TextStyle { get; set; } = new DefaultStyle();

    /// <summary>The width of the document page in pixels. Defaults to 800.</summary>
    public float PageWidth { get; set; } = 800;

    /// <summary>The margins applied inside the document page boundary.</summary>
    public DocumentMargins DocumentMargins { get; set; }

    /// <summary>The default horizontal text alignment for new paragraphs. Defaults to <see cref="TextAlignment.Left"/>.</summary>
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

    /// <summary>Whether to render a visual separator between pages. Defaults to <c>false</c>.</summary>
    public bool ShowPageBreaks { get; set; } = false;

    /// <summary>The height of a single page in pixels. Defaults to 1056 (US Letter at 96 dpi).</summary>
    public float PageHeight { get; set; } = 1056f;   // US Letter at 96 dpi

    /// <summary>The vertical gap in pixels rendered between pages when <see cref="ShowPageBreaks"/> is enabled. Defaults to 24.</summary>
    public float PageGap { get; set; } = 24f;

    /// <summary>The background color of the canvas area surrounding the document page. Defaults to #E8E8E8.</summary>
    public SKColor CanvasBackgroundColor { get; set; } = new SKColor(0xE8, 0xE8, 0xE8);
}
