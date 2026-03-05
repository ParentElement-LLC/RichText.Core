using ParentElement.RichText.Core.Abstractions.Controllers;
using ParentElement.RichText.Core.Tables;
using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;
using SkiaSharp;
using System.Text;

namespace ParentElement.RichText.Core.IO
{
    public class RtfImporter : ImporterBase
    {
        // Half-points → px  (inverse of PxToHalfPoints in RtfExporter)
        private static float HalfPointsToPx(int halfPoints) => halfPoints / 1.5f;
        // Twips → px
        private static float TwipsToPx(int twips) => twips / 15f;

        // List extra indent per level (matches RtfExporter.kListIndentPerLevel)
        private const float _kListIndentPerLevel = 30f;


        public override async Task ImportAsync(IDocumentController controller, Stream inputStream)
        {
            using var reader = new StreamReader(inputStream, Encoding.ASCII, leaveOpen: true);
            var rtfText = await reader.ReadToEndAsync();
            var blocks = ParseRtf(rtfText);
            await InsertMixedBlocks(controller, blocks);
        }

        public override async Task ImportAsync(IDocumentController controller, string filePath)
        {
            await using var stream = File.OpenRead(filePath);
            await ImportAsync(controller, stream);
        }


        private enum TokenKind { OpenGroup, CloseGroup, ControlWord, ControlSymbol, Text }

        private readonly struct RtfToken
        {
            public TokenKind Kind      { get; init; }
            public string?   Word      { get; init; }
            public int?      Parameter { get; init; }
            public string?   Text      { get; init; }
        }


        private static IEnumerable<RtfToken> Tokenize(string rtf)
        {
            int i = 0;
            while (i < rtf.Length)
            {
                char c = rtf[i];

                if (c == '{')
                {
                    yield return new RtfToken { Kind = TokenKind.OpenGroup };
                    i++;
                }
                else if (c == '}')
                {
                    yield return new RtfToken { Kind = TokenKind.CloseGroup };
                    i++;
                }
                else if (c == '\\')
                {
                    i++;
                    if (i >= rtf.Length) break;
                    c = rtf[i];

                    if (char.IsLetter(c))
                    {
                        int wordStart = i;
                        while (i < rtf.Length && char.IsLetter(rtf[i])) i++;
                        var word = rtf[wordStart..i];

                        int? param = null;
                        bool negative = false;
                        if (i < rtf.Length && rtf[i] == '-') { negative = true; i++; }
                        if (i < rtf.Length && char.IsDigit(rtf[i]))
                        {
                            int numStart = i;
                            while (i < rtf.Length && char.IsDigit(rtf[i])) i++;
                            param = int.Parse(rtf[numStart..i]) * (negative ? -1 : 1);
                        }
                        else if (negative) i--;

                        if (i < rtf.Length && rtf[i] == ' ') i++;

                        yield return new RtfToken
                        {
                            Kind      = TokenKind.ControlWord,
                            Word      = word,
                            Parameter = param
                        };
                    }
                    else
                    {
                        yield return new RtfToken
                        {
                            Kind = TokenKind.ControlSymbol,
                            Word = c.ToString()
                        };
                        i++;
                    }
                }
                else
                {
                    var sb = new StringBuilder();
                    while (i < rtf.Length && rtf[i] != '{' && rtf[i] != '}' && rtf[i] != '\\')
                    {
                        if (rtf[i] != '\r' && rtf[i] != '\n')
                            sb.Append(rtf[i]);
                        i++;
                    }
                    if (sb.Length > 0)
                        yield return new RtfToken { Kind = TokenKind.Text, Text = sb.ToString() };
                }
            }
        }

        private sealed class FormatState
        {
            public int     FontIndex   { get; set; }
            public int     FontSize    { get; set; } = 24;  // half-points
            public bool    Bold        { get; set; }
            public bool    Italic      { get; set; }
            public bool    Underline   { get; set; }
            public bool    Strike      { get; set; }
            public FontVariant Variant { get; set; } = FontVariant.Normal;
            public int     ColorIndex  { get; set; }
            public int     BgIndex     { get; set; }
            public Destination Dest    { get; set; } = Destination.Main;

