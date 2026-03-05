using ParentElement.RichText.Core.Input;

namespace ParentElement.RichText.Core.Tests.Input;

public class ShortcutHandlerTests
{
    [Fact]
    public async Task Execute_UnregisteredShortcut_ReturnsFalse()
    {
        var handler = new ShortcutHandler();
        var shortcut = new Shortcut(KeyCode.A, control: true);

        var result = await handler.Execute(shortcut);

        Assert.False(result);
    }

    [Fact]
    public async Task Execute_RegisteredShortcut_ReturnsTrue()
    {
        var handler = new ShortcutHandler();
        var shortcut = new Shortcut(KeyCode.C, control: true);
        handler.Map(shortcut, () => Task.CompletedTask);

        var result = await handler.Execute(shortcut);

        Assert.True(result);
    }

    [Fact]
    public async Task Execute_RegisteredShortcut_InvokesAction()
    {
        var handler = new ShortcutHandler();
        var shortcut = new Shortcut(KeyCode.V, control: true);
        var actionInvoked = false;

        handler.Map(shortcut, () =>
        {
            actionInvoked = true;
            return Task.CompletedTask;
        });

        await handler.Execute(shortcut);

        Assert.True(actionInvoked);
    }

    [Fact]
    public async Task Execute_DifferentShortcut_DoesNotInvokeAction()
    {
        var handler = new ShortcutHandler();
        var registered = new Shortcut(KeyCode.C, control: true);
        var executed = new Shortcut(KeyCode.V, control: true);
        var actionInvoked = false;

        handler.Map(registered, () =>
        {
            actionInvoked = true;
            return Task.CompletedTask;
        });

        await handler.Execute(executed);

        Assert.False(actionInvoked);
    }

    [Fact]
    public async Task Map_RemapsExistingShortcut_UsesNewAction()
    {
        var handler = new ShortcutHandler();
        var shortcut = new Shortcut(KeyCode.Z, control: true);
        var originalInvoked = false;
        var newInvoked = false;

        handler.Map(shortcut, () => { originalInvoked = true; return Task.CompletedTask; });
        handler.Map(shortcut, () => { newInvoked = true; return Task.CompletedTask; });

        await handler.Execute(shortcut);

        Assert.False(originalInvoked);
        Assert.True(newInvoked);
    }

    [Fact]
    public async Task Execute_MultipleShortcuts_InvokesCorrectAction()
    {
        var handler = new ShortcutHandler();
        var copy = new Shortcut(KeyCode.C, control: true);
        var paste = new Shortcut(KeyCode.V, control: true);
        var undo = new Shortcut(KeyCode.Z, control: true);

        var invoked = string.Empty;

        handler.Map(copy, () => { invoked = "copy"; return Task.CompletedTask; });
        handler.Map(paste, () => { invoked = "paste"; return Task.CompletedTask; });
        handler.Map(undo, () => { invoked = "undo"; return Task.CompletedTask; });

        await handler.Execute(paste);

        Assert.Equal("paste", invoked);
    }

    [Fact]
    public async Task Execute_AsyncAction_AwaitsCompletion()
    {
        var handler = new ShortcutHandler();
        var shortcut = new Shortcut(KeyCode.S, control: true);
        var completed = false;

        handler.Map(shortcut, async () =>
        {
            await Task.Delay(1);
            completed = true;
        });

        await handler.Execute(shortcut);

        Assert.True(completed);
    }

    [Fact]
    public async Task Map_ShortcutWithModifierCombination_WorksCorrectly()
    {
        var handler = new ShortcutHandler();
        var shortcut = new Shortcut(KeyCode.Z, shift: true, control: true);
        var invoked = false;

        handler.Map(shortcut, () => { invoked = true; return Task.CompletedTask; });

        // Non-matching shortcut (no shift)
        await handler.Execute(new Shortcut(KeyCode.Z, control: true));
        Assert.False(invoked);

        // Matching shortcut
        await handler.Execute(shortcut);
        Assert.True(invoked);
    }
}
