namespace ParentElement.RichText.Core.Input;

public struct KeyInfo
{
    /// <summary>The logical key that was pressed.</summary>
    public readonly KeyCode KeyCode { get; }

    /// <summary>Whether the Shift modifier was held when the key was pressed.</summary>
    public readonly bool Shift { get; }

    /// <summary>Whether the Control modifier was held when the key was pressed.</summary>
    public readonly bool Control { get; }

    /// <summary>Whether the Alt modifier was held when the key was pressed.</summary>
    public readonly bool Alt { get; }

    /// <summary>The Unicode character produced by this key event, or <c>null</c> if the key produces no character (e.g. when Control or Alt is held).</summary>
    public readonly char? Character { get; }

    /// <summary>Whether Control or Alt is held, indicating a command shortcut rather than text input.</summary>
    public readonly bool HasCommandModifiers => Control || Alt;

    /// <summary>Whether any modifier key (Shift, Control, or Alt) is held.</summary>
    public readonly bool HasModifiers => Shift || Control || Alt;

    /// <summary>Whether this key event will produce a printable character (no Control or Alt held, and the key maps to a visible glyph).</summary>
    public readonly bool IsPrintableCharacter { get; }

    /// <summary>Initializes a new <see cref="KeyInfo"/> for the given key and modifier state.</summary>
    public KeyInfo(KeyCode keyCode, bool shift, bool control, bool alt)
    {
        KeyCode = keyCode;
        Shift = shift;
        Control = control;
        Alt = alt;
        Character = control || alt ? null : KeyMap.GetCharacter(keyCode, shift);
        IsPrintableCharacter =
            !control && !alt && !KeyMap.IsModifierKey(keyCode) && KeyMap.IsPrintableCharacter(keyCode);
    }

    /// <summary>Returns a <see cref="Shortcut"/> representing the same key and modifier combination as this event.</summary>
    public Shortcut AsShortcut()
    {
        return new Shortcut(KeyCode, Shift, Control, Alt);
    }
}
