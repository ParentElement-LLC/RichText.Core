using ParentElement.RichText.Core.Content;

namespace ParentElement.RichText.Core.Tests.Content;

public class ContentTableTests
{
    [Fact]
    public void DefaultProperties_AreZeroOrNull()
    {
        var table = new ContentTable();

        Assert.Equal(0, table.Rows);
        Assert.Equal(0, table.Cols);
        Assert.Equal(0f, table.TotalWidth);
        Assert.Equal(0f, table.CellPadding);
        Assert.Equal(0f, table.BorderWidth);
        Assert.Null(table.ColWidths);
        Assert.Null(table.RowHeights);
        Assert.Null(table.BorderStyle);
        Assert.Null(table.BorderColor);
        Assert.Null(table.Cells);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var table = new ContentTable
        {
            Rows = 3,
            Cols = 4,
            TotalWidth = 600f,
            CellPadding = 4f,
            BorderStyle = "solid",
            BorderWidth = 1f,
            BorderColor = "#C0C0C0"
        };

        Assert.Equal(3, table.Rows);
        Assert.Equal(4, table.Cols);
        Assert.Equal(600f, table.TotalWidth);
        Assert.Equal(4f, table.CellPadding);
        Assert.Equal("solid", table.BorderStyle);
        Assert.Equal(1f, table.BorderWidth);
        Assert.Equal("#C0C0C0", table.BorderColor);
    }

    [Fact]
    public void ColWidths_CanBeSetToArray()
    {
        var widths = new float[] { 100f, 200f, 300f };
        var table = new ContentTable { ColWidths = widths };

        Assert.Equal(3, table.ColWidths.Length);
        Assert.Equal(100f, table.ColWidths[0]);
        Assert.Equal(200f, table.ColWidths[1]);
        Assert.Equal(300f, table.ColWidths[2]);
    }

    [Fact]
    public void RowHeights_CanBeSetToArray()
    {
        var heights = new float[] { 30f, 45f };
        var table = new ContentTable { RowHeights = heights };

        Assert.Equal(2, table.RowHeights.Length);
        Assert.Equal(30f, table.RowHeights[0]);
        Assert.Equal(45f, table.RowHeights[1]);
    }

    [Fact]
    public void Cells_CanBeSetToJaggedArray()
    {
        var cell = new ContentTableCell();
        var cells = new ContentTableCell[][]
        {
            new[] { cell, null! },
            new[] { null!, cell }
        };

        var table = new ContentTable { Cells = cells };

        Assert.Equal(2, table.Cells.Length);
        Assert.Equal(cell, table.Cells[0][0]);
        Assert.Null(table.Cells[0][1]);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("solid")]
    [InlineData("dashed")]
    [InlineData("dotted")]
    public void BorderStyle_AcceptsValidValues(string style)
    {
        var table = new ContentTable { BorderStyle = style };
        Assert.Equal(style, table.BorderStyle);
    }
}
