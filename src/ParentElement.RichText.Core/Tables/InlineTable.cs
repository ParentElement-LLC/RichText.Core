using ParentElement.Topten.RichTextKit;
using SkiaSharp;

namespace ParentElement.RichText.Core.Tables;

public class InlineTable : IInlineObject
{
    internal const float _cellPadding = 4f;

    private readonly TableOptions _options;
    private TableCell?[][] _cells;   // null = covered by a spanning primary cell
    private readonly float _totalWidth;
    private float[] _rowHeights;
    private float[] _colWidths;
    private float[] _manualColWidths;  // NaN = auto-distributed, else fixed pixel width
    private readonly IStyle _defaultStyle;
    private readonly Action _invalidate;
    private SKPaint? _headerFillPaint;
    private SKPaint? _whiteFillPaint;

    // Multi-cell selection state
    private (int row, int col) _selAnchor;
    private (int row, int col) _selEnd;
    private bool _hasMultiSelection;

    public Action? OnHeightChanged { get; set; }

    public float Width => _totalWidth;
    private float EffBw => _options.BorderStyle == TableBorderStyle.None ? 0f : _options.BorderWidth;
    public float BorderWidth => EffBw;

    public IReadOnlyList<float> ColWidths => _colWidths;
    public IReadOnlyList<float> RowHeights { get { RecalcRowHeights(); return _rowHeights; } }
    public TableBorderStyle BorderStyle => _options.BorderStyle;
    public SKColor BorderColor => _options.BorderColor;

    public float Height
    {
        get
        {
            RecalcRowHeights();
            float h = (_options.Rows + 1) * EffBw;
            foreach (var rh in _rowHeights) h += rh;
            return h;
        }
    }

    public int Rows => _options.Rows;
    public int Cols => _options.Columns;

    /// <summary>Returns true when no primary cells remain (every grid slot is null).</summary>
    public bool IsEmpty
    {
        get
        {
            for (int r = 0; r < _options.Rows; r++)
                for (int c = 0; c < _options.Columns; c++)
                    if (_cells[r][c] != null) return false;
            return true;
        }
    }

    public InlineTable(TableOptions opts, float contentWidth, IStyle defaultStyle, Action invalidate)
    {
        _options = opts;
        _totalWidth = contentWidth;
        _defaultStyle = defaultStyle;
        _invalidate = invalidate;

        _colWidths = new float[opts.Columns];
        _manualColWidths = new float[opts.Columns];
        for (int c = 0; c < opts.Columns; c++)
            _manualColWidths[c] = float.NaN; // all auto initially
        RecalcEffectiveColWidths();

        _rowHeights = new float[opts.Rows];

        _cells = new TableCell?[opts.Rows][];
        for (int r = 0; r < opts.Rows; r++)
        {
            _cells[r] = new TableCell?[opts.Columns];
            for (int c = 0; c < opts.Columns; c++)
            {
                bool isHeader = opts.IncludeHeader && r == 0;
                float cellContentWidth = _colWidths[c] - 2 * _cellPadding;
                _cells[r][c] = CreateCell(cellContentWidth, isHeader);
            }
        }

        RecalcRowHeights();
    }

    /// <summary>
    /// Recomputes <see cref="_colWidths"/> from <see cref="_manualColWidths"/>: auto columns
    /// share the width left over after summing all fixed-width columns, but each auto column
    /// is guaranteed to be at least as wide as its widest word (plus cell padding).
    /// </summary>
    private void RecalcEffectiveColWidths()
    {
        float bw = EffBw;
        float available = _totalWidth - (_options.Columns + 1) * bw;

        // Assign fixed columns; collect auto column indices.
        float manualSum = 0f;
        int autoCount = 0;
        for (int c = 0; c < _options.Columns; c++)
        {
            if (float.IsNaN(_manualColWidths[c]))
            {
                autoCount++;
            }
            else
            {
                // Enforce content minimum even for manually-sized columns so that a column
                // never becomes narrower than its widest word / widest image.
                _colWidths[c] = Math.Max(_manualColWidths[c], GetColumnMinWidth(c));
                manualSum += _colWidths[c];
            }
        }

        if (autoCount == 0) return;

        // Compute minimum required width for each auto column (widest word + 2×padding).
        float pool = available - manualSum;

        // Greedy: any auto column whose minimum width exceeds the equal share gets its
        // minimum immediately; the remaining pool is re-divided among the rest.
        bool[] resolved = new bool[_options.Columns];
        int remaining = autoCount;

        bool changed = true;
        while (changed && remaining > 0)
        {
            changed = false;
            float share = pool / remaining;
            for (int c = 0; c < _options.Columns; c++)
            {
                if (resolved[c] || !float.IsNaN(_manualColWidths[c])) continue;
                float minW = GetColumnMinWidth(c);
                if (minW > share)
                {
                    _colWidths[c] = minW;
                    pool -= minW;
                    resolved[c] = true;
                    remaining--;
                    changed = true;
                }
            }
        }

        float finalWidth = remaining > 0 ? Math.Max(pool / remaining, 10f) : 10f;
        for (int c = 0; c < _options.Columns; c++)
        {
            if (!resolved[c] && float.IsNaN(_manualColWidths[c]))
                _colWidths[c] = finalWidth;
        }
    }

