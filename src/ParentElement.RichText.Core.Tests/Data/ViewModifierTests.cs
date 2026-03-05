using System.Numerics;
using ParentElement.RichText.Core.Data;

namespace ParentElement.RichText.Core.Tests.Data;

public class ViewModifierTests
{
    [Fact]
    public void Scale_GetSet_Works()
    {
        var vm = new ViewModifier();
        vm.Scale = new Vector2(2f, 2f);

        Assert.Equal(new Vector2(2f, 2f), vm.Scale);
    }

    [Fact]
    public void Offset_GetSet_Works()
    {
        var vm = new ViewModifier();
        vm.Scale = new Vector2(1f, 1f);
        vm.Offset = new Vector2(50f, 100f);

        Assert.Equal(new Vector2(50f, 100f), vm.Offset);
    }

    [Fact]
    public void ScaledOffset_WithIdentityScale_EqualOffset()
    {
        var vm = new ViewModifier();
        vm.Scale = new Vector2(1f, 1f);
        vm.Offset = new Vector2(30f, 60f);

        Assert.Equal(30f, vm.ScaledOffset.X, 3);
        Assert.Equal(60f, vm.ScaledOffset.Y, 3);
    }

    [Fact]
    public void ScaledOffset_WithScale2_IsHalfOfOffset()
    {
        var vm = new ViewModifier();
        vm.Scale = new Vector2(2f, 2f);
        vm.Offset = new Vector2(40f, 80f);

        Assert.Equal(20f, vm.ScaledOffset.X, 3);
        Assert.Equal(40f, vm.ScaledOffset.Y, 3);
    }

    [Fact]
    public void ScaledOffset_UpdatesWhenScaleChanges()
    {
        var vm = new ViewModifier();
        vm.Scale = new Vector2(2f, 2f);
        vm.Offset = new Vector2(40f, 80f);

        // ScaledOffset = 40/2, 80/2 = 20, 40
        Assert.Equal(20f, vm.ScaledOffset.X, 3);

        // Change scale — ScaledOffset should update
        vm.Scale = new Vector2(4f, 4f);
        Assert.Equal(10f, vm.ScaledOffset.X, 3);
        Assert.Equal(20f, vm.ScaledOffset.Y, 3);
    }

    [Fact]
    public void ScaledOffset_UpdatesWhenOffsetChanges()
    {
        var vm = new ViewModifier();
        vm.Scale = new Vector2(2f, 2f);
        vm.Offset = new Vector2(20f, 40f);

        Assert.Equal(10f, vm.ScaledOffset.X, 3);

        vm.Offset = new Vector2(60f, 120f);
        Assert.Equal(30f, vm.ScaledOffset.X, 3);
        Assert.Equal(60f, vm.ScaledOffset.Y, 3);
    }

    [Fact]
    public void ScaledOffset_WithZeroOffset_IsZero()
    {
        var vm = new ViewModifier();
        vm.Scale = new Vector2(5f, 5f);
        vm.Offset = new Vector2(0f, 0f);

        Assert.Equal(0f, vm.ScaledOffset.X);
        Assert.Equal(0f, vm.ScaledOffset.Y);
    }
}
