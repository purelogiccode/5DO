using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FiveDo.Frontend.Avalonia.Views;

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
