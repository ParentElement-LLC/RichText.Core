using ParentElement.RichText.Core.Content;

namespace ParentElement.RichText.Core.Tests.Content;

public class ContentBlockTests
{
    [Fact]
    public void DefaultType_IsParagraph()
    {
        var block = new ContentBlock();
        Assert.Equal(ContentBlockType.Paragraph, block.Type);
    }

    [Fact]
    public void DefaultOptionalProperties_AreNull()
    {
        var block = new ContentBlock();

        Assert.Null(block.Alignment);
        Assert.Null(block.ListType);
        Assert.Null(block.ListLevel);
        Assert.Null(block.BlockIndent);
        Assert.Null(block.LineSpacing);
        Assert.Null(block.Runs);
    }

    [Fact]
    public void Runs_CanBeSet()
    {
        var runs = new List<ContentRun>
        {
            new ContentRun { Text = "Hello" },
            new ContentRun { Text = " World" }
        };

        var block = new ContentBlock { Runs = runs };

        Assert.Equal(2, block.Runs.Count());
    }

    [Fact]
    public void BlockIndent_CanBeSet()
    {
        var block = new ContentBlock { BlockIndent = 24f };
        Assert.Equal(24f, block.BlockIndent);
    }

    [Fact]
    public void LineSpacing_CanBeSet()
    {
        var block = new ContentBlock { LineSpacing = 1.5f };
        Assert.Equal(1.5f, block.LineSpacing);
    }

    [Fact]
    public void ListLevel_CanBeSet()
    {
        var block = new ContentBlock { ListLevel = 2 };
        Assert.Equal(2, block.ListLevel);
    }

    [Fact]
    public void Type_CanBeSet()
    {
        var block = new ContentBlock { Type = ContentBlockType.Paragraph };
        Assert.Equal(ContentBlockType.Paragraph, block.Type);
    }
}
