using ParentElement.RichText.Core.Abstractions.Controllers;
using ParentElement.RichText.Core.Content;
using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;
using SkiaSharp;
using System.Text;

namespace ParentElement.RichText.Core.IO
{
    /// <summary>Exports document content to Rich Text Format (RTF).</summary>
    public class RtfExporter : ExporterBase
    {
        // At 96 DPI: 1 inch = 96px = 1440 twips → 1px = 15 twips
        private static int PxToTwips(float px) => (int)Math.Round(px * 15f);

        // Font size: 1pt = 96/72 px → 1 half-point = 96/144 px → 1px ≈ 1.5 half-points
        private static int PxToHalfPoints(float px) => (int)Math.Round(px * 1.5f);

        // List extra indent per level (matches TextParagraph.kListIndentPerLevel = 30f px)
        private const float _kListIndentPerLevel = 30f;
        // Bullet hanging indent (matches TextParagraph.kBulletWidth = 20f px)
        private const float _kBulletHang = 20f;

        /// <inheritdoc/>
        public override async Task ExportAsync(IDocumentController controller, Stream outputStream)
        {
            var reader = controller.GetContentReader();
            var blocks = reader.GetContent().ToList();
            var defaultStyle = reader.Document.DefaultStyle;

            var sb = BuildRtf(blocks, defaultStyle,
                pageWidthPx:  reader.Document.PageWidth,
                marginLeftPx: reader.Document.MarginLeft,
                marginRightPx: reader.Document.MarginRight,
                marginTopPx:  reader.Document.MarginTop,
                marginBotPx:  reader.Document.MarginBottom,
                firstLineIndentPx: reader.Document.FirstLineIndent);

            using var writer = new StreamWriter(outputStream, Encoding.ASCII, leaveOpen: true);
            await writer.WriteAsync(sb);
            await writer.FlushAsync();
        }

        /// <summary>
        /// Generates a complete RTF document string from the supplied content blocks.
        /// Omit page-geometry arguments (leave at 0) for clipboard fragments.
        /// </summary>
        public static string ToRtfString(IReadOnlyList<ContentBlock> blocks, IStyle? defaultStyle = null)
            => BuildRtf(blocks, defaultStyle,
                pageWidthPx: 0, marginLeftPx: 0, marginRightPx: 0,
                marginTopPx: 0, marginBotPx: 0, firstLineIndentPx: 0);