    /// <summary>
    /// Returns the minimum rendered width for the given column: the maximum of
    /// (cell.MinContentWidth + 2×padding) across all single-span primary cells in that column.
    /// </summary>
    public float GetColumnMinWidth(int col)
    {
        // _cells is null during the constructor's first RecalcEffectiveColWidths() call
        // (before cells are created). Return the bare minimum so equal distribution applies.
        if (_cells == null) return 10f;

        float min = 10f;
        for (int r = 0; r < _options.Rows; r++)
        {
            var cell = _cells[r]?[col];
            // Only single-column-span cells constrain this specific column's minimum.
            if (cell != null && cell.ColSpan == 1)
                min = Math.Max(min, cell.Controller.MinContentWidth + 2 * _cellPadding);
        }
        return min;
    }

    /// <summary>
    /// Returns the index of the inner column divider (0 = between col 0 and col 1) that the
    /// given table-local point is within <paramref name="tolerance"/> pixels of.
    /// Returns -1 if not near any inner divider. Outer borders are excluded.
    /// </summary>
    public int HitTestColumnDivider(SKPoint tableLocal, float tolerance)
    {
        if (_options.Columns < 2) return -1;
        RecalcRowHeights();
        if (tableLocal.Y < 0 || tableLocal.Y > Height) return -1;

        float bw = EffBw;
        float x = bw; // left edge of column 0
        for (int d = 0; d < _options.Columns - 1; d++)
        {
            x += _colWidths[d]; // x = left edge of border between col d and col d+1
            // Border occupies [x, x+bw]; hit zone is [x-tolerance, x+bw+tolerance]
            if (tableLocal.X >= x - tolerance && tableLocal.X <= x + bw + tolerance)
                return d;
            x += bw; // x = left edge of col d+1
        }
        return -1;
    }

    /// <summary>
    /// Resizes the two columns on either side of <paramref name="dividerIdx"/> by fixing
    /// their widths to <paramref name="newLeftWidth"/> and <paramref name="newRightWidth"/>
    /// respectively. All other columns (auto or manual) are left unchanged.
    /// </summary>
    public void ResizeDivider(int dividerIdx, float newLeftWidth, float newRightWidth)
    {
        if (dividerIdx < 0 || dividerIdx + 1 >= _options.Columns) return;
        _manualColWidths[dividerIdx]     = Math.Max(newLeftWidth,  10f);
        _manualColWidths[dividerIdx + 1] = Math.Max(newRightWidth, 10f);
        RecalcEffectiveColWidths();
        UpdateAllCellWidths();
        RecalcRowHeights();
        OnHeightChanged?.Invoke();
    }

    /// <summary>
    /// After column widths change, updates every primary cell's controller width to match
    /// its actual rendered width (accounting for ColSpan).
    /// </summary>
    private void UpdateAllCellWidths()
    {
        for (int r = 0; r < _options.Rows; r++)
            for (int c = 0; c < _options.Columns; c++)
            {
                var cell = _cells[r][c];
                if (cell == null) continue;
                var bounds = GetCellBounds(r, c);
                cell.Controller.SetWidth(Math.Max(bounds.Width - 2 * _cellPadding, 10f));
            }
    }

    private TableCell CreateCell(float cellContentWidth, bool isHeader)
    {
        SKColor? bgOverride = isHeader ? _options.HeaderBackgroundColor : (SKColor?)null;
        return new TableCell(Math.Max(cellContentWidth, 10f), _defaultStyle, isHeader, _invalidate, OnCellContentChanged, bgOverride);
    }