            public FormatState Clone() => (FormatState)MemberwiseClone();
        }

        // Per-paragraph state (reset on \pard; does not stack with groups)
        private sealed class ParagraphState
        {
            public TextAlignment Alignment      { get; set; } = TextAlignment.Left;
            public int           BlockIndentTwips { get; set; }
            public int           ListId         { get; set; }  // 0=none, 1=bullet, 2=numbered
            public int           ListLevel      { get; set; }
            public int           LineSl         { get; set; }
            public bool          LineSlMult     { get; set; }

            public void Reset()
            {
                Alignment       = TextAlignment.Left;
                BlockIndentTwips = 0;
                ListId          = 0;
                ListLevel       = 0;
                LineSl          = 0;
                LineSlMult      = false;
            }
        }

        private enum Destination { Main, FontTable, FontEntry, ColorTable, Skip }

        private sealed class RtfCellDef
        {
            public int    RightEdgeTwips;
            public bool   IsVMergeFirst;  // \clvmgf — rowspan primary
            public bool   IsVMergeCont;   // \clvmrg — rowspan continuation
            public bool   IsHMergeCont;   // \clmrg  — colspan continuation (Word format)
            public int    BgColorIdx;     // \clcbpat (1-based; 0 = none)
            public string BdrStyle   = "none";  // "solid", "dashed", "dotted", "none"
            public int    BdrWidthTwips;
            public int    BdrColorIdx;    // 1-based; 0 = none
        }

        private sealed class RtfRawCell
        {
            public RtfCellDef             Def        = new();
            public List<ParsedParagraph>  Paragraphs = new();
        }

        private sealed class RtfRawRow
        {
            public List<RtfRawCell> Cells = new();
        }

