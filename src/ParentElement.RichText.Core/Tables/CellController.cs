using ParentElement.RichText.Core.Controllers;
using ParentElement.RichText.Core.Geometry;
using ParentElement.RichText.Core.Input;
using ParentElement.Topten.RichTextKit;
using ParentElement.Topten.RichTextKit.Editor;
using SkiaSharp;

namespace ParentElement.RichText.Core.Tables;

internal class CellController : ITextDocumentView, ICellWriter
{
    private readonly TextDocument _doc;
    private readonly CaretController _caret;
    private readonly StyleManager _styleManager;
    private readonly ShortcutHandler _shortcuts;

    private IStyle _textStyle;
    private TextRange _selection;
    private TextRange _pendingSelection;
    private float? _ghostXCoord;
    private float _cachedMinWidth = float.NaN;  // NaN = dirty; set by MinContentWidth

    public Action? Invalidate { get; set; }
    public Action? ContentChanged { get; set; }

    public TextRange Selection => _selection;
    internal IStyle TextStyle => _textStyle;
    internal SelectionInfo GetDocSelectionInfo() => _doc.GetSelectionInfo(_selection);

    public CellController(float cellContentWidth, IStyle defaultStyle, Action invalidate, Action contentChanged, SKColor? backgroundOverride = null)
    {
        Invalidate = invalidate;
        ContentChanged = contentChanged;

        _styleManager = new StyleManager();
        _styleManager.DefaultStyle = defaultStyle;
        _styleManager.CurrentStyle = defaultStyle;
        _textStyle = _styleManager.FromStyle(defaultStyle);

        if (backgroundOverride.HasValue)
        {
            _textStyle = _styleManager.Update(backgroundColor: backgroundOverride.Value);
            _styleManager.DefaultStyle = _textStyle;
        }

        _doc = new TextDocument();
        _doc.PageWidth = cellContentWidth;
        _doc.RegisterView(this);

        _caret = new CaretController();
        _caret.RequestRedraw = invalidate;

        _shortcuts = new ShortcutHandler();
        CreateShortcuts();

        MoveCaret(_selection.CaretPosition);
    }

    public void SetWidth(float w)
    {
        _doc.PageWidth = w;
        _doc.ForceRelayout();
    }

    public float MeasuredHeight => _doc.MeasuredHeight;

    /// <summary>
    /// Minimum document-content width (pixels) at which no word needs to be broken mid-word.
    /// Computed by temporarily laying out at width=1 and reading the widest line.
    /// Result is cached until content or style changes.
    /// </summary>
    internal float MinContentWidth
    {
        get
        {
            if (!float.IsNaN(_cachedMinWidth)) return _cachedMinWidth;

            float saved = _doc.PageWidth;
            _doc.PageWidth = 1f;
            // Trigger full layout at minimal width so TextBlock.MaxWidth is applied.
            _ = _doc.MeasuredHeight;

            float max = 0f;
            foreach (var para in _doc.GetPragraphs())
            {
                var tb = para.TextBlock;
                if (tb == null) continue;
                // ContentXCoord is the paragraph's left offset (indent, bullet margin, etc.)
                float w = para.ContentXCoord + tb.MeasuredWidth;
                if (w > max) max = w;
            }

            _doc.PageWidth = saved;
            // Document is now dirty and will re-layout at saved width on next paint.

            _cachedMinWidth = Math.Max(max, 0f);
            return _cachedMinWidth;
        }
    }

    public void Paint(SKCanvas canvas, float availableHeight, TextPaintOptions? opts)
        => _doc.Paint(canvas, 0, availableHeight, opts);

    public void PaintCaret(SKCanvas canvas)
        => _caret.Draw(canvas);

    public void Click(SKPoint cellLocal)
    {
        var pos = _doc.HitTest(cellLocal.X, cellLocal.Y);
        if (pos.IsNone) return;
        var ghostX = _ghostXCoord;
        _doc.Navigate(pos.CaretPosition, NavigationKind.None, float.MaxValue, ref ghostX);

        // Auto-select an inline image when the click lands on or adjacent to one
        int clickedIdx = pos.ClosestCodePointIndex;
        var imageIdx = GetInlineImageDocIndex(clickedIdx)
                    ?? GetInlineImageDocIndex(clickedIdx - 1);
        if (imageIdx.HasValue)
        {
            _selection = new TextRange(imageIdx.Value, imageIdx.Value + 1);
            MoveCaret(_selection.CaretPosition);
            return;
        }

        _selection = new TextRange(pos.CaretPosition);
        MoveCaret(pos.CaretPosition);
    }

