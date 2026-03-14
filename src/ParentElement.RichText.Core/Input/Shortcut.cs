namespace ParentElement.RichText.Core.Input;

/// <summary>Represents a keyboard shortcut as a base key combined with optional Shift, Control, and Alt modifiers.</summary>
public struct Shortcut
{
    /// <summary>Initializes a new <see cref="Shortcut"/> for the given base key and optional modifier flags.</summary>
    public Shortcut(KeyCode baseKey, bool shift = false, bool control = false, bool alt = false)
    {
        BaseKey = baseKey;
        ShiftKey = shift;
        ControlKey = control;
        AltKey = alt;
    }

    /// <summary>The primary key of the shortcut combination.</summary>
    public KeyCode BaseKey { get; set; }

    /// <summary>Whether the Control modifier is part of this shortcut.</summary>
    public bool ControlKey { get; set; }

    /// <summary>Whether the Shift modifier is part of this shortcut.</summary>
    public bool ShiftKey { get; set; }

    /// <summary>Whether the Alt modifier is part of this shortcut.</summary>
    public bool AltKey { get; set; }
}