    private void OnCellContentChanged()
    {
        // Snapshot current column widths so we can detect whether any column needs to grow.
        float[] oldColWidths = (float[])_colWidths.Clone();
        float oldHeight = _rowHeights.Sum() + (_options.Rows + 1) * EffBw;

        // Recompute column widths — content minimums may have changed (new word, image added).
        // MinContentWidth caches results per-cell; only the cell that changed has a dirty cache,
        // so this is effectively one temp-relayout + cheap cache reads for all other cells.
        RecalcEffectiveColWidths();

        bool widthsChanged = false;
        for (int c = 0; c < _options.Columns; c++)
        {
            if (Math.Abs(_colWidths[c] - oldColWidths[c]) > 0.1f)
            {
                widthsChanged = true;
                break;
            }
        }

        if (widthsChanged)
            UpdateAllCellWidths();

        RecalcRowHeights();
        float newHeight = _rowHeights.Sum() + (_options.Rows + 1) * EffBw;

        if (widthsChanged || Math.Abs(newHeight - oldHeight) > 0.1f)
            OnHeightChanged?.Invoke();
    }

    public void RecalcRowHeights()
    {
        for (int r = 0; r < _options.Rows; r++)
        {
            float maxH = 20f; // minimum cell height
            for (int c = 0; c < _options.Columns; c++)
            {
                var cell = _cells[r][c];
                // Only single-row-span primary cells directly constrain this row's height
                if (cell != null && cell.RowSpan == 1)
                    maxH = Math.Max(maxH, cell.Controller.MeasuredHeight + 2 * _cellPadding);
            }
            _rowHeights[r] = maxH;
        }
    }

    /// <summary>
    /// Returns the primary-cell coordinates (row, col) for the cell that owns position
    /// (row, col), or null if the position is out of range.
    /// </summary>
    private (int r, int c)? FindPrimaryCell(int row, int col)
    {
        if (row < 0 || row >= _options.Rows || col < 0 || col >= _options.Columns) return null;
        if (_cells[row][col] != null) return (row, col);

        // Search for a spanning cell that covers (row, col)
        for (int r = 0; r <= row; r++)
            for (int c = 0; c <= col; c++)
            {
                var cell = _cells[r][c];
                if (cell == null) continue;
                if (r + cell.RowSpan > row && c + cell.ColSpan > col)
                    return (r, c);
            }
        return null;
    }

    /// <summary>
    /// Returns the table-local Y offset of the START of the top border of <paramref name="row"/>.
    /// </summary>
    public float GetRowTop(int row)
    {
        RecalcRowHeights();
        float bw = EffBw;
        float y = 0f;
        for (int r = 0; r < row; r++)
            y += bw + _rowHeights[r];
        return y;
    }

    /// <summary>
    /// Returns the index of the first row whose bottom edge would exceed
    /// <paramref name="availableHeight"/> pixels from the table top.
    /// Returns 0 if no rows fit; returns <see cref="Rows"/> if every row fits.
    /// </summary>
    public int GetRowSplitIndex(float availableHeight)
    {
        RecalcRowHeights();
        float bw = EffBw;
        float y = bw; // top border
        for (int r = 0; r < _options.Rows; r++)
        {
            y += _rowHeights[r] + bw;
            if (y > availableHeight)
                return r;
        }
        return _options.Rows;
    }

    /// <summary>
    /// Returns the content bounds (excluding border, including cell padding space) for the
    /// primary cell at (row, col), accounting for its ColSpan and RowSpan.
    /// </summary>
    public SKRect GetCellBounds(int row, int col)
    {
        float bw = EffBw;
        float left = bw;
        for (int c = 0; c < col; c++)
            left += _colWidths[c] + bw;
        float top = bw;
        for (int r = 0; r < row; r++)
            top += _rowHeights[r] + bw;

        var cell = _cells[row][col];
        int colSpan = cell?.ColSpan ?? 1;
        int rowSpan = cell?.RowSpan ?? 1;

        // Sum spanned column widths including interior borders
        float width = -bw;
        for (int c = col; c < Math.Min(col + colSpan, _options.Columns); c++)
            width += _colWidths[c] + bw;

        // Sum spanned row heights including interior borders
        float height = -bw;
        for (int r = row; r < Math.Min(row + rowSpan, _options.Rows); r++)
            height += _rowHeights[r] + bw;

        return new SKRect(left, top, left + width, top + height);
    }