    public void DragTo(SKPoint cellLocal)
    {
        var pos = _doc.HitTest(cellLocal.X, cellLocal.Y);
        if (pos.IsNone) return;
        _selection = new TextRange(_selection.Start, pos.ClosestCodePointIndex);
        _selection = _selection.Clamp(_doc.Length - 1);
        MoveCaret(pos.CaretPosition);
    }

    public void Insert(string text)
    {
        _doc.ReplaceText(this, _selection, text, EditSemantics.Typing, _textStyle);
    }

    public void Backspace()
    {
        if (!_selection.IsRange)
        {
            var extendTo = _doc.Navigate(_selection.CaretPosition, NavigationKind.CharacterLeft, 0, ref _ghostXCoord);
            _selection = TextRange.Union(_selection, new TextRange(extendTo));
            if (!_selection.IsRange) return;
        }
        _doc.ReplaceText(this, _selection, null, EditSemantics.Backspace);
    }

    public void Delete()
    {
        if (!_selection.IsRange)
        {
            var extendTo = _doc.Navigate(_selection.CaretPosition, NavigationKind.CharacterRight, 0, ref _ghostXCoord);
            _selection = TextRange.Union(_selection, new TextRange(extendTo));
            if (!_selection.IsRange) return;
        }
        _doc.ReplaceText(this, _selection, null, EditSemantics.ForwardDelete);
    }

    public void Navigate(NavigationKind kind, bool extend = false)
    {
        if (_selection.IsRange && !extend)
        {
            if (_selection.Start > _selection.End != IsLeftwardNavigation(kind))
                _selection = _selection.Reversed;
            if (kind == NavigationKind.CharacterLeft || kind == NavigationKind.CharacterRight)
                kind = NavigationKind.None;
        }

        var ghostXCoord = _ghostXCoord;
        var oldPos = _selection.CaretPosition;
        var newPos = _doc.Navigate(oldPos, kind, float.MaxValue, ref ghostXCoord);

        if (extend)
            _selection = new TextRange(_selection.Start, newPos.CodePointIndex, newPos.AltPosition);
        else
            _selection = new TextRange(newPos.CodePointIndex, newPos.CodePointIndex, newPos.AltPosition);

        _selection = _selection.Clamp(_doc.Length - 1);
        MoveCaret(newPos);
        _ghostXCoord = ghostXCoord;
        _caret.Italic = _textStyle.FontItalic ?? false;
    }

    public void ApplyStyle(IStyle style)
    {
        _textStyle = _styleManager.FromStyle(style);
        _caret.Italic = _textStyle.FontItalic ?? false;

        if (_selection.IsRange)
        {
            var prev = _selection;
            _doc.MergeStyles(this, _selection, style);
            _selection = prev;
            MoveCaret(_selection.CaretPosition);
        }
    }

    public void ApplyBold()
        => ApplyStyle(new Style { FontWeight = _textStyle.FontWeight == 400 ? 700 : 400 });

    public void ApplyItalic()
        => ApplyStyle(new Style { FontItalic = !_textStyle.FontItalic });

    public void ApplyUnderline()
        => ApplyStyle(new Style { Underline = _textStyle.Underline == UnderlineStyle.Solid ? UnderlineStyle.None : UnderlineStyle.Solid });

    public void ApplyStrikethrough()
        => ApplyStyle(new Style { StrikeThrough = _textStyle.StrikeThrough == StrikeThroughStyle.None ? StrikeThroughStyle.Solid : StrikeThroughStyle.None });

    public void ApplySubscript()
        => ApplyStyle(new Style { FontVariant = _textStyle.FontVariant == FontVariant.SubScript ? FontVariant.Normal : FontVariant.SubScript });

    public void ApplySuperscript()
        => ApplyStyle(new Style { FontVariant = _textStyle.FontVariant == FontVariant.SuperScript ? FontVariant.Normal : FontVariant.SuperScript });

