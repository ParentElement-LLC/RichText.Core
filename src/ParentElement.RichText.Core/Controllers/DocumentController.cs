using ParentElement.RichText.Core.Abstractions.Controllers;
using ParentElement.RichText.Core.Abstractions.IO;
using ParentElement.RichText.Core.Content;
using ParentElement.RichText.Core.Data;
using ParentElement.RichText.Core.Geometry;
using ParentElement.RichText.Core.Images;
using ParentElement.RichText.Core.Input;
using ParentElement.RichText.Core.Tables;
using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;
using SkiaSharp;
using System.Numerics;

namespace ParentElement.RichText.Core.Controllers;

/// <summary>
/// Core rich-text editor controller. Manages the document, selection, caret,
/// tables, images, clipboard, undo/redo, and rendering.
/// Implement <see cref="IDocumentController"/> to consume this type via abstraction.
/// </summary>
public class DocumentController : IDocumentController
{
    /// <inheritdoc/>
    public ShortcutHandler Shortcuts => _shortcuts;

    /// <inheritdoc/>
    public SKColor SelectionColor { get; set; } = SKColors.LightGray;

    /// <inheritdoc/>
    public SKColor BackgroundColor
    {
        get => _backgroundPaint.Color;
        set => _backgroundPaint = new SKPaint { Color = value };
    }

    /// <inheritdoc/>
    public Rectangle VisibleBounds
    {
        get { return _visibleBounds; }
        set
        {
            //TODO:  Allow the document width the remain while just changing the bounds
            _visibleBounds = value;

            if (OnContentSizeChanged != null)
            {
                var info = new DocumentInfo(_document.MeasuredWidth, GetReportedDocumentHeight(), _viewModifier.Offset);
                OnContentSizeChanged(info);
            }

            Invalidate();
        }
    }

    /// <inheritdoc/>
    public bool HasFocus { get; set; }

    /// <inheritdoc/>
    public bool ReadOnly { get; set; }

    /// <inheritdoc/>
    public float DocumentHeight
    {
        get
        {
            if (_document == null)
                return 0f;

            if (Settings.ShowPageBreaks)
                return Math.Max(0f, TotalScrollHeight() - VisibleBounds.Height);

            return _document.MeasuredHeight - VisibleBounds.Height;
        }
    }

    /// <inheritdoc/>
    public float PageWidth
    {
        get => _document.PageWidth;
        set => _document.PageWidth = value;
    }

    /// <inheritdoc/>
    public float ScrollScale
    {
        get => _scrollScale;
        set => _scrollScale = Math.Abs(value);
    }

    /// <inheritdoc/>
    public DocumentSettings Settings { get; private set; }

    /// <summary>
    /// Initializes a new <see cref="DocumentController"/> with the given document settings and clipboard handler.
    /// </summary>
    public DocumentController(DocumentSettings config, IClipboardHandler clipboard)
    {
        Settings = config;

        _styleManager = StyleManager.Default.Value!;

        //TODO:  Consider a rework of the StyleManager - we can cache previous styles statically but if we have more than one rich text control we'll get conflicts with Default and Current styles.
        //Sets the default style
        _styleManager.DefaultStyle = config.TextStyle;
        _styleManager.CurrentStyle = config.TextStyle;

        //Create a merged style from the current and provided styles
        _textStyle = _styleManager.FromStyle(config.TextStyle);

        _document = new TextDocument();

        _caretView = new CaretController();
        _caretView.RequestRedraw = Invalidate;

        PageWidth = config.PageWidth;

        _document.RegisterView(this);

        _document.SetMargins(config.DocumentMargins.Left, config.DocumentMargins.Top, config.DocumentMargins.Right, config.DocumentMargins.Bottom);

        _backgroundPaint = new SKPaint { Color = config.DocumentBackgroundColor };
        _clipboard = clipboard;

        _shortcuts = new ShortcutHandler();
        CreateDefaultShortcuts();

        MoveCaret(_selection.CaretPosition);
    }

    #region Text Styling

