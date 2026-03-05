using ParentElement.RichText.Core.Input;

namespace ParentElement.RichText.Core.Tests.Input;

public class KeyInfoTests
{
    [Fact]
    public void Constructor_SetsKeyCode()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: false);
        Assert.Equal(KeyCode.A, info.KeyCode);
    }

    [Fact]
    public void Constructor_SetsModifiers()
    {
        var info = new KeyInfo(KeyCode.C, shift: true, control: true, alt: false);

        Assert.True(info.Shift);
        Assert.True(info.Control);
        Assert.False(info.Alt);
    }

    [Fact]
    public void Constructor_PlainLetter_SetsCharacter()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: false);
        Assert.Equal('a', info.Character);
    }

    [Fact]
    public void Constructor_ShiftLetter_SetsUpperCaseCharacter()
    {
        var info = new KeyInfo(KeyCode.A, shift: true, control: false, alt: false);
        Assert.Equal('A', info.Character);
    }

    [Fact]
    public void Constructor_WithControl_CharacterIsNull()
    {
        var info = new KeyInfo(KeyCode.C, shift: false, control: true, alt: false);
        Assert.Null(info.Character);
    }

    [Fact]
    public void Constructor_WithAlt_CharacterIsNull()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: true);
        Assert.Null(info.Character);
    }

    [Fact]
    public void HasCommandModifiers_WithControl_ReturnsTrue()
    {
        var info = new KeyInfo(KeyCode.C, shift: false, control: true, alt: false);
        Assert.True(info.HasCommandModifiers);
    }

    [Fact]
    public void HasCommandModifiers_WithAlt_ReturnsTrue()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: true);
        Assert.True(info.HasCommandModifiers);
    }

    [Fact]
    public void HasCommandModifiers_WithOnlyShift_ReturnsFalse()
    {
        var info = new KeyInfo(KeyCode.A, shift: true, control: false, alt: false);
        Assert.False(info.HasCommandModifiers);
    }

    [Fact]
    public void HasCommandModifiers_NoModifiers_ReturnsFalse()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: false);
        Assert.False(info.HasCommandModifiers);
    }

    [Fact]
    public void HasModifiers_WithShift_ReturnsTrue()
    {
        var info = new KeyInfo(KeyCode.A, shift: true, control: false, alt: false);
        Assert.True(info.HasModifiers);
    }

    [Fact]
    public void HasModifiers_WithControl_ReturnsTrue()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: true, alt: false);
        Assert.True(info.HasModifiers);
    }

    [Fact]
    public void HasModifiers_WithAlt_ReturnsTrue()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: true);
        Assert.True(info.HasModifiers);
    }

    [Fact]
    public void HasModifiers_NoModifiers_ReturnsFalse()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: false);
        Assert.False(info.HasModifiers);
    }

    [Fact]
    public void IsPrintableCharacter_PlainLetter_ReturnsTrue()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: false);
        Assert.True(info.IsPrintableCharacter);
    }

    [Fact]
    public void IsPrintableCharacter_WithControl_ReturnsFalse()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: true, alt: false);
        Assert.False(info.IsPrintableCharacter);
    }

    [Fact]
    public void IsPrintableCharacter_WithAlt_ReturnsFalse()
    {
        var info = new KeyInfo(KeyCode.A, shift: false, control: false, alt: true);
        Assert.False(info.IsPrintableCharacter);
    }

    [Fact]
    public void IsPrintableCharacter_ModifierKey_ReturnsFalse()
    {
        var info = new KeyInfo(KeyCode.Shift, shift: true, control: false, alt: false);
        Assert.False(info.IsPrintableCharacter);
    }

    [Fact]
    public void IsPrintableCharacter_FunctionKey_ReturnsFalse()
    {
        var info = new KeyInfo(KeyCode.F1, shift: false, control: false, alt: false);
        Assert.False(info.IsPrintableCharacter);
    }

    [Fact]
    public void IsPrintableCharacter_Space_ReturnsTrue()
    {
        var info = new KeyInfo(KeyCode.Space, shift: false, control: false, alt: false);
        Assert.True(info.IsPrintableCharacter);
    }

    [Fact]
    public void AsShortcut_ReturnsShortcutWithMatchingProperties()
    {
        var info = new KeyInfo(KeyCode.C, shift: false, control: true, alt: false);
        var shortcut = info.AsShortcut();

        Assert.Equal(KeyCode.C, shortcut.BaseKey);
        Assert.True(shortcut.ControlKey);
        Assert.False(shortcut.ShiftKey);
        Assert.False(shortcut.AltKey);
    }

    [Fact]
    public void AsShortcut_WithAllModifiers_CopiesAllModifiers()
    {
        var info = new KeyInfo(KeyCode.Z, shift: true, control: true, alt: true);
        var shortcut = info.AsShortcut();

        Assert.Equal(KeyCode.Z, shortcut.BaseKey);
        Assert.True(shortcut.ShiftKey);
        Assert.True(shortcut.ControlKey);
        Assert.True(shortcut.AltKey);
    }
}