        private static List<object> ParseRtf(string rtfText)
        {
            var tokens    = Tokenize(rtfText);
            var fontTable = new List<string>();
            var colorTable = new List<SKColor>();

            var stateStack   = new Stack<FormatState>();
            var currentState = new FormatState();
            var paraState    = new ParagraphState();

            var blocks      = new List<object>();      // mixed ParsedParagraph + ParsedTable
            var currentRuns = new List<ParsedRun>();

            byte colorR = 0, colorG = 0, colorB = 0;
            var fontNameBuilder = new StringBuilder();

            bool inPict    = false;
            int  pictDepth = 0;
            int  currentDepth = 0;
            bool isPngBlip = false;
            int  picWTwips = 0;
            int  picHTwips = 0;
            var  pictHex   = new StringBuilder();

            var rawRows           = new List<RtfRawRow>();
            var currentRowCellDefs = new List<RtfCellDef>();
            var pendingCellDef    = new RtfCellDef();
            var currentRowCells   = new List<RtfRawCell>();
            var currentCellParas  = new List<ParsedParagraph>();
            bool inTable          = false;   // currently in table cell content
            bool inTableRowDef    = false;   // after \trowd, collecting cell defs
            bool inBorderDef      = false;   // collecting border attrs for current cell def
            int  rowGapTwips      = 60;      // \trgaph (default = 4 px = 60 twips)

            // Flush any accumulated table rows as a ParsedTable block
            void FlushPendingTable()
            {
                if (rawRows.Count == 0) return;
                var tbl = BuildParsedTable(rawRows, colorTable, rowGapTwips);
                if (tbl != null) blocks.Add(tbl);
                rawRows.Clear();
            }

            foreach (var token in tokens)
            {
                switch (token.Kind)
                {
                    case TokenKind.OpenGroup:
                        currentDepth++;
                        stateStack.Push(currentState);
                        currentState = currentState.Clone();
                        break;

                    case TokenKind.CloseGroup:
                        // Closing a pict group — convert accumulated hex to an image run.
                        if (inPict && currentDepth == pictDepth)
                        {
                            if (isPngBlip && pictHex.Length > 0)
                            {
                                var imgBytes = HexToBytes(pictHex.ToString());
                                if (imgBytes.Length > 0)
                                {
                                    var target = inTable ? currentCellParas.Count > 0
                                        ? (List<ParsedRun>)null! // will add to currentRuns
                                        : null : null;
                                    currentRuns.Add(new ParsedRun("", new Style(), imgBytes,
                                        TwipsToPx(picWTwips), TwipsToPx(picHTwips)));
                                }
                            }
                            inPict = false;
                            pictHex.Clear();
                        }

                        currentDepth--;

                        if (currentState.Dest == Destination.FontEntry)
                        {
                            var name = fontNameBuilder.ToString().TrimEnd(';', ' ');
                            while (fontTable.Count <= currentState.FontIndex)
                                fontTable.Add(string.Empty);
                            fontTable[currentState.FontIndex] = name;
                            fontNameBuilder.Clear();
                        }

                        if (stateStack.Count > 0)
                            currentState = stateStack.Pop();
                        break;

                    case TokenKind.ControlSymbol:
                        if (!inPict)
                        {
                            if (token.Word == "*" && stateStack.Count > 0)
                            {
                                currentState.Dest = Destination.Skip;
                            }
                            else if (token.Word == "\\" && currentState.Dest == Destination.Main)
                            {
                                FlushText("\\", currentState, fontTable, colorTable, currentRuns);
                            }
                            else if (token.Word == "{" && currentState.Dest == Destination.Main)
                            {
                                FlushText("{", currentState, fontTable, colorTable, currentRuns);
                            }
                            else if (token.Word == "}" && currentState.Dest == Destination.Main)
                            {
                                FlushText("}", currentState, fontTable, colorTable, currentRuns);
                            }
                        }
                        break;

                    case TokenKind.ControlWord:
                        if (!inPict && token.Word == "pict")
                        {
                            inPict     = true;
                            pictDepth  = currentDepth;
                            isPngBlip  = false;
                            picWTwips  = 0;
                            picHTwips  = 0;
                            pictHex.Clear();
                        }
                        else if (inPict)
                        {
                            switch (token.Word)
                            {
                                case "pngblip":  isPngBlip = true; break;
                                case "picwgoal": picWTwips = token.Parameter ?? 0; break;
                                case "pichgoal": picHTwips = token.Parameter ?? 0; break;
                            }
                        }
                        else
                        {
                            // Handle table structure words before general formatting
                            bool handled = true;
                            switch (token.Word)
                            {
                                case "trowd":
                                    inTableRowDef  = true;
                                    inTable        = false;
                                    inBorderDef    = false;
                                    currentRowCellDefs.Clear();
                                    currentRowCells.Clear();
                                    currentCellParas.Clear();
                                    currentRuns.Clear();
                                    pendingCellDef = new RtfCellDef();
                                    rowGapTwips    = 60;
                                    break;

                                case "trgaph":
                                    if (inTableRowDef)
                                        rowGapTwips = token.Parameter ?? 60;
                                    break;

                                case "clvmgf":
                                    if (inTableRowDef) pendingCellDef.IsVMergeFirst = true;
                                    break;
                                case "clvmrg":
                                    if (inTableRowDef) pendingCellDef.IsVMergeCont = true;
                                    break;
                                case "clmgf":
                                    // Horizontal merge first — we rely on \cellx width for colspan,
                                    // so nothing special to record here.
                                    break;
                                case "clmrg":
                                    if (inTableRowDef) pendingCellDef.IsHMergeCont = true;
                                    break;
                                case "clcbpat":
                                    if (inTableRowDef) pendingCellDef.BgColorIdx = token.Parameter ?? 0;
                                    break;
                                case "clshdng":
                                    // Shading percentage — not needed for our import
                                    break;

                                case "clbrdrl":
                                case "clbrdrt":
                                case "clbrdrb":
                                case "clbrdrr":
                                    if (inTableRowDef) inBorderDef = true;
                                    break;

                                case "brdrs":
                                    if (inTableRowDef && inBorderDef) pendingCellDef.BdrStyle = "solid";
                                    break;
                                case "brdrdash":
                                    if (inTableRowDef && inBorderDef) pendingCellDef.BdrStyle = "dashed";
                                    break;
                                case "brdrdot":
                                    if (inTableRowDef && inBorderDef) pendingCellDef.BdrStyle = "dotted";
                                    break;
                                case "brdrnone":
                                    if (inTableRowDef && inBorderDef) pendingCellDef.BdrStyle = "none";
                                    break;
                                case "brdrw":
                                    if (inTableRowDef && inBorderDef) pendingCellDef.BdrWidthTwips = token.Parameter ?? 0;
                                    break;
                                case "brdrcf":
                                    if (inTableRowDef && inBorderDef) pendingCellDef.BdrColorIdx = token.Parameter ?? 0;
                                    break;

                                case "cellx":
                                    if (inTableRowDef)
                                    {
                                        pendingCellDef.RightEdgeTwips = token.Parameter ?? 0;
                                        currentRowCellDefs.Add(pendingCellDef);
                                        pendingCellDef = new RtfCellDef();
                                        inBorderDef    = false;
                                    }
                                    break;

                                case "intbl":
                                    if (!inTable)
                                    {
                                        inTable       = true;
                                        inTableRowDef = false;
                                    }
                                    break;

                                case "cell":
                                    if (inTable)
                                    {
                                        // Flush any pending runs as the final cell paragraph
                                        currentCellParas.Add(BuildParagraph(paraState, new List<ParsedRun>(currentRuns)));
                                        currentRuns.Clear();
                                        paraState.Reset();

                                        // Find the matching cell definition for this cell
                                        int defIdx = currentRowCells.Count;
                                        var def = defIdx < currentRowCellDefs.Count
                                            ? currentRowCellDefs[defIdx]
                                            : new RtfCellDef();

                                        currentRowCells.Add(new RtfRawCell
                                        {
                                            Def        = def,
                                            Paragraphs = new List<ParsedParagraph>(currentCellParas),
                                        });
                                        currentCellParas.Clear();
                                    }
                                    break;

                                case "row":
                                    if (inTable || inTableRowDef)
                                    {
                                        rawRows.Add(new RtfRawRow { Cells = new List<RtfRawCell>(currentRowCells) });
                                        currentRowCells.Clear();
                                        currentRowCellDefs.Clear();
                                        currentCellParas.Clear();
                                        currentRuns.Clear();
                                        inTable       = false;
                                        inTableRowDef = false;
                                    }
                                    break;

                                case "par":
                                    if (inTable)
                                    {
                                        // Paragraph break inside a table cell
                                        currentCellParas.Add(BuildParagraph(paraState, new List<ParsedRun>(currentRuns)));
                                        currentRuns.Clear();
                                        paraState.Reset();
                                    }
                                    else
                                    {
                                        FlushPendingTable();
                                        blocks.Add(BuildParagraph(paraState, new List<ParsedRun>(currentRuns)));
                                        currentRuns.Clear();
                                        paraState.Reset();
                                    }
                                    break;

                                default:
                                    handled = false;
                                    break;
                            }

                            if (!handled)
                            {
                                HandleControlWord(token, currentState, paraState, fontTable, colorTable,
                                    ref colorR, ref colorG, ref colorB, fontNameBuilder, currentRuns);
                            }
                        }
                        break;

                    case TokenKind.Text:
                        if (inPict)
                        {
                            pictHex.Append(token.Text);
                        }
                        else if (currentState.Dest == Destination.FontEntry)
                        {
                            fontNameBuilder.Append(token.Text);
                        }
                        else if (currentState.Dest == Destination.Main)
                        {
                            FlushText(token.Text!, currentState, fontTable, colorTable, currentRuns);
                        }
                        break;
                }
            }

            // End of document: flush any open table, then flush any remaining runs
            if (inTable && currentCellParas.Count > 0 && currentRowCells.Count == 0)
            {
                // Malformed RTF — cell never got \cell; add as a synthetic row
                if (currentRuns.Count > 0)
                    currentCellParas.Add(BuildParagraph(paraState, new List<ParsedRun>(currentRuns)));
                int defIdx2 = currentRowCells.Count;
                var def2    = defIdx2 < currentRowCellDefs.Count ? currentRowCellDefs[defIdx2] : new RtfCellDef();
                currentRowCells.Add(new RtfRawCell { Def = def2, Paragraphs = new List<ParsedParagraph>(currentCellParas) });
                rawRows.Add(new RtfRawRow { Cells = new List<RtfRawCell>(currentRowCells) });
                currentRuns.Clear();
            }

            FlushPendingTable();

            if (currentRuns.Count > 0)
                blocks.Add(BuildParagraph(paraState, new List<ParsedRun>(currentRuns)));

            return blocks;
        }

