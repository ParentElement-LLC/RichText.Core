using SkiaSharp;
using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;
using ParentElement.RichText.Core.Abstractions.IO;
using ParentElement.RichText.Core.Abstractions.Controllers;
using ParentElement.RichText.Core.Tables;

namespace ParentElement.RichText.Core.IO
{
    public abstract class ImporterBase : IImportContent
    {
        public abstract Task ImportAsync(IDocumentController controller, Stream inputStream);

        public abstract Task ImportAsync(IDocumentController controller, string filePath);

        /// <summary>
        /// A text run within a parsed paragraph. When <see cref="ImageData"/> is non-null the run
        /// represents an inline image and <see cref="Text"/> / <see cref="Style"/> are unused.
        /// </summary>
        protected record ParsedRun(
            string Text,
            Style Style,
            byte[]? ImageData = null,
            float ImageWidth = 0f,
            float ImageHeight = 0f);

        protected record ParsedParagraph(
            TextAlignment Alignment,
            List<ParsedRun> Runs,
            float BlockIndent = 0f,
            ListType ListType = ListType.None,
            int ListLevel = 0,
            float? LineSpacing = null);

        /// <summary>
        /// Intermediate table model shared by HTML and OpenXml importers.
        /// </summary>
        protected sealed class ParsedTable
        {
            public ParsedTable(int rows, int cols, ParsedTableCell?[][] cells,
                TableBorderStyle borderStyle, float borderWidth, SKColor borderColor,
                bool includeHeader, float[]? colWidths = null)
            {
                Rows = rows; Cols = cols; Cells = cells;
                BorderStyle = borderStyle; BorderWidth = borderWidth; BorderColor = borderColor;
                IncludeHeader = includeHeader;
                ColWidths = colWidths;
            }
            public int Rows { get; }
            public int Cols { get; }
            public ParsedTableCell?[][] Cells { get; }
            public TableBorderStyle BorderStyle { get; }
            public float BorderWidth { get; }
            public SKColor BorderColor { get; }
            public bool IncludeHeader { get; }
            /// <summary>Optional explicit column widths in pixels. Null = auto-distribute.</summary>
            public float[]? ColWidths { get; }
        }

        protected sealed class ParsedTableCell
        {
            public ParsedTableCell(bool isHeader, int rowSpan, int colSpan,
                List<ParsedParagraph> content, SKColor? cellBackgroundColor)
            {
                IsHeader = isHeader; RowSpan = rowSpan; ColSpan = colSpan;
                Content = content; CellBackgroundColor = cellBackgroundColor;
            }
            public bool IsHeader { get; }
            /// <summary>Mutable so OOXML importer can set the value after a two-pass scan.</summary>
            public int RowSpan { get; set; }
            /// <summary>Mutable so OOXML importer can set the value after a two-pass scan.</summary>
            public int ColSpan { get; set; }
            public List<ParsedParagraph> Content { get; }
            /// <summary>Background color declared on the cell element itself (cell-level fill).</summary>
            public SKColor? CellBackgroundColor { get; }
        }

        protected async Task InsertParsedContent(IDocumentController controller, IEnumerable<ParsedParagraph> paragraphs)
        {
            var paragraphList = paragraphs.ToList();

            for (int i = 0; i < paragraphList.Count; i++)
            {
                var paragraph = paragraphList[i];

                await controller.ApplyAlignment(paragraph.Alignment);

                foreach (var run in paragraph.Runs)
                {
                    if (run.ImageData != null)
                    {
                        var skImage = SKImage.FromEncodedData(run.ImageData);
                        if (skImage != null)
                            controller.InsertInlineImage(skImage, run.ImageWidth, run.ImageHeight);
                    }
                    else
                    {
                        await controller.ApplyStyle(run.Style);
                        controller.Insert(run.Text);
                    }
                }

                // Always call ApplyListFormat (even for None) to override inherited list style.
                controller.ApplyListFormat(paragraph.ListType, paragraph.ListLevel);

                if (paragraph.BlockIndent > 0f)
                    controller.SetBlockIndent(paragraph.BlockIndent);

                if (paragraph.LineSpacing.HasValue)
                    controller.AdjustLineSpacing(paragraph.LineSpacing.Value);

                if (i < paragraphList.Count - 1)
                    controller.Insert("\u2029");
            }
        }

        protected async Task InsertMixedBlocks(IDocumentController controller, List<object> blocks)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];

