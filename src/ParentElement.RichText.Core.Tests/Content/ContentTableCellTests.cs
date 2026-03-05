using ParentElement.RichText.Core.Content;

namespace ParentElement.RichText.Core.Tests.Content;

public class ContentTableCellTests
{
    [Fact]
    public void DefaultRowSpan_IsOne()
    {
        var cell = new ContentTableCell();
        Assert.Equal(1, cell.RowSpan);
    }

    [Fact]
    public void DefaultColSpan_IsOne()
    {
        var cell = new ContentTableCell();
        Assert.Equal(1, cell.ColSpan);
    }

    [Fact]
    public void DefaultIsHeader_IsFalse()
    {
        var cell = new ContentTableCell();
        Assert.False(cell.IsHeader);
    }

    [Fact]
    public void DefaultBackgroundColor_IsNull()
    {
        var cell = new ContentTableCell();
        Assert.Null(cell.BackgroundColor);
    }

    [Fact]
    public void DefaultContent_IsNull()
    {
        var cell = new ContentTableCell();
        Assert.Null(cell.Content);
    }

    [Fact]
    public void RowSpan_CanBeSet()
    {
        var cell = new ContentTableCell { RowSpan = 3 };
        Assert.Equal(3, cell.RowSpan);
    }

    [Fact]
    public void ColSpan_CanBeSet()
    {
        var cell = new ContentTableCell { ColSpan = 2 };
        Assert.Equal(2, cell.ColSpan);
    }

    [Fact]
    public void IsHeader_CanBeSetToTrue()
    {
        var cell = new ContentTableCell { IsHeader = true };
        Assert.True(cell.IsHeader);
    }

    [Fact]
    public void BackgroundColor_CanBeSetToCssHex()
    {
        var cell = new ContentTableCell { BackgroundColor = "#C0C0C0" };
        Assert.Equal("#C0C0C0", cell.BackgroundColor);
    }

    [Fact]
    public void Content_CanBeSetToList()
    {
        var blocks = new List<ContentBlock> { new ContentBlock() };
        var cell = new ContentTableCell { Content = blocks };

        Assert.NotNull(cell.Content);
        Assert.Single(cell.Content);
    }
}