        private static ParsedTable? BuildParsedTable(
            List<RtfRawRow> rawRows,
            List<SKColor> colorTable,
            int gapTwips)
        {
            if (rawRows.Count == 0) return null;
            int nRows = rawRows.Count;

            // 1. Collect all unique right-edge values → canonical column boundaries (sorted)
            var edgeSet = new SortedSet<int>();
            foreach (var row in rawRows)
                foreach (var cell in row.Cells)
                    if (cell.Def.RightEdgeTwips > 0)
                        edgeSet.Add(cell.Def.RightEdgeTwips);

            if (edgeSet.Count == 0) return null;
            var colEdges = edgeSet.ToArray();   // ascending sorted right-edge positions
            int nCols    = colEdges.Length;

            // 2. Compute net column widths in pixels (right edge delta minus padding on both sides)
            float cellPadding = TwipsToPx(gapTwips);
            var colWidthsPx = new float[nCols];
            int prevEdge = 0;
            for (int c = 0; c < nCols; c++)
            {
                float totalPx = TwipsToPx(colEdges[c] - prevEdge);
                colWidthsPx[c] = Math.Max(1f, totalPx - 2f * cellPadding);
                prevEdge = colEdges[c];
            }

            // 3. Extract table-level border style from the first bordered cell
            var   tableBdrStyle = TableBorderStyle.None;
            float tableBdrWidth = 1f;
            var   tableBdrColor = new SKColor(192, 192, 192);
            bool  borderFound   = false;
            foreach (var row in rawRows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.Def.BdrStyle != "none" && cell.Def.BdrWidthTwips > 0)
                    {
                        tableBdrStyle = cell.Def.BdrStyle switch
                        {
                            "dashed" => TableBorderStyle.Dashed,
                            "dotted" => TableBorderStyle.Dotted,
                            _        => TableBorderStyle.Solid,
                        };
                        tableBdrWidth = TwipsToPx(cell.Def.BdrWidthTwips);
                        if (cell.Def.BdrColorIdx > 0 && cell.Def.BdrColorIdx <= colorTable.Count)
                            tableBdrColor = colorTable[cell.Def.BdrColorIdx - 1];
                        borderFound = true;
                        break;
                    }
                }
                if (borderFound) break;
            }