    public (int row, int col)? HitTestCell(SKPoint tableLocalPoint)
    {
        for (int r = 0; r < _options.Rows; r++)
            for (int c = 0; c < _options.Columns; c++)
            {
                if (_cells[r][c] == null) continue;
                if (GetCellBounds(r, c).Contains(tableLocalPoint))
                    return (r, c);
            }
        return null;
    }

    /// <summary>Returns the primary cell at (r, c), or null if the position is covered.</summary>
    public TableCell? GetCell(int r, int c) => _cells[r][c];

    // -----------------------------------------------------------------------------------------
    // Multi-cell selection
    // -----------------------------------------------------------------------------------------

    public bool HasMultiSelection => _hasMultiSelection;

    /// <summary>
    /// Enumerates the <see cref="CellController"/> for every primary cell whose top-left
    /// corner falls inside the current multi-cell selection bounding rect.
    /// </summary>
    internal IEnumerable<CellController> GetSelectedCellControllers()
    {
        if (!_hasMultiSelection) yield break;
        var (minRow, minCol, maxRow, maxCol) = GetSelectionBoundingRect();
        for (int r = minRow; r <= maxRow; r++)
            for (int c = minCol; c <= maxCol; c++)
            {
                var cell = _cells[r][c];
                if (cell != null)
                    yield return cell.Controller;
            }
    }

    /// <summary>Returns true if <paramref name="tableLocalPoint"/> falls within the
    /// painted bounds of the current multi-cell selection.</summary>
    public bool IsPointInSelection(SKPoint tableLocalPoint)
    {
        if (!_hasMultiSelection) return false;
        var (minRow, minCol, maxRow, maxCol) = GetSelectionBoundingRect();
        return GetRectBounds(minRow, minCol, maxRow, maxCol).Contains(tableLocalPoint);
    }

    /// <summary>Returns the inclusive (minRow, maxRow) spanned by the current multi-cell selection.</summary>
    internal (int minRow, int maxRow) GetSelectedRowRange()
    {
        var (minRow, _, maxRow, _) = GetSelectionBoundingRect();
        return (minRow, maxRow);
    }

    /// <summary>Returns the inclusive (minCol, maxCol) spanned by the current multi-cell selection.</summary>
    internal (int minCol, int maxCol) GetSelectedColRange()
    {
        var (_, minCol, _, maxCol) = GetSelectionBoundingRect();
        return (minCol, maxCol);
    }

    public void SetMultiSelection(int r1, int c1, int r2, int c2)
    {
        _selAnchor = (r1, c1);
        _selEnd = (r2, c2);
        _hasMultiSelection = true;
    }

    public void ClearMultiSelection()
    {
        _hasMultiSelection = false;
    }

    /// <summary>
    /// Returns the bounding grid rectangle (inclusive) that spans both the anchor and end
    /// primary cells, accounting for their ColSpan/RowSpan.
    /// </summary>
    private (int minRow, int minCol, int maxRow, int maxCol) GetSelectionBoundingRect()
    {
        var aCell = _cells[_selAnchor.row][_selAnchor.col];
        var eCell = _cells[_selEnd.row][_selEnd.col];
        int minRow = Math.Min(_selAnchor.row, _selEnd.row);
        int minCol = Math.Min(_selAnchor.col, _selEnd.col);
        int maxRow = Math.Max(
            _selAnchor.row + (aCell?.RowSpan ?? 1) - 1,
            _selEnd.row   + (eCell?.RowSpan ?? 1) - 1);
        int maxCol = Math.Max(
            _selAnchor.col + (aCell?.ColSpan ?? 1) - 1,
            _selEnd.col   + (eCell?.ColSpan ?? 1) - 1);
        return (minRow, minCol, maxRow, maxCol);
    }

    /// <summary>
    /// Returns the table-local pixel rect for the given grid coordinate range (inclusive).
    /// </summary>
    private SKRect GetRectBounds(int minRow, int minCol, int maxRow, int maxCol)
    {
        float bw = EffBw;
        float left = bw;
        for (int c = 0; c < minCol; c++) left += _colWidths[c] + bw;
        float top = bw;
        for (int r = 0; r < minRow; r++) top += _rowHeights[r] + bw;
        float width = -bw;
        for (int c = minCol; c <= maxCol; c++) width += _colWidths[c] + bw;
        float height = -bw;
        for (int r = minRow; r <= maxRow; r++) height += _rowHeights[r] + bw;
        return new SKRect(left, top, left + width, top + height);
    }

