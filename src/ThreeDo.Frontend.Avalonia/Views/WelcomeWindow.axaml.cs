using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ThreeDo.Frontend.Avalonia.Views;

/// <summary>The action chosen by the first-run welcome dialog.</summary>
public enum WelcomeAction
{
    /// <summary>Continue without opening a file.</summary>
    Continue,

    /// <summary>Open a BIOS ROM.</summary>
    OpenBios,

    /// <summary>Open a disc image.</summary>
    OpenDisc,
}

/// <summary>
/// First-run guide shown when no BIOS path has been configured. It explains the
/// user-provided BIOS/disc requirement and returns the next action.
/// </summary>
public partial class WelcomeWindow : Window
{
    /// <summary>Creates the dialog.</summary>
    public WelcomeWindow()
    {
        InitializeComponent();
    }

    /// <summary>Gets the action chosen by the user.</summary>
    public WelcomeAction Action { get; private set; } = WelcomeAction.Continue;

    private void OnContinue(object? sender, RoutedEventArgs e)
    {
        Action = WelcomeAction.Continue;
        Close();
    }

    private void OnOpenBios(object? sender, RoutedEventArgs e)
    {
        Action = WelcomeAction.OpenBios;
        Close();
    }

    private void OnOpenDisc(object? sender, RoutedEventArgs e)
    {
        Action = WelcomeAction.OpenDisc;
        Close();
    }
}