                if (block is ParsedParagraph para)
                {
                    await controller.ApplyAlignment(para.Alignment);

                    foreach (var run in para.Runs)
                    {
                        if (run.ImageData != null)
                        {
                            var skImage = SKImage.FromEncodedData(run.ImageData);
                            if (skImage != null)
                                controller.InsertInlineImage(skImage, run.ImageWidth, run.ImageHeight);
                        }
                        else
                        {
                            await controller.ApplyStyle(run.Style);
                            controller.Insert(run.Text);
                        }
                    }

                    controller.ApplyListFormat(para.ListType, para.ListLevel);

                    if (para.BlockIndent > 0f)
                        controller.SetBlockIndent(para.BlockIndent);

                    if (para.LineSpacing.HasValue)
                        controller.AdjustLineSpacing(para.LineSpacing.Value);
                }
                else if (block is ParsedTable tbl)
                {
                    InsertParsedTable(controller, tbl);
                }

                if (i < blocks.Count - 1)
                    controller.Insert("\u2029");
            }
        }

        protected static void InsertParsedTable(IDocumentController controller, ParsedTable tbl)
        {
            // Determine header background color from the first header cell that declares one
            var headerBg = SKColors.White;
            if (tbl.IncludeHeader)
            {
                for (int c = 0; c < tbl.Cols; c++)
                {
                    var hc = tbl.Cells[0][c];
                    if (hc?.CellBackgroundColor.HasValue == true)
                    {
                        headerBg = hc.CellBackgroundColor!.Value;
                        break;
                    }
                }
            }

            var opts = new TableOptions
            {
                Rows                  = tbl.Rows,
                Columns               = tbl.Cols,
                BorderStyle           = tbl.BorderStyle,
                BorderWidth           = tbl.BorderWidth,
                BorderColor           = tbl.BorderColor,
                IncludeHeader         = tbl.IncludeHeader,
                HeaderBackgroundColor = headerBg,
            };
            var table = controller.InsertTable(opts);

            // Step 0.5: apply explicit column widths when provided
            if (tbl.ColWidths != null && tbl.ColWidths.Length == tbl.Cols && tbl.Cols > 1)
            {
                for (int d = 0; d < tbl.Cols - 1; d++)
                    table.ResizeDivider(d, tbl.ColWidths[d], tbl.ColWidths[d + 1]);
            }

            // Step 1: apply all merges before writing any content
            for (int r = 0; r < tbl.Rows; r++)
                for (int c = 0; c < tbl.Cols; c++)
                {
                    var cell = tbl.Cells[r][c];
                    if (cell == null) continue;
                    if (cell.RowSpan > 1 || cell.ColSpan > 1)
                        table.MergeRange(r, c, r + cell.RowSpan - 1, c + cell.ColSpan - 1);
                }

            // Step 2: correct IsHeader flags (constructor only marks row-0 cells as header)
            for (int r = 0; r < tbl.Rows; r++)
                for (int c = 0; c < tbl.Cols; c++)
                {
                    var cell = tbl.Cells[r][c];
                    if (cell == null) continue;
                    var tableCell = table.GetCell(r, c);
                    if (tableCell != null)
                        tableCell.IsHeader = cell.IsHeader;
                }

            // Step 3: write cell content
            for (int r = 0; r < tbl.Rows; r++)
                for (int c = 0; c < tbl.Cols; c++)
                {
                    var cell = tbl.Cells[r][c];
                    if (cell == null) continue;
                    var content = cell.Content;
                    table.WriteToCell(r, c, writer => WriteCellContent(writer, content));
                }
        }

        protected static void WriteCellContent(ICellWriter writer, List<ParsedParagraph> paragraphs)
        {
            for (int i = 0; i < paragraphs.Count; i++)
            {
                var para = paragraphs[i];
                writer.ApplyAlignment(para.Alignment);

                foreach (var run in para.Runs)
                {
                    if (run.ImageData != null)
                    {
                        var skImage = SKImage.FromEncodedData(run.ImageData);
                        if (skImage != null)
                            writer.InsertInlineImage(skImage, run.ImageWidth, run.ImageHeight);
                    }
                    else
                    {
                        writer.ApplyStyle(run.Style);
                        writer.Insert(run.Text);
                    }
                }

                writer.ApplyListFormat(para.ListType, para.ListLevel);

                if (para.BlockIndent > 0f)
                    writer.SetBlockIndent(para.BlockIndent);

                if (para.LineSpacing.HasValue)
                    writer.AdjustLineSpacing(para.LineSpacing.Value);

                if (i < paragraphs.Count - 1)
                    writer.Insert("\u2029");
            }
        }
    }
}