        private static string BuildRtf(
            IReadOnlyList<ContentBlock> blocks,
            IStyle? defaultStyle,
            float pageWidthPx,
            float marginLeftPx, float marginRightPx,
            float marginTopPx,  float marginBotPx,
            float firstLineIndentPx)
        {
            var defaultFontName = defaultStyle?.FontFamily ?? "Times New Roman";

            // First pass: collect all unique fonts, colors, and list usage
            // (recurses into table cell content)
            var fonts = new List<string>();
            var colors = new List<SKColor>();
            bool hasLists = false;

            AddFont(fonts, defaultFontName);
            CollectFontsAndColors(blocks, fonts, colors, ref hasLists);

            var sb = new StringBuilder(4096);

            // RTF header
            sb.Append(@"{\rtf1\ansi\ansicpg1252\deff0");

            // Font table
            sb.Append(@"{\fonttbl");
            for (int i = 0; i < fonts.Count; i++)
                sb.Append($@"{{\f{i}\fnil\fcharset0 {fonts[i]};}}");
            sb.Append('}');

            // Color table — index 0 is the implicit "auto" color; explicit entries start at 1
            sb.Append(@"{\colortbl ;");
            foreach (var color in colors)
                sb.Append($@"\red{color.Red}\green{color.Green}\blue{color.Blue};");
            sb.Append('}');

            // List table (if there are any list items in the doc or in table cells)
            if (hasLists)
            {
                sb.Append(@"{\*\listtable");

                // List 1: Bullet (9 levels)
                sb.Append(@"{\list\listtemplateid1");
                string[] bulletHex = { @"\'b7", @"\'d8", @"\'a7", @"\'b7", @"\'d8", @"\'a7", @"\'b7", @"\'d8", @"\'a7" };
                for (int lvl = 0; lvl < 9; lvl++)
                {
                    int li = PxToTwips((lvl + 1) * _kListIndentPerLevel);
                    int fi = PxToTwips(_kBulletHang);
                    sb.Append($@"{{\listlevel\levelnfc23\leveljc0\levelstartat1");
                    sb.Append($@"{{\leveltext\'01{bulletHex[lvl]};}}");
                    sb.Append($@"{{\levelnumbers;}}\fi-{fi}\li{li}}}");
                }
                sb.Append(@"\listid1}");

                // List 2: Numbered (9 levels)
                sb.Append(@"{\list\listtemplateid2");
                for (int lvl = 0; lvl < 9; lvl++)
                {
                    int li = PxToTwips((lvl + 1) * _kListIndentPerLevel);
                    int fi = PxToTwips(_kBulletHang);
                    sb.Append($@"{{\listlevel\levelnfc0\leveljc0\levelstartat1");
                    sb.Append($@"{{\leveltext\'02\'00.;}}");
                    sb.Append($@"{{\levelnumbers\'01;}}\fi-{fi}\li{li}}}");
                }
                sb.Append(@"\listid2}");

                sb.Append('}'); // end listtable

                // List override table
                sb.Append(@"{\*\listoverridetable");
                sb.Append(@"{\listoverride\listid1\listoverridecount0\ls1}");
                sb.Append(@"{\listoverride\listid2\listoverridecount0\ls2}");
                sb.Append('}'); // end listoverridetable
            }

            // Document-level page geometry (omitted when values are 0, e.g. clipboard use)
            if (pageWidthPx > 0)
            {
                sb.Append($@"\paperw{PxToTwips(pageWidthPx)}\paperh15840");
                sb.Append($@"\margl{PxToTwips(marginLeftPx)}");
                sb.Append($@"\margr{PxToTwips(marginRightPx)}");
                sb.Append($@"\margt{PxToTwips(marginTopPx)}");
                sb.Append($@"\margb{PxToTwips(marginBotPx)}");
            }

            var paragraphMark = '\u2029'.ToString();
            var defaultFontIndex = fonts.IndexOf(defaultFontName);

            foreach (var block in blocks)
            {
                if (block.Type != ContentBlockType.Paragraph)
                    continue;

                // Inline table run — emit as RTF table rows
                var tableRun = block.Runs?.FirstOrDefault(r => r.IsTable);
                if (tableRun != null)
                {
                    AppendTableRtf(sb, tableRun.Table, fonts, colors, defaultStyle, defaultFontIndex);
                    continue;
                }

                // Reset paragraph formatting and set alignment
                sb.Append(@"\pard");

                var align = block.Alignment.HasValue && block.Alignment.Value != TextAlignment.Auto
                    ? block.Alignment.Value
                    : TextAlignment.Left;

                sb.Append(align switch
                {
                    TextAlignment.Center => @"\qc",
                    TextAlignment.Right  => @"\qr",
                    _                    => @"\ql"
                });

                // List formatting
                var blockListType = block.ListType ?? ListType.None;
                var blockListLevel = block.ListLevel ?? 0;
                if (blockListType != ListType.None)
                {
                    int listId = blockListType == ListType.Bullet ? 1 : 2;
                    int totalIndentTwips = PxToTwips((blockListLevel + 1) * _kListIndentPerLevel + (block.BlockIndent ?? 0f));
                    int hangTwips = PxToTwips(_kBulletHang);
                    sb.Append($@"\ls{listId}\ilvl{blockListLevel}\li{totalIndentTwips}\fi-{hangTwips}");
                }
                else if ((block.BlockIndent ?? 0f) > 0f)
                {
                    sb.Append($@"\li{PxToTwips(block.BlockIndent!.Value)}");
                }

                // Line spacing
                if (block.LineSpacing.HasValue && block.LineSpacing.Value != 1.0f)
                {
                    int slValue = (int)Math.Round(block.LineSpacing.Value * 240f);
                    sb.Append($@"\sl{slValue}\slmult1");
                }

                // Paragraph-level default font and size
                sb.Append($@"\f{defaultFontIndex}");

                if (defaultStyle?.FontSize.HasValue == true)
                    sb.Append($@"\fs{PxToHalfPoints(defaultStyle.FontSize.Value)}");

                if (firstLineIndentPx > 0 && blockListType == ListType.None)
                    sb.Append($@"\fi{PxToTwips(firstLineIndentPx)}");

                sb.Append(' ');

                // Each run is wrapped in a group so its formatting is automatically scoped
                foreach (var run in block.Runs!)
                {
                    if (run.IsImage)
                    {
                        int wTwips = PxToTwips(run.ImageWidth);
                        int hTwips = PxToTwips(run.ImageHeight);
                        sb.Append(@"{\pict\pngblip");
                        sb.Append($@"\picwgoal{wTwips}\pichgoal{hTwips} ");
                        foreach (var b in run.ImageData)
                            sb.Append(b.ToString("x2"));
                        sb.Append('}');
                        continue;
                    }

                    var text = run.Text.Replace(paragraphMark, string.Empty);
                    if (text.Length == 0)
                        continue;

                    sb.Append('{');

                    AppendRunFormatting(sb, run.Style, fonts, colors);

                    sb.Append(' ');
                    sb.Append(EscapeRtf(text));
                    sb.Append('}');
                }

                sb.Append(@"\par");
            }

            sb.Append('}');

            return sb.ToString();
        }

