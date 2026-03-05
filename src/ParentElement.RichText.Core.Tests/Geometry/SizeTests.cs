using ParentElement.RichText.Core.Geometry;

namespace ParentElement.RichText.Core.Tests.Geometry;

public class SizeTests
{
    [Fact]
    public void Constructor_SetsWidthAndHeight()
    {
        var size = new Size(640f, 480f);

        Assert.Equal(640f, size.Width);
        Assert.Equal(480f, size.Height);
    }

    [Fact]
    public void Constructor_WithZeroValues()
    {
        var size = new Size(0f, 0f);

        Assert.Equal(0f, size.Width);
        Assert.Equal(0f, size.Height);
    }

    [Fact]
    public void Constructor_WithNegativeValues()
    {
        var size = new Size(-100f, -200f);

        Assert.Equal(-100f, size.Width);
        Assert.Equal(-200f, size.Height);
    }

    [Fact]
    public void Constructor_WithDecimalValues()
    {
        var size = new Size(1.5f, 2.75f);

        Assert.Equal(1.5f, size.Width);
        Assert.Equal(2.75f, size.Height);
    }
}
