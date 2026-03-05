using System.Numerics;
using ParentElement.RichText.Core.Data;
using ParentElement.RichText.Core.Geometry;

namespace ParentElement.RichText.Core.Tests.Geometry;

public class PointTests
{
    [Fact]
    public void Constructor_SetsXAndY()
    {
        var point = new Point(3.5f, 7.2f);

        Assert.Equal(3.5f, point.X);
        Assert.Equal(7.2f, point.Y);
    }

    [Fact]
    public void Constructor_WithZeroValues()
    {
        var point = new Point(0f, 0f);

        Assert.Equal(0f, point.X);
        Assert.Equal(0f, point.Y);
    }

    [Fact]
    public void Constructor_WithNegativeValues()
    {
        var point = new Point(-10f, -20f);

        Assert.Equal(-10f, point.X);
        Assert.Equal(-20f, point.Y);
    }

    [Fact]
    public void ToSkia_ReturnsCorrectSKPoint()
    {
        var point = new Point(5f, 10f);
        var skPoint = point.ToSkia();

        Assert.Equal(5f, skPoint.X);
        Assert.Equal(10f, skPoint.Y);
    }

    [Fact]
    public void ToSkia_ZeroPoint_ReturnsZeroSKPoint()
    {
        var point = new Point(0f, 0f);
        var skPoint = point.ToSkia();

        Assert.Equal(0f, skPoint.X);
        Assert.Equal(0f, skPoint.Y);
    }

    [Fact]
    public void FromView_WithIdentityScale_ReturnsTranslatedPoint()
    {
        var point = new Point(100f, 200f);
        var view = new ViewModifier
        {
            Scale = new Vector2(1f, 1f),
            Offset = new Vector2(0f, 0f)
        };

        var result = point.FromView(view);

        Assert.Equal(100f, result.X, 3);
        Assert.Equal(200f, result.Y, 3);
    }

    [Fact]
    public void FromView_WithScale_DividesCoordinates()
    {
        var point = new Point(200f, 400f);
        var view = new ViewModifier
        {
            Scale = new Vector2(2f, 2f),
            Offset = new Vector2(0f, 0f)
        };

        var result = point.FromView(view);

        Assert.Equal(100f, result.X, 3);
        Assert.Equal(200f, result.Y, 3);
    }

    [Fact]
    public void FromView_WithOffset_AppliesScaledOffset()
    {
        var point = new Point(100f, 100f);
        var view = new ViewModifier
        {
            Scale = new Vector2(2f, 2f),
            Offset = new Vector2(20f, 40f)
        };

        // ScaledOffset = offset / scale = (20/2, 40/2) = (10, 20)
        // result.X = point.X / scale.X + scaledOffset.X = 100/2 + 10 = 60
        // result.Y = point.Y / scale.X + scaledOffset.Y = 100/2 + 20 = 70
        var result = point.FromView(view);

        Assert.Equal(60f, result.X, 3);
        Assert.Equal(70f, result.Y, 3);
    }

    [Fact]
    public void Properties_AreMutable()
    {
        var point = new Point(1f, 2f);
        point.X = 99f;
        point.Y = 88f;

        Assert.Equal(99f, point.X);
        Assert.Equal(88f, point.Y);
    }
}