        /// <summary>
        /// Appends a complete RTF table (all rows and cells) to <paramref name="sb"/>.
        /// </summary>
        private static void AppendTableRtf(
            StringBuilder sb,
            ContentTable table,
            List<string> fonts,
            List<SKColor> colors,
            IStyle? defaultStyle,
            int defaultFontIndex)
        {
            float pad   = table.CellPadding;
            bool hasBorder = table.BorderStyle != "none" && table.BorderWidth > 0f;

            // RTF border style keyword
            string bdrKw = table.BorderStyle switch
            {
                "dashed" => @"\brdrdash",
                "dotted" => @"\brdrdot",
                _        => @"\brdrs",
            };
            int bwTwips       = PxToTwips(table.BorderWidth);
            int borderColorIdx = hasBorder ? GetColorIndex(colors, ParseHexColor(table.BorderColor)) : 0;

            // Pre-compute each column's right edge (cumulative twips from left).
            // cellXTwips[c] = right edge of column c (content + padding on both sides).
            var cellXTwips = new int[table.Cols];
            float cumX = 0f;
            for (int c = 0; c < table.Cols; c++)
            {
                cumX += table.ColWidths[c] + 2f * pad;
                cellXTwips[c] = PxToTwips(cumX);
            }

            var cells         = table.Cells;
            var paragraphMark = '\u2029'.ToString();

            for (int r = 0; r < table.Rows; r++)
            {
                sb.Append(@"\trowd");

                int c = 0;
                while (c < table.Cols)
                {
                    var cell = cells[r][c];

                    if (cell == null && IsRowSpanCovered(r, c, cells))
                    {
                        // Rowspan continuation slot
                        sb.Append(@"\clvmrg");
                        AppendCellBorders(sb, hasBorder, bdrKw, bwTwips, borderColorIdx);
                        sb.Append($@"\cellx{cellXTwips[c]}");
                        c++;
                    }
                    else if (cell != null)
                    {
                        // Primary cell — may span multiple rows and/or columns
                        if (cell.RowSpan > 1)
                            sb.Append(@"\clvmgf");

                        AppendCellBorders(sb, hasBorder, bdrKw, bwTwips, borderColorIdx);

                        if (cell.BackgroundColor != null)
                            sb.Append($@"\clshdng10000\clcbpat{GetColorIndex(colors, ParseHexColor(cell.BackgroundColor))}");

                        // \cellx marks the right edge of the last spanned column
                        int lastCol = c + cell.ColSpan - 1;
                        sb.Append($@"\cellx{cellXTwips[lastCol]}");

                        c += cell.ColSpan;
                    }
                    else
                    {
                        // Colspan-covered slot — already accounted for in the primary cell's \cellx
                        c++;
                    }
                }

                c = 0;
                while (c < table.Cols)
                {
                    var cell = cells[r][c];

                    if (cell == null && IsRowSpanCovered(r, c, cells))
                    {
                        // Empty cell placeholder for rowspan continuation
                        sb.Append(@"\pard\intbl\cell");
                        c++;
                    }
                    else if (cell != null)
                    {
                        AppendCellContent(sb, cell.Content, fonts, colors, defaultStyle,
                            defaultFontIndex, paragraphMark);
                        c += cell.ColSpan;
                    }
                    else
                    {
                        // Colspan-covered — no content emitted
                        c++;
                    }
                }

                sb.Append(@"\row");
            }
        }

        /// <summary>
        /// Appends the four border keywords for one cell (all sides share the same style).
        /// </summary>
        private static void AppendCellBorders(
            StringBuilder sb, bool hasBorder, string bdrKw, int bwTwips, int colorIdx)
        {
            if (!hasBorder) return;
            string colorPart = colorIdx > 0 ? $@"\brdrcf{colorIdx}" : "";
            foreach (var side in new[] { "l", "t", "b", "r" })
                sb.Append($@"\clbrdr{side}{bdrKw}\brdrw{bwTwips}{colorPart}");
        }

