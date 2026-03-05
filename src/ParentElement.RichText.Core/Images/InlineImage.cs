using SkiaSharp;
using ParentElement.Topten.RichTextKit;

namespace ParentElement.RichText.Core.Images
{
    /// <summary>
    /// An inline image that flows with text as a U+FFFC Object Replacement Character placeholder.
    /// Implements <see cref="IInlineObject"/> so the layout engine can correctly size and paint it.
    /// </summary>
    public class InlineImage : IInlineObject
    {
        private readonly SKImage _image;
        private readonly SKPaint _paint = new SKPaint { IsAntialias = true };

        /// <summary>Creates an inline image with explicit display dimensions.</summary>
        public InlineImage(SKImage image, float displayWidth, float displayHeight)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            Width = displayWidth;
            Height = displayHeight;
        }

        /// <inheritdoc/>
        public float Width { get; set; }

        /// <inheritdoc/>
        public float Height { get; set; }

        /// <summary>The underlying Skia image.</summary>
        public SKImage Image => _image;

        /// <inheritdoc/>
        public void Paint(SKCanvas canvas, SKPoint origin)
        {
            var destRect = new SKRect(origin.X, origin.Y, origin.X + Width, origin.Y + Height);
            canvas.DrawImage(_image, destRect, new SKSamplingOptions(SKFilterMode.Linear), _paint);
        }

        /// <summary>
        /// Returns the raw PNG-encoded bytes of this image for export purposes.
        /// </summary>
        public byte[] GetPngBytes()
        {
            using var data = _image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}
