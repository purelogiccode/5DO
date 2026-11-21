using System;

namespace FiveDo.Frontend.Avalonia;

/// <summary>Dialog dimensions and text sizes used by the first-run welcome guide.</summary>
/// <param name="Width">Window width.</param>
/// <param name="Height">Window height.</param>
/// <param name="HeaderFontSize">Welcome heading size.</param>
/// <param name="BodyFontSize">Body and action label size.</param>
/// <param name="ButtonHeight">Minimum action-button height.</param>
/// <param name="ButtonFontSize">Action-button label size.</param>
internal sealed record WelcomeLayout(
    double Width,
    double Height,
    double HeaderFontSize,
    double BodyFontSize,
    double ButtonHeight,
    double ButtonFontSize);

/// <summary>
/// Detects a Steam Deck-style session so the first-run BIOS offer can use a larger,
/// touch- and controller-friendly layout.
/// </summary>
internal static class DeckMode
{
    /// <summary>
    /// Returns whether the current process looks like a Steam Deck or gamescope session.
    /// </summary>
    /// <param name="readEnvironment">
    /// Optional environment reader for tests. Defaults to
    /// <see cref="Environment.GetEnvironmentVariable(string)"/>.
    /// </param>
    internal static bool IsDeckSession(Func<string, string?>? readEnvironment = null)
    {
        readEnvironment ??= Environment.GetEnvironmentVariable;
        return IsEnabled(readEnvironment("SteamDeck"))
            || IsEnabled(readEnvironment("SteamOS"))
            || IsEnabled(readEnvironment("SteamGamepadUI"))
            || (readEnvironment("XDG_CURRENT_DESKTOP")?.Contains("gamescope", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Returns the welcome-dialog layout for the current session.</summary>
    /// <param name="deck">Whether the Deck layout should be used.</param>
    internal static WelcomeLayout Welcome(bool deck) =>
        deck
            ? new WelcomeLayout(
                Width: 960,
                Height: 680,
                HeaderFontSize: 30,
                BodyFontSize: 18,
                ButtonHeight: 54,
                ButtonFontSize: 18)
            : new WelcomeLayout(
                Width: 560,
                Height: 500,
                HeaderFontSize: 22,
                BodyFontSize: 13,
                ButtonHeight: 32,
                ButtonFontSize: 13);

    private static bool IsEnabled(string? value)
    {
        return value is not null
            && (string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));
    }
}
