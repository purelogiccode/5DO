using Avalonia.Input;
using FiveDo.Frontend.Avalonia.Views;

namespace FiveDo.Tests.Frontend;

public sealed class FullscreenShortcutTests
{
    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Alt, true)]
    [InlineData(Key.Enter, KeyModifiers.Alt | KeyModifiers.Shift, true)]
    [InlineData(Key.Enter, KeyModifiers.None, false)]
    [InlineData(Key.Enter, KeyModifiers.Control, false)]
    [InlineData(Key.F11, KeyModifiers.Alt, false)]
    [InlineData(Key.Escape, KeyModifiers.Alt, false)]
    public void IsFullscreenShortcut_RecognizesAltEnter(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.IsFullscreenShortcut(key, modifiers));
    }
}
