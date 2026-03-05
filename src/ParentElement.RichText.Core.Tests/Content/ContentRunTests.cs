using ParentElement.RichText.Core.Content;

namespace ParentElement.RichText.Core.Tests.Content;

public class ContentRunTests
{
    [Fact]
    public void IsImage_WhenImageDataIsNull_ReturnsFalse()
    {
        var run = new ContentRun { Text = "Hello", ImageData = null };

        Assert.False(run.IsImage);
    }

    [Fact]
    public void IsImage_WhenImageDataIsSet_ReturnsTrue()
    {
        var run = new ContentRun { ImageData = new byte[] { 1, 2, 3 } };

        Assert.True(run.IsImage);
    }

    [Fact]
    public void IsTable_WhenTableIsNull_ReturnsFalse()
    {
        var run = new ContentRun { Text = "Hello", Table = null };

        Assert.False(run.IsTable);
    }

    [Fact]
    public void IsTable_WhenTableIsSet_ReturnsTrue()
    {
        var run = new ContentRun { Table = new ContentTable() };

        Assert.True(run.IsTable);
    }

    [Fact]
    public void TextRun_IsNeitherImageNorTable()
    {
        var run = new ContentRun { Text = "Some text" };

        Assert.False(run.IsImage);
        Assert.False(run.IsTable);
    }

    [Fact]
    public void ImageRun_HasImageDimensions()
    {
        var run = new ContentRun
        {
            ImageData = new byte[] { 0xFF },
            ImageWidth = 200f,
            ImageHeight = 150f
        };

        Assert.Equal(200f, run.ImageWidth);
        Assert.Equal(150f, run.ImageHeight);
        Assert.True(run.IsImage);
    }

    [Fact]
    public void DefaultImageDimensions_AreZero()
    {
        var run = new ContentRun();

        Assert.Equal(0f, run.ImageWidth);
        Assert.Equal(0f, run.ImageHeight);
    }
}
