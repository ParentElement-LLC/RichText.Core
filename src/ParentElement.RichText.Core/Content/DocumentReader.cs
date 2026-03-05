using ParentElement.RichText.Core.Images;
using ParentElement.RichText.Core.Tables;
using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;
using ParentElement.Topten.RichTextKit.Utils;
using SkiaSharp;

namespace ParentElement.RichText.Core.Content
{
    public class DocumentReader
    {
        private readonly TextDocument _document;

        //TODO:  Get document info instead of exposing directly?
        /// <summary>The underlying <see cref="TextDocument"/> being read.</summary>
        public TextDocument Document=> _document;

        public DocumentReader(TextDocument document)
        {
            _document = document;
        }

        /// <summary>Returns all content blocks in the document.</summary>
        public IEnumerable<ContentBlock> GetContent() => GetContent(selection: null);

        /// <summary>
        /// Returns content blocks filtered to the given selection range.
        /// When <paramref name="selection"/> is null the full document is returned.
        /// </summary>
        public IEnumerable<ContentBlock> GetContent(TextRange? selection)
        {
            int selMin = selection?.Minimum ?? 0;
            int selMax = selection?.Maximum ?? int.MaxValue;
            bool filter = selection.HasValue;

            var buffer = new Utf32Buffer();

            foreach (var para in _document.GetPragraphs())
            {
                if (para.TextBlock == null)
                    continue;

                int paraStart = para.CodePointIndex;
                int paraEnd   = paraStart + para.Length; // exclusive (past the \u2029)

                // Skip paragraphs entirely outside the selection
                if (filter && (paraEnd <= selMin || paraStart > selMax))
                    continue;

                // Relative bounds of the selection within this paragraph
                int relSelStart = filter ? Math.Max(0, selMin - paraStart)                 : 0;
                int relSelEnd   = filter ? Math.Min(para.Length - 1, selMax - paraStart)   : para.Length - 1;

                var block = new ContentBlock
                {
                    Type      = ContentBlockType.Paragraph,
                    Alignment = para.TextBlock.Alignment,
                    ListType  = para.ListType != ListType.None ? para.ListType : (ListType?)null,
                    ListLevel = para.ListLevel,
                    BlockIndent = para.BlockIndent > 0f ? para.BlockIndent : (float?)null,
                };
                var ls = para.TextBlock.LineSpacing;
                block.LineSpacing = ls != 1.0f ? ls : (float?)null;

                var runs = para.TextBlock.StyleRuns;

                if (runs == null)
                {
                    block.Runs = new List<ContentRun>(0);
                }
                else if (!filter)
                {
                    // Fast path – no clipping needed
                    block.Runs = runs.Select(r =>
                    {
                        if (r.InlineObject is InlineImage img)
                            return new ContentRun { Style = r.Style, ImageData = img.GetPngBytes(), ImageWidth = img.Width, ImageHeight = img.Height };

                        if (r.InlineObject is InlineTable tbl)
                            return new ContentRun { Style = r.Style, Table = BuildContentTable(tbl) };

                        buffer.Add(r.CodePoints);
                        var cr = new ContentRun { Text = buffer.ToString(), Style = r.Style };
                        buffer.Clear();
                        return cr;
                    });
                }
                else
                {
                    // Filtered path – clip each run to the selection range
                    var filteredRuns = new List<ContentRun>();

                    foreach (var r in runs)
                    {
                        // Skip runs entirely outside the selection
                        if (r.End <= relSelStart || r.Start > relSelEnd)
                            continue;

                        if (r.InlineObject is InlineImage img)
                        {
                            filteredRuns.Add(new ContentRun
                            {
                                Style       = r.Style,
                                ImageData   = img.GetPngBytes(),
                                ImageWidth  = img.Width,
                                ImageHeight = img.Height,
                            });
                            continue;
                        }

                        if (r.InlineObject is InlineTable tbl)
                        {
                            filteredRuns.Add(new ContentRun { Style = r.Style, Table = BuildContentTable(tbl) });
                            continue;
                        }

                        int clipStart = Math.Max(r.Start, relSelStart);
                        int clipEnd   = Math.Min(r.End, relSelEnd + 1);
                        if (clipStart >= clipEnd) continue;

                        buffer.Add(r.CodePoints.SubSlice(clipStart - r.Start, clipEnd - clipStart));
                        filteredRuns.Add(new ContentRun { Text = buffer.ToString(), Style = r.Style });
                        buffer.Clear();
                    }

                    block.Runs = filteredRuns;
                    if (filteredRuns.Count == 0)
                        continue;
                }

                yield return block;
            }
        }

        private ContentTable BuildContentTable(InlineTable tbl)
        {
            var colWidths = tbl.ColWidths.ToArray();
            var rowHeights = tbl.RowHeights.ToArray();

            var cells = new ContentTableCell[tbl.Rows][];
            for (int r = 0; r < tbl.Rows; r++)
            {
                cells[r] = new ContentTableCell[tbl.Cols];
                for (int c = 0; c < tbl.Cols; c++)
                {
                    var cell = tbl.GetCell(r, c);
                    if (cell == null) continue;

                    string? bgColor = null;
                    if (cell.BackgroundColor.HasValue)
                    {
                        var bg = cell.BackgroundColor.Value;
                        bgColor = $"#{bg.Red:X2}{bg.Green:X2}{bg.Blue:X2}";
                    }

                    cells[r][c] = new ContentTableCell
                    {
                        RowSpan         = cell.RowSpan,
                        ColSpan         = cell.ColSpan,
                        IsHeader        = cell.IsHeader,
                        BackgroundColor = bgColor,
                        Content         = cell.Controller.GetContentReader().GetContent().ToList(),
                    };
                }
            }

            string borderStyle = tbl.BorderStyle switch
            {
                TableBorderStyle.Dashed => "dashed",
                TableBorderStyle.Dotted => "dotted",
                TableBorderStyle.None   => "none",
                _                       => "solid",
            };

            SKColor bc = tbl.BorderColor;
            string borderColor = $"#{bc.Red:X2}{bc.Green:X2}{bc.Blue:X2}";

            return new ContentTable
            {
                Rows        = tbl.Rows,
                Cols        = tbl.Cols,
                TotalWidth  = tbl.Width,
                ColWidths   = colWidths,
                RowHeights  = rowHeights,
                CellPadding = InlineTable._cellPadding,
                BorderStyle = borderStyle,
                BorderWidth = tbl.BorderWidth,
                BorderColor = borderColor,
                Cells       = cells,
            };
        }

        /// <summary>Enumerates all distinct styles used in the document, starting with the document default style.</summary>
        public IEnumerable<StyleInfo> GetStyles()
        {
            yield return new StyleInfo(_document.DefaultStyle);

            foreach(var para in _document.GetPragraphs())
            {
                if (para.TextBlock == null)
                    continue;

                var block = para.TextBlock;

                foreach(var run in block.StyleRuns)
                    yield return new StyleInfo(_document.DefaultStyle).Difference(run.Style);
            }
        }

        
    }
}