    /// <inheritdoc/>
    public Task ApplyStyle(IStyle style)
    {
        if (ApplyToSelectedCells(cc => cc.ApplyStyle(style))) return Task.CompletedTask;
        if (ActiveCellController is { } ccStyle) { ccStyle.ApplyStyle(style); AfterCellEdit(); return Task.CompletedTask; }
        lock (_drawLock)
        {
            _textStyle = _styleManager.FromStyle(style);

            _caretView.Italic = style.FontItalic ?? false;

            if (_selection.IsRange)
            {
                var previousSelection = _selection;
                _document.MergeStyles(this, _selection, style);

                _selection = previousSelection;
                MoveCaret(_selection.CaretPosition);

                if (OnContentSizeChanged != null)
                {
                    var info = new DocumentInfo(_document.MeasuredWidth, GetReportedDocumentHeight(), _viewModifier.Offset);
                    OnContentSizeChanged(info);
                }
            }
        }

        PublishSelectionInfo();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void ReplaceStyle(IStyle style)
    {
        _textStyle = _styleManager.FromStyle(style);

        var previousSelection = _selection;
        _document.MergeStyles(this, _selection, _textStyle);

        var textBuffer = _document.GetText(_selection);

        _document.ReplaceText(this, _selection, textBuffer.ToString(), EditSemantics.Typing, _textStyle);

        _selection = previousSelection;
        MoveCaret(_selection.CaretPosition);

        //TODO:  Move to function so it's not duplicated
        if (OnContentSizeChanged != null)
        {
            var info = new DocumentInfo(_document.MeasuredWidth, GetReportedDocumentHeight(), _viewModifier.Offset);
            OnContentSizeChanged(info);
        }

        if (_selection.IsRange)
        {
            ApplyStyle(style);
        }
        else
        {
            PublishSelectionInfo();
        }
    }

    /// <inheritdoc/>
    public Task ApplyBold()
    {
        if (ApplyToSelectedCells(cc => cc.ApplyBold())) return Task.CompletedTask;
        if (ActiveCellController is { } ccBold) { ccBold.ApplyBold(); AfterCellEdit(); return Task.CompletedTask; }
        var style = new Style() { FontWeight = _textStyle.FontWeight == 400 ? 700 : 400 };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplyUnderline()
    {
        if (ApplyToSelectedCells(cc => cc.ApplyUnderline())) return Task.CompletedTask;
        if (ActiveCellController is { } ccUnder) { ccUnder.ApplyUnderline(); AfterCellEdit(); return Task.CompletedTask; }
        var style = new Style() { Underline = _textStyle.Underline == UnderlineStyle.Solid ? UnderlineStyle.None : UnderlineStyle.Solid };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplyItalic()
    {
        if (ApplyToSelectedCells(cc => cc.ApplyItalic())) return Task.CompletedTask;
        if (ActiveCellController is { } ccItalic) { ccItalic.ApplyItalic(); AfterCellEdit(); return Task.CompletedTask; }
        var style = new Style() { FontItalic = !_textStyle.FontItalic };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplyStrikethrough()
    {
        if (ApplyToSelectedCells(cc => cc.ApplyStrikethrough())) return Task.CompletedTask;
        if (ActiveCellController is { } ccStrike) { ccStrike.ApplyStrikethrough(); AfterCellEdit(); return Task.CompletedTask; }
        var strikeStyle = _textStyle.StrikeThrough == StrikeThroughStyle.None
            ? StrikeThroughStyle.Solid : StrikeThroughStyle.None;
        var style = new Style() { StrikeThrough = strikeStyle };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplyFontColor(SKColor color)
    {
        if (ApplyToSelectedCells(cc => cc.ApplyFontColor(color))) return Task.CompletedTask;
        if (ActiveCellController is { } ccFc) { ccFc.ApplyFontColor(color); AfterCellEdit(); return Task.CompletedTask; }
        if (!_selection.IsRange && color == _textStyle?.TextColor)
            return Task.CompletedTask;
        var style = new Style { TextColor = color };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplyBackgroundColor(SKColor color)
    {
        if (ApplyToSelectedCells(cc => cc.ApplyBackgroundColor(color))) return Task.CompletedTask;
        if (ActiveCellController is { } ccBg) { ccBg.ApplyBackgroundColor(color); AfterCellEdit(); return Task.CompletedTask; }
        if (!_selection.IsRange && color == _textStyle?.BackgroundColor)
            return Task.CompletedTask;
        var style = new Style { BackgroundColor = color };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplyFontFamily(string fontFamily)
    {
        if (ApplyToSelectedCells(cc => cc.ApplyFontFamily(fontFamily))) return Task.CompletedTask;
        if (ActiveCellController is { } ccFf) { ccFf.ApplyFontFamily(fontFamily); AfterCellEdit(); return Task.CompletedTask; }
        if (!_selection.IsRange && fontFamily != null && fontFamily == _textStyle?.FontFamily)
            return Task.CompletedTask;
        var style = new Style { FontFamily = fontFamily };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplyFontSize(int fontSize)
    {
        if (ApplyToSelectedCells(cc => cc.ApplyFontSize(fontSize))) return Task.CompletedTask;
        if (ActiveCellController is { } ccFs) { ccFs.ApplyFontSize(fontSize); AfterCellEdit(); return Task.CompletedTask; }
        var style = new Style { FontSize = fontSize };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplySubscript()
    {
        if (ApplyToSelectedCells(cc => cc.ApplySubscript())) return Task.CompletedTask;
        if (ActiveCellController is { } ccSub) { ccSub.ApplySubscript(); AfterCellEdit(); return Task.CompletedTask; }
        var style = new Style
        {
            FontVariant = _textStyle.FontVariant == FontVariant.SubScript
                ? FontVariant.Normal
                : FontVariant.SubScript
        };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplySuperscript()
    {
        if (ApplyToSelectedCells(cc => cc.ApplySuperscript())) return Task.CompletedTask;
        if (ActiveCellController is { } ccSup) { ccSup.ApplySuperscript(); AfterCellEdit(); return Task.CompletedTask; }
        var style = new Style
        {
            FontVariant = _textStyle.FontVariant == FontVariant.SuperScript
                ? FontVariant.Normal
                : FontVariant.SuperScript
        };
        return ApplyStyle(style);
    }

    /// <inheritdoc/>
    public Task ApplyAlignment(TextAlignment alignment)
    {
        if (ApplyToSelectedCells(cc => cc.ApplyAlignment(alignment))) return Task.CompletedTask;
        if (ActiveCellController is { } ccAlign) { ccAlign.ApplyAlignment(alignment); AfterCellEdit(); return Task.CompletedTask; }
        _document.ToggleParagraphAlignment(_selection, alignment);
        MoveCaret(_selection.CaretPosition);
        PublishSelectionInfo();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void SetBlockIndent(float value)
    {
        _document.SetBlockIndent(_selection, value);
        MoveCaret(_selection.CaretPosition);
    }

    /// <summary>
    /// Directly sets list type and level on the current paragraph without toggle logic.
    /// Use during import to avoid inherited-style interference from CopyStyleFrom.
    /// </summary>
    public void ApplyListFormat(ListType type, int level)
    {
        _document.SetListStyleAndLevel(_selection, type, level);
        MoveCaret(_selection.CaretPosition);
        PublishSelectionInfo();
    }

    /// <inheritdoc/>
    public void AdjustParagraphIndent(float amount)
    {
        _document.AdjustParagraphIndent(_selection, amount);
        MoveCaret(_selection.CaretPosition);
    }

    /// <inheritdoc/>
    public void ToggleBulletList()
    {
        if (ApplyToSelectedCells(cc => cc.ToggleBulletList())) return;
        if (ActiveCellController is { } ccBullet) { ccBullet.ToggleBulletList(); AfterCellEdit(); return; }
        var currentType = GetListTypeAtSelection();
        var newType = currentType == ListType.Bullet ? ListType.None : ListType.Bullet;
        _document.SetListStyle(_selection, newType);
        MoveCaret(_selection.CaretPosition);
        PublishSelectionInfo();
    }

    /// <inheritdoc/>
    public void ToggleNumberedList()
    {
        if (ApplyToSelectedCells(cc => cc.ToggleNumberedList())) return;
        if (ActiveCellController is { } ccNum) { ccNum.ToggleNumberedList(); AfterCellEdit(); return; }
        var currentType = GetListTypeAtSelection();
        var newType = currentType == ListType.Numbered ? ListType.None : ListType.Numbered;
        _document.SetListStyle(_selection, newType);
        MoveCaret(_selection.CaretPosition);
        PublishSelectionInfo();
    }

    private ListType GetListTypeAtSelection()
        => _document.GetSelectionInfo(_selection).ParagraphListType ?? ListType.None;

    /// <inheritdoc/>
    public void ChangeListLevel(int delta)
    {
        if (ApplyToSelectedCells(cc => cc.ChangeListLevel(delta))) return;
        if (ActiveCellController is { } ccLevel) { ccLevel.ChangeListLevel(delta); AfterCellEdit(); return; }
        _document.SetListLevel(_selection, delta);
        MoveCaret(_selection.CaretPosition);
        PublishSelectionInfo();
    }

    /// <inheritdoc/>
    public void AdjustLineSpacing(float amount)
    {
        if (ApplyToSelectedCells(cc => cc.AdjustLineSpacing(amount))) return;
        if (ActiveCellController is { } ccSpacing) { ccSpacing.AdjustLineSpacing(amount); AfterCellEdit(); return; }
        _document.SetLineSpacing(_selection, amount);
        MoveCaret(_selection.CaretPosition);
        PublishSelectionInfo();
    }

    /// <inheritdoc/>
    public void ToggleFirstLineIndent()
    {
        _document.FirstLineIndent = _document.FirstLineIndent == 20 ? 0 : 20;
    }

    #endregion

    #region Document Navigation

    /// <inheritdoc/>
    public void ScrollBy(float x, float y)
    {
        x = _viewModifier.Offset.X + (x * _scrollScale);
        y = _viewModifier.Offset.Y + (y * _scrollScale);

        float maxScrollY = Math.Max(0f, (Settings.ShowPageBreaks ? TotalScrollHeight() : _document.MeasuredHeight) - VisibleBounds.Height);
        var offsetX = Math.Clamp(x, 0, Math.Max(0f, _document.PageWidth - VisibleBounds.Width));
        var offsetY = Math.Clamp(y, 0, maxScrollY);

        _viewModifier.Offset = new Vector2(offsetX, offsetY);

        //TODO:  Move to function so it's not duplicated
        if (OnContentSizeChanged != null)
        {
            var info = new DocumentInfo(_document.MeasuredWidth, GetReportedDocumentHeight(), _viewModifier.Offset);
            OnContentSizeChanged(info);
        }

        Invalidate();
    }

    /// <inheritdoc/>
    public void ScrollTo(float x, float y)
    {
        float maxScrollY = Math.Max(0f, (Settings.ShowPageBreaks ? TotalScrollHeight() : _document.MeasuredHeight) - VisibleBounds.Height);
        var offsetX = Math.Clamp(x, 0, Math.Max(0f, _document.PageWidth - VisibleBounds.Width));
        var offsetY = Math.Clamp(y, 0, maxScrollY);

        _viewModifier.Offset = new Vector2(offsetX, offsetY);
        Invalidate();
    }

    /// <inheritdoc/>
    public void Click(Point point)
    {
        var offset = point.FromView(_viewModifier);
        if (Settings.ShowPageBreaks)
            offset = AdjustPointForPageBreaks(offset);
        var docPoint = new SKPoint(offset.X, offset.Y);

        // Cancel any ongoing drag state from a previous interaction
        _potentialImageDrag = false;
        _isDraggingImage = false;
        _imageDragDropIndex = -1;
        _activeHandle = ResizeHandleType.None;
        _resizingCellImage = false;
        _draggingColDivider = -1;
        _draggingColTable = null;

        // --- Cell image resize handle check (must run before table hit-test re-activates cell) ---
        if (_activeTable != null && ActiveCellController is { } cellAcc)
        {
            var cellOrigin = GetActiveCellOrigin();
            var (cellImage, cellLocalBounds) = cellAcc.GetSelectedImageInfo();
            if (cellImage != null)
            {
                var docBounds = new SKRect(
                    cellOrigin.X + cellLocalBounds.Left,
                    cellOrigin.Y + cellLocalBounds.Top,
                    cellOrigin.X + cellLocalBounds.Right,
                    cellOrigin.Y + cellLocalBounds.Bottom);
                var handle = HitTestHandles(docPoint, docBounds);
                if (handle != ResizeHandleType.None)
                {
                    _activeHandle = handle;
                    _resizeDragStartDoc = docPoint;
                    _resizeOriginalWidth = cellImage.Width;
                    _resizeOriginalHeight = cellImage.Height;
                    _resizingCellImage = true;
                    return;
                }
                // Click on the image body — keep the image selected, don't re-activate cell
                if (docBounds.Contains(docPoint))
                    return;
            }
        }

        // --- Resize handle check on currently selected image ---
        var (selectedImage, imageBounds) = GetSelectedImageInfo();
        if (selectedImage != null)
        {
            var handle = HitTestHandles(docPoint, imageBounds);
            if (handle != ResizeHandleType.None)
            {
                _activeHandle = handle;
                _resizeDragStartDoc = docPoint;
                _resizeOriginalWidth = selectedImage.Width;
                _resizeOriginalHeight = selectedImage.Height;
                return;
            }

            // Click on the image body (not a handle) → start a potential drag
            if (imageBounds.Contains(docPoint))
            {
                _potentialImageDrag = true;
                _imageDragStartDoc = docPoint;
                _imageDragCurrentDoc = docPoint;
                return; // keep existing image selection
            }
        }

        // --- Table hit-test ---
        var (hitTable, hitTableBounds, hitTableDocIdx) = FindTableAt(docPoint);
        if (hitTable != null)
        {
            var tableLocal = new SKPoint(docPoint.X - hitTableBounds.Left, docPoint.Y - hitTableBounds.Top);

            // Column divider check takes priority over cell activation
            float divTolerance = Math.Max(hitTable.BorderWidth / 2f + 3f, 4f);
            int divider = hitTable.HitTestColumnDivider(tableLocal, divTolerance);
            if (divider >= 0)
            {
                _draggingColTable = hitTable;
                _draggingColDivider = divider;
                _draggingColStartX = docPoint.X;
                _draggingColOrigWidth      = hitTable.ColWidths[divider];
                _draggingColRightOrigWidth = hitTable.ColWidths[divider + 1];
                _draggingColMinWidth      = hitTable.GetColumnMinWidth(divider);
                _draggingColRightMinWidth = hitTable.GetColumnMinWidth(divider + 1);
                return;
            }

            var cellHit = hitTable.HitTestCell(tableLocal);
            if (cellHit.HasValue)
            {
                ActivateCell(hitTable, hitTableDocIdx, cellHit.Value.row, cellHit.Value.col);
                _selAnchorRow = cellHit.Value.row;
                _selAnchorCol = cellHit.Value.col;
                _isMultiCellSelecting = false;
                hitTable.ClearMultiSelection();
                var cellBounds = hitTable.GetCellBounds(cellHit.Value.row, cellHit.Value.col);
                var cellOrigin = new SKPoint(
                    hitTableBounds.Left + cellBounds.Left + InlineTable._cellPadding,
                    hitTableBounds.Top + cellBounds.Top + InlineTable._cellPadding);
                ActiveCellController!.Click(new SKPoint(docPoint.X - cellOrigin.X, docPoint.Y - cellOrigin.Y));
                PublishSelectionInfo();
                Invalidate();
                return;
            }
        }

        // If clicked outside any table while a table is active, deactivate
        if (_activeTable != null) DeactivateTable();

        // --- Normal text hit-test ---
        _activeHandle = ResizeHandleType.None;
        var position = _document.HitTest(offset.X, offset.Y);

        if (position.IsNone)
            return;

        // If the hit lands on or adjacent to an inline image, auto-select it and
        // start a potential drag so the user can immediately drag without a second click.
        int clickedDocIndex = position.ClosestCodePointIndex;
        var imageDocIndex = GetInlineImageDocIndex(clickedDocIndex)
                         ?? GetInlineImageDocIndex(clickedDocIndex - 1);
        if (imageDocIndex.HasValue)
        {
            var imgRange = new TextRange(imageDocIndex.Value, imageDocIndex.Value + 1);
            SetSelection(imgRange);
            MoveCaret(imgRange.CaretPosition);

            _potentialImageDrag = true;
            _imageDragStartDoc = docPoint;
            _imageDragCurrentDoc = docPoint;
            return;
        }

        // Normal text click
        var ghostXCoord = _ghostXCoord;
        _document.Navigate(position.CaretPosition, NavigationKind.None, (float)VisibleBounds.Height, ref ghostXCoord);
        SetSelection(new TextRange(position.CaretPosition));
        MoveCaret(position.CaretPosition);
    }

    /// <inheritdoc/>
    public void DragTo(Point point)
    {
        var offset = point.FromView(_viewModifier);
        if (Settings.ShowPageBreaks)
            offset = AdjustPointForPageBreaks(offset);
        var docPoint = new SKPoint(offset.X, offset.Y);

        // --- Column resize drag ---
        if (_draggingColDivider >= 0 && _draggingColTable != null)
        {
            float total    = _draggingColOrigWidth + _draggingColRightOrigWidth;
            float delta    = docPoint.X - _draggingColStartX;
            // Clamp so neither column can go below its word-wrap minimum.
            float newLeft  = Math.Clamp(_draggingColOrigWidth + delta,
                                        _draggingColMinWidth,
                                        total - _draggingColRightMinWidth);
            float newRight = total - newLeft;
            _draggingColTable.ResizeDivider(_draggingColDivider, newLeft, newRight);
            // OnHeightChanged fires → InvalidateTableTextBlock + ForceRelayout + Invalidate
            return;
        }

        // --- Resize drag ---
        if (_activeHandle != ResizeHandleType.None)
        {
            if (_resizingCellImage && ActiveCellController is { } resizeCellAcc)
            {
                var (resizingCellImage, _) = resizeCellAcc.GetSelectedImageInfo();
                if (resizingCellImage != null)
                    ApplyCellResizeDelta(resizingCellImage, docPoint);
            }
            else
            {
                var (resizingImage, _) = GetSelectedImageInfo();
                if (resizingImage != null)
                    ApplyResizeDelta(resizingImage, docPoint);
            }
            return;
        }

        // --- Image move drag ---
        if (_potentialImageDrag || _isDraggingImage)
        {
            // Transition from "potential" to actual drag once threshold is exceeded
            if (!_isDraggingImage)
            {
                float thresholdDoc = _dragThreshold / Math.Max(_viewModifier.Scale.X, 0.01f);
                float dx = docPoint.X - _imageDragStartDoc.X;
                float dy = docPoint.Y - _imageDragStartDoc.Y;
                if (dx * dx + dy * dy < thresholdDoc * thresholdDoc)
                    return; // still within threshold, stay in potential-drag mode

                _isDraggingImage = true;
                _potentialImageDrag = false;
            }

            _imageDragCurrentDoc = docPoint;

            // Determine drop position via hit-test
            var hit = _document.HitTest(docPoint.X, docPoint.Y);
            _imageDragDropIndex = hit.IsNone ? -1 : hit.ClosestCodePointIndex;

            Invalidate();
            return;
        }

        // --- Table drag (single-cell text selection or multi-cell selection) ---
        if (_activeTable != null)
        {
            var (hitTable2, hitTableBounds2, _) = FindTableAt(docPoint);
            if (hitTable2 == _activeTable && hitTableBounds2 != default)
            {
                var tableLocal2 = new SKPoint(docPoint.X - hitTableBounds2.Left, docPoint.Y - hitTableBounds2.Top);
                var cellHit2 = _activeTable.HitTestCell(tableLocal2);
                if (cellHit2.HasValue)
                {
                    var (hr, hc) = cellHit2.Value;
                    if (!_isMultiCellSelecting && hr == _selAnchorRow && hc == _selAnchorCol)
                    {
                        // Same cell — route to cell controller for in-cell text selection
                        var cellOrigin = GetActiveCellOrigin();
                        ActiveCellController?.DragTo(new SKPoint(docPoint.X - cellOrigin.X, docPoint.Y - cellOrigin.Y));
                        PublishSelectionInfo();
                    }
                    else
                    {
                        // Different cell — enter or update multi-cell selection
                        if (!_isMultiCellSelecting)
                        {
                            _isMultiCellSelecting = true;
                            _activeRow = -1;
                            _activeCol = -1;
                        }
                        _activeTable.SetMultiSelection(_selAnchorRow, _selAnchorCol, hr, hc);
                        Invalidate();
                    }
                }
            }
            return;
        }

        // --- Normal text selection drag ---
        var position = _document.HitTest(offset.X, offset.Y);
        if (position.IsNone)
            return;

        var ghostXCoord = _ghostXCoord;
        _document.Navigate(position.CaretPosition, NavigationKind.None, (float)VisibleBounds.Height, ref ghostXCoord);
        SetSelection(new TextRange(_selection.Start, position.ClosestCodePointIndex));
        MoveCaret(position.CaretPosition);
    }

    private void Navigate(NavigationKind kind, bool extend = false)
    {
        // If cancelling selection the navigation starts from whichever
        // end of the selection is in the direction of the navigation.
        // eg: if navigating left, start navigation from the end of
        //     the selection closer to the start of the document
        if (_selection.IsRange && !extend)
        {
            // Swap the selection range if moving from the other end
            if (_selection.Start > _selection.End != IsLeftwardNavigation(kind))
            {
                _selection = _selection.Reversed;
            }

            // For CharacterLeft/Right when have a selection, the caret just 
            // moves to the end of the selection - it doesn't then move by 
            // a character so set the nav kind to none.
            if (kind == NavigationKind.CharacterLeft || kind == NavigationKind.CharacterRight)
            {
                kind = NavigationKind.None;
            }
        }

        // Navigate from current position to new position
        var ghostXCoord = _ghostXCoord;
        var oldPos = _selection.CaretPosition;
        var newPos = _document.Navigate(oldPos, kind, (float)VisibleBounds.Height, ref ghostXCoord);

        // When moving vertically and extending selection and we hit the top or bottom of the 
        // document, instead of stopping mid-line, move to the document home/end.
        if (extend)
        {
            if (oldPos.CodePointIndex == newPos.CodePointIndex)
            {
                switch (kind)
                {
                    case NavigationKind.LineUp:
                    case NavigationKind.PageUp:
                        newPos = _document.Navigate(oldPos, NavigationKind.DocumentHome, (float)VisibleBounds.Height, ref ghostXCoord);
                        break;

                    case NavigationKind.LineDown:
                    case NavigationKind.PageDown:
                        newPos = _document.Navigate(oldPos, NavigationKind.DocumentEnd, (float)VisibleBounds.Height, ref ghostXCoord);
                        break;
                }
            }

            //TODO:  Verify this behavior is correct.
            //See if our old position is still within the selection.
            if (_selection.IsRange && oldPos.CodePointIndex >= _selection.Minimum && oldPos.CodePointIndex <= _selection.Maximum)
            {
                TextRange selectionRange;


                if (newPos.CodePointIndex < _selection.Minimum && oldPos.CodePointIndex == _selection.Maximum)
                {
                    selectionRange = new TextRange(oldPos.CodePointIndex, newPos.CodePointIndex, newPos.AltPosition);
                }
                else if (newPos.CodePointIndex > _selection.Maximum && oldPos.CodePointIndex == _selection.Minimum)
                {
                    selectionRange = new TextRange(oldPos.CodePointIndex, newPos.CodePointIndex, newPos.AltPosition);
                }
                else
                {
                    selectionRange = new TextRange(_selection.Start, newPos.CodePointIndex, newPos.AltPosition);
                }

                SetSelection(selectionRange, true);
            }
            else
            {
                SetSelection(new TextRange(oldPos.CodePointIndex, newPos.CodePointIndex, newPos.AltPosition), true);
            }

            MoveCaret(_selection.CaretPosition);
        }
        else
        {
            SetSelection(new TextRange(newPos.CodePointIndex, newPos.CodePointIndex, newPos.AltPosition), true);
        }

        // Move caret
        //MoveCaret(newPos, extend); //TODO:  Implement
        MoveCaret(newPos);

        // Store ghost position 
        // (do this after call to MoveCaret as it clears the ghost pos)
        _ghostXCoord = ghostXCoord;

        _caretView.Italic = _textStyle.FontItalic ?? false;

    }

    private void MoveCaret(CaretPosition pos)
    {
        var caretInfo = _document.GetCaretInfo(pos);

        var caretRect = caretInfo.CaretRectangle;
        _caretView.VisibleBounds = new Rectangle(caretRect.Left, caretRect.Top, 2, caretRect.Height);
        _textStyle = GetStyleAtCurrentPostion();
        _caretView.Italic = _textStyle.FontItalic ?? false;

        float caretScrollTop = Settings.ShowPageBreaks
            ? DocYToPagedScreenY(caretRect.Top)
            : caretRect.Top;

        if (VisibleBounds.Height > 0)
        {
            if (caretScrollTop < _viewModifier.Offset.Y)
            {
                ScrollTo(_viewModifier.Offset.X, caretScrollTop - 20);
            }
            else if (caretScrollTop > _viewModifier.Offset.Y + VisibleBounds.Height)
            {
                ScrollTo(_viewModifier.Offset.X, caretScrollTop - VisibleBounds.Height + 20);
            }
        }

        Invalidate();
    }

    #endregion

    #region Text Selection

    /// <inheritdoc/>
    public Task SelectAll()
    {
        if (ActiveCellController is { } ccSelAll) { ccSelAll.SelectAll(); AfterCellEdit(); return Task.CompletedTask; }
        SetSelection(new TextRange(0, _document.Length - 1));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void SelectNone()
    {
        SetSelection(new TextRange(0));
    }

    /// <inheritdoc/>
    public void ClearDocument()
    {
        var range = new TextRange(0, _document.Length - 1);
        if (range.IsRange)
            _document.ReplaceText(this, range, null, EditSemantics.None);
        SelectNone();
        PublishSelectionInfo();
    }

    /// <inheritdoc/>
    public bool IsModified { get; private set; }

    /// <inheritdoc/>
    public void MarkClean() => IsModified = false;

    private TextRange SetSelection(TextRange value, bool fireEvent = true)
    {
        // Clamp valid
        value = value.Clamp(_document.Length - 1);

        // Clear the ghost position
        _ghostXCoord = null;

        // Remember if there was a selection
        //bool hadSelection = _selection.IsRange;

        // Store new selection
        _selection = value;

        // Scroll to ensure the caret is visible
        //_caretView.EnsureVisible();

        //if (fireEvent)
        //    OnSelectionChanged();

        PublishSelectionInfo();

        Invalidate();

        return value;
    }

    #endregion

    #region Input

    /// <inheritdoc/>
    public async Task Cut()
    {
        if (ReadOnly) return;
        if (ActiveCellController is { } ccCut)
        {
            if (ccCut.Selection.IsRange)
            {
                await _clipboard.SetTextAsync(ccCut.GetSelectedText());
                ccCut.Delete();
                AfterCellEdit();
            }
            return;
        }
        if (await Copy())
            await Delete();
        PublishSelectionInfo();
    }

    /// <inheritdoc/>
    public async Task<bool> Copy()
    {
        if (ActiveCellController is { } ccCopy)
        {
            if (!ccCopy.Selection.IsRange) return false;
            await _clipboard.SetTextAsync(ccCopy.GetSelectedText());
            return true;
        }
        if (!_selection.IsRange)
            return false;

        // Build content blocks for the selection
        var blocks = new DocumentReader(_document).GetContent(_selection).ToList();

        // Image-only selection → copy just the image
        if (blocks.Count == 1)
        {
            var runs = blocks[0].Runs.ToArray();
            if (runs.Length == 1 && runs[0].IsImage)
            {
                await _clipboard.SetImageBytesAsync(runs[0].ImageData);
                return true;
            }
        }

        // Rich text copy: HTML + RTF + plain text
        var textBuffer = _document.GetText(_selection);
        textBuffer.Replace(0x2029, '\n');
        await _clipboard.SetRichDataAsync(blocks, textBuffer.ToString());

        return true;
    }

    /// <inheritdoc/>
    public async Task Paste()
    {
        if (ActiveCellController is { } ccPaste)
        {
            var pasteText = await _clipboard.GetTextAsync();
            if (pasteText != null)
            {
                pasteText = pasteText.Replace("\r\n", "\u2029").Replace('\n', '\u2029');
                ccPaste.Insert(pasteText);
                AfterCellEdit();
            }
            return;
        }

        // Try image first
        var imageData = await _clipboard.GetImageDataAsync();
        if (imageData != null)
        {
            var skImage = SKImage.FromEncodedData(imageData);
            if (skImage != null)
            {
                InsertInlineImage(skImage, skImage.Width, skImage.Height);
                return;
            }
        }

        // Get text from clipboard
        var text = await _clipboard.GetTextAsync();

        // Insert it
        if (text != null)
        {
            // Clean up CRLF and convert to paragraph separators
            text = text.Replace("\r\n", "\u2029").Replace('\n', '\u2029');

            // We want same selection semantics as typing (ie: move caret to end)
            _document.ReplaceText(this, _selection, text, EditSemantics.Typing, _textStyle);

            // but we don't want to be able to extend unit, so seal that last unit
            _document.UndoManager.Seal();

            PublishSelectionInfo();
        }
    }

    private Task Delete()
    {
        if (ReadOnly)
            return Task.CompletedTask;

        var semantics = EditSemantics.None;

        // If no selection, extend the selection to the next character
        if (!_selection.IsRange)
        {
            var extendTo = _document.Navigate(_selection.CaretPosition, NavigationKind.CharacterRight, 0, ref _ghostXCoord);
            _selection = TextRange.Union(_selection, new TextRange(extendTo));

            if (!_selection.IsRange)
                return Task.CompletedTask;

            semantics = EditSemantics.ForwardDelete;
        }

        // Delete the text
        _document.ReplaceText(this, _selection, null, semantics);

        PublishSelectionInfo();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Execute a backspace command.
    /// </summary>
    protected Task Backspace()
    {
        if (ReadOnly)
            return Task.CompletedTask;

        // If no selection, extend the selection to the previous character
        var semantics = EditSemantics.None;
        if (!_selection.IsRange)
        {
            var extendTo = _document.Navigate(_selection.CaretPosition, NavigationKind.CharacterLeft, 0, ref _ghostXCoord);
            _selection = TextRange.Union(_selection, new TextRange(extendTo));
            if (!_selection.IsRange)
                return Task.CompletedTask;

            semantics = EditSemantics.Backspace;
        }

        // Delete the text
        _document.ReplaceText(this, _selection, null, semantics);

        PublishSelectionInfo();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Insert(string text)
    {
        if (ActiveCellController is { } ccInsert) { ccInsert.Insert(text); AfterCellEdit(); return; }
        _document.ReplaceText(this, _selection, text, _overtype ? EditSemantics.Overtype : EditSemantics.Typing, _textStyle);
    }

    /// <summary>
    /// Inserts an inline image at the current caret/selection position.
    /// The image flows with text as a single character-width placeholder.
    /// </summary>
    /// <param name="image">The Skia image to embed.</param>
    /// <param name="displayWidth">Desired display width in pixels.</param>
    /// <param name="displayHeight">Desired display height in pixels.</param>
    public void InsertInlineImage(SKImage image, float displayWidth, float displayHeight)
    {
        if (ReadOnly) return;
        if (ActiveCellController is { } ccImg) { ccImg.InsertInlineImage(image, displayWidth, displayHeight); AfterCellEdit(); return; }

        // Clamp image to available content width (page width minus left/right margins).
        var contentWidth = PageWidth - Settings.DocumentMargins.Left - Settings.DocumentMargins.Right;
        if (displayWidth > contentWidth && contentWidth > 0)
        {
            var scale = contentWidth / displayWidth;
            displayWidth = contentWidth;
            displayHeight *= scale;
        }

        var inlineImage = new InlineImage(image, displayWidth, displayHeight);
        _document.InsertInlineObject(this, _selection, inlineImage, _textStyle);

        PublishSelectionInfo();
    }

    /// <inheritdoc/>
    public InlineTable InsertTable(TableOptions options)
    {
        if (ReadOnly) return new InlineTable(options,
            PageWidth - Settings.DocumentMargins.Left - Settings.DocumentMargins.Right,
            _textStyle, () => { });

        var contentWidth = PageWidth - Settings.DocumentMargins.Left - Settings.DocumentMargins.Right;
        var table = new InlineTable(options, contentWidth, _textStyle, Invalidate);
        table.OnHeightChanged = () =>
        {
            // The TextBlock that lays out the table paragraph caches its own layout result
            // (_needsLayout = false after first layout).  Simply calling _document.ForceRelayout()
            // only resets the document-level flag; the TextBlock still returns its stale
            // MeasuredHeight and the paragraph is positioned using the old table height.
            // We must force the TextBlock itself to re-run layout so it re-queries InlineTable.Height.
            InvalidateTableTextBlock(table);
            _document.ForceRelayout();
            Invalidate();
        };
        _document.InsertInlineObject(this, _selection, table, _textStyle);
        PublishSelectionInfo();
        return table;
    }

    /// <summary>
    /// Finds the TextBlock paragraph that contains <paramref name="table"/> as an inline object
    /// and calls ForceRelayout() on it so the next layout pass re-queries the table height.
    /// </summary>
    private void InvalidateTableTextBlock(InlineTable table)
    {
        foreach (var para in _document.GetPragraphs())
        {
            var tb = para.TextBlock;
            if (tb == null) continue;
            foreach (var line in tb.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.InlineObject && ReferenceEquals(run.InlineObject, table))
                    {
                        tb.ForceRelayout();
                        return;
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool IsInTableCell => _activeTable != null;

    /// <inheritdoc/>
    public bool IsInTableHeaderRow =>
        _activeTable != null && _activeRow >= 0 && _activeCol >= 0 &&
        (_activeTable.GetCell(_activeRow, _activeCol)?.IsHeader ?? false);

    /// <inheritdoc/>
    public void InsertTableRowBefore()
    {
        if (ReadOnly || _activeTable == null || IsInTableHeaderRow) return;
        _activeTable.InsertRow(_activeRow);
        _activeRow++;  // keep focus on the row the user was editing (it shifted down)
        ActiveCellController?.SelectNone();
        InvalidateTableTextBlock(_activeTable);
        AfterCellEdit();
    }

    /// <inheritdoc/>
    public void InsertTableRowAfter()
    {
        if (ReadOnly || _activeTable == null) return;
        _activeTable.InsertRow(_activeRow + 1);
        // stay in current row; new row is below
        ActiveCellController?.SelectNone();
        InvalidateTableTextBlock(_activeTable);
        AfterCellEdit();
    }

    /// <inheritdoc/>
    public void InsertTableColumnBefore()
    {
        if (ReadOnly || _activeTable == null) return;
        _activeTable.InsertColumn(_activeCol);
        _activeCol++;  // keep focus on the column the user was editing (it shifted right)
        ActiveCellController?.SelectNone();
        InvalidateTableTextBlock(_activeTable);
        AfterCellEdit();
    }

    /// <inheritdoc/>
    public void InsertTableColumnAfter()
    {
        if (ReadOnly || _activeTable == null) return;
        _activeTable.InsertColumn(_activeCol + 1);
        // stay in current column; new column is to the right
        ActiveCellController?.SelectNone();
        InvalidateTableTextBlock(_activeTable);
        AfterCellEdit();
    }

    /// <inheritdoc/>
    public bool IsMultiCellSelectingTable => _isMultiCellSelecting;

    /// <inheritdoc/>
    public bool CanMergeSelectedCells() =>
        _isMultiCellSelecting && _activeTable != null && _activeTable.CanMergeSelection();

    /// <inheritdoc/>
    public void MergeSelectedCells()
    {
        if (ReadOnly || !_isMultiCellSelecting || _activeTable == null) return;
        var (newRow, newCol) = _activeTable.MergeSelection();
        _isMultiCellSelecting = false;
        _selAnchorRow = newRow;
        _selAnchorCol = newCol;
        _activeRow = newRow;
        _activeCol = newCol;
        ActiveCellController?.SelectNone();
        InvalidateTableTextBlock(_activeTable);
        AfterCellEdit();
    }

    /// <inheritdoc/>
    public void DeleteTableRow()
    {
        if (ReadOnly || _activeTable == null) return;

        int minRow, maxRow;
        if (_isMultiCellSelecting)
        {
            (minRow, maxRow) = _activeTable.GetSelectedRowRange();
        }
        else
        {
            minRow = maxRow = _activeRow;
        }

        int rowsToDelete = maxRow - minRow + 1;
        if (rowsToDelete >= _activeTable.Rows)
        {
            DeleteActiveTable();
            return;
        }

        for (int r = maxRow; r >= minRow; r--)
        {
            _activeTable.DeleteRow(r);
            if (_activeTable.IsEmpty) { DeleteActiveTable(); return; }
        }

        _isMultiCellSelecting = false;
        _activeTable.ClearMultiSelection();
        _activeRow = Math.Min(minRow, _activeTable.Rows - 1);
        _activeCol = Math.Min(_selAnchorCol, _activeTable.Cols - 1);
        _selAnchorRow = _activeRow;
        _selAnchorCol = _activeCol;
        ActiveCellController?.SelectNone();
        InvalidateTableTextBlock(_activeTable);
        AfterCellEdit();
    }

    /// <inheritdoc/>
    public void DeleteTableColumn()
    {
        if (ReadOnly || _activeTable == null) return;

        int minCol, maxCol;
        if (_isMultiCellSelecting)
        {
            (minCol, maxCol) = _activeTable.GetSelectedColRange();
        }
        else
        {
            minCol = maxCol = _activeCol;
        }

        int colsToDelete = maxCol - minCol + 1;
        if (colsToDelete >= _activeTable.Cols)
        {
            DeleteActiveTable();
            return;
        }

        for (int c = maxCol; c >= minCol; c--)
        {
            _activeTable.DeleteColumn(c);
            if (_activeTable.IsEmpty) { DeleteActiveTable(); return; }
        }

        _isMultiCellSelecting = false;
        _activeTable.ClearMultiSelection();
        _activeCol = Math.Min(minCol, _activeTable.Cols - 1);
        _activeRow = Math.Min(_selAnchorRow, _activeTable.Rows - 1);
        _selAnchorRow = _activeRow;
        _selAnchorCol = _activeCol;
        ActiveCellController?.SelectNone();
        InvalidateTableTextBlock(_activeTable);
        AfterCellEdit();
    }

    private void DeleteActiveTable()
    {
        if (_activeTable == null) return;
        int tableDocIndex = _activeTableDocIndex;
        DeactivateTable();
        var tableRange = new TextRange(tableDocIndex, tableDocIndex + 1);
        _document.ReplaceText(this, tableRange, null, EditSemantics.None);
        PublishSelectionInfo();
    }

    /// <inheritdoc/>
    public Task Undo()
    {
        if (ReadOnly) return Task.CompletedTask;
        if (ActiveCellController is { } ccUndo) { ccUndo.Undo(); AfterCellEdit(); return Task.CompletedTask; }
        _document.Undo(this);
        PublishSelectionInfo();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task Redo()
    {
        if (ReadOnly) return Task.CompletedTask;
        if (ActiveCellController is { } ccRedo) { ccRedo.Redo(); AfterCellEdit(); return Task.CompletedTask; }
        _document.Redo(this);
        PublishSelectionInfo();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<bool> OnKeyEvent(KeyInfo key)
    {
        if (_activeTable != null)
        {
            // Multi-cell selection mode: Escape deactivates; any other key exits the
            // multi-select, activates the anchor cell, and re-processes the key.
            if (_isMultiCellSelecting)
            {
                if (key.AsShortcut().Equals(new Shortcut(KeyCode.Escape)))
                {
                    DeactivateTable();
                    return true;
                }
                var sc = key.AsShortcut();
                if (sc.Equals(new Shortcut(KeyCode.Delete)) || sc.Equals(new Shortcut(KeyCode.Back)))
                {
                    if (!ReadOnly)
                        ApplyToSelectedCells(cc => cc.ClearContent());
                    return true;
                }
                _isMultiCellSelecting = false;
                _activeRow = _selAnchorRow;
                _activeCol = _selAnchorCol;
                _activeTable.ClearMultiSelection();
                Invalidate();
                return await OnKeyEvent(key);   // re-process in single-cell mode
            }

            var shortcut = key.AsShortcut();

            if (shortcut.Equals(new Shortcut(KeyCode.Escape)))
            {
                DeactivateTable();
                Invalidate();
                return true;
            }

            if (shortcut.Equals(new Shortcut(KeyCode.Tab)))
            {
                NavigateToNextCell(forward: true);
                return true;
            }

            if (shortcut.Equals(new Shortcut(KeyCode.Tab, shift: true)))
            {
                NavigateToNextCell(forward: false);
                return true;
            }

            if (shortcut.Equals(new Shortcut(KeyCode.X, control: true)))
            {
                await Cut();
                return true;
            }

            if (shortcut.Equals(new Shortcut(KeyCode.C, control: true)))
            {
                await Copy();
                return true;
            }

            if (shortcut.Equals(new Shortcut(KeyCode.V, control: true)))
            {
                await Paste();
                return true;
            }

            // Delegate to the cell; publish toolbar state afterwards so the
            // toolbar reflects any navigation or style change the key caused.
            bool cellHandled = await ActiveCellController!.OnKeyEvent(key);
            if (cellHandled) PublishSelectionInfo();
            return cellHandled;
        }

        return await _shortcuts.Execute(key.AsShortcut());
    }

    #endregion

    #region ITextDocumentView Members

    void ITextDocumentView.OnReset()
    {
        //NoOp
    }

    void ITextDocumentView.OnRedraw()
    {
        //NoOp
    }

    void ITextDocumentView.OnDocumentWillChange(ITextDocumentView view)
    {
        //// Default pending selection it the current selection
        _pendingSelection = _selection;

        //// Track height changes to document
        _pendingContentOffset = _viewModifier.Offset;
        _lastHeight = _document.MeasuredHeight;
    }

    void ITextDocumentView.OnDocumentChange(ITextDocumentView view, DocumentChangeInfo info)
    {
        if (view == this)
        {
            switch (info.Semantics)
            {
                case EditSemantics.None:
                    _pendingSelection = new TextRange(info.CodePointIndex, info.CodePointIndex + info.NewLength);
                    break;

                case EditSemantics.Backspace:
                    if (info.IsUndoing)
                        _pendingSelection = new TextRange(info.CodePointIndex + info.NewLength);
                    else
                        _pendingSelection = new TextRange(info.CodePointIndex);
                    break;

                case EditSemantics.ForwardDelete:
                    _pendingSelection = new TextRange(info.CodePointIndex);
                    break;

                case EditSemantics.Typing:
                    if (info.IsUndoing)
                        _pendingSelection = new TextRange(info.CodePointIndex, info.CodePointIndex + info.NewLength);
                    else
                        _pendingSelection = new TextRange(info.CodePointIndex + info.NewLength);
                    break;

                case EditSemantics.Overtype:
                    if (info.IsUndoing)
                        _pendingSelection = new TextRange(info.CodePointIndex, info.CodePointIndex);
                    else
                        _pendingSelection = new TextRange(info.CodePointIndex + info.NewLength);
                    break;

                case EditSemantics.ImeComposition:
                    if (info.IsUndoing)
                        _pendingSelection = new TextRange(info.CodePointIndex, info.CodePointIndex + info.NewLength);
                    else
                        _pendingSelection = new TextRange(info.CodePointIndex + info.ImeCaretOffset);
                    break;
            }
        }
        else
        {
            // Update the selection
            _pendingSelection = _pendingSelection.UpdateForEdit(info.CodePointIndex, info.OldLength, info.NewLength);

            // If the document height changed, then work out if it was before
            // the region we have on view and if so, adjust our content offset
            // so that what we have on view stays in the same position
            var deltaHeight = _document.MeasuredHeight - _lastHeight;
            _lastHeight = _document.MeasuredHeight;
            if (deltaHeight != 0)
            {
                var changeYCoord = _document.GetCaretInfo(new CaretPosition(info.CodePointIndex)).CaretRectangle.Top;
                if (changeYCoord < -_pendingContentOffset.Y)
                    _pendingContentOffset = new Vector2(_pendingContentOffset.X, _pendingContentOffset.Y - deltaHeight);
            }
        }
    }

    void ITextDocumentView.OnDocumentDidChange(ITextDocumentView view)
    {
        IsModified = true;

        //// Update for changes made during edit
        //UpdateContentSize();
        _viewModifier.Offset = _pendingContentOffset;

        SetSelection(_pendingSelection, true);

        MoveCaret(_selection.CaretPosition);

        // Clamp the current offset
        ScrollBy(0, 0);

        if (OnContentSizeChanged != null)
        {
            var info = new DocumentInfo(_document.MeasuredWidth, GetReportedDocumentHeight(), _viewModifier.Offset);
            OnContentSizeChanged(info);
        }

        // Repaint
        Invalidate();
    }

    #endregion

    #region Page Break Helpers

    // Content height per page = PageHeight minus top+bottom margins.
    // Doc space is continuous; page p's content occupies docY:
    //   [topMargin + p*contentH,  topMargin + (p+1)*contentH]
    // In paged-screen space, a page adds (topMargin + bottomMargin + pg) of
    // extra vertical offset per page beyond page 0.

    private float PageContentHeight() =>
        Settings.PageHeight - Settings.DocumentMargins.Top - Settings.DocumentMargins.Bottom;

    private float TotalScrollHeight()
    {
        float contentH = PageContentHeight();
        float textH = Math.Max(0f, _document.MeasuredHeight - Settings.DocumentMargins.Top);
        int n = Math.Max(1, (int)Math.Ceiling(textH / contentH));
        return n * Settings.PageHeight + (n - 1) * Settings.PageGap;
    }

    private float DocYToPagedScreenY(float docY)
    {
        float topMargin = Settings.DocumentMargins.Top;
        float contentH = PageContentHeight();
        float marginShift = topMargin + Settings.DocumentMargins.Bottom + Settings.PageGap;
        if (docY < topMargin) return docY;
        int page = (int)((docY - topMargin) / contentH);
        return docY + page * marginShift;
    }

    private float PagedScreenYToDocY(float screenY)
    {
        float ph = Settings.PageHeight;
        float phg = ph + Settings.PageGap;
        float topMargin = Settings.DocumentMargins.Top;
        float bottomMargin = Settings.DocumentMargins.Bottom;
        float contentH = ph - topMargin - bottomMargin;

        int page = (int)(screenY / phg);
        float local = screenY - page * phg;
        // Clamp to content area within the page
        if (local < topMargin) local = topMargin;
        if (local > ph - bottomMargin) local = ph - bottomMargin - 1;

        // docY = page * contentH + local_y  (derived from the translate formula)
        return page * contentH + local;
    }

    private Point AdjustPointForPageBreaks(Point screenPoint)
    {
        float scrollX = _viewModifier.Offset.X;
        float pageX = Math.Max(0f, (VisibleBounds.Width - _document.PageWidth) / 2f);
        float docY = PagedScreenYToDocY(screenPoint.Y);
        return new Point(screenPoint.X - pageX + scrollX, docY);
    }

    private float GetReportedDocumentHeight() =>
        Settings.ShowPageBreaks ? TotalScrollHeight() : _document.MeasuredHeight;

    #endregion

    /// <inheritdoc/>
    public void ApplyDocumentSettings(DocumentSettings settings)
    {
        Settings = settings;
        _backgroundPaint = new SKPaint { Color = settings.DocumentBackgroundColor };
        _canvasBackgroundPaint = new SKPaint { Color = settings.CanvasBackgroundColor };
        _pageShadowPaint = new SKPaint { Color = new SKColor(0, 0, 0, 60) };
        _document.PageWidth = settings.PageWidth;
        _document.SetMargins(settings.DocumentMargins.Left, settings.DocumentMargins.Top, settings.DocumentMargins.Right, settings.DocumentMargins.Bottom);
    }

    private readonly object _drawLock = new object();
    private bool _isDrawing = false;

    /// <inheritdoc/>
    public void Draw(SKCanvas canvas)
    {
        //Prevent unnecessary draw operations if one is in progress, skip the next
        if (_isDrawing)
            return;

        lock (_drawLock)
        {
            _isDrawing = true;

            canvas.Save();

            if (Settings.ShowPageBreaks)
            {
                DrawWithPageBreaks(canvas);
            }
            else
            {
                // Paint Background
                canvas.DrawRect(VisibleBounds.TopLeft.X, VisibleBounds.TopLeft.Y, VisibleBounds.Width, VisibleBounds.Height, _backgroundPaint);

                var highlightRange = _selection;

                // In overtype mode, paint the character that will be overtyped
                // in the selection highlight
                if (_overtype && !_document.IsImeComposing)
                    highlightRange = _document.GetOvertypeRange(highlightRange);

                // Suppress standard selection highlight for the single-image selection so
                // the handles are the only visual indicator.
                var (selectedImage, imageBounds) = GetSelectedImageInfo();
                var opts = (highlightRange.IsRange && selectedImage == null)
                    ? new TextPaintOptions { Selection = highlightRange, SelectionColor = SelectionColor }
                    : null;

                // Paint the document
                canvas.Scale(_viewModifier.Scale.X, _viewModifier.Scale.Y);
                canvas.Translate(-_viewModifier.ScaledOffset.X, -_viewModifier.ScaledOffset.Y);

                _document.Paint(canvas, _viewModifier.ScaledOffset.Y, _viewModifier.ScaledOffset.Y + VisibleBounds.Height, opts);
                DrawGhostBordersForBorderlessTables(canvas);

                if (selectedImage != null)
                {
                    if (_isDraggingImage)
                        DrawImageDragVisuals(canvas, selectedImage, imageBounds);
                    else
                        DrawResizeHandles(canvas, imageBounds);
                }

                // Draw active table cell overlay
                if (_activeTable != null)
                    DrawActiveCellOverlay(canvas);

                // Hide the text caret while dragging an image
                if (HasFocus && !_isDraggingImage && _activeTable == null)
                    _caretView.Draw(canvas);
            }

            canvas.Restore();

            _isDrawing = false;
        }
    }

    private void DrawWithPageBreaks(SKCanvas canvas)
    {
        float ph = Settings.PageHeight;
        float pg = Settings.PageGap;
        float topMargin = Settings.DocumentMargins.Top;
        float bottomMargin = Settings.DocumentMargins.Bottom;
        float contentH = ph - topMargin - bottomMargin;
        // Each additional page adds this much extra vertical translation over the raw doc offset.
        float marginShift = topMargin + bottomMargin + pg;

        float scrollY = _viewModifier.Offset.Y;
        float scrollX = _viewModifier.Offset.X;
        float viewW = VisibleBounds.Width;
        float viewH = VisibleBounds.Height;
        float scaleX = _viewModifier.Scale.X;
        float scaleY = _viewModifier.Scale.Y;
        float pageW = _document.PageWidth;
        float pageX = Math.Max(0f, (viewW - pageW) / 2f) - scrollX;

        // Gray canvas background
        canvas.DrawRect(0, 0, viewW, viewH, _canvasBackgroundPaint);

        // Compute visible page range
        float textH = Math.Max(0f, _document.MeasuredHeight - topMargin);
        int totalPages = Math.Max(1, (int)Math.Ceiling(textH / contentH));
        float phg = ph + pg;
        int firstPage = Math.Max(0, (int)(scrollY / phg));
        int lastPage = Math.Min(totalPages - 1, (int)((scrollY + viewH) / phg));

        // Compute selection/highlight options once
        var highlightRange = _selection;
        if (_overtype && !_document.IsImeComposing)
            highlightRange = _document.GetOvertypeRange(highlightRange);
        var (selectedImage, imageBounds) = GetSelectedImageInfo();
        var opts = (highlightRange.IsRange && selectedImage == null)
            ? new TextPaintOptions { Selection = highlightRange, SelectionColor = SelectionColor }
            : null;

        // Pre-compute line-aligned page boundaries so no line is ever split between pages.
        // splitBoundary[i] = doc Y where content of page (firstPage+i) STARTS.
        // splitBoundary[count] = doc Y where content of lastPage ENDS.
        int numBoundaries = lastPage - firstPage + 2;
        float[] splitBoundary = new float[numBoundaries];
        for (int i = 0; i < numBoundaries; i++)
        {
            int pageIdx = firstPage + i;
            splitBoundary[i] = pageIdx == 0
                ? 0f
                : FindPageSplitY(topMargin + pageIdx * contentH);
        }

        for (int p = firstPage; p <= lastPage; p++)
        {
            int j = p - firstPage;
            float minDocY = splitBoundary[j];
            float maxDocY = splitBoundary[j + 1];

            float pageCanvasY = p * phg - scrollY;

            canvas.DrawRect(pageX + 4, pageCanvasY + 4, pageW, ph, _pageShadowPaint);
            canvas.DrawRect(pageX, pageCanvasY, pageW, ph, _backgroundPaint);

            canvas.Save();

            // Clip top: for page 0 use the nominal top margin; for pages 1+ use the
            // exact screen Y where the pushed line starts (line-aligned split boundary).
            float clipTop = (p == 0)
                ? pageCanvasY + topMargin
                : minDocY + p * marginShift - scrollY;
            float clipBot = maxDocY + p * marginShift - scrollY;
            clipBot += GetTableSplitBorderWidth(maxDocY); // include bottom border of last split row
            clipTop = Math.Max(clipTop, pageCanvasY);
            clipBot = Math.Min(clipBot, pageCanvasY + ph);

            canvas.ClipRect(new SKRect(pageX, clipTop, pageX + pageW, clipBot));

            canvas.Scale(scaleX, scaleY);
            canvas.Translate(pageX / scaleX, (p * marginShift - scrollY) / scaleY);

            _document.Paint(canvas, minDocY, maxDocY, opts?.Clone());
            DrawTableSplitBordersAtY(canvas, minDocY);
            DrawTableSplitBordersAtY(canvas, maxDocY);
            DrawGhostBordersForBorderlessTables(canvas);

            if (selectedImage != null)
            {
                if (_isDraggingImage)
                    DrawImageDragVisuals(canvas, selectedImage, imageBounds);
                else
                    DrawResizeHandles(canvas, imageBounds);
            }

            if (_activeTable != null)
                DrawActiveCellOverlay(canvas);

            if (HasFocus && !_isDraggingImage && _activeTable == null)
                _caretView.Draw(canvas);

            canvas.Restore();
        }
    }

    /// <summary>
    /// Returns the doc-Y split point for a page boundary at <paramref name="rawBoundary"/>.
    /// If the last line before the boundary straddles it, that line's top Y is returned so
    /// it is pushed entirely to the next page.  Otherwise <paramref name="rawBoundary"/> is returned.
    /// </summary>
    private float FindPageSplitY(float rawBoundary)
    {
        float lastLineTop = float.MinValue;
        float lastLineBot = float.MinValue;
        TextLine? lastLine = null;

        foreach (var para in _document.GetPragraphs())
        {
            if (para.TextBlock == null) continue;
            float paraTop = para.ContentYCoord;
            if (paraTop >= rawBoundary) break;

            foreach (var line in para.TextBlock.Lines)
            {
                float lineTop = paraTop + line.YCoord;
                if (lineTop >= rawBoundary) break;
                lastLineTop = lineTop;
                lastLineBot = lineTop + line.Height;
                lastLine = line;
            }
        }

        // If the last line straddles the boundary...
        if (lastLineTop > float.MinValue && lastLineBot > rawBoundary && lastLine != null)
        {
            // If the line contains a table, snap the split to the nearest row boundary
            // so no row is ever cut across pages — the whole straddling row moves to the next page.
            foreach (var run in lastLine.Runs)
            {
                if (run.RunKind == FontRunKind.InlineObject && run.InlineObject is InlineTable table)
                {
                    float tableTop = lastLineTop + lastLine.BaseLine + run.Ascent;
                    int splitRow = table.GetRowSplitIndex(rawBoundary - tableTop);

                    if (splitRow == 0)
                        break; // No rows fit before the boundary; fall through to push whole line

                    if (splitRow < table.Rows)
                        return tableTop + table.GetRowTop(splitRow); // snap to row boundary

                    return rawBoundary; // All rows fit — no split required
                }
            }

            // Regular (non-table) line: push it entirely to the next page.
            return lastLineTop;
        }

        return rawBoundary;
    }

    /// <summary>
    /// For every borderless table in the document, draws ghost (light gray dotted) borders
    /// around the table and each cell so the grid is always visible.
    /// Must be called while the canvas transform for the current page or view is still active.
    /// </summary>
    private void DrawGhostBordersForBorderlessTables(SKCanvas canvas)
    {
        foreach (var para in _document.GetPragraphs())
        {
            if (para.TextBlock == null) continue;
            foreach (var line in para.TextBlock.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.InlineObject &&
                        run.InlineObject is InlineTable table &&
                        table.NeedsGhostBorders)
                    {
                        float tableTop = para.ContentYCoord + line.YCoord + line.BaseLine + run.Ascent;
                        var origin = new SKPoint(para.ContentXCoord + run.XCoord, tableTop);
                        table.PaintGhostBorders(canvas, origin);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draws the missing horizontal border at <paramref name="splitDocY"/> for any
    /// row-spanning table cell whose full height straddles that document Y coordinate.
    /// Must be called while the page's canvas transform (translate + clip) is still active.
    /// </summary>
    private void DrawTableSplitBordersAtY(SKCanvas canvas, float splitDocY)
    {
        foreach (var para in _document.GetPragraphs())
        {
            if (para.TextBlock == null) continue;
            foreach (var line in para.TextBlock.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind != FontRunKind.InlineObject || run.InlineObject is not InlineTable table)
                        continue;
                    float tableTop = para.ContentYCoord + line.YCoord + line.BaseLine + run.Ascent;
                    float tableBot = tableTop + table.Height;
                    if (splitDocY <= tableTop || splitDocY >= tableBot) continue;

                    var origin = new SKPoint(para.ContentXCoord + run.XCoord, tableTop);
                    table.PaintSplitBorders(canvas, origin, splitDocY - tableTop);
                }
            }
        }
    }

    /// <summary>
    /// If <paramref name="splitDocY"/> falls inside a table (i.e. it is a row-boundary split
    /// produced by <see cref="FindPageSplitY"/>), returns that table's border width so the
    /// caller can extend the page clip to include the bottom border of the last visible row.
    /// Returns 0 when <paramref name="splitDocY"/> is not inside any table.
    /// </summary>
    private float GetTableSplitBorderWidth(float splitDocY)
    {
        foreach (var para in _document.GetPragraphs())
        {
            if (para.TextBlock == null) continue;
            foreach (var line in para.TextBlock.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind != FontRunKind.InlineObject || run.InlineObject is not InlineTable table)
                        continue;
                    float tableTop = para.ContentYCoord + line.YCoord + line.BaseLine + run.Ascent;
                    if (splitDocY > tableTop && splitDocY < tableTop + table.Height)
                        return table.BorderWidth;
                }
            }
        }
        return 0f;
    }

    /// <inheritdoc/>
    public int GetPageCount()
    {
        float contentH = Settings.PageHeight - Settings.DocumentMargins.Top - Settings.DocumentMargins.Bottom;
        float textH = Math.Max(0f, _document.MeasuredHeight - Settings.DocumentMargins.Top);
        return Math.Max(1, (int)Math.Ceiling(textH / contentH));
    }

    /// <summary>
    /// Renders a single document page to a new <see cref="SKBitmap"/> at the given scale factor
    /// (1f = 96 DPI, 3.125f ≈ 300 DPI, etc.).  Caller is responsible for disposing the bitmap.
    /// </summary>
    public SKBitmap RenderPageToBitmap(int pageNumber, float scale)
    {
        var bitmap = new SKBitmap(
            (int)(PageWidth * scale),
            (int)(Settings.PageHeight * scale),
            SKColorType.Bgra8888,
            SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Scale(scale, scale);
        DrawPageForPrint(canvas, pageNumber);
        canvas.Flush();
        return bitmap;
    }

    private void DrawPageForPrint(SKCanvas canvas, int pageNumber)
    {
        float ph = Settings.PageHeight;
        float topMargin = Settings.DocumentMargins.Top;
        float contentH = ph - topMargin - Settings.DocumentMargins.Bottom;

        float minDocY = pageNumber == 0 ? 0f : FindPageSplitY(topMargin + pageNumber * contentH);
        float maxDocY = Math.Min(FindPageSplitY(topMargin + (pageNumber + 1) * contentH), _document.MeasuredHeight);

        // Mirror the clip logic from DrawWithPageBreaks, adapted for a per-page PDF canvas
        // (no marginShift, no scroll — translate is simply -pageNumber * contentH).
        // clipTop: for page 0 use the nominal top margin; for pages 1+ use the exact canvas Y
        //          where the line-aligned minDocY lands, so pushed lines are not double-drawn.
        // clipBot: always the canvas Y of the line-aligned maxDocY, so straddling lines are
        //          excluded from this page and not visible below its edge.
        float clipTop = pageNumber == 0
            ? topMargin
            : minDocY - pageNumber * contentH;
        float clipBot = maxDocY - pageNumber * contentH;
        clipBot += GetTableSplitBorderWidth(maxDocY); // include bottom border of last split row
        clipTop = Math.Max(0f, clipTop);
        clipBot = Math.Min(ph, clipBot);

        canvas.DrawRect(0, 0, PageWidth, ph, _backgroundPaint);

        canvas.Save();
        canvas.ClipRect(new SKRect(0, clipTop, PageWidth, clipBot));
        canvas.Translate(0, -pageNumber * contentH);
        _document.Paint(canvas, minDocY, maxDocY, null);
        DrawTableSplitBordersAtY(canvas, minDocY);
        DrawTableSplitBordersAtY(canvas, maxDocY);
        canvas.Restore();
    }

    /// <inheritdoc/>
    public void PrintToPdf(Stream stream)
    {
        float contentH = Settings.PageHeight - Settings.DocumentMargins.Top - Settings.DocumentMargins.Bottom;
        float textH = Math.Max(0f, _document.MeasuredHeight - Settings.DocumentMargins.Top);
        int pageCount = Math.Max(1, (int)Math.Ceiling(textH / contentH));

        using var skStream = new SKManagedWStream(stream);
        using var document = SKDocument.CreatePdf(skStream, 96f);
        for (int p = 0; p < pageCount; p++)
        {
            var canvas = document.BeginPage(PageWidth, Settings.PageHeight);
            DrawPageForPrint(canvas, p);
            document.EndPage();
        }
        document.Close();
    }

    #region Image Resize Handles

    /// <summary>
    /// If the current selection is a single inline image, returns that image and its
    /// bounding rectangle in document coordinates.  Otherwise returns (null, empty).
    /// </summary>
    private (InlineImage? image, SKRect bounds) GetSelectedImageInfo()
    {
        if (!_selection.IsRange || _selection.Length != 1)
            return (null, default);

        int targetDocIndex = _selection.Start;

        foreach (var para in _document.GetPragraphs())
        {
            var tb = para.TextBlock;
            if (tb == null)
                continue;

            int paraStart = para.CodePointIndex;

            foreach (var line in tb.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.InlineObject &&
                        run.InlineObject is InlineImage img &&
                        run.Start + paraStart == targetDocIndex)
                    {
                        float top = para.ContentYCoord + line.YCoord + line.BaseLine + run.Ascent;
                        var bounds = new SKRect(
                            para.ContentXCoord + run.XCoord,
                            top,
                            para.ContentXCoord + run.XCoord + run.Width,
                            top + img.Height);
                        return (img, bounds);
                    }
                }
            }
        }

        return (null, default);
    }

    /// <summary>Returns the bounding rect of a single handle in document coordinates.</summary>
    private static SKRect GetHandleRect(SKRect imageBounds, ResizeHandleType handle)
    {
        float hs = _handleSize / 2f;
        SKPoint center = handle switch
        {
            ResizeHandleType.TopLeft => new SKPoint(imageBounds.Left, imageBounds.Top),
            ResizeHandleType.TopCenter => new SKPoint(imageBounds.MidX, imageBounds.Top),
            ResizeHandleType.TopRight => new SKPoint(imageBounds.Right, imageBounds.Top),
            ResizeHandleType.MiddleRight => new SKPoint(imageBounds.Right, imageBounds.MidY),
            ResizeHandleType.BottomRight => new SKPoint(imageBounds.Right, imageBounds.Bottom),
            ResizeHandleType.BottomCenter => new SKPoint(imageBounds.MidX, imageBounds.Bottom),
            ResizeHandleType.BottomLeft => new SKPoint(imageBounds.Left, imageBounds.Bottom),
            ResizeHandleType.MiddleLeft => new SKPoint(imageBounds.Left, imageBounds.MidY),
            _ => throw new ArgumentOutOfRangeException(nameof(handle)),
        };
        return new SKRect(center.X - hs, center.Y - hs, center.X + hs, center.Y + hs);
    }

    /// <summary>Returns which handle (if any) contains the given document-space point.</summary>
    private static ResizeHandleType HitTestHandles(SKPoint docPoint, SKRect imageBounds)
    {
        foreach (ResizeHandleType handle in Enum.GetValues<ResizeHandleType>())
        {
            if (handle == ResizeHandleType.None)
                continue;
            if (GetHandleRect(imageBounds, handle).Contains(docPoint))
                return handle;
        }
        return ResizeHandleType.None;
    }

    /// <summary>Returns the appropriate cursor for the given resize handle.</summary>
    private static EditorCursor CursorForHandle(ResizeHandleType handle) => handle switch
    {
        ResizeHandleType.TopLeft or ResizeHandleType.BottomRight => EditorCursor.ResizeDiagNWSE,
        ResizeHandleType.TopRight or ResizeHandleType.BottomLeft => EditorCursor.ResizeDiagNESW,
        ResizeHandleType.TopCenter or ResizeHandleType.BottomCenter => EditorCursor.ResizeNS,
        ResizeHandleType.MiddleLeft or ResizeHandleType.MiddleRight => EditorCursor.ResizeEW,
        _ => EditorCursor.Default,
    };

    /// <summary>
    /// Computes new width/height for the image being resized based on the current drag position.
    /// Aspect ratio is preserved for corner handles; edge handles resize in one direction only.
    /// </summary>
    private (float newW, float newH) ComputeResizeDimensions(SKPoint currentDocPoint)
    {
        float deltaX = currentDocPoint.X - _resizeDragStartDoc.X;
        float deltaY = currentDocPoint.Y - _resizeDragStartDoc.Y;

        float rawDeltaW = 0f, rawDeltaH = 0f;

        switch (_activeHandle)
        {
            case ResizeHandleType.TopLeft: rawDeltaW = -deltaX; rawDeltaH = -deltaY; break;
            case ResizeHandleType.TopCenter: rawDeltaH = -deltaY; break;
            case ResizeHandleType.TopRight: rawDeltaW = deltaX; rawDeltaH = -deltaY; break;
            case ResizeHandleType.MiddleRight: rawDeltaW = deltaX; break;
            case ResizeHandleType.BottomRight: rawDeltaW = deltaX; rawDeltaH = deltaY; break;
            case ResizeHandleType.BottomCenter: rawDeltaH = deltaY; break;
            case ResizeHandleType.BottomLeft: rawDeltaW = -deltaX; rawDeltaH = deltaY; break;
            case ResizeHandleType.MiddleLeft: rawDeltaW = -deltaX; break;
        }

        bool isCorner =
            _activeHandle == ResizeHandleType.TopLeft ||
            _activeHandle == ResizeHandleType.TopRight ||
            _activeHandle == ResizeHandleType.BottomRight ||
            _activeHandle == ResizeHandleType.BottomLeft;

        float newW, newH;

        if (isCorner && _resizeOriginalWidth > 0)
        {
            // Maintain aspect ratio: scale driven by the larger absolute delta
            float scaleFromW = (_resizeOriginalWidth + rawDeltaW) / _resizeOriginalWidth;
            float scaleFromH = (_resizeOriginalHeight + rawDeltaH) / _resizeOriginalHeight;
            float scale = (Math.Abs(rawDeltaW) >= Math.Abs(rawDeltaH)) ? scaleFromW : scaleFromH;
            newW = _resizeOriginalWidth * scale;
            newH = _resizeOriginalHeight * scale;
        }
        else
        {
            newW = _resizeOriginalWidth + rawDeltaW;
            newH = _resizeOriginalHeight + rawDeltaH;
        }

        // Enforce a sensible minimum size
        const float MinSize = 10f;
        return (Math.Max(newW, MinSize), Math.Max(newH, MinSize));
    }

    /// <summary>
    /// Applies the resize delta to <paramref name="image"/> based on the active handle and
    /// the current drag position in document coordinates.
    /// </summary>
    private void ApplyResizeDelta(InlineImage image, SKPoint currentDocPoint)
    {
        var (newW, newH) = ComputeResizeDimensions(currentDocPoint);
        image.Width = newW;
        image.Height = newH;

        // Force RTK to re-run the layout so text reflows around the new size
        _document.ForceRelayout();
        Invalidate();
    }

    /// <summary>
    /// Applies the resize delta to a cell image. Forces the cell document to relayout so the
    /// row height is recalculated and propagated to the parent document.
    /// </summary>
    private void ApplyCellResizeDelta(InlineImage image, SKPoint currentDocPoint)
    {
        var (newW, newH) = ComputeResizeDimensions(currentDocPoint);
        image.Width = newW;
        image.Height = newH;

        // Force the cell doc to relayout (updates MeasuredHeight for the new image size)
        ActiveCellController!.ForceRelayout();
        // Propagate height change to table and parent document
        AfterCellEdit();
    }

    private void DrawResizeHandles(SKCanvas canvas, SKRect imageBounds)
    {
        // Lazy-initialise cached paints
        _selectionBorderPaint ??= new SKPaint
        {
            Color = new SKColor(0x29, 0x78, 0xFF),  // blue
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
        };
        _handleFillPaint ??= new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
        };
        _handleBorderPaint ??= new SKPaint
        {
            Color = new SKColor(0x29, 0x78, 0xFF),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
        };

        // Outline around the image
        canvas.DrawRect(imageBounds, _selectionBorderPaint);

        // Draw each of the 8 handles
        foreach (ResizeHandleType handle in Enum.GetValues<ResizeHandleType>())
        {
            if (handle == ResizeHandleType.None)
                continue;
            var rect = GetHandleRect(imageBounds, handle);
            canvas.DrawRect(rect, _handleFillPaint);
            canvas.DrawRect(rect, _handleBorderPaint);
        }
    }

    /// <summary>
    /// Called when the pointer moves (with no button pressed) so the cursor can be updated.
    /// </summary>
    public void HoverAt(Point point)
    {
        var offset = point.FromView(_viewModifier);
        if (Settings.ShowPageBreaks)
            offset = AdjustPointForPageBreaks(offset);
        var docPoint = new SKPoint(offset.X, offset.Y);

        // Check active cell image handles first
        if (_activeTable != null && ActiveCellController is { } hoverCellAcc)
        {
            var cellOrigin = GetActiveCellOrigin();
            var (_, cellLocalBounds) = hoverCellAcc.GetSelectedImageInfo();
            if (cellLocalBounds != default)
            {
                var docBounds = new SKRect(
                    cellOrigin.X + cellLocalBounds.Left,
                    cellOrigin.Y + cellLocalBounds.Top,
                    cellOrigin.X + cellLocalBounds.Right,
                    cellOrigin.Y + cellLocalBounds.Bottom);
                var handle = HitTestHandles(docPoint, docBounds);
                if (handle != ResizeHandleType.None)
                {
                    RequestCursorUpdate?.Invoke(CursorForHandle(handle));
                    return;
                }
            }
        }

        var (_, imageBounds) = GetSelectedImageInfo();
        if (imageBounds != default)
        {
            // Resize handle takes priority
            var handle = HitTestHandles(docPoint, imageBounds);
            if (handle != ResizeHandleType.None)
            {
                RequestCursorUpdate?.Invoke(CursorForHandle(handle));
                return;
            }

            // Image body → show move cursor
            if (imageBounds.Contains(docPoint))
            {
                RequestCursorUpdate?.Invoke(EditorCursor.Move);
                return;
            }
        }

        // --- Column divider hover ---
        var (hoverTable, hoverTableBounds, _) = FindTableAt(docPoint);
        if (hoverTable != null)
        {
            var tableLocal = new SKPoint(docPoint.X - hoverTableBounds.Left, docPoint.Y - hoverTableBounds.Top);
            float tolerance = Math.Max(hoverTable.BorderWidth / 2f + 3f, 4f);
            if (hoverTable.HitTestColumnDivider(tableLocal, tolerance) >= 0)
            {
                RequestCursorUpdate?.Invoke(EditorCursor.ResizeEW);
                return;
            }
        }

        RequestCursorUpdate?.Invoke(EditorCursor.Default);
    }

    /// <summary>
    /// Called when the primary pointer button is released.
    /// Commits a live resize or completes an image drag-move.
    /// </summary>
    public void PointerReleased(Point point)
    {
        // --- Commit image drag-move ---
        if (_isDraggingImage)
        {
            if (_imageDragDropIndex >= 0)
                MoveImageToPosition(_imageDragDropIndex);

            _isDraggingImage = false;
            _imageDragDropIndex = -1;
            Invalidate();
            return;
        }

        // Cancel potential drag that never exceeded the threshold (was just a click)
        if (_potentialImageDrag)
        {
            _potentialImageDrag = false;
            return;
        }

        // --- Commit column resize drag ---
        if (_draggingColDivider >= 0)
        {
            IsModified = true;
            _draggingColDivider = -1;
            _draggingColTable = null;
            return;
        }

        // --- Commit resize ---
        if (_activeHandle == ResizeHandleType.None)
            return;

        if (_resizingCellImage && ActiveCellController is { } commitAcc)
        {
            // The cell image dimensions were mutated in-place. Replace the image object in
            // the cell document so the resize gets a proper undo entry.
            commitAcc.CommitImageResize();
            AfterCellEdit();
        }
        else
        {
            // The InlineImage dimensions were mutated in-place during the drag.
            // Now replace the image in the document so the resize gets a proper undo entry.
            var (resizedImage, _) = GetSelectedImageInfo();
            if (resizedImage != null)
            {
                int imgDocIndex = _selection.Start;
                var newImage = new InlineImage(resizedImage.Image, resizedImage.Width, resizedImage.Height);
                _document.InsertInlineObject(this, _selection, newImage, _textStyle);

                // Re-select the image so the handles remain visible after the resize
                var imgRange = new TextRange(imgDocIndex, imgDocIndex + 1);
                SetSelection(imgRange);
                MoveCaret(imgRange.CaretPosition);

                PublishSelectionInfo();
            }
        }

        _resizingCellImage = false;
        _activeHandle = ResizeHandleType.None;
        Invalidate();
    }

    /// <summary>
    /// Returns the document-level code point index of an inline image whose start equals
    /// <paramref name="docIndex"/>, or null if there is no inline image there.
    /// </summary>
    private int? GetInlineImageDocIndex(int docIndex)
    {
        if (docIndex < 0)
            return null;

        foreach (var para in _document.GetPragraphs())
        {
            var tb = para.TextBlock;
            if (tb == null)
                continue;

            int paraStart = para.CodePointIndex;

            foreach (var line in tb.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.InlineObject &&
                        run.InlineObject is InlineImage &&
                        run.Start + paraStart == docIndex)
                    {
                        return docIndex;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Moves the currently selected inline image to the text position
    /// <paramref name="dropDocIndex"/> as a single undoable operation.
    /// </summary>
    private void MoveImageToPosition(int dropDocIndex)
    {
        var (image, _) = GetSelectedImageInfo();
        if (image == null)
            return;

        int sourceDocIndex = _selection.Start;

        // Dropping at the same position or immediately after it means no real move
        if (dropDocIndex == sourceDocIndex || dropDocIndex == sourceDocIndex + 1)
            return;

        // After we delete the source the indices past it shift by -1
        int insertIndex = dropDocIndex > sourceDocIndex ? dropDocIndex - 1 : dropDocIndex;

        var imageCopy = new InlineImage(image.Image, image.Width, image.Height);

        using (_document.BeginEditGroup("Move image"))
        {
            // Delete from source
            _document.ReplaceText(this, new TextRange(sourceDocIndex, sourceDocIndex + 1),
                                  null, EditSemantics.Typing);

            // Insert at target
            _document.InsertInlineObject(this, new TextRange(insertIndex), imageCopy, _textStyle);
        }

        // Keep the newly placed image selected so handles are immediately visible
        var imgRange = new TextRange(insertIndex, insertIndex + 1);
        SetSelection(imgRange);
        MoveCaret(imgRange.CaretPosition);
        PublishSelectionInfo();
    }

    #region Image Drag Visuals

    private static readonly float[] _ghostColorMatrix =
    {
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 0.45f, 0,   // 45 % opacity for ghost
    };

    private SKPaint? _ghostImagePaint;
    private SKPaint? _dropCursorPaint;

    private void DrawImageDragVisuals(SKCanvas canvas, InlineImage image, SKRect originalBounds)
    {
        // Lazy-initialise paints
        _ghostImagePaint ??= new SKPaint
        {
            ColorFilter = SKColorFilter.CreateColorMatrix(_ghostColorMatrix),
            IsAntialias = true,
        };
        _dropCursorPaint ??= new SKPaint
        {
            Color = new SKColor(0x29, 0x78, 0xFF),
            StrokeWidth = 2f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
        };

        // Draw a dashed border at the original image position to show its "slot"
        using var dashedPaint = new SKPaint
        {
            Color = new SKColor(0x29, 0x78, 0xFF, 0x80),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            PathEffect = SKPathEffect.CreateDash(new[] { 4f, 4f }, 0f),
            IsAntialias = true,
        };
        canvas.DrawRect(originalBounds, dashedPaint);

        // Ghost image follows cursor (top-left anchored to cursor with small offset)
        const float offset = 4f;
        var ghostRect = new SKRect(
            _imageDragCurrentDoc.X + offset,
            _imageDragCurrentDoc.Y + offset,
            _imageDragCurrentDoc.X + offset + image.Width,
            _imageDragCurrentDoc.Y + offset + image.Height);
        canvas.DrawImage(image.Image, ghostRect,
            new SKSamplingOptions(SKFilterMode.Linear), _ghostImagePaint);

        // Drop-position cursor — a blue vertical line with small horizontal serifs
        if (_imageDragDropIndex >= 0)
        {
            var caretInfo = _document.GetCaretInfo(new CaretPosition(_imageDragDropIndex));
            if (!caretInfo.IsNone)
            {
                float x = caretInfo.CaretRectangle.MidX;
                float y0 = caretInfo.CaretRectangle.Top - 4f;
                float y1 = caretInfo.CaretRectangle.Bottom + 4f;

                // Vertical bar
                canvas.DrawLine(x, y0, x, y1, _dropCursorPaint);
                // Top serif
                canvas.DrawLine(x - 3f, y0, x + 3f, y0, _dropCursorPaint);
                // Bottom serif
                canvas.DrawLine(x - 3f, y1, x + 3f, y1, _dropCursorPaint);
            }
        }
    }

    #endregion

    #endregion

    #region Table Helpers

    private CellController? ActiveCellController =>
        (_activeTable != null && _activeRow >= 0 && _activeCol >= 0)
            ? _activeTable.GetCell(_activeRow, _activeCol)?.Controller
            : null;

    /// <summary>
    /// When a multi-cell selection is active, selects all content in each selected cell
    /// and runs <paramref name="action"/> on it, then redraws.
    /// Returns <c>true</c> if the multi-cell path was taken (caller should return early).
    /// </summary>
    private bool ApplyToSelectedCells(Action<CellController> action)
    {
        if (!_isMultiCellSelecting || _activeTable == null) return false;
        foreach (var cc in _activeTable.GetSelectedCellControllers())
        {
            cc.SelectAll();
            action(cc);
        }
        AfterCellEdit();
        return true;
    }

    private (InlineTable? table, SKRect bounds, int docIndex) FindTableAt(SKPoint docPoint)
    {
        foreach (var para in _document.GetPragraphs())
        {
            var tb = para.TextBlock;
            if (tb == null) continue;
            int paraStart = para.CodePointIndex;

            foreach (var line in tb.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.InlineObject && run.InlineObject is InlineTable table)
                    {
                        float top = para.ContentYCoord + line.YCoord + line.BaseLine + run.Ascent;
                        var bounds = new SKRect(
                            para.ContentXCoord + run.XCoord,
                            top,
                            para.ContentXCoord + run.XCoord + run.Width,
                            top + table.Height);
                        if (bounds.Contains(docPoint))
                            return (table, bounds, run.Start + paraStart);
                    }
                }
            }
        }
        return (null, default, -1);
    }

    private SKRect GetTableBounds(int docIndex, InlineTable targetTable)
    {
        foreach (var para in _document.GetPragraphs())
        {
            var tb = para.TextBlock;
            if (tb == null) continue;
            int paraStart = para.CodePointIndex;

            foreach (var line in tb.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.InlineObject &&
                        run.InlineObject is InlineTable table &&
                        run.Start + paraStart == docIndex &&
                        ReferenceEquals(table, targetTable))
                    {
                        float top = para.ContentYCoord + line.YCoord + line.BaseLine + run.Ascent;
                        return new SKRect(
                            para.ContentXCoord + run.XCoord,
                            top,
                            para.ContentXCoord + run.XCoord + run.Width,
                            top + table.Height);
                    }
                }
            }
        }
        return default;
    }

    private void ActivateCell(InlineTable table, int tableDocIndex, int row, int col)
    {
        _activeTable = table;
        _activeTableDocIndex = tableDocIndex;
        _activeRow = row;
        _activeCol = col;
        // Set parent selection to the table's doc index so the table stays "selected"
        SetSelection(new TextRange(tableDocIndex));
    }

    private void DeactivateTable()
    {
        _activeTable?.ClearMultiSelection();
        _activeTable = null;
        _activeTableDocIndex = -1;
        _activeRow = -1;
        _activeCol = -1;
        _selAnchorRow = -1;
        _selAnchorCol = -1;
        _isMultiCellSelecting = false;
        _resizingCellImage = false;
        _activeHandle = ResizeHandleType.None;
        PublishSelectionInfo();
        Invalidate();
    }

    private SKPoint GetActiveCellOrigin()
    {
        var tableBounds = GetTableBounds(_activeTableDocIndex, _activeTable!);
        var cellBounds = _activeTable!.GetCellBounds(_activeRow, _activeCol);
        return new SKPoint(
            tableBounds.Left + cellBounds.Left + InlineTable._cellPadding,
            tableBounds.Top + cellBounds.Top + InlineTable._cellPadding);
    }

    private void AfterCellEdit()
    {
        IsModified = true;
        if (_activeTable != null)
            InvalidateTableTextBlock(_activeTable);
        _document.ForceRelayout();
        PublishSelectionInfo();
        Invalidate();
    }

    private void NavigateToNextCell(bool forward)
    {
        if (_activeTable == null) return;

        int rows = _activeTable.Rows;
        int cols = _activeTable.Cols;
        int r = _activeRow;
        int c = _activeCol;

        if (forward)
        {
            do
            {
                c++;
                if (c >= cols) { c = 0; r++; }
                if (r >= rows)
                {
                    _activeTable.AddRow();
                    rows = _activeTable.Rows;
                    r = rows - 1;
                    c = 0;
                    AfterCellEdit();
                    break;
                }
            } while (_activeTable.GetCell(r, c) == null); // skip covered cells
        }
        else
        {
            do
            {
                c--;
                if (c < 0) { c = cols - 1; r--; }
                if (r < 0) { r = 0; c = 0; break; } // stay at first primary cell
            } while (_activeTable.GetCell(r, c) == null);
        }

        _activeRow = r;
        _activeCol = c;
        ActiveCellController?.SelectNone();
        Invalidate();
    }

    private void DrawActiveCellOverlay(SKCanvas canvas)
    {
        if (_activeTable == null) return;
        if (_isMultiCellSelecting) return;

        var cc = ActiveCellController;
        if (cc == null) return;

        var tableBounds = GetTableBounds(_activeTableDocIndex, _activeTable);
        if (tableBounds == default) return;

        var cellBounds = _activeTable.GetCellBounds(_activeRow, _activeCol);
        var cellRect = new SKRect(
            tableBounds.Left + cellBounds.Left,
            tableBounds.Top + cellBounds.Top,
            tableBounds.Left + cellBounds.Right,
            tableBounds.Top + cellBounds.Bottom);
        var cellContentOrigin = new SKPoint(cellRect.Left + InlineTable._cellPadding, cellRect.Top + InlineTable._cellPadding);
        float contentHeight = cellRect.Height - 2 * InlineTable._cellPadding;

        // Repaint cell with selection highlight
        var cellSel = cc.Selection;
        var selOpts = cellSel.IsRange
            ? new TextPaintOptions { Selection = cellSel, SelectionColor = SelectionColor }
            : null;

        if (selOpts != null)
        {
            canvas.Save();
            canvas.ClipRect(cellRect);
            canvas.Translate(cellContentOrigin.X, cellContentOrigin.Y);
            cc.Paint(canvas, contentHeight, selOpts);
            canvas.Restore();
        }

        // Draw caret
        if (HasFocus)
        {
            canvas.Save();
            canvas.Translate(cellContentOrigin.X, cellContentOrigin.Y);
            cc.PaintCaret(canvas);
            canvas.Restore();
        }

        // Draw resize handles if a cell image is selected
        var (cellImage, cellImageLocalBounds) = cc.GetSelectedImageInfo();
        if (cellImage != null)
        {
            var imageDocBounds = new SKRect(
                cellContentOrigin.X + cellImageLocalBounds.Left,
                cellContentOrigin.Y + cellImageLocalBounds.Top,
                cellContentOrigin.X + cellImageLocalBounds.Right,
                cellContentOrigin.Y + cellImageLocalBounds.Bottom);
            DrawResizeHandles(canvas, imageDocBounds);
        }

        // Blue border around active cell
        _activeCellBorderPaint ??= new SKPaint
        {
            Color = new SKColor(0x29, 0x78, 0xFF),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
        };
        canvas.DrawRect(cellRect, _activeCellBorderPaint);
    }

    #endregion

    /// <inheritdoc/>
    public DocumentReader GetContentReader()
    {
        return new DocumentReader(_document);
    }

    private void PublishSelectionInfo()
    {
        if (OnNavigation == null) return;
        if (ActiveCellController is { } cc)
            OnNavigation(new NavigationInfo(cc.Selection, cc.TextStyle, cc.GetDocSelectionInfo()));
        else
            OnNavigation(new NavigationInfo(_selection, _textStyle, _document.GetSelectionInfo(_selection)));
    }

    private void CreateDefaultShortcuts()
    {
        _shortcuts.Map(new Shortcut(KeyCode.U, control: true), ApplyUnderline);
        _shortcuts.Map(new Shortcut(KeyCode.B, control: true), ApplyBold);
        _shortcuts.Map(new Shortcut(KeyCode.I, control: true), ApplyItalic);
        _shortcuts.Map(new Shortcut(KeyCode.Z, control: true), Undo);
        _shortcuts.Map(new Shortcut(KeyCode.Y, control: true), Redo);
        _shortcuts.Map(new Shortcut(KeyCode.V, control: true), Paste);
        _shortcuts.Map(new Shortcut(KeyCode.X, control: true), Cut);
        _shortcuts.Map(new Shortcut(KeyCode.C, control: true), Copy);
        _shortcuts.Map(new Shortcut(KeyCode.A, control: true), SelectAll);
        _shortcuts.Map(new Shortcut(KeyCode.Back), Backspace);
        _shortcuts.Map(new Shortcut(KeyCode.E, control: true), () => ApplyAlignment(TextAlignment.Center));
        _shortcuts.Map(new Shortcut(KeyCode.L, control: true), () => ApplyAlignment(TextAlignment.Left));
        _shortcuts.Map(new Shortcut(KeyCode.R, control: true), () => ApplyAlignment(TextAlignment.Right));
        _shortcuts.Map(new Shortcut(KeyCode.J, control: true), () => ApplyAlignment(TextAlignment.Justified));

        _shortcuts.Map(new Shortcut(KeyCode.Enter), () =>
        {
            // On an empty list item, pressing Enter exits the list instead of adding another item
            var selInfo = _document.GetSelectionInfo(_selection);
            if (selInfo.ParagraphListType.GetValueOrDefault() != ListType.None &&
                _document.IsCurrentParagraphEmpty(_selection))
            {
                _document.SetListStyle(_selection, ListType.None);
                MoveCaret(_selection.CaretPosition);
                PublishSelectionInfo();
                return Task.CompletedTask;
            }
            _document.ReplaceText(this, _selection, "\u2029", EditSemantics.Typing, _textStyle);
            return Task.CompletedTask;
        });
        _shortcuts.Map(new Shortcut(KeyCode.Enter, shift: true), () => { Insert("\n"); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.Tab), () =>
        {
            if (_document.GetSelectionInfo(_selection).ParagraphListType.GetValueOrDefault() != ListType.None)
                ChangeListLevel(1);
            return Task.CompletedTask;
        });
        _shortcuts.Map(new Shortcut(KeyCode.Tab, shift: true), () =>
        {
            if (_document.GetSelectionInfo(_selection).ParagraphListType.GetValueOrDefault() != ListType.None)
                ChangeListLevel(-1);
            return Task.CompletedTask;
        });

        _shortcuts.Map(new Shortcut(KeyCode.Up), () => { Navigate(NavigationKind.LineUp, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Up, shift: true), () => { Navigate(NavigationKind.LineUp, true); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.Down), () => { Navigate(NavigationKind.LineDown, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Down, shift: true), () => { Navigate(NavigationKind.LineDown, true); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.Left), () => { Navigate(NavigationKind.CharacterLeft, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Left, shift: true), () => { Navigate(NavigationKind.CharacterLeft, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Left, control: true), () => { Navigate(NavigationKind.WordLeft, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Left, control: true, shift: true), () => { Navigate(NavigationKind.WordLeft, true); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.Right), () => { Navigate(NavigationKind.CharacterRight, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Right, shift: true), () => { Navigate(NavigationKind.CharacterRight, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Right, control: true), () => { Navigate(NavigationKind.WordRight, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Right, control: true, shift: true), () => { Navigate(NavigationKind.WordRight, true); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.PageUp), () => { Navigate(NavigationKind.PageUp, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.PageUp, shift: true), () => { Navigate(NavigationKind.PageUp, true); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.PageDown), () => { Navigate(NavigationKind.PageDown, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.PageDown, shift: true), () => { Navigate(NavigationKind.PageDown, true); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.Insert), () => { _overtype = !_overtype; return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Insert, shift: true), Copy);
        _shortcuts.Map(new Shortcut(KeyCode.Insert, control: true), Paste);

        _shortcuts.Map(new Shortcut(KeyCode.Delete), Delete);
        _shortcuts.Map(new Shortcut(KeyCode.Delete, shift: true), Cut);

        _shortcuts.Map(new Shortcut(KeyCode.End), () => { Navigate(NavigationKind.LineEnd, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.End, shift: true), () => { Navigate(NavigationKind.LineEnd, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.End, control: true), () => { Navigate(NavigationKind.DocumentEnd, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.End, control: true, shift: true), () => { Navigate(NavigationKind.DocumentEnd, true); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.Home), () => { Navigate(NavigationKind.LineHome, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Home, shift: true), () => { Navigate(NavigationKind.LineHome, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Home, control: true), () => { Navigate(NavigationKind.DocumentHome, false); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Home, control: true, shift: true), () => { Navigate(NavigationKind.DocumentHome, true); return Task.CompletedTask; });
    }

    private IStyle GetStyleAtCurrentPostion()
    {
        var style = _selection.IsRange && _selection.Start > _selection.End
            ? _document.GetStyleAtOffset(_selection.Start)
            : _document.GetStyleAtOffset(_selection.End);

        if (style.IsSame(_textStyle))
            return _textStyle;

        var result = _styleManager.FromStyle(style);

        return result;
    }

    private static bool IsLeftwardNavigation(NavigationKind kind)
    {
        return kind switch
        {
            NavigationKind.CharacterLeft or
            NavigationKind.WordLeft or
            NavigationKind.LineUp or
            NavigationKind.LineHome or
            NavigationKind.PageUp or
            NavigationKind.DocumentHome => true,
            _ => false,
        };
    }

    private void Invalidate()
    {
        RequestRedraw?.Invoke();
    }

    #region Fields

    /// <inheritdoc/>
    public Action? RequestRedraw { get; set; }

    /// <inheritdoc/>
    public Action<EditorCursor>? RequestCursorUpdate { get; set; }

    /// <inheritdoc/>
    public event Action<NavigationInfo>? OnNavigation;

    /// <inheritdoc/>
    public event Action<DocumentInfo>? OnContentSizeChanged;

    private readonly TextDocument _document;
    private readonly CaretController _caretView;
    private readonly ShortcutHandler _shortcuts;
    private readonly StyleManager _styleManager;
    private readonly IClipboardHandler _clipboard;

    private SKPaint _backgroundPaint;
    private SKPaint _canvasBackgroundPaint = new SKPaint { Color = new SKColor(0xE8, 0xE8, 0xE8) };
    private SKPaint _pageShadowPaint = new SKPaint { Color = new SKColor(0, 0, 0, 60) };
    private IStyle _textStyle;

    private bool _overtype = false;
    private float _lastHeight;
    private float? _ghostXCoord;
    private float _scrollScale = 20;

    private TextRange _selection;
    private TextRange _pendingSelection;
    private ViewModifier _viewModifier = new() { Offset = new Vector2(0, 0), Scale = new Vector2(1, 1) };
    private Vector2 _pendingContentOffset;
    private Rectangle _visibleBounds;

    // --- Active table/cell state ---
    private InlineTable? _activeTable = null;
    private int _activeTableDocIndex = -1;
    private int _activeRow = -1, _activeCol = -1;
    private SKPaint? _activeCellBorderPaint;
    private int _selAnchorRow = -1, _selAnchorCol = -1;
    private bool _isMultiCellSelecting = false;


    // --- Column resize drag state ---
    private InlineTable? _draggingColTable;
    private int _draggingColDivider = -1;  // index of inner divider; -1 = not dragging
    private float _draggingColStartX;
    private float _draggingColOrigWidth;       // original width of left column at drag start
    private float _draggingColRightOrigWidth;  // original width of right column at drag start
    private float _draggingColMinWidth;        // minimum allowable width of left column
    private float _draggingColRightMinWidth;   // minimum allowable width of right column

    // --- Image resize state ---
    private ResizeHandleType _activeHandle = ResizeHandleType.None;
    private bool _resizingCellImage = false;
    private SKPoint _resizeDragStartDoc;
    private float _resizeOriginalWidth;
    private float _resizeOriginalHeight;
    private SKPaint? _handleFillPaint;
    private SKPaint? _handleBorderPaint;
    private SKPaint? _selectionBorderPaint;

    private const float _handleSize = 8f;

    // --- Image drag-move state ---
    private bool _potentialImageDrag = false;
    private bool _isDraggingImage = false;
    private SKPoint _imageDragStartDoc;
    private SKPoint _imageDragCurrentDoc;
    private int _imageDragDropIndex = -1;

    private const float _dragThreshold = 5f;  // screen pixels before drag is committed

    #endregion
}