    /// <summary>
    /// Returns true if the current multi-cell selection can be merged into a single cell.
    /// The selection must span at least two cells and form a self-contained rectangle
    /// (no spanning cell crosses the selection boundary).
    /// </summary>
    public bool CanMergeSelection()
    {
        if (!_hasMultiSelection || _selAnchor == _selEnd) return false;
        var (minRow, minCol, maxRow, maxCol) = GetSelectionBoundingRect();

        // Each primary cell within the rect must be fully contained in the rect
        for (int r = minRow; r <= maxRow; r++)
            for (int c = minCol; c <= maxCol; c++)
            {
                var cell = _cells[r][c];
                if (cell == null) continue;
                if (r + cell.RowSpan - 1 > maxRow || c + cell.ColSpan - 1 > maxCol)
                    return false;
            }

        // Each covered (null) position must be owned by a primary inside the rect
        for (int r = minRow; r <= maxRow; r++)
            for (int c = minCol; c <= maxCol; c++)
            {
                if (_cells[r][c] != null) continue;
                var primary = FindPrimaryCell(r, c);
                if (!primary.HasValue) return false;
                if (primary.Value.r < minRow || primary.Value.r > maxRow ||
                    primary.Value.c < minCol || primary.Value.c > maxCol)
                    return false;
            }

        return true;
    }

    /// <summary>
    /// Merges all cells in the current multi-cell selection into the top-left primary cell.
    /// Content from all merged cells is appended to the surviving cell.
    /// Returns the (row, col) of the surviving merged cell.
    /// </summary>
    public (int row, int col) MergeSelection()
    {
        if (!CanMergeSelection()) return (_selAnchor.row, _selAnchor.col);
        var (minRow, minCol, maxRow, maxCol) = GetSelectionBoundingRect();

        var primaryCell = _cells[minRow][minCol]!;

        // Append all other primary cells' content to the top-left cell
        for (int r = minRow; r <= maxRow; r++)
            for (int c = minCol; c <= maxCol; c++)
            {
                if (r == minRow && c == minCol) continue;
                _cells[r][c]?.Controller.AppendTextTo(primaryCell.Controller);
            }

        // Expand the primary cell's span and update its layout width to match
        primaryCell.ColSpan = maxCol - minCol + 1;
        primaryCell.RowSpan = maxRow - minRow + 1;
        var mergedBounds = GetCellBounds(minRow, minCol);
        primaryCell.Controller.SetWidth(Math.Max(mergedBounds.Width - 2 * _cellPadding, 10f));

        for (int r = minRow; r <= maxRow; r++)
            for (int c = minCol; c <= maxCol; c++)
                if (r != minRow || c != minCol)
                    _cells[r][c] = null;

        ClearMultiSelection();
        RecalcRowHeights();
        OnHeightChanged?.Invoke();
        return (minRow, minCol);
    }

    // -----------------------------------------------------------------------------------------
    // Import helpers
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Programmatically merges the rectangle of cells from (minRow,minCol) to (maxRow,maxCol)
    /// into the primary cell at (minRow,minCol), discarding content in covered cells.
    /// Use during document import to apply colspan/rowspan from HTML/DOCX tables.
    /// </summary>
    public void MergeRange(int minRow, int minCol, int maxRow, int maxCol)
    {
        if (minRow < 0 || minCol < 0 || maxRow >= _options.Rows || maxCol >= _options.Columns) return;
        if (minRow == maxRow && minCol == maxCol) return;

        var primaryCell = _cells[minRow][minCol];
        if (primaryCell == null) return;

        primaryCell.ColSpan = maxCol - minCol + 1;
        primaryCell.RowSpan = maxRow - minRow + 1;

        var mergedBounds = GetCellBounds(minRow, minCol);
        primaryCell.Controller.SetWidth(Math.Max(mergedBounds.Width - 2 * _cellPadding, 10f));

        for (int r = minRow; r <= maxRow; r++)
            for (int c = minCol; c <= maxCol; c++)
                if (r != minRow || c != minCol)
                    _cells[r][c] = null;

        RecalcRowHeights();
    }

    /// <summary>
    /// Invokes <paramref name="populate"/> with an <see cref="ICellWriter"/> for the primary
    /// cell at (row, col), allowing callers outside this assembly to write cell content.
    /// Does nothing if (row, col) is out of range or covered by a spanning cell.
    /// </summary>
    public void WriteToCell(int row, int col, Action<ICellWriter> populate)
    {
        if (row < 0 || row >= _options.Rows || col < 0 || col >= _options.Columns) return;
        var cell = _cells[row][col];
        if (cell == null) return;
        populate(cell.Controller);
    }

