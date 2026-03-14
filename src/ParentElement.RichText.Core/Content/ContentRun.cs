#nullable disable
using ParentElement.Topten.RichTextKit;

namespace ParentElement.RichText.Core.Content
{
    /// <summary>A contiguous sequence of characters (or a single inline image/table) within a <see cref="ContentBlock"/>, sharing a common character style.</summary>
    public class ContentRun
    {
        /// <summary>The plain text content of this run. <c>null</c> for image and table runs.</summary>
        public string Text { get; set; }

        /// <summary>The character style applied to this run.</summary>
        public IStyle Style { get; set; }

        /// <summary>True when this run represents an inline image rather than text.</summary>
        public bool IsImage => ImageData != null;

        /// <summary>PNG-encoded image bytes (non-null only for inline image runs).</summary>
        public byte[] ImageData { get; set; }

        /// <summary>Display width of the inline image in pixels.</summary>
        public float ImageWidth { get; set; }

        /// <summary>Display height of the inline image in pixels.</summary>
        public float ImageHeight { get; set; }

        /// <summary>True when this run represents an inline table.</summary>
        public bool IsTable => Table != null;

        /// <summary>Table content data (non-null only for inline table runs).</summary>
        public ContentTable Table { get; set; }
    }
}
