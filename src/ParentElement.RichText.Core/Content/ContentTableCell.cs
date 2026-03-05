#nullable disable

namespace ParentElement.RichText.Core.Content
{
    public class ContentTableCell
    {
        /// <summary>The number of rows this cell spans. Defaults to 1.</summary>
        public int RowSpan { get; set; } = 1;

        /// <summary>The number of columns this cell spans. Defaults to 1.</summary>
        public int ColSpan { get; set; } = 1;

        /// <summary>Whether this cell is a header cell.</summary>
        public bool IsHeader { get; set; }

        /// <summary>CSS hex color string (e.g. "#C0C0C0"), or null for no explicit background.</summary>
        public string BackgroundColor { get; set; }

        /// <summary>The block-level content contained within this cell.</summary>
        public IReadOnlyList<ContentBlock> Content { get; set; }
    }
}