    // -----------------------------------------------------------------------------------------
    // Structural mutations (row/column add/delete)
    // -----------------------------------------------------------------------------------------

    public void AddRow()
    {
        var newRow = new TableCell?[_options.Columns];
        for (int c = 0; c < _options.Columns; c++)
        {
            float cellContentWidth = _colWidths[c] - 2 * _cellPadding;
            newRow[c] = CreateCell(Math.Max(cellContentWidth, 10f), false);
        }

        var newCells = new TableCell?[_options.Rows + 1][];
        for (int r = 0; r < _options.Rows; r++)
            newCells[r] = _cells[r];
        newCells[_options.Rows] = newRow;
        _cells = newCells;

        Array.Resize(ref _rowHeights, _options.Rows + 1);
        _rowHeights[_options.Rows] = 20f;

        _options.Rows++;
        RecalcRowHeights();
        OnHeightChanged?.Invoke();
    }

    /// <summary>
    /// Inserts a new (non-header) row at <paramref name="insertAt"/>, shifting all existing
    /// rows at or after that index down by one.
    /// </summary>
    public void InsertRow(int insertAt)
    {
        insertAt = Math.Clamp(insertAt, 0, _options.Rows);

        var newRow = new TableCell?[_options.Columns];
        for (int c = 0; c < _options.Columns; c++)
        {
            float cellContentWidth = Math.Max(_colWidths[c] - 2 * _cellPadding, 10f);
            newRow[c] = CreateCell(cellContentWidth, false);
        }

        var newCells = new TableCell?[_options.Rows + 1][];
        var newRowHeights = new float[_options.Rows + 1];
        for (int r = 0; r < insertAt; r++) { newCells[r] = _cells[r]; newRowHeights[r] = _rowHeights[r]; }
        newCells[insertAt] = newRow;
        newRowHeights[insertAt] = 20f;
        for (int r = insertAt; r < _options.Rows; r++) { newCells[r + 1] = _cells[r]; newRowHeights[r + 1] = _rowHeights[r]; }
        _cells = newCells;
        _rowHeights = newRowHeights;

        _options.Rows++;
        RecalcRowHeights();
        OnHeightChanged?.Invoke();
    }

    /// <summary>
    /// Inserts a new column at <paramref name="insertAt"/>, shifting all existing columns at
    /// or after that index right by one, then redistributes the total width evenly.
    /// </summary>
    public void InsertColumn(int insertAt)
    {
        insertAt = Math.Clamp(insertAt, 0, _options.Columns);

        for (int r = 0; r < _options.Rows; r++)
        {
            bool isHeader = _options.IncludeHeader && r == 0;
            var newRow = new TableCell?[_options.Columns + 1];
            for (int c = 0; c < insertAt; c++) newRow[c] = _cells[r][c];
            newRow[insertAt] = CreateCell(10f, isHeader);
            for (int c = insertAt; c < _options.Columns; c++) newRow[c + 1] = _cells[r][c];
            _cells[r] = newRow;
        }

        var newColWidths = new float[_options.Columns + 1];
        for (int c = 0; c < insertAt; c++) newColWidths[c] = _colWidths[c];
        newColWidths[insertAt] = 0f;
        for (int c = insertAt; c < _options.Columns; c++) newColWidths[c + 1] = _colWidths[c];
        _colWidths = newColWidths;

        _options.Columns++;

        // Reset all columns to auto and redistribute evenly
        _manualColWidths = new float[_options.Columns];
        for (int c = 0; c < _options.Columns; c++)
            _manualColWidths[c] = float.NaN;
        RecalcEffectiveColWidths();
        UpdateAllCellWidths();

        RecalcRowHeights();
        OnHeightChanged?.Invoke();
    }

    /// <summary>
    /// Removes the specified row.  Callers must ensure <see cref="Rows"/> &gt; 1 before calling.
    /// </summary>
    public void DeleteRow(int row)
    {
        if (row < 0 || row >= _options.Rows) return;

        var newCells = new TableCell?[_options.Rows - 1][];
        var newRowHeights = new float[_options.Rows - 1];
        int di = 0;
        for (int r = 0; r < _options.Rows; r++)
        {
            if (r == row) continue;
            newCells[di] = _cells[r];
            newRowHeights[di] = _rowHeights[r];
            di++;
        }
        _cells = newCells;
        _rowHeights = newRowHeights;

        _options.Rows--;
        RecalcRowHeights();
        OnHeightChanged?.Invoke();
    }

