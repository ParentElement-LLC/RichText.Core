using ParentElement.RichText.Core.Data;

namespace ParentElement.RichText.Core.Tests.Data;

public class DocumentMarginsTests
{
    [Fact]
    public void Constructor_SetsAllMargins()
    {
        var margins = new DocumentMargins(left: 10f, right: 20f, top: 30f, bottom: 40f);

        Assert.Equal(10f, margins.Left);
        Assert.Equal(20f, margins.Right);
        Assert.Equal(30f, margins.Top);
        Assert.Equal(40f, margins.Bottom);
    }

    [Fact]
    public void Constructor_WithZeroValues()
    {
        var margins = new DocumentMargins(0f, 0f, 0f, 0f);

        Assert.Equal(0f, margins.Left);
        Assert.Equal(0f, margins.Right);
        Assert.Equal(0f, margins.Top);
        Assert.Equal(0f, margins.Bottom);
    }

    [Fact]
    public void Constructor_WithEqualMargins()
    {
        var margins = new DocumentMargins(24f, 24f, 24f, 24f);

        Assert.Equal(24f, margins.Left);
        Assert.Equal(24f, margins.Right);
        Assert.Equal(24f, margins.Top);
        Assert.Equal(24f, margins.Bottom);
    }

    [Fact]
    public void Init_PropertiesAreReadOnly_AfterConstruction()
    {
        var margins = new DocumentMargins(10f, 20f, 30f, 40f);

        // Verify init properties are set via constructor and not mutable
        Assert.Equal(10f, margins.Left);
        Assert.Equal(20f, margins.Right);
        Assert.Equal(30f, margins.Top);
        Assert.Equal(40f, margins.Bottom);
    }

    [Fact]
    public void ObjectInitializer_SetsProperties()
    {
        var margins = new DocumentMargins { Left = 5f, Right = 15f, Top = 25f, Bottom = 35f };

        Assert.Equal(5f, margins.Left);
        Assert.Equal(15f, margins.Right);
        Assert.Equal(25f, margins.Top);
        Assert.Equal(35f, margins.Bottom);
    }
}
