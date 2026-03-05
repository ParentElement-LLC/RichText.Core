#nullable disable

using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;

namespace ParentElement.RichText.Core.Content
{
    public class ContentBlock
    {
        /// <summary>The structural type of this block (e.g. paragraph).</summary>
        public ContentBlockType Type { get; set; }

        /// <summary>The sequence of inline runs (text, images, and tables) that make up this block's content.</summary>
        public IEnumerable<ContentRun> Runs { get; set; }

        /// <summary>The horizontal text alignment of this block, or <c>null</c> to inherit the document default.</summary>
        public TextAlignment? Alignment { get; set; }

        /// <summary>The list marker type applied to this block, or <c>null</c> if the block is not a list item.</summary>
        public ListType? ListType { get; set; }

        /// <summary>The zero-based nesting level of this list item, or <c>null</c> if not a list item.</summary>
        public int? ListLevel { get; set; }

        /// <summary>The absolute left block indent in pixels, or <c>null</c> if no indent is applied.</summary>
        public float? BlockIndent { get; set; }

        /// <summary>The line height multiplier for this block, or <c>null</c> if the default (1.0) is used.</summary>
        public float? LineSpacing { get; set; }
    }
}
