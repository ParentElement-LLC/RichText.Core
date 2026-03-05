using System.Numerics;

namespace ParentElement.RichText.Core.Data;

public struct ViewModifier
{
    private Vector2 _scale;
    private Vector2 _offset;
    private Vector2 _scaledOffset;

    /// <summary>Gets or sets the zoom scale applied to the view. Setting this automatically recomputes <see cref="ScaledOffset"/>.</summary>
    public Vector2 Scale {
        get => _scale;
        set
        {
            _scale = value;
            _scaledOffset = _offset / _scale;
        }
    }

    /// <summary>Gets or sets the pixel offset of the viewport. Setting this automatically recomputes <see cref="ScaledOffset"/>.</summary>
    public Vector2 Offset {
        get => _offset;
        set
        {
            _offset = value;
            _scaledOffset = _offset / _scale;
        }
    }

    /// <summary>Gets the offset pre-divided by the current scale, used to efficiently transform pointer coordinates to document space.</summary>
    public Vector2 ScaledOffset { get => _scaledOffset; }
}
