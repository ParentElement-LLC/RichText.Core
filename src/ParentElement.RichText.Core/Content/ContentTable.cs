#nullable disable

namespace ParentElement.RichText.Core.Content
{
    public class ContentTable
    {
        /// <summary>The number of rows in the table.</summary>
        public int Rows { get; set; }

        /// <summary>The number of columns in the table.</summary>
        public int Cols { get; set; }

        /// <summary>The total rendered width of the table in pixels, including all columns and borders.</summary>
        public float TotalWidth { get; set; }

        /// <summary>Per-column widths in pixels (net content width, excluding borders).</summary>
        public float[] ColWidths { get; set; }

        /// <summary>Per-row heights in pixels (including cell padding).</summary>
        public float[] RowHeights { get; set; }

        /// <summary>Cell padding in pixels on each side.</summary>
        public float CellPadding { get; set; }

        /// <summary>CSS border-style value: "none", "solid", "dashed", or "dotted".</summary>
        public string BorderStyle { get; set; }

        /// <summary>The thickness of the table borders in pixels.</summary>
        public float BorderWidth { get; set; }

        /// <summary>CSS hex color string (e.g. "#C0C0C0").</summary>
        public string BorderColor { get; set; }

        /// <summary>
        /// Grid of cells. A null entry means the slot is covered by a spanning primary cell.
        /// </summary>
        public ContentTableCell[][] Cells { get; set; }
    }
}