        /// <summary>
        /// Appends the RTF paragraph content for one table cell, ending with <c>\cell</c>.
        /// Multiple paragraphs are separated by <c>\par</c>; the last ends with <c>\cell</c>.
        /// </summary>
        private static void AppendCellContent(
            StringBuilder sb,
            IReadOnlyList<ContentBlock> content,
            List<string> fonts,
            List<SKColor> colors,
            IStyle? defaultStyle,
            int defaultFontIndex,
            string paragraphMark)
        {
            if (content.Count == 0)
            {
                sb.Append(@"\pard\intbl\cell");
                return;
            }

            for (int i = 0; i < content.Count; i++)
            {
                var block = content[i];
                bool isLast = i == content.Count - 1;

                // Every paragraph inside a table cell must carry \intbl
                sb.Append(@"\pard\intbl");

                var align = block.Alignment.HasValue && block.Alignment.Value != TextAlignment.Auto
                    ? block.Alignment.Value
                    : TextAlignment.Left;

                sb.Append(align switch
                {
                    TextAlignment.Center => @"\qc",
                    TextAlignment.Right  => @"\qr",
                    _                    => @"\ql"
                });

                var blockListType  = block.ListType  ?? ListType.None;
                var blockListLevel = block.ListLevel ?? 0;

                if (blockListType != ListType.None)
                {
                    int listId            = blockListType == ListType.Bullet ? 1 : 2;
                    int totalIndentTwips  = PxToTwips((blockListLevel + 1) * _kListIndentPerLevel + (block.BlockIndent ?? 0f));
                    int hangTwips         = PxToTwips(_kBulletHang);
                    sb.Append($@"\ls{listId}\ilvl{blockListLevel}\li{totalIndentTwips}\fi-{hangTwips}");
                }
                else if ((block.BlockIndent ?? 0f) > 0f)
                {
                    sb.Append($@"\li{PxToTwips(block.BlockIndent!.Value)}");
                }

                if (block.LineSpacing.HasValue && block.LineSpacing.Value != 1.0f)
                {
                    int slValue = (int)Math.Round(block.LineSpacing.Value * 240f);
                    sb.Append($@"\sl{slValue}\slmult1");
                }

                sb.Append($@"\f{defaultFontIndex}");
                if (defaultStyle?.FontSize.HasValue == true)
                    sb.Append($@"\fs{PxToHalfPoints(defaultStyle.FontSize.Value)}");

                sb.Append(' ');

                foreach (var run in block.Runs)
                {
                    if (run.IsImage)
                    {
                        int wTwips = PxToTwips(run.ImageWidth);
                        int hTwips = PxToTwips(run.ImageHeight);
                        sb.Append(@"{\pict\pngblip");
                        sb.Append($@"\picwgoal{wTwips}\pichgoal{hTwips} ");
                        foreach (var b in run.ImageData)
                            sb.Append(b.ToString("x2"));
                        sb.Append('}');
                        continue;
                    }

                    if (run.IsTable) continue; // nested tables not supported in RTF

                    var text = run.Text?.Replace(paragraphMark, string.Empty);
                    if (string.IsNullOrEmpty(text)) continue;

                    sb.Append('{');
                    AppendRunFormatting(sb, run.Style, fonts, colors);
                    sb.Append(' ');
                    sb.Append(EscapeRtf(text));
                    sb.Append('}');
                }

                // Between paragraphs: \par; after the last paragraph: \cell
                sb.Append(isLast ? @"\cell" : @"\par");
            }
        }

        /// <summary>
        /// Returns true if slot [r][c] is covered by a rowspan from a primary cell above it.
        /// (As opposed to being covered by a colspan from a primary cell in the same row.)
        /// </summary>
        private static bool IsRowSpanCovered(int r, int c, ContentTableCell[][] cells)
        {
            for (int rr = r - 1; rr >= 0; rr--)
            {
                var cell = cells[rr][c];
                if (cell != null)
                    return (rr + cell.RowSpan) > r;
                // null cell above may itself be a rowspan continuation — keep searching upward
            }
            return false;
        }

        /// <summary>Parses a CSS "#RRGGBB" hex string to an SKColor.</summary>
        private static SKColor ParseHexColor(string hex)
        {
            if (hex != null && hex.Length == 7 && hex[0] == '#')
            {
                return new SKColor(
                    Convert.ToByte(hex.Substring(1, 2), 16),
                    Convert.ToByte(hex.Substring(3, 2), 16),
                    Convert.ToByte(hex.Substring(5, 2), 16));
            }
            return SKColors.Black;
        }

