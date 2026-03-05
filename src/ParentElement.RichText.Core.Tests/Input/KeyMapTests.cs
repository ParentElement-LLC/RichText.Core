using ParentElement.RichText.Core.Input;

namespace ParentElement.RichText.Core.Tests.Input;

public class KeyMapTests
{
    // IsPrintableCharacter tests

    [Theory]
    [InlineData(KeyCode.A)]
    [InlineData(KeyCode.M)]
    [InlineData(KeyCode.Z)]
    public void IsPrintableCharacter_LetterKeys_ReturnsTrue(KeyCode code)
    {
        Assert.True(KeyMap.IsPrintableCharacter(code));
    }

    [Theory]
    [InlineData(KeyCode.D0)]
    [InlineData(KeyCode.D5)]
    [InlineData(KeyCode.D9)]
    public void IsPrintableCharacter_DigitKeys_WithoutShift_ReturnsTrue(KeyCode code)
    {
        Assert.True(KeyMap.IsPrintableCharacter(code, shiftModifier: false));
    }

    [Theory]
    [InlineData(KeyCode.D0)]
    [InlineData(KeyCode.D5)]
    [InlineData(KeyCode.D9)]
    public void IsPrintableCharacter_DigitKeys_WithShift_ReturnsFalse(KeyCode code)
    {
        Assert.False(KeyMap.IsPrintableCharacter(code, shiftModifier: true));
    }

    [Theory]
    [InlineData(KeyCode.NumPad0)]
    [InlineData(KeyCode.NumPad5)]
    [InlineData(KeyCode.NumPad9)]
    public void IsPrintableCharacter_NumPadDigits_ReturnsTrue(KeyCode code)
    {
        Assert.True(KeyMap.IsPrintableCharacter(code));
    }

    [Fact]
    public void IsPrintableCharacter_Space_ReturnsTrue()
    {
        Assert.True(KeyMap.IsPrintableCharacter(KeyCode.Space));
    }

    [Theory]
    [InlineData(KeyCode.Enter)]
    [InlineData(KeyCode.Escape)]
    [InlineData(KeyCode.Tab)]
    [InlineData(KeyCode.Back)]
    [InlineData(KeyCode.Delete)]
    [InlineData(KeyCode.Up)]
    [InlineData(KeyCode.Down)]
    [InlineData(KeyCode.Left)]
    [InlineData(KeyCode.Right)]
    [InlineData(KeyCode.F1)]
    [InlineData(KeyCode.F12)]
    public void IsPrintableCharacter_ControlKeys_ReturnsFalse(KeyCode code)
    {
        Assert.False(KeyMap.IsPrintableCharacter(code));
    }

    [Theory]
    [InlineData(KeyCode.OemSemicolon)]
    [InlineData(KeyCode.OemPlus)]
    [InlineData(KeyCode.OemComma)]
    [InlineData(KeyCode.OemMinus)]
    [InlineData(KeyCode.OemPeriod)]
    [InlineData(KeyCode.OemQuestion)]
    [InlineData(KeyCode.OemTilde)]
    public void IsPrintableCharacter_OemKeys_ReturnsTrue(KeyCode code)
    {
        Assert.True(KeyMap.IsPrintableCharacter(code));
    }

    [Theory]
    [InlineData(KeyCode.Multiply)]
    [InlineData(KeyCode.Add)]
    [InlineData(KeyCode.Subtract)]
    [InlineData(KeyCode.Divide)]
    public void IsPrintableCharacter_NumPadOperators_ReturnsTrue(KeyCode code)
    {
        Assert.True(KeyMap.IsPrintableCharacter(code));
    }

    // GetCharacter tests

