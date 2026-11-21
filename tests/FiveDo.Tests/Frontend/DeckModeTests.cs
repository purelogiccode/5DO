using System;
using System.Collections.Generic;
using FiveDo.Frontend.Avalonia;

namespace FiveDo.Tests.Frontend;

public sealed class DeckModeTests
{
    [Theory]
    [InlineData("SteamDeck", "1", true)]
    [InlineData("SteamDeck", "true", true)]
    [InlineData("SteamOS", "1", true)]
    [InlineData("SteamGamepadUI", "1", true)]
    [InlineData("SteamDeck", "0", false)]
    [InlineData("SteamOS", "", false)]
    public void IsDeckSession_ReadsTheSteamEnvironment(string name, string value, bool expected)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [name] = value,
        };

        Assert.Equal(expected, DeckMode.IsDeckSession(key => environment.GetValueOrDefault(key)));
    }

    [Fact]
    public void IsDeckSession_DetectsGamescope()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_CURRENT_DESKTOP"] = "gamescope",
        };

        Assert.True(DeckMode.IsDeckSession(key => environment.GetValueOrDefault(key)));
    }

    [Fact]
    public void IsDeckSession_RejectsAnOrdinaryDesktop()
    {
        Assert.False(DeckMode.IsDeckSession(_ => null));
        Assert.False(DeckMode.IsDeckSession(key => string.Equals(key, "XDG_CURRENT_DESKTOP", StringComparison.Ordinal) ? "GNOME" : null));
    }

    [Fact]
    public void WelcomeLayout_UsesLargerControlsOnDeck()
    {
        WelcomeLayout deck = DeckMode.Welcome(deck: true);
        WelcomeLayout desktop = DeckMode.Welcome(deck: false);

        Assert.True(deck.Width > desktop.Width);
        Assert.True(deck.Height > desktop.Height);
        Assert.True(deck.HeaderFontSize > desktop.HeaderFontSize);
        Assert.True(deck.BodyFontSize > desktop.BodyFontSize);
        Assert.True(deck.ButtonHeight >= 44);
        Assert.True(deck.ButtonFontSize > desktop.ButtonFontSize);
        Assert.Equal(960, deck.Width);
        Assert.Equal(680, deck.Height);
    }
}