    public void ApplyFontColor(SKColor color)
        => ApplyStyle(new Style { TextColor = color });

    public void ApplyBackgroundColor(SKColor color)
        => ApplyStyle(new Style { BackgroundColor = color });

    public void ApplyFontFamily(string fontFamily)
        => ApplyStyle(new Style { FontFamily = fontFamily });

    public void ApplyFontSize(int fontSize)
        => ApplyStyle(new Style { FontSize = fontSize });

    public void ApplyAlignment(TextAlignment alignment)
    {
        _doc.ToggleParagraphAlignment(_selection, alignment);
        MoveCaret(_selection.CaretPosition);
    }

    public void AdjustParagraphIndent(float amount)
    {
        _doc.AdjustParagraphIndent(_selection, amount);
        MoveCaret(_selection.CaretPosition);
    }

    public void ToggleBulletList()
    {
        var current = _doc.GetSelectionInfo(_selection).ParagraphListType ?? ListType.None;
        _doc.SetListStyle(_selection, current == ListType.Bullet ? ListType.None : ListType.Bullet);
        MoveCaret(_selection.CaretPosition);
    }

    public void ToggleNumberedList()
    {
        var current = _doc.GetSelectionInfo(_selection).ParagraphListType ?? ListType.None;
        _doc.SetListStyle(_selection, current == ListType.Numbered ? ListType.None : ListType.Numbered);
        MoveCaret(_selection.CaretPosition);
    }

    public void ChangeListLevel(int delta)
    {
        _doc.SetListLevel(_selection, delta);
        MoveCaret(_selection.CaretPosition);
    }

    public void AdjustLineSpacing(float amount)
    {
        _doc.SetLineSpacing(_selection, amount);
        MoveCaret(_selection.CaretPosition);
    }

    public void ApplyListFormat(ListType type, int level)
    {
        _doc.SetListStyleAndLevel(_selection, type, level);
        MoveCaret(_selection.CaretPosition);
    }

    public void SetBlockIndent(float value)
    {
        _doc.SetBlockIndent(_selection, value);
    }

    public void InsertInlineImage(SKImage image, float w, float h)
    {
        float contentWidth = _doc.PageWidth;
        if (w > contentWidth && contentWidth > 0)
        {
            var scale = contentWidth / w;
            w = contentWidth;
            h *= scale;
        }

        var inlineImage = new Images.InlineImage(image, w, h);
        _doc.InsertInlineObject(this, _selection, inlineImage, _textStyle);
    }

    public void Undo() => _doc.Undo(this);
    public void Redo() => _doc.Redo(this);

    public void ClearContent()
    {
        SelectAll();
        if (_selection.IsRange)
            _doc.ReplaceText(this, _selection, null, EditSemantics.ForwardDelete);
    }

    public void SelectAll()
    {
        _selection = new TextRange(0, _doc.Length - 1);
        _selection = _selection.Clamp(_doc.Length - 1);
        MoveCaret(_selection.CaretPosition);
        Invalidate?.Invoke();
    }

    public void SelectNone()
    {
        _selection = new TextRange(0);
        MoveCaret(_selection.CaretPosition);
        Invalidate?.Invoke();
    }

    public string GetSelectedText()
    {
        if (!_selection.IsRange) return string.Empty;
        return _doc.GetText(_selection).ToString();
    }

    /// <summary>
    /// Appends this cell's text content to <paramref name="target"/> as a new paragraph.
    /// </summary>
    internal void AppendTextTo(CellController target)
    {
        var all = new TextRange(0, _doc.Length - 1);
        if (!all.IsRange) return;
        var text = _doc.GetText(all).ToString();
        if (string.IsNullOrEmpty(text)) return;
        target.Navigate(NavigationKind.DocumentEnd);
        target.Insert("\u2029" + text);
    }

    public Task<bool> OnKeyEvent(KeyInfo key)
        => _shortcuts.Execute(key.AsShortcut());

    /// <summary>
    /// Forces the cell's internal document to re-run layout on the next access.
    /// Call this after mutating an inline object's dimensions in-place.
    /// </summary>
    internal void ForceRelayout() => _doc.ForceRelayout();