    /// <summary>
    /// Removes the specified column and redistributes the freed width evenly among the
    /// remaining columns.  Callers must ensure <see cref="Cols"/> &gt; 1 before calling.
    /// </summary>
    public void DeleteColumn(int col)
    {
        if (col < 0 || col >= _options.Columns) return;

        for (int r = 0; r < _options.Rows; r++)
        {
            var newRow = new TableCell?[_options.Columns - 1];
            int di = 0;
            for (int c = 0; c < _options.Columns; c++)
            {
                if (c != col) newRow[di++] = _cells[r][c];
            }
            _cells[r] = newRow;
        }

        var newColWidths = new float[_options.Columns - 1];
        int dci = 0;
        for (int c = 0; c < _options.Columns; c++)
        {
            if (c != col) newColWidths[dci++] = _colWidths[c];
        }
        _colWidths = newColWidths;

        _options.Columns--;

        // Reset all columns to auto and redistribute evenly
        _manualColWidths = new float[_options.Columns];
        for (int c = 0; c < _options.Columns; c++)
            _manualColWidths[c] = float.NaN;
        RecalcEffectiveColWidths();
        UpdateAllCellWidths();

        RecalcRowHeights();
        OnHeightChanged?.Invoke();
    }

    // -----------------------------------------------------------------------------------------
    // Painting
    // -----------------------------------------------------------------------------------------

    public void Paint(SKCanvas canvas, SKPoint origin)
    {
        RecalcRowHeights();

        float tableHeight = Height;
        float tableWidth = _totalWidth;

        // White background
        _whiteFillPaint ??= new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(origin.X, origin.Y, tableWidth, tableHeight, _whiteFillPaint);

        // Paint each primary cell (skip covered cells)
        for (int r = 0; r < _options.Rows; r++)
        {
            for (int c = 0; c < _options.Columns; c++)
            {
                var cell = _cells[r][c];
                if (cell == null) continue;   // covered by a spanning cell

                var cellBounds = GetCellBounds(r, c);
                var cellRect = new SKRect(
                    origin.X + cellBounds.Left,
                    origin.Y + cellBounds.Top,
                    origin.X + cellBounds.Right,
                    origin.Y + cellBounds.Bottom);

                // Header row background
                if (cell.IsHeader)
                {
                    _headerFillPaint ??= new SKPaint { Color = _options.HeaderBackgroundColor, Style = SKPaintStyle.Fill };
                    canvas.DrawRect(cellRect, _headerFillPaint);
                }

                // Clip and paint cell content
                canvas.Save();
                canvas.ClipRect(cellRect);
                canvas.Translate(cellRect.Left + _cellPadding, cellRect.Top + _cellPadding);
                cell.Controller.Paint(canvas, cellRect.Height - 2 * _cellPadding, null);
                canvas.Restore();
            }
        }

        DrawBorders(canvas, origin);

        if (_hasMultiSelection)
            DrawMultiCellSelection(canvas, origin);
    }

