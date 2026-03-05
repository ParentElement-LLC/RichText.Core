using System.Globalization;

namespace ParentElement.RichText.Core.Input;

public static class KeyMap
{
    /// <summary>
    /// Returns <c>true</c> if the given <paramref name="code"/> can produce a visible character.
    /// Digit keys (D0–D9) are only considered printable when <paramref name="shiftModifier"/> is <c>false</c>.
    /// </summary>
    public static bool IsPrintableCharacter(KeyCode code, bool shiftModifier = false)
    {
        return
            (code >= KeyCode.A && code <= KeyCode.Z) ||
            (!shiftModifier && code >= KeyCode.D0 && code <= KeyCode.D9) ||
            (code >= KeyCode.NumPad0 && code <= KeyCode.NumPad9) ||
            (code >= KeyCode.Multiply && code <= KeyCode.Divide) ||
            (code >= KeyCode.OemSemicolon && code <= KeyCode.OemBackslash) ||
            code == KeyCode.Space;

    }

    /// <summary>
    /// Returns the Unicode character produced by the given <paramref name="keyCode"/> under the specified
    /// <paramref name="shift"/> and <paramref name="capsLock"/> state, or <c>null</c> if the key produces no character.
    /// </summary>
    public static char? GetCharacter(KeyCode keyCode, bool shift, bool capsLock = false)
    {
        if (!IsPrintableCharacter(keyCode, shift))
            return null;

        //If shift key is pressed, see if we're in the modified specials map.
        if (shift)
        {
            if (_alternateSpecialCharacterMap.ContainsKey(keyCode))
                return _alternateSpecialCharacterMap[keyCode];

            return capsLock ? keyCode.ToString().ToLower(CultureInfo.CurrentCulture)[0] : keyCode.ToString().ToUpper(CultureInfo.CurrentCulture)[0];
        }

        if (_specialCharacterMap.ContainsKey(keyCode))
            return _specialCharacterMap[keyCode];

        return capsLock ? keyCode.ToString().ToUpper(CultureInfo.CurrentCulture)[0] : keyCode.ToString().ToLower(CultureInfo.CurrentCulture)[0];
    }

    private static readonly Dictionary<KeyCode, char> _specialCharacterMap = new()
    {
        { KeyCode.D0, '0' },
        { KeyCode.D1, '1' },
        { KeyCode.D2, '2' },
        { KeyCode.D3, '3' },
        { KeyCode.D4, '4' },
        { KeyCode.D5, '5' },
        { KeyCode.D6, '6' },
        { KeyCode.D7, '7' },
        { KeyCode.D8, '8' },
        { KeyCode.D9, '9' },
        { KeyCode.NumPad0, '0' },
        { KeyCode.NumPad1, '1' },
        { KeyCode.NumPad2, '2' },
        { KeyCode.NumPad3, '3' },
        { KeyCode.NumPad4, '4' },
        { KeyCode.NumPad5, '5' },
        { KeyCode.NumPad6, '6' },
        { KeyCode.NumPad7, '7' },
        { KeyCode.NumPad8, '8' },
        { KeyCode.NumPad9, '9' },
        { KeyCode.Separator, '.' },
        { KeyCode.Divide, '/' },
        { KeyCode.Multiply, '*' },
        { KeyCode.Subtract, '-' },
        { KeyCode.Add, '+' },
        { KeyCode.OemMinus, '-' },
        { KeyCode.OemPlus, '=' },
        { KeyCode.OemOpenBrackets, '['},
        { KeyCode.OemCloseBrackets, ']'},
        { KeyCode.OemBackslash, '\\'},
        { KeyCode.OemTilde, '`'},
        { KeyCode.OemSemicolon, ';'},
        { KeyCode.OemQuotes, '\''},
        { KeyCode.OemComma, ','},
        { KeyCode.OemPeriod, '.'},
        { KeyCode.OemQuestion, '/'},
        { KeyCode.OemPipe, '\\'}
    };

    private static readonly Dictionary<KeyCode, char> _alternateSpecialCharacterMap = new()
    {
        { KeyCode.OemMinus, '_' },
        { KeyCode.OemPlus, '+' },
        { KeyCode.OemOpenBrackets, '{'},
        { KeyCode.OemCloseBrackets, '}'},
        { KeyCode.OemBackslash, '|'},
        { KeyCode.OemTilde, '~'},
        { KeyCode.OemSemicolon, ':'},
        { KeyCode.OemQuotes, '"'},
        { KeyCode.OemComma, '<'},
        { KeyCode.OemPeriod, '>'},
        { KeyCode.OemQuestion, '?'},
        { KeyCode.OemPipe, '|'},
        { KeyCode.D0, ')' },
        { KeyCode.D1, '!' },
        { KeyCode.D2, '@' },
        { KeyCode.D3, '#' },
        { KeyCode.D4, '$' },
        { KeyCode.D5, '%' },
        { KeyCode.D6, '^' },
        { KeyCode.D7, '&' },
        { KeyCode.D8, '*' },
        { KeyCode.D9, '(' },
    };

    /// <summary>Returns <c>true</c> if the given <paramref name="key"/> is a modifier key (Shift, Control, or Alt in any variant).</summary>
    public static bool IsModifierKey(KeyCode key)
    {
        return key switch
        {
            KeyCode.Control or KeyCode.LControlKey or KeyCode.RControlKey => true,
            KeyCode.Shift or KeyCode.LShiftKey or KeyCode.RShiftKey => true,
            KeyCode.Alt or KeyCode.LAltKey or KeyCode.RAltKey => true,
            _ => false,
        };
    }
}