    /// <summary>Returns a <see cref="Content.DocumentReader"/> for this cell's content.</summary>
    internal Content.DocumentReader GetContentReader() => new Content.DocumentReader(_doc);

    /// <summary>
    /// If the current cell selection is a single inline image, returns that image and
    /// its bounds in cell-content-local coordinates.  Otherwise returns (null, default).
    /// </summary>
    internal (Images.InlineImage? image, SKRect cellLocalBounds) GetSelectedImageInfo()
    {
        if (!_selection.IsRange || _selection.Length != 1)
            return (null, default);

        int targetIdx = _selection.Start;

        foreach (var para in _doc.GetPragraphs())
        {
            var tb = para.TextBlock;
            if (tb == null) continue;

            int paraStart = para.CodePointIndex;

            foreach (var line in tb.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.InlineObject &&
                        run.InlineObject is Images.InlineImage img &&
                        run.Start + paraStart == targetIdx)
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

    /// <summary>
    /// Commits an in-place image resize as a proper undo entry by replacing the image
    /// object in the cell document.  The selection is updated to keep the image selected.
    /// </summary>
    internal void CommitImageResize()
    {
        var (img, _) = GetSelectedImageInfo();
        if (img == null) return;

        int imgDocIndex = _selection.Start;
        var newImage = new Images.InlineImage(img.Image, img.Width, img.Height);
        _doc.InsertInlineObject(this, _selection, newImage, _textStyle);

        var imgRange = new TextRange(imgDocIndex, imgDocIndex + 1);
        _selection = imgRange.Clamp(_doc.Length - 1);
        MoveCaret(_selection.CaretPosition);
    }

    private int? GetInlineImageDocIndex(int docIndex)
    {
        if (docIndex < 0) return null;

        foreach (var para in _doc.GetPragraphs())
        {
            var tb = para.TextBlock;
            if (tb == null) continue;

            int paraStart = para.CodePointIndex;

            foreach (var line in tb.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.InlineObject &&
                        run.InlineObject is Images.InlineImage &&
                        run.Start + paraStart == docIndex)
                        return docIndex;
                }
            }
        }

        return null;
    }

    #region ITextDocumentView

    void ITextDocumentView.OnReset() { }
    void ITextDocumentView.OnRedraw() { }

