namespace ParentElement.RichText.Core.Input;

public class ShortcutHandler
{
    private Dictionary<Shortcut, Func<Task>> _shortcuts = new Dictionary<Shortcut, Func<Task>>();

    /// <summary>
    /// Registers <paramref name="action"/> to be invoked when <paramref name="keyCombination"/> is executed.
    /// If the shortcut was already mapped, the previous action is replaced.
    /// </summary>
    public void Map(Shortcut keyCombination, Func<Task> action)
    {
        if (_shortcuts.ContainsKey(keyCombination))
            _shortcuts.Remove(keyCombination);

        _shortcuts.Add(keyCombination, action);
    }

    /// <summary>
    /// Executes the action registered for <paramref name="keyCombination"/>.
    /// Returns <c>true</c> if a matching shortcut was found and invoked, or <c>false</c> if no mapping exists.
    /// </summary>
    public async Task<bool> Execute(Shortcut keyCombination)
    {
        if(_shortcuts.ContainsKey(keyCombination))
        {
            var act = _shortcuts[keyCombination];
            await act();

            return true;
        }

        return false;
    }

}
