using ParentElement.RichText.Core.Input;

namespace ParentElement.RichText.Core.Tests.Input;

public class ShortcutTests
{
    [Fact]
    public void Constructor_SetsBaseKey()
    {
        var shortcut = new Shortcut(KeyCode.A);

        Assert.Equal(KeyCode.A, shortcut.BaseKey);
    }

    [Fact]
    public void Constructor_DefaultModifiers_AreFalse()
    {
        var shortcut = new Shortcut(KeyCode.S);

        Assert.False(shortcut.ControlKey);
        Assert.False(shortcut.ShiftKey);
        Assert.False(shortcut.AltKey);
    }

    [Fact]
    public void Constructor_WithControlKey()
    {
        var shortcut = new Shortcut(KeyCode.C, control: true);

        Assert.Equal(KeyCode.C, shortcut.BaseKey);
        Assert.True(shortcut.ControlKey);
        Assert.False(shortcut.ShiftKey);
        Assert.False(shortcut.AltKey);
    }

    [Fact]
    public void Constructor_WithShiftKey()
    {
        var shortcut = new Shortcut(KeyCode.Z, shift: true);

        Assert.True(shortcut.ShiftKey);
        Assert.False(shortcut.ControlKey);
        Assert.False(shortcut.AltKey);
    }

    [Fact]
    public void Constructor_WithAltKey()
    {
        var shortcut = new Shortcut(KeyCode.F4, alt: true);

        Assert.True(shortcut.AltKey);
        Assert.False(shortcut.ControlKey);
        Assert.False(shortcut.ShiftKey);
    }

    [Fact]
    public void Constructor_WithAllModifiers()
    {
        var shortcut = new Shortcut(KeyCode.Delete, shift: true, control: true, alt: true);

        Assert.True(shortcut.ShiftKey);
        Assert.True(shortcut.ControlKey);
        Assert.True(shortcut.AltKey);
    }

    [Fact]
    public void Shortcut_UsableAsDictionaryKey_SameValues_AreEqual()
    {
        var s1 = new Shortcut(KeyCode.C, control: true);
        var s2 = new Shortcut(KeyCode.C, control: true);
        var dict = new Dictionary<Shortcut, string>();

        dict[s1] = "copy";

        Assert.True(dict.ContainsKey(s2));
    }

    [Fact]
    public void Shortcut_UsableAsDictionaryKey_DifferentModifiers_AreNotEqual()
    {
        var s1 = new Shortcut(KeyCode.C, control: true);
        var s2 = new Shortcut(KeyCode.C, shift: true);
        var dict = new Dictionary<Shortcut, string>();

        dict[s1] = "copy";

        Assert.False(dict.ContainsKey(s2));
    }

    [Fact]
    public void Shortcut_UsableAsDictionaryKey_DifferentKeys_AreNotEqual()
    {
        var s1 = new Shortcut(KeyCode.C, control: true);
        var s2 = new Shortcut(KeyCode.V, control: true);
        var dict = new Dictionary<Shortcut, string>();

        dict[s1] = "copy";

        Assert.False(dict.ContainsKey(s2));
    }
}