    void ITextDocumentView.OnDocumentWillChange(ITextDocumentView view)
    {
        _pendingSelection = _selection;
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
                    _pendingSelection = info.IsUndoing
                        ? new TextRange(info.CodePointIndex + info.NewLength)
                        : new TextRange(info.CodePointIndex);
                    break;
                case EditSemantics.ForwardDelete:
                    _pendingSelection = new TextRange(info.CodePointIndex);
                    break;
                case EditSemantics.Typing:
                    _pendingSelection = info.IsUndoing
                        ? new TextRange(info.CodePointIndex, info.CodePointIndex + info.NewLength)
                        : new TextRange(info.CodePointIndex + info.NewLength);
                    break;
                case EditSemantics.Overtype:
                    _pendingSelection = info.IsUndoing
                        ? new TextRange(info.CodePointIndex, info.CodePointIndex)
                        : new TextRange(info.CodePointIndex + info.NewLength);
                    break;
                case EditSemantics.ImeComposition:
                    _pendingSelection = info.IsUndoing
                        ? new TextRange(info.CodePointIndex, info.CodePointIndex + info.NewLength)
                        : new TextRange(info.CodePointIndex + info.ImeCaretOffset);
                    break;
            }
        }
        else
        {
            _pendingSelection = _pendingSelection.UpdateForEdit(info.CodePointIndex, info.OldLength, info.NewLength);
        }
    }

    void ITextDocumentView.OnDocumentDidChange(ITextDocumentView view)
    {
        _selection = _pendingSelection.Clamp(_doc.Length - 1);
        MoveCaret(_selection.CaretPosition);
        _cachedMinWidth = float.NaN;  // content or style changed; invalidate cached minimum width
        ContentChanged?.Invoke();
    }

    #endregion

    private void MoveCaret(CaretPosition pos)
    {
        var info = _doc.GetCaretInfo(pos);
        var rect = info.CaretRectangle;
        _caret.VisibleBounds = new Rectangle(rect.Left, rect.Top, 2, rect.Height);
        _caret.Italic = _textStyle.FontItalic ?? false;
        Invalidate?.Invoke();
    }

    private void CreateShortcuts()
    {
        _shortcuts.Map(new Shortcut(KeyCode.Back), () => { Backspace(); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Delete), () => { Delete(); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Enter), () =>
        {
            var selInfo = _doc.GetSelectionInfo(_selection);
            if (selInfo.ParagraphListType.GetValueOrDefault() != ListType.None &&
                _doc.IsCurrentParagraphEmpty(_selection))
            {
                _doc.SetListStyle(_selection, ListType.None);
                MoveCaret(_selection.CaretPosition);
            }
            else
            {
                _doc.ReplaceText(this, _selection, "\u2029", EditSemantics.Typing, _textStyle);
            }
            return Task.CompletedTask;
        });
        _shortcuts.Map(new Shortcut(KeyCode.Enter, shift: true), () => { Insert("\n"); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.B, control: true), () => { ApplyBold(); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.I, control: true), () => { ApplyItalic(); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.U, control: true), () => { ApplyUnderline(); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Z, control: true), () => { Undo(); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Y, control: true), () => { Redo(); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.A, control: true), () => { SelectAll(); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.E, control: true), () => { ApplyAlignment(TextAlignment.Center); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.L, control: true), () => { ApplyAlignment(TextAlignment.Left); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.R, control: true), () => { ApplyAlignment(TextAlignment.Right); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.J, control: true), () => { ApplyAlignment(TextAlignment.Justified); return Task.CompletedTask; });

        _shortcuts.Map(new Shortcut(KeyCode.Tab), () =>
        {
            if (_doc.GetSelectionInfo(_selection).ParagraphListType.GetValueOrDefault() != ListType.None)
                ChangeListLevel(1);
            return Task.CompletedTask;
        });
        _shortcuts.Map(new Shortcut(KeyCode.Tab, shift: true), () =>
        {
            if (_doc.GetSelectionInfo(_selection).ParagraphListType.GetValueOrDefault() != ListType.None)
                ChangeListLevel(-1);
            return Task.CompletedTask;
        });

        _shortcuts.Map(new Shortcut(KeyCode.Up), () => { Navigate(NavigationKind.LineUp); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Up, shift: true), () => { Navigate(NavigationKind.LineUp, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Down), () => { Navigate(NavigationKind.LineDown); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Down, shift: true), () => { Navigate(NavigationKind.LineDown, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Left), () => { Navigate(NavigationKind.CharacterLeft); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Left, shift: true), () => { Navigate(NavigationKind.CharacterLeft, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Left, control: true), () => { Navigate(NavigationKind.WordLeft); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Left, control: true, shift: true), () => { Navigate(NavigationKind.WordLeft, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Right), () => { Navigate(NavigationKind.CharacterRight); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Right, shift: true), () => { Navigate(NavigationKind.CharacterRight, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Right, control: true), () => { Navigate(NavigationKind.WordRight); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Right, control: true, shift: true), () => { Navigate(NavigationKind.WordRight, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Home), () => { Navigate(NavigationKind.LineHome); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Home, shift: true), () => { Navigate(NavigationKind.LineHome, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Home, control: true), () => { Navigate(NavigationKind.DocumentHome); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.Home, control: true, shift: true), () => { Navigate(NavigationKind.DocumentHome, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.End), () => { Navigate(NavigationKind.LineEnd); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.End, shift: true), () => { Navigate(NavigationKind.LineEnd, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.End, control: true), () => { Navigate(NavigationKind.DocumentEnd); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.End, control: true, shift: true), () => { Navigate(NavigationKind.DocumentEnd, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.PageUp), () => { Navigate(NavigationKind.PageUp); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.PageUp, shift: true), () => { Navigate(NavigationKind.PageUp, true); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.PageDown), () => { Navigate(NavigationKind.PageDown); return Task.CompletedTask; });
        _shortcuts.Map(new Shortcut(KeyCode.PageDown, shift: true), () => { Navigate(NavigationKind.PageDown, true); return Task.CompletedTask; });
    }

    private static bool IsLeftwardNavigation(NavigationKind kind) => kind switch
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