        private static void CollectFontsAndColors(
            IEnumerable<ContentBlock> blocks,
            List<string> fonts,
            List<SKColor> colors,
            ref bool hasLists)
        {
            foreach (var block in blocks)
            {
                if (block.ListType.HasValue && block.ListType.Value != ListType.None)
                    hasLists = true;

                foreach (var run in block.Runs)
                {
                    if (run.IsImage) continue;

                    if (run.IsTable)
                    {
                        var table = run.Table;
                        // Collect border color
                        if (table.BorderStyle != "none" && table.BorderWidth > 0f)
                            AddColor(colors, ParseHexColor(table.BorderColor));
                        // Recurse into each cell
                        for (int r = 0; r < table.Rows; r++)
                            for (int c = 0; c < table.Cols; c++)
                            {
                                var cell = table.Cells[r][c];
                                if (cell == null) continue;
                                if (cell.BackgroundColor != null)
                                    AddColor(colors, ParseHexColor(cell.BackgroundColor));
                                CollectFontsAndColors(cell.Content, fonts, colors, ref hasLists);
                            }
                        continue;
                    }

                    if (run.Style.FontFamily != null)
                        AddFont(fonts, run.Style.FontFamily);

                    if (run.Style.TextColor.HasValue)
                        AddColor(colors, run.Style.TextColor.Value);

                    if (run.Style.BackgroundColor.HasValue)
                        AddColor(colors, run.Style.BackgroundColor.Value);
                }
            }
        }

        /// <inheritdoc/>
        public override async Task ExportAsync(IDocumentController controller, string filePath)
        {
            await using var stream = File.Create(filePath);
            await ExportAsync(controller, stream);
        }

        private static void AppendRunFormatting(StringBuilder sb, IStyle style, List<string> fonts, List<SKColor> colors)
        {
            if (style.FontFamily != null)
            {
                var fontIndex = fonts.IndexOf(style.FontFamily);
                if (fontIndex >= 0)
                    sb.Append($@"\f{fontIndex}");
            }

            if (style.FontSize.HasValue)
                sb.Append($@"\fs{PxToHalfPoints(style.FontSize.Value)}");

            if (style.FontWeight.HasValue)
                sb.Append(style.FontWeight.Value >= 700 ? @"\b" : @"\b0");

            if (style.FontItalic.HasValue)
                sb.Append(style.FontItalic.Value ? @"\i" : @"\i0");

            if (style.Underline.HasValue)
                sb.Append(style.Underline.Value != UnderlineStyle.None ? @"\ul" : @"\ulnone");

            if (style.StrikeThrough.HasValue)
                sb.Append(style.StrikeThrough.Value == StrikeThroughStyle.Solid ? @"\strike" : @"\strike0");

            if (style.FontVariant.HasValue)
            {
                sb.Append(style.FontVariant.Value switch
                {
                    FontVariant.SuperScript => @"\super",
                    FontVariant.SubScript => @"\sub",
                    _ => @"\nosupersub"
                });
            }

            if (style.TextColor.HasValue)
                sb.Append($@"\cf{GetColorIndex(colors, style.TextColor.Value)}");

            if (style.BackgroundColor.HasValue)
                sb.Append($@"\cb{GetColorIndex(colors, style.BackgroundColor.Value)}");
        }

        private static void AddFont(List<string> fonts, string fontName)
        {
            if (!fonts.Any(f => string.Equals(f, fontName, StringComparison.OrdinalIgnoreCase)))
                fonts.Add(fontName);
        }

        private static void AddColor(List<SKColor> colors, SKColor color)
        {
            if (!colors.Any(c => c.Red == color.Red && c.Green == color.Green && c.Blue == color.Blue))
                colors.Add(color);
        }

        private static int GetColorIndex(List<SKColor> colors, SKColor color) =>
            colors.FindIndex(c => c.Red == color.Red && c.Green == color.Green && c.Blue == color.Blue) + 1;

        private static string EscapeRtf(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '{':  sb.Append(@"\{"); break;
                    case '}':  sb.Append(@"\}"); break;
                    default:
                        if (c > 127)
                        {
                            var codePoint = (int)c;
                            var rtfVal = codePoint > 32767 ? codePoint - 65536 : codePoint;
                            sb.Append($@"\u{rtfVal}?");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
