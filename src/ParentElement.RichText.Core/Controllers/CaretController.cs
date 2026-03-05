#nullable disable
using ParentElement.RichText.Core.Geometry;
using SkiaSharp;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ParentElement.RichText.Core.Controllers;

internal class CaretController
{
    private SKPaint _standardPaint;
    private SKPaint _italicPaint;
    private SKColor _color = SKColors.Black;
    private float _strokeWidth;
    private bool _italic;
    private bool _flash;
    private Timer _flashTimer;
    private Rectangle _visibleBounds;
    private Rectangle _frame;

    public float CaretWidth
    {
        get => _strokeWidth;
        set
        {
            if (_strokeWidth != value)
            {
                _strokeWidth = value;
                _italicPaint = null;
                Invalidate();
            }
        }
    }

    public SKColor Color
    {
        get => _color;
        set
        {
            if (_color == value)
            {
                _color = value;
                _italicPaint = null;
                Invalidate();
            }
        }
    }

    public bool Italic
    {
        get => _italic;
        set
        {
            if (_italic != value)
            {
                _italic = value;
                _frame = value ? _visibleBounds.Inflate(1, 0) : _visibleBounds;
                Invalidate();
            }
        }
    }

    public Rectangle VisibleBounds
    {
        get => _visibleBounds; set
        {
            _visibleBounds = value;
            _frame = value;
            ResetFlash();
        }
    }

    internal void ResetFlash()
    {
        if (!_flash)
        {
            _flash = true;
            Invalidate();
        }

        if (_flashTimer != null)
        {
            _flashTimer.Stop();
            _flashTimer.Start();
        }
    }

    internal Action RequestRedraw { get; set; }

    internal CaretController()
    {
        _flashTimer = new Timer(500);
        _flashTimer.Elapsed += OnFlashTimer;
        ResetFlash();
    }

    private void OnFlashTimer(object sender, ElapsedEventArgs e)
    {
        _flash = !_flash;
        Invalidate();
    }

    private void Invalidate()
    {
        RequestRedraw?.Invoke();
    }

    internal void Draw(SKCanvas canvas)
    {

        

        if (!_flash)
            return;

        // Vertical or italic?
        if (_italic)
        {
            if (_italicPaint == null)
            {
                _italicPaint = new SKPaint()
                {
                    Color = _color,
                    StrokeWidth = _strokeWidth,
                    IsAntialias = true
                };
            }

            canvas.DrawLine(_frame.TopRight.ToSkia(), _frame.BottomLeft.ToSkia(), _italicPaint);
        }
        else
        {
            if (_standardPaint == null)
            {
                _standardPaint = new SKPaint()
                {
                    Color = _color,
                    StrokeWidth = _strokeWidth,
                    IsAntialias = false,
                };
            }

            //Using SKIA to Draw because DrawingContext is different in Avalonia
            //as demonstrated here: https://youtu.be/qVEEWRdb_mE?t=323
            //canvas.DrawRect(rect.ToSkia(), _standardPaint);

            canvas.DrawRect(_frame.TopLeft.X, _frame.TopLeft.Y, _frame.Width, _frame.Height, _standardPaint);
        }
    }

    protected void OnFrameChanged()
    {
        // Caret moved so rest the flash timer to avoid flashing when moving
        ResetFlash();
    }

    internal void EnsureVisible() { }
}
