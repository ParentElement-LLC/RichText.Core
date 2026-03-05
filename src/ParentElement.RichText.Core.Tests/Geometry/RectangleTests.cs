using ParentElement.RichText.Core.Geometry;

namespace ParentElement.RichText.Core.Tests.Geometry;

public class RectangleTests
{
    [Fact]
    public void Constructor_WithXYWidthHeight_SetsAllProperties()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);

        Assert.Equal(10f, rect.X);
        Assert.Equal(20f, rect.Y);
        Assert.Equal(100f, rect.Width);
        Assert.Equal(50f, rect.Height);
    }

    [Fact]
    public void Constructor_WithWidthHeight_SetsXYToZero()
    {
        var rect = new Rectangle(200f, 150f);

        Assert.Equal(0f, rect.X);
        Assert.Equal(0f, rect.Y);
        Assert.Equal(200f, rect.Width);
        Assert.Equal(150f, rect.Height);
    }

    [Fact]
    public void Constructor_CopyConstructor_CopiesAllValues()
    {
        var original = new Rectangle(5f, 10f, 80f, 60f);
        var copy = new Rectangle(original);

        Assert.Equal(original.X, copy.X);
        Assert.Equal(original.Y, copy.Y);
        Assert.Equal(original.Width, copy.Width);
        Assert.Equal(original.Height, copy.Height);
    }

    [Fact]
    public void Top_ReturnsY()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        Assert.Equal(20f, rect.Top);
    }

    [Fact]
    public void Bottom_ReturnsYPlusHeight()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        Assert.Equal(70f, rect.Bottom);
    }

    [Fact]
    public void Left_ReturnsX()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        Assert.Equal(10f, rect.Left);
    }

    [Fact]
    public void Right_ReturnsXPlusWidth()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        Assert.Equal(110f, rect.Right);
    }

    [Fact]
    public void TopLeft_ReturnsCorrectPoint()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var pt = rect.TopLeft;
        Assert.Equal(10f, pt.X);
        Assert.Equal(20f, pt.Y);
    }

    [Fact]
    public void TopRight_ReturnsCorrectPoint()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var pt = rect.TopRight;
        Assert.Equal(110f, pt.X);
        Assert.Equal(20f, pt.Y);
    }

    [Fact]
    public void BottomLeft_ReturnsCorrectPoint()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var pt = rect.BottomLeft;
        Assert.Equal(10f, pt.X);
        Assert.Equal(70f, pt.Y);
    }

    [Fact]
    public void BottomRight_ReturnsCorrectPoint()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var pt = rect.BottomRight;
        Assert.Equal(110f, pt.X);
        Assert.Equal(70f, pt.Y);
    }

    [Fact]
    public void Center_ReturnsCorrectPoint()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var center = rect.Center;
        Assert.Equal(60f, center.X);
        Assert.Equal(45f, center.Y);
    }

    [Fact]
    public void Inflate_WithUniformAmount_IncreasesWidthAndHeight()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var inflated = rect.Inflate(5f);

        Assert.Equal(10f, inflated.X);
        Assert.Equal(20f, inflated.Y);
        Assert.Equal(105f, inflated.Width);
        Assert.Equal(55f, inflated.Height);
    }

    [Fact]
    public void Inflate_WithSeparateAmounts_IncreasesWidthAndHeightIndependently()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var inflated = rect.Inflate(10f, 20f);

        Assert.Equal(110f, inflated.Width);
        Assert.Equal(70f, inflated.Height);
    }

    [Fact]
    public void Inflate_PreservesXAndY()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var inflated = rect.Inflate(10f, 20f);

        Assert.Equal(10f, inflated.X);
        Assert.Equal(20f, inflated.Y);
    }

    [Fact]
    public void Deflate_WithUniformAmount_DecreasesWidthAndHeight()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var deflated = rect.Deflate(5f);

        Assert.Equal(95f, deflated.Width);
        Assert.Equal(45f, deflated.Height);
    }

    [Fact]
    public void Deflate_WithSeparateAmounts_DecreasesWidthAndHeightIndependently()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var deflated = rect.Deflate(10f, 20f);

        Assert.Equal(90f, deflated.Width);
        Assert.Equal(30f, deflated.Height);
    }

    [Fact]
    public void Deflate_PreservesXAndY()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var deflated = rect.Deflate(10f, 20f);

        Assert.Equal(10f, deflated.X);
        Assert.Equal(20f, deflated.Y);
    }

    [Fact]
    public void Inflate_ThenDeflate_ReturnsOriginalSize()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var result = rect.Inflate(15f).Deflate(15f);

        Assert.Equal(rect.Width, result.Width);
        Assert.Equal(rect.Height, result.Height);
    }

    [Fact]
    public void ToSkia_ReturnsCorrectSKRect()
    {
        var rect = new Rectangle(10f, 20f, 100f, 50f);
        var skRect = rect.ToSkia();

        Assert.Equal(10f, skRect.Left);
        Assert.Equal(20f, skRect.Top);
        Assert.Equal(110f, skRect.Right);  // X + Width
        Assert.Equal(70f, skRect.Bottom);  // Y + Height
    }

    [Fact]
    public void ToSkia_ZeroRectangle_ReturnsZeroSKRect()
    {
        var rect = new Rectangle(0f, 0f, 0f, 0f);
        var skRect = rect.ToSkia();

        Assert.Equal(0f, skRect.Left);
        Assert.Equal(0f, skRect.Top);
        Assert.Equal(0f, skRect.Right);
        Assert.Equal(0f, skRect.Bottom);
    }
}