    [Theory]
    [InlineData(KeyCode.A, false, 'a')]
    [InlineData(KeyCode.A, true, 'A')]
    [InlineData(KeyCode.Z, false, 'z')]
    [InlineData(KeyCode.Z, true, 'Z')]
    public void GetCharacter_LetterKeys_ReturnsCorrectCase(KeyCode code, bool shift, char expected)
    {
        var result = KeyMap.GetCharacter(code, shift);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(KeyCode.D0, false, '0')]
    [InlineData(KeyCode.D1, false, '1')]
    [InlineData(KeyCode.D2, false, '2')]
    [InlineData(KeyCode.D3, false, '3')]
    [InlineData(KeyCode.D4, false, '4')]
    [InlineData(KeyCode.D5, false, '5')]
    [InlineData(KeyCode.D6, false, '6')]
    [InlineData(KeyCode.D7, false, '7')]
    [InlineData(KeyCode.D8, false, '8')]
    [InlineData(KeyCode.D9, false, '9')]
    public void GetCharacter_DigitKeys_ReturnsDigit(KeyCode code, bool shift, char expected)
    {
        var result = KeyMap.GetCharacter(code, shift);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(KeyCode.D0)]
    [InlineData(KeyCode.D1)]
    [InlineData(KeyCode.D2)]
    [InlineData(KeyCode.D3)]
    [InlineData(KeyCode.D4)]
    [InlineData(KeyCode.D5)]
    [InlineData(KeyCode.D6)]
    [InlineData(KeyCode.D7)]
    [InlineData(KeyCode.D8)]
    [InlineData(KeyCode.D9)]
    public void GetCharacter_DigitKeys_WithShift_ReturnsNull(KeyCode code)
    {
        // IsPrintableCharacter returns false for digit keys when shift is held,
        // so GetCharacter returns null before consulting the alternate map.
        var result = KeyMap.GetCharacter(code, shift: true);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(KeyCode.NumPad0, '0')]
    [InlineData(KeyCode.NumPad5, '5')]
    [InlineData(KeyCode.NumPad9, '9')]
    public void GetCharacter_NumPadDigits_ReturnsDigit(KeyCode code, char expected)
    {
        var result = KeyMap.GetCharacter(code, shift: false);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(KeyCode.OemMinus, false, '-')]
    [InlineData(KeyCode.OemMinus, true, '_')]
    [InlineData(KeyCode.OemPlus, false, '=')]
    [InlineData(KeyCode.OemPlus, true, '+')]
    [InlineData(KeyCode.OemSemicolon, false, ';')]
    [InlineData(KeyCode.OemSemicolon, true, ':')]
    [InlineData(KeyCode.OemComma, false, ',')]
    [InlineData(KeyCode.OemComma, true, '<')]
    [InlineData(KeyCode.OemPeriod, false, '.')]
    [InlineData(KeyCode.OemPeriod, true, '>')]
    [InlineData(KeyCode.OemQuestion, false, '/')]
    [InlineData(KeyCode.OemQuestion, true, '?')]
    [InlineData(KeyCode.OemTilde, false, '`')]
    [InlineData(KeyCode.OemTilde, true, '~')]
    [InlineData(KeyCode.OemOpenBrackets, false, '[')]
    [InlineData(KeyCode.OemOpenBrackets, true, '{')]
    [InlineData(KeyCode.OemCloseBrackets, false, ']')]
    [InlineData(KeyCode.OemCloseBrackets, true, '}')]
    [InlineData(KeyCode.OemQuotes, false, '\'')]
    [InlineData(KeyCode.OemQuotes, true, '"')]
    public void GetCharacter_OemKeys_ReturnsCorrectCharacter(KeyCode code, bool shift, char expected)
    {
        var result = KeyMap.GetCharacter(code, shift);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(KeyCode.Multiply, '*')]
    [InlineData(KeyCode.Add, '+')]
    [InlineData(KeyCode.Subtract, '-')]
    [InlineData(KeyCode.Divide, '/')]
    [InlineData(KeyCode.Separator, '.')]
    public void GetCharacter_NumPadOperators_ReturnsCorrectChar(KeyCode code, char expected)
    {
        var result = KeyMap.GetCharacter(code, shift: false);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(KeyCode.Enter)]
    [InlineData(KeyCode.Escape)]
    [InlineData(KeyCode.Tab)]
    [InlineData(KeyCode.F1)]
    [InlineData(KeyCode.Up)]
    public void GetCharacter_NonPrintableKeys_ReturnsNull(KeyCode code)
    {
        var result = KeyMap.GetCharacter(code, shift: false);
        Assert.Null(result);
    }

    // IsModifierKey tests

    [Theory]
    [InlineData(KeyCode.Shift)]
    [InlineData(KeyCode.LShiftKey)]
    [InlineData(KeyCode.RShiftKey)]
    public void IsModifierKey_ShiftVariants_ReturnsTrue(KeyCode code)
    {
        Assert.True(KeyMap.IsModifierKey(code));
    }

    [Theory]
    [InlineData(KeyCode.Control)]
    [InlineData(KeyCode.LControlKey)]
    [InlineData(KeyCode.RControlKey)]
    public void IsModifierKey_ControlVariants_ReturnsTrue(KeyCode code)
    {
        Assert.True(KeyMap.IsModifierKey(code));
    }

    [Theory]
    [InlineData(KeyCode.Alt)]
    [InlineData(KeyCode.LAltKey)]
    [InlineData(KeyCode.RAltKey)]
    public void IsModifierKey_AltVariants_ReturnsTrue(KeyCode code)
    {
        Assert.True(KeyMap.IsModifierKey(code));
    }

    [Theory]
    [InlineData(KeyCode.A)]
    [InlineData(KeyCode.D1)]
    [InlineData(KeyCode.Space)]
    [InlineData(KeyCode.Enter)]
    [InlineData(KeyCode.F1)]
    [InlineData(KeyCode.OemSemicolon)]
    public void IsModifierKey_NonModifierKeys_ReturnsFalse(KeyCode code)
    {
        Assert.False(KeyMap.IsModifierKey(code));
    }
}