    private void DrawMultiCellSelection(SKCanvas canvas, SKPoint origin)
    {
        var (minRow, minCol, maxRow, maxCol) = GetSelectionBoundingRect();
        var rect = GetRectBounds(minRow, minCol, maxRow, maxCol);

        using var fillPaint = new SKPaint
        {
            Color = new SKColor(0x29, 0x78, 0xFF, 0x33),
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawRect(origin.X + rect.Left, origin.Y + rect.Top, rect.Width, rect.Height, fillPaint);

        using var borderPaint = new SKPaint
        {
            Color = new SKColor(0x29, 0x78, 0xFF),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
        };
        canvas.DrawRect(origin.X + rect.Left, origin.Y + rect.Top, rect.Width, rect.Height, borderPaint);
    }

    private void DrawBorders(SKCanvas canvas, SKPoint origin)
    {
        if (_options.BorderStyle == TableBorderStyle.None) return;
        float bw = EffBw;
        if (bw <= 0f) return;

        using var paint = CreateBorderPaint();

        // Draw a border rect for each primary cell, expanded outward by bw/2 so that
        // shared edges between adjacent cells land on the same pixel coordinates.
        for (int r = 0; r < _options.Rows; r++)
        {
            for (int c = 0; c < _options.Columns; c++)
            {
                if (_cells[r][c] == null) continue;
                var b = GetCellBounds(r, c);
                canvas.DrawRect(
                    origin.X + b.Left - bw / 2f,
                    origin.Y + b.Top - bw / 2f,
                    b.Width + bw,
                    b.Height + bw,
                    paint);
            }
        }
    }

    /// <summary>
    /// Returns true when the table has no visible borders, meaning ghost borders should be
    /// shown on hover to give the user a visual grid reference.
    /// </summary>
    public bool NeedsGhostBorders => EffBw < 1f;

    /// <summary>
    /// Draws a light gray dotted border around the table and each cell.
    /// Called when hovering over a borderless table so the user can see the cell layout.
    /// </summary>
    public void PaintGhostBorders(SKCanvas canvas, SKPoint origin)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(0xB0, 0xD0, 0xFF),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new[] { 0.01f, 5f }, 0f),
        };

        RecalcRowHeights();

        // Precompute column X and row Y boundaries
        float[] colX = new float[_options.Columns + 1];
        for (int c = 0; c < _options.Columns; c++)
            colX[c + 1] = colX[c] + _colWidths[c];

        float[] rowY = new float[_options.Rows + 1];
        for (int r = 0; r < _options.Rows; r++)
            rowY[r + 1] = rowY[r] + _rowHeights[r];

        // Horizontal lines: one segment per column slice at each row boundary.
        // Skip interior boundaries where a spanning cell crosses them.
        for (int r = 0; r <= _options.Rows; r++)
        {
            float y = origin.Y + rowY[r];
            for (int c = 0; c < _options.Columns; c++)
            {
                bool isOuter = r == 0 || r == _options.Rows;
                bool draw = isOuter || FindPrimaryCell(r - 1, c) != FindPrimaryCell(r, c);
                if (draw)
                    canvas.DrawLine(origin.X + colX[c], y, origin.X + colX[c + 1], y, paint);
            }
        }

        // Vertical lines: one segment per row slice at each column boundary.
        // Skip interior boundaries where a spanning cell crosses them.
        for (int c = 0; c <= _options.Columns; c++)
        {
            float x = origin.X + colX[c];
            for (int r = 0; r < _options.Rows; r++)
            {
                bool isOuter = c == 0 || c == _options.Columns;
                bool draw = isOuter || FindPrimaryCell(r, c - 1) != FindPrimaryCell(r, c);
                if (draw)
                    canvas.DrawLine(x, origin.Y + rowY[r], x, origin.Y + rowY[r + 1], paint);
            }
        }
    }

    /// <summary>
    /// Draws a horizontal border segment at <paramref name="splitLocalY"/> (table-local Y)
    /// for every row-spanning cell whose full painted area straddles that coordinate.
    /// Call this while the canvas transform for the current page is active so that
    /// spanning cells receive a visible border at each page-break clip edge.
    /// </summary>
    public void PaintSplitBorders(SKCanvas canvas, SKPoint origin, float splitLocalY)
    {
        if (_options.BorderStyle == TableBorderStyle.None) return;
        float bw = EffBw;
        if (bw <= 0f) return;

        using var paint = CreateBorderPaint();
        float lineY = origin.Y + splitLocalY + bw / 2f;

        for (int r = 0; r < _options.Rows; r++)
        {
            for (int c = 0; c < _options.Columns; c++)
            {
                var cell = _cells[r][c];
                if (cell == null || cell.RowSpan <= 1) continue;

                var b = GetCellBounds(r, c);
                // The cell's full painted border spans from b.Top - bw/2 to b.Bottom + bw/2.
                // If splitLocalY falls strictly inside that range, we need a border there.
                if (splitLocalY <= b.Top - bw / 2f || splitLocalY >= b.Bottom + bw / 2f) continue;

                canvas.DrawLine(origin.X + b.Left - bw / 2f, lineY,
                                origin.X + b.Right + bw / 2f, lineY, paint);
            }
        }
    }

    private SKPaint CreateBorderPaint()
    {
        float bw = EffBw;
        var paint = new SKPaint
        {
            Color = _options.BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = bw,
            IsAntialias = false,
        };

        switch (_options.BorderStyle)
        {
            case TableBorderStyle.Dashed:
                paint.PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0f);
                break;
            case TableBorderStyle.Dotted:
                paint.PathEffect = SKPathEffect.CreateDash(new[] { bw + 1f, 4f }, 0f);
                break;
        }

        return paint;
    }
}
