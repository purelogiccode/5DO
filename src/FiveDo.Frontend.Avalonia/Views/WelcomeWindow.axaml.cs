using Avalonia.Controls;
using Avalonia.Interactivity;
using FiveDo.Localization;
using FiveDo.Storage;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// First-run guide shown when no BIOS path has been configured. It explains the
/// user-provided BIOS/disc requirement and returns the next action. When discovery
/// already found a BIOS, the dialog offers it directly so a new user does not have to
/// navigate a file picker on first launch.
/// </summary>
public partial class WelcomeWindow : Window
{
    /// <summary>Creates the dialog without a discovered BIOS.</summary>
    public WelcomeWindow()
        : this(null)
    {
    }

    /// <summary>Creates the dialog, optionally offering a discovered BIOS.</summary>
    /// <param name="discoveredBios">A BIOS image found by discovery, or <see langword="null"/>.</param>
    public WelcomeWindow(BiosProfile? discoveredBios)
    {
        InitializeComponent();
        DiscoveredBios = discoveredBios;
        if (discoveredBios is not null)
        {
            DiscoveredBiosPanel.IsVisible = true;
            DiscoveredBiosText.Text = Localizer.Format(
                "ui.found_bios_on_this_computer",
                discoveredBios.Name,
                discoveredBios.RegionLabel,
                discoveredBios.FileName);
            Height = 460;
        }
    }

    /// <summary>Gets the action chosen by the user.</summary>
    public WelcomeAction Action { get; private set; } = WelcomeAction.Continue;

    /// <summary>Gets the discovered BIOS, when discovery found one.</summary>
    public BiosProfile? DiscoveredBios { get; }

    /// <summary>Gets the discovered BIOS path, when discovery found one.</summary>
    public string? DiscoveredBiosPath => DiscoveredBios?.Path;

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

    private void OnUseDiscoveredBios(object? sender, RoutedEventArgs e)
    {
        Action = WelcomeAction.UseDiscoveredBios;
        Close();
    }
}