            // 4. Map each row's cells to grid column positions
            //    Each entry: (colStart, colSpan, isVMergeFirst, isVMergeCont, rawCell)
            var rawGrid = new List<(int colStart, int colSpan, bool vmgf, bool vmrg, RtfRawCell cell)>[nRows];
            for (int r = 0; r < nRows; r++)
            {
                rawGrid[r] = new();
                int colCursor = 0;

                foreach (var rawCell in rawRows[r].Cells)
                {
                    // Skip Word-style horizontal-merge continuation cells;
                    // absorb their width into the preceding primary cell.
                    if (rawCell.Def.IsHMergeCont)
                    {
                        int hEnd = FindColIndex(colEdges, rawCell.Def.RightEdgeTwips);
                        if (rawGrid[r].Count > 0)
                        {
                            var last = rawGrid[r][^1];
                            int newSpan = hEnd - last.colStart + 1;
                            rawGrid[r][^1] = (last.colStart, newSpan, last.vmgf, last.vmrg, last.cell);
                        }
                        colCursor = hEnd + 1;
                        continue;
                    }

                    int colStart = colCursor;
                    int colEnd   = FindColIndex(colEdges, rawCell.Def.RightEdgeTwips);
                    if (colEnd < colStart) colEnd = colStart;
                    int colSpan  = colEnd - colStart + 1;

                    rawGrid[r].Add((colStart, colSpan, rawCell.Def.IsVMergeFirst, rawCell.Def.IsVMergeCont, rawCell));
                    colCursor = colEnd + 1;
                }
            }

            // 5. Build the ParsedTableCell grid
            var grid = new ParsedTableCell?[nRows][];
            for (int r = 0; r < nRows; r++)
                grid[r] = new ParsedTableCell?[nCols];

