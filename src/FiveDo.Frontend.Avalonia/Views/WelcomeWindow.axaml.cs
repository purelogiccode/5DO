using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FiveDo.Host;
using FiveDo.Localization;
using FiveDo.Storage;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// First-run guide shown when no BIOS path has been configured. It explains the
/// user-provided BIOS/disc requirement and returns the next action. When discovery
/// already found a BIOS, the dialog offers it directly so a new user does not have to
/// navigate a file picker on first launch. Steam Deck sessions get a larger layout and
/// include the BIOS folder plus a re-scan action so the dialog is usable without a
/// keyboard.
/// </summary>
public partial class WelcomeWindow : Window
{
    private readonly bool _deckLayout;
    private readonly Func<BiosDiscoveryResult?>? _rescan;

    /// <summary>Creates the dialog without a discovered BIOS.</summary>
    public WelcomeWindow()
        : this(discovery: null, deckLayout: false, rescan: null)
    {
    }

    /// <summary>Creates the dialog, optionally offering a discovered BIOS.</summary>
    /// <param name="discoveredBios">A BIOS image found by discovery, or <see langword="null"/>.</param>
    public WelcomeWindow(BiosProfile? discoveredBios)
        : this(
            discoveredBios is null ? null : new BiosDiscoveryResult([], discoveredBios),
            deckLayout: false,
            rescan: null)
    {
    }

    /// <summary>Creates the dialog with a full discovery result and optional re-scan.</summary>
    /// <param name="discovery">The discovery result, or <see langword="null"/>.</param>
    /// <param name="deckLayout">Whether to use the larger Deck layout.</param>
    /// <param name="rescan">Optional callback that runs discovery again.</param>
    public WelcomeWindow(
        BiosDiscoveryResult? discovery,
        bool deckLayout,
        Func<BiosDiscoveryResult?>? rescan)
    {
        InitializeComponent();
        _deckLayout = deckLayout;
        _rescan = rescan;
        ApplyLayout(deckLayout);
        ApplyDiscovery(discovery);
        string? biosFolder = FindBiosFolder(discovery);
        if (!string.IsNullOrWhiteSpace(biosFolder))
        {
            BiosFolderPanel.IsVisible = true;
            BiosFolderText.Text = biosFolder;
        }
    }

    /// <summary>Gets the action chosen by the user.</summary>
    public WelcomeAction Action { get; private set; } = WelcomeAction.Continue;

    /// <summary>Gets the discovered BIOS, when discovery found one.</summary>
    public BiosProfile? DiscoveredBios { get; private set; }

    /// <summary>Gets the discovered BIOS path, when discovery found one.</summary>
    public string? DiscoveredBiosPath => DiscoveredBios?.Path;

    private void ApplyLayout(bool deckLayout)
    {
        WelcomeLayout layout = DeckMode.Welcome(deckLayout);
        Width = layout.Width;
        Height = layout.Height;
        HeaderText.FontSize = layout.HeaderFontSize;
        IntroText.FontSize = layout.BodyFontSize;
        DiscHelpText.FontSize = layout.BodyFontSize;
        Step1Text.FontSize = layout.BodyFontSize;
        Step2Text.FontSize = layout.BodyFontSize;
        Step3Text.FontSize = layout.BodyFontSize;
        ContentPanel.Spacing = deckLayout ? 16 : 12;
        ContentPanel.Width = Math.Max(0, layout.Width - 40);
        CanResize = deckLayout;
        // The content width is fixed to the designed Deck width. Do not let a resize
        // shrink the window below it, or the StackPanel would be clipped horizontally.
        MinWidth = deckLayout ? layout.Width : 0;
        MinHeight = deckLayout ? layout.Height : 0;

        foreach (Button button in new[]
        {
            ContinueButton,
            OpenBiosButton,
            OpenDiscButton,
            UseDiscoveredBiosButton,
            ScanButton,
            OpenBiosFolderButton,
        })
        {
            button.MinHeight = layout.ButtonHeight;
            button.FontSize = layout.ButtonFontSize;
        }
    }

    private void ApplyDiscovery(BiosDiscoveryResult? discovery)
    {
        DiscoveredBios = discovery?.PreferredPrimary;
        if (DiscoveredBios is null)
        {
            DiscoveredBiosPanel.IsVisible = false;
            DiscoveredBiosText.Text = string.Empty;
        }
        else
        {
            DiscoveredBiosPanel.IsVisible = true;
            DiscoveredBiosText.Text = Localizer.Format(
                "ui.found_bios_on_this_computer",
                DiscoveredBios.Name,
                DiscoveredBios.RegionLabel,
                DiscoveredBios.FileName);
        }

        if (!_deckLayout)
        {
            // The discovered-BIOS panel adds a button and a second text block. Keep
            // the fixed desktop layout tall enough instead of clipping the BIOS folder
            // panel at the bottom.
            Height = DiscoveredBios is null ? 500 : 620;
        }
    }

    private static string? FindBiosFolder(BiosDiscoveryResult? discovery)
    {
        try
        {
            BiosDiscoveryFolder? folder = discovery?.Folders.FirstOrDefault(
                item => string.Equals(item.Source, "5DO data folder", StringComparison.Ordinal));
            if (folder is not null)
            {
                return folder.Path;
            }

            return BiosDiscovery.DefaultFolders()
                .FirstOrDefault(item => string.Equals(item.Source, "5DO data folder", StringComparison.Ordinal))
                ?.Path;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "The welcome guide could not resolve the BIOS folder.");
            return null;
        }
    }

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

    private async void OnScan(object? sender, RoutedEventArgs e)
    {
        if (_rescan is null)
        {
            return;
        }

        ScanButton.IsEnabled = false;
        try
        {
            BiosDiscoveryResult? discovery = await Task.Run(_rescan).ConfigureAwait(true);
            ApplyDiscovery(discovery);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "BIOS re-scan from the welcome guide failed.");
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    private void OnOpenBiosFolder(object? sender, RoutedEventArgs e)
    {
        var folder = BiosFolderText.Text;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "The BIOS folder could not be opened from the welcome guide.");
        }
    }
}
