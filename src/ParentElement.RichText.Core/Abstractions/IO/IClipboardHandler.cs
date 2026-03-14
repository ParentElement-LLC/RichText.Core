using ParentElement.RichText.Core.Content;

namespace ParentElement.RichText.Core.Abstractions.IO;

/// <summary>Platform-specific clipboard abstraction injected into <see cref="Core.Controllers.DocumentController"/> to support cut, copy, and paste operations.</summary>
public interface IClipboardHandler
{
    /// <summary>Retrieves plain text from the clipboard, or <c>null</c> if the clipboard contains no text.</summary>
    Task<string?> GetTextAsync();

    /// <summary>Places the given <paramref name="text"/> onto the clipboard as plain text.</summary>
    Task SetTextAsync(string text);

    /// <summary>Retrieves raw image bytes (PNG) from the clipboard, or <c>null</c> if the clipboard contains no image.</summary>
    Task<byte[]?> GetImageDataAsync();

    /// <summary>
    /// Sets rich-text clipboard data (HTML + RTF) alongside plain text.
    /// </summary>
    Task SetRichDataAsync(IReadOnlyList<ContentBlock> blocks, string? plainText);

    /// <summary>
    /// Sets clipboard data for an image-only selection (PNG bytes).
    /// </summary>
    Task SetImageBytesAsync(byte[] pngData);
}