            for (int r = 0; r < nRows; r++)
            {
                foreach (var (colStart, colSpan, vmgf, vmrg, rawCell) in rawGrid[r])
                {
                    if (vmrg) continue; // rowspan continuation — grid slot stays null

                    // Count rowspan extent by looking for vmrg entries in subsequent rows
                    int rowSpan = 1;
                    if (vmgf)
                    {
                        for (int rr = r + 1; rr < nRows; rr++)
                        {
                            bool hasCont = rawGrid[rr].Any(x => x.colStart == colStart && x.vmrg);
                            if (hasCont) rowSpan++;
                            else break;
                        }
                    }

                    // Background color
                    SKColor? bgColor = null;
                    if (rawCell.Def.BgColorIdx > 0 && rawCell.Def.BgColorIdx <= colorTable.Count)
                        bgColor = colorTable[rawCell.Def.BgColorIdx - 1];

                    grid[r][colStart] = new ParsedTableCell(
                        isHeader:            false, // RTF has no "header row" concept
                        rowSpan:             rowSpan,
                        colSpan:             colSpan,
                        content:             rawCell.Paragraphs,
                        cellBackgroundColor: bgColor);
                }
            }

            return new ParsedTable(
                rows:        nRows,
                cols:        nCols,
                cells:       grid,
                borderStyle: tableBdrStyle,
                borderWidth: tableBdrWidth,
                borderColor: tableBdrColor,
                includeHeader: false,
                colWidths:   colWidthsPx);
        }

        /// <summary>
        /// Returns the 0-based index into <paramref name="sortedEdges"/> of the entry that equals
        /// <paramref name="rightEdge"/>, or the closest index if not found exactly.
        /// </summary>
        private static int FindColIndex(int[] sortedEdges, int rightEdge)
        {
            int idx = Array.BinarySearch(sortedEdges, rightEdge);
            if (idx >= 0) return idx;
            // BinarySearch returns ~insertionPoint when not found; take the nearest existing index
            int insertPoint = ~idx;
            if (insertPoint >= sortedEdges.Length) return sortedEdges.Length - 1;
            if (insertPoint == 0) return 0;
            // Return the closer of the two neighbors
            return (sortedEdges[insertPoint] - rightEdge) <= (rightEdge - sortedEdges[insertPoint - 1])
                ? insertPoint
                : insertPoint - 1;
        }

        private static byte[] HexToBytes(string hex)
        {
            // Strip all whitespace that may be present in the RTF hex stream.
            var sb = new StringBuilder(hex.Length);
            foreach (char c in hex)
                if (!char.IsWhiteSpace(c)) sb.Append(c);

            var clean = sb.ToString();
            if (clean.Length % 2 != 0) return Array.Empty<byte>();

            var bytes = new byte[clean.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(clean.AsSpan(i * 2, 2),
                        System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                    return Array.Empty<byte>();
            }
            return bytes;
        }

        private static ParsedParagraph BuildParagraph(ParagraphState paraState, List<ParsedRun> runs)
        {
            var listType = paraState.ListId switch
            {
                1 => ListType.Bullet,
                2 => ListType.Numbered,
                _ => ListType.None
            };

            float blockIndentPx;
            if (listType != ListType.None)
            {
                blockIndentPx = Math.Max(0f, TwipsToPx(paraState.BlockIndentTwips) - (paraState.ListLevel + 1) * _kListIndentPerLevel);
            }
            else
            {
                blockIndentPx = TwipsToPx(paraState.BlockIndentTwips);
            }

            float? lineSpacing = paraState.LineSlMult && paraState.LineSl > 0
                ? paraState.LineSl / 240f
                : (float?)null;

            return new ParsedParagraph(paraState.Alignment, runs, blockIndentPx, listType, paraState.ListLevel, lineSpacing);
        }

        private static void HandleControlWord(
            RtfToken token,
            FormatState state,
            ParagraphState paraState,
            List<string> fontTable,
            List<SKColor> colorTable,
            ref byte colorR, ref byte colorG, ref byte colorB,
            StringBuilder fontNameBuilder,
            List<ParsedRun> currentRuns)
        {
            if (state.Dest == Destination.Skip) return;

            var word  = token.Word!;
            var param = token.Parameter;

            switch (word)
            {
                case "fonttbl":
                    state.Dest = Destination.FontTable;
                    break;
                case "colortbl":
                    state.Dest = Destination.ColorTable;
                    break;

                case "f":
                    if (state.Dest == Destination.FontTable)
                    {
                        state.Dest      = Destination.FontEntry;
                        state.FontIndex = param ?? 0;
                    }
                    else if (state.Dest == Destination.Main)
                    {
                        state.FontIndex = param ?? 0;
                    }
                    break;

                case "red":
                    colorR = (byte)(param ?? 0);
                    break;
                case "green":
                    colorG = (byte)(param ?? 0);
                    break;
                case "blue":
                    colorB = (byte)(param ?? 0);
                    if (state.Dest == Destination.ColorTable)
                        colorTable.Add(new SKColor(colorR, colorG, colorB));
                    break;

                case "pard":
                    paraState.Reset();
                    state.Bold      = false;
                    state.Italic    = false;
                    state.Underline = false;
                    state.Strike    = false;
                    state.Variant   = FontVariant.Normal;
                    state.ColorIndex = 0;
                    state.BgIndex   = 0;
                    break;
                case "ql":
                    paraState.Alignment = TextAlignment.Left;
                    break;
                case "qr":
                    paraState.Alignment = TextAlignment.Right;
                    break;
                case "qc":
                    paraState.Alignment = TextAlignment.Center;
                    break;

                case "li":
                    paraState.BlockIndentTwips = param ?? 0;
                    break;

                case "ls":
                    paraState.ListId = param ?? 0;
                    break;
                case "ilvl":
                    paraState.ListLevel = param ?? 0;
                    break;

                case "sl":
                    paraState.LineSl = param ?? 0;
                    break;
                case "slmult":
                    paraState.LineSlMult = (param ?? 0) != 0;
                    break;

                case "fs":
                    state.FontSize = param ?? 24;
                    break;
                case "b":
                    state.Bold = param != 0;
                    break;
                case "i":
                    state.Italic = param != 0;
                    break;
                case "ul":
                    state.Underline = true;
                    break;
                case "ulnone":
                    state.Underline = false;
                    break;
                case "strike":
                    state.Strike = param != 0;
                    break;
                case "super":
                    state.Variant = FontVariant.SuperScript;
                    break;
                case "sub":
                    state.Variant = FontVariant.SubScript;
                    break;
                case "nosupersub":
                    state.Variant = FontVariant.Normal;
                    break;
                case "cf":
                    state.ColorIndex = param ?? 0;
                    break;
                case "cb":
                    state.BgIndex = param ?? 0;
                    break;

                case "u":
                {
                    if (param.HasValue && state.Dest == Destination.Main)
                    {
                        int cp = param.Value < 0 ? param.Value + 65536 : param.Value;
                        FlushText(char.ConvertFromUtf32(cp), state, fontTable, colorTable, currentRuns);
                    }
                    break;
                }
            }
        }

        private static void FlushText(
            string text,
            FormatState state,
            List<string> fontTable,
            List<SKColor> colorTable,
            List<ParsedRun> runs)
        {
            if (string.IsNullOrEmpty(text)) return;

            var style = new Style
            {
                FontSize      = HalfPointsToPx(state.FontSize),
                FontWeight    = state.Bold ? 700 : 400,
                FontItalic    = state.Italic,
                Underline     = state.Underline ? UnderlineStyle.Solid : UnderlineStyle.None,
                StrikeThrough = state.Strike ? StrikeThroughStyle.Solid : StrikeThroughStyle.None,
                FontVariant   = state.Variant,
            };

            if (state.FontIndex < fontTable.Count)
            {
                var family = fontTable[state.FontIndex];
                if (!string.IsNullOrWhiteSpace(family))
                    style.FontFamily = family;
            }

            if (state.ColorIndex > 0 && state.ColorIndex <= colorTable.Count)
                style.TextColor = colorTable[state.ColorIndex - 1];

            if (state.BgIndex > 0 && state.BgIndex <= colorTable.Count)
                style.BackgroundColor = colorTable[state.BgIndex - 1];

            runs.Add(new ParsedRun(text, style));
        }
    }
}